using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage presentation: arena banners and boss intros, map-reveal holograms, enemy spawn bursts, reward and clear effects.
// Static helpers (Play/Burst/Pulse/Beam/Flash/Dust/Shake) are shared by StageDoor, FacilityFx and StageGuideFx.
public class StageFx : MonoBehaviour
{
    static readonly Color RifleColor = new Color(0.3f, 0.9f, 1f);
    static readonly Color HammerColor = new Color(1f, 0.55f, 0.15f);
    static readonly Color ClearColor = new Color(0.4f, 1f, 0.6f);
    static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f);
    static readonly Color DangerColor = new Color(1f, 0.2f, 0.12f);
    static readonly string[] BossNames = { "STAGE 1", "MIDBOSS  GUNWARDEN", "MIDBOSS  HAMMERLORD", "BOSS  THE CRUSHER" };
    static readonly string[] BossGroups = { "", "MidBoss0", "MidBoss1", "BossRoom" };
    static readonly Vector3[] HoloPositions = { new Vector3(-28f, 4f, 53f), new Vector3(28f, 4f, 53f) };
    const float SpawnPoll = 0.12f;
    const float HoloAppearDelay = 0.35f;
    const float HoloAppearTime = 0.5f;
    const float BarFraction = 0.09f;
    const float BannerTime = 2.4f;
    const float ShakeRange = 55f;

    static Texture2D ringTexture;
    static Material ringMaterial;
    static Mesh quadMesh;
    static ThirdPersonCamera cameraRig;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    public static StageFx Instance { get; private set; }

    class Banner { public string title, subtitle; public Color color; public float start; }
    class Hologram { public Transform root; public Material material; public Color color; public float appearAt = -1f, boost; }

    StageManager manager;
    WeaponAvatar avatar;
    readonly Vector3[] centers = new Vector3[4];
    readonly List<Banner> banners = new List<Banner>();
    readonly HashSet<Enemy> seen = new HashSet<Enemy>();
    readonly Hologram[] holograms = new Hologram[2];
    Transform bossTag;
    TextMesh bossTagText;
    Enemy tagged;
    Boss taggedBoss;
    float nextPoll, revealStart = -100f;
    Font font;
    GUIStyle titleStyle, subStyle;

    // ---- shared helpers -------------------------------------------------------------------------------------------

    public static void Play(AudioClip clip, Vector3 position, float volume, float pitch)
    {
        if (clip == null) return;
        var go = new GameObject("StageSfx"); go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip; source.volume = volume; source.pitch = pitch; source.spatialBlend = 0.6f; source.Play();
        Destroy(go, clip.length / pitch + 0.2f);
    }

    public static void Shake(Vector3 position, float magnitude, float duration, float range = ShakeRange)
    {
        if (cameraRig == null) cameraRig = FindFirstObjectByType<ThirdPersonCamera>();
        if (cameraRig == null) return;
        float falloff = 1f - Mathf.Clamp01(Vector3.Distance(position, cameraRig.transform.position) / range);
        if (falloff > 0.05f) cameraRig.Shake(magnitude * falloff, duration);
    }

    static void FovPunch(float amount, float duration)
    {
        if (cameraRig == null) cameraRig = FindFirstObjectByType<ThirdPersonCamera>();
        if (cameraRig != null) cameraRig.FovPunch(amount, duration);
    }

    public static void Flash(Vector3 position, Color color, float intensity, float range, float duration)
    {
        var go = new GameObject("StageFlash"); go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
        var visual = go.AddComponent<StageFxVisual>();
        visual.Setup(StageFxVisual.Kind.Light, color, duration, intensity);
    }

    // One-shot soft sparks; a non-zero box makes the emitter a ground strip instead of a point.
    public static void Burst(Vector3 position, Color color, int count, float speed, float size, float life, float gravity = 0f, Vector3 box = default)
    {
        var go = new GameObject("StageBurst"); go.transform.position = position;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.5f; main.loop = false; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color; main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission; emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var shape = ps.shape;
        if (box == default) { shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.2f; }
        else { shape.shapeType = ParticleSystemShapeType.Box; shape.scale = box * 2f; }
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        var over = ps.colorOverLifetime; over.enabled = true; over.color = fade;
        var shrink = ps.sizeOverLifetime; shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = EnemyTelegraphFx.GlowMaterial;
        ps.Play();
        Destroy(go, life + 1f);
    }

    public static void Dust(Vector3 position, Vector3 box) => Burst(position, new Color(0.8f, 0.75f, 0.65f, 0.7f), 40, 3.5f, 0.9f, 0.9f, -0.15f, box);

    // Flat expanding ring on the floor.
    public static void Pulse(Vector3 position, Color color, float radius, float duration)
    {
        var go = MakeQuad("StagePulse", RingMaterial, position + Vector3.up * 0.08f, Quaternion.Euler(90f, 0f, 0f));
        go.AddComponent<StageFxVisual>().Setup(StageFxVisual.Kind.Pulse, color, duration, radius);
    }

    // Vertical soft light column (two crossed quads).
    public static void Beam(Vector3 position, Color color, float height, float width, float duration)
    {
        var root = new GameObject("StageBeam"); root.transform.position = position + Vector3.up * height * 0.5f;
        for (int i = 0; i < 2; i++)
        {
            var q = MakeQuad("Plane", EnemyTelegraphFx.GlowMaterial, root.transform.position, Quaternion.Euler(0f, i * 90f, 0f));
            q.transform.SetParent(root.transform, true);
        }
        root.AddComponent<StageFxVisual>().Setup(StageFxVisual.Kind.Beam, color, duration, width, height);
    }

    public static Mesh QuadMesh
    {
        get
        {
            if (quadMesh != null) return quadMesh;
            quadMesh = new Mesh { name = "StageQuad" };
            quadMesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
            quadMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quadMesh.RecalculateBounds();
            return quadMesh;
        }
    }

    public static Material RingMaterial
    {
        get
        {
            if (ringMaterial != null) return ringMaterial;
            ringTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float r = Vector2.Distance(new Vector2((x + 0.5f) / 64f, (y + 0.5f) / 64f), new Vector2(0.5f, 0.5f)) * 2f;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) / 0.1f);
                    ringTexture.SetPixel(x, y, new Color(1f, 1f, 1f, ring * ring + (1f - Mathf.SmoothStep(0f, 0.9f, r)) * 0.12f));
                }
            ringTexture.Apply();
            ringMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = ringTexture };
            return ringMaterial;
        }
    }

    public static GameObject MakeQuad(string name, Material material, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject(name); go.transform.SetPositionAndRotation(position, rotation);
        go.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return go;
    }

    public static void Tint(Renderer renderer, Color color)
    {
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor(ColorId, color);
        renderer.SetPropertyBlock(block);
    }

    // ---- lifecycle ------------------------------------------------------------------------------------------------

    void Start()
    {
        Instance = this;
        manager = GetComponent<StageManager>();
        avatar = FindFirstObjectByType<WeaponAvatar>();
        for (int i = 0; i < centers.Length; i++)
        {
            var entrance = transform.Find("Arena" + i + "_Entrance");
            if (entrance != null) centers[i] = new Vector3(entrance.position.x, 0f, entrance.position.z + 14f);
        }
        foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) seen.Add(enemy);
        foreach (var boss in FindObjectsByType<Boss>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (boss.GetComponent<BossFx>() == null) boss.gameObject.AddComponent<BossFx>();
        BuildHolograms();
        manager.ArenaEntered += OnArenaEntered;
        manager.ArenaCleared += OnArenaCleared;
        manager.MapRevealStarted += OnRevealStarted;
        manager.RouteSelected += OnRouteSelected;
        manager.RewardAcquired += OnReward;
        manager.RunCompleted += OnRunCompleted;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (font != null) Destroy(font);
        foreach (var holo in holograms) if (holo != null && holo.material != null) Destroy(holo.material);
        if (manager == null) return;
        manager.ArenaEntered -= OnArenaEntered;
        manager.ArenaCleared -= OnArenaCleared;
        manager.MapRevealStarted -= OnRevealStarted;
        manager.RouteSelected -= OnRouteSelected;
        manager.RewardAcquired -= OnReward;
        manager.RunCompleted -= OnRunCompleted;
    }

    void Update()
    {
        if (Time.unscaledTime >= nextPoll) { nextPoll = Time.unscaledTime + SpawnPoll; PollSpawns(); }
        UpdateHolograms();
        UpdateBossTag();
        banners.RemoveAll(b => Time.unscaledTime - b.start > BannerTime);
    }

    // ---- enemy spawn ----------------------------------------------------------------------------------------------

    void PollSpawns()
    {
        foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (!seen.Add(enemy) || enemy.IsDead || enemy.GetComponent<MidBoss>() != null || enemy.GetComponent<Boss>() != null) continue;
            var attack = enemy.GetComponent<EnemyAttack>();
            bool ranged = attack != null && attack.Kind == EnemyAttackKind.Ranged;
            Color color = ranged ? EnemyTelegraphFx.RangedCore : DangerColor;
            Vector3 p = enemy.transform.position;
            Pulse(p, color, 1.7f, 0.45f);
            Beam(p, color, 9f, 0.9f, 0.4f);
            Burst(p + Vector3.up * 0.2f, color, 14, 4f, 0.35f, 0.6f, 0.6f);
            Dust(p + Vector3.up * 0.1f, new Vector3(0.6f, 0.05f, 0.6f));
            Play(ProceduralSfx.Swing, p, 0.35f, 0.6f);
        }
    }

    // ---- arena / boss ---------------------------------------------------------------------------------------------

    void OnArenaEntered(int index)
    {
        Vector3 center = centers[index];
        if (index == 0)
        {
            AddBanner("STAGE 1", "SURVIVE THE WAVES", DangerColor);
            Pulse(center, DangerColor, 16f, 0.9f);
            return;
        }
        var group = transform.Find(BossGroups[index]);
        var midBoss = index < 3 && group != null ? group.GetComponentInChildren<MidBoss>(true) : null;
        var finalBoss = index == 3 && group != null ? group.GetComponentInChildren<Boss>(true) : null;
        Transform bossTransform = finalBoss != null ? finalBoss.transform : midBoss != null ? midBoss.transform : null;
        AddBanner(BossNames[index], index == 3 ? Boss.DisplayName + " · DEFEAT IT TO CLEAR THE STAGE" : "BLOCKING THE ROAD AHEAD", DangerColor);
        Vector3 p = bossTransform != null ? bossTransform.position : center;
        Pulse(p, DangerColor, 12f, 0.9f);
        Pulse(p, Color.white, 6f, 0.6f);
        Beam(p, DangerColor, 18f, 3.5f, 0.9f);
        Burst(p + Vector3.up * 0.3f, DangerColor, 50, 8f, 0.7f, 1.0f, 0.4f);
        Dust(p + Vector3.up * 0.1f, new Vector3(2.5f, 0.05f, 2.5f));
        Flash(p + Vector3.up * 3f, DangerColor, 8f, 22f, 0.9f);
        Play(ProceduralSfx.HitHeavy, p, 1f, 0.55f);
        Shake(p, 0.5f, 0.6f);
        FovPunch(-6f, 0.5f);
        if (bossTransform != null) TagBoss(bossTransform.GetComponent<Enemy>(), finalBoss, index == 3 ? "THE CRUSHER" : BossNames[index]);
        if (finalBoss != null)
        {
            var fx = finalBoss.GetComponent<BossFx>();
            if (fx != null) fx.PlayIntro();
        }
    }

    public void Announce(string title, string subtitle, Color color) => AddBanner(title, subtitle, color);

    void TagBoss(Enemy boss, Boss controller, string label)
    {
        if (bossTag == null)
        {
            var go = new GameObject("BossTag");
            bossTagText = go.AddComponent<TextMesh>();
            bossTagText.anchor = TextAnchor.LowerCenter; bossTagText.alignment = TextAlignment.Center;
            bossTagText.fontSize = 64; bossTagText.characterSize = 0.07f; bossTagText.color = new Color(1f, 0.45f, 0.35f);
            bossTag = go.transform;
        }
        tagged = boss;
        taggedBoss = controller;
        bossTagText.text = label;
        bossTag.gameObject.SetActive(true);
    }

    void UpdateBossTag()
    {
        if (bossTag == null || !bossTag.gameObject.activeSelf) return;
        if (tagged == null || tagged.IsDead) { bossTag.gameObject.SetActive(false); return; }
        if (taggedBoss != null) bossTagText.color = taggedBoss.IsInvulnerable ? new Color(0.6f, 0.85f, 1f) : taggedBoss.Phase == 3 ? new Color(0.9f, 0.4f, 1f) : taggedBoss.Phase == 2 ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.5f, 0.2f);
        bossTag.position = tagged.transform.position + Vector3.up * (2.2f * tagged.transform.localScale.y + 0.6f);
        if (Camera.main != null) bossTag.rotation = Camera.main.transform.rotation;
    }

    void OnArenaCleared(int index)
    {
        Vector3 center = centers[index];
        Pulse(center, ClearColor, 22f, 1.1f);
        Pulse(center, Color.white, 12f, 0.7f);
        Burst(center + Vector3.up * 0.5f, ClearColor, 60, 9f, 0.5f, 1.2f, 0.5f);
        Flash(center + Vector3.up * 4f, ClearColor, 6f, 28f, 1.1f);
        Play(ProceduralSfx.Upgrade, center, 0.9f, 1f);
        FovPunch(4f, 0.3f);
        if (avatar != null)
        {
            Vector3 a = avatar.transform.position;
            Pulse(a, ClearColor, 4f, 0.7f);
            Burst(avatar.Center, ClearColor, 36, 5f, 0.3f, 0.9f, -0.2f);
            Beam(a, ClearColor, 6f, 1.2f, 0.7f);
        }
        AddBanner(index == 0 ? "ARENA CLEARED" : index == 3 ? "BOSS DEFEATED" : "MIDBOSS DEFEATED", index == 3 ? "" : "HP RESTORED", ClearColor);
    }

    void OnReward(WeaponData data)
    {
        if (avatar == null) return;
        Vector3 p = avatar.transform.position;
        AddBanner("WEAPON ACQUIRED", data != null ? data.displayName : "", GoldColor);
        Beam(p, GoldColor, 14f, 2f, 1.3f);
        Pulse(p, GoldColor, 6f, 0.9f);
        Pulse(p, Color.white, 3f, 0.6f);
        Burst(p + Vector3.up, GoldColor, 70, 7f, 0.4f, 1.4f, -0.2f);
        Flash(p + Vector3.up * 2f, GoldColor, 6f, 14f, 1f);
        Play(ProceduralSfx.Upgrade, p, 1f, 1.25f);
        FovPunch(5f, 0.35f);
    }

    void OnRunCompleted()
    {
        AddBanner("STAGE CLEAR", "THE CRUSHER HAS FALLEN", GoldColor);
        StartCoroutine(Fireworks(centers[3]));
    }

    IEnumerator Fireworks(Vector3 center)
    {
        for (int i = 0; i < 9; i++)
        {
            Vector3 p = center + new Vector3(Random.Range(-9f, 9f), Random.Range(6f, 14f), Random.Range(-8f, 8f));
            Color c = Color.HSVToRGB(Random.value, 0.55f, 1f);
            Burst(p, c, 60, 9f, 0.5f, 1.4f, 0.5f);
            Flash(p, c, 4f, 20f, 0.5f);
            Play(ProceduralSfx.Snip, p, 0.5f, Random.Range(0.6f, 0.9f));
            Shake(p, 0.08f, 0.15f);
            yield return new WaitForSecondsRealtime(0.22f);
        }
        Play(ProceduralSfx.Upgrade, center, 1f, 0.8f);
    }

    // ---- map reveal / route holograms -----------------------------------------------------------------------------

    void BuildHolograms()
    {
        for (int i = 0; i < 2; i++)
        {
            var holo = new Hologram { color = i == 0 ? RifleColor : HammerColor };
            holo.material = new Material(Shader.Find("Sprites/Default")) { color = new Color(holo.color.r, holo.color.g, holo.color.b, 0.6f) };
            holo.root = new GameObject(i == 0 ? "HologramRifle" : "HologramHammer").transform;
            holo.root.SetParent(transform, false);
            holo.root.position = HoloPositions[i];
            var shape = new GameObject("Shape").transform;
            shape.SetParent(holo.root, false);
            if (i == 0) { Part(shape, new Vector3(0f, 0f, 0.2f), new Vector3(0.3f, 0.4f, 2.6f), holo); Part(shape, new Vector3(0f, -0.35f, -1.4f), new Vector3(0.3f, 0.8f, 0.7f), holo); Part(shape, new Vector3(0f, 0.28f, 0.2f), new Vector3(0.12f, 0.16f, 1.2f), holo); }
            else { Part(shape, new Vector3(0f, 0f, -0.5f), new Vector3(0.26f, 0.26f, 2.8f), holo); Part(shape, new Vector3(0f, 0f, 1.1f), new Vector3(1.4f, 1.0f, 0.9f), holo); }
            shape.localScale = Vector3.one * 1.3f;
            var ring = MakeQuad("HoloRing", RingMaterial, holo.root.position + Vector3.down * 3.95f, Quaternion.Euler(90f, 0f, 0f));
            ring.transform.SetParent(holo.root, true); ring.transform.localScale = new Vector3(5f, 5f, 1f);
            Tint(ring.GetComponent<MeshRenderer>(), new Color(holo.color.r, holo.color.g, holo.color.b, 0.7f));
            holo.root.gameObject.SetActive(false);
            holograms[i] = holo;
        }
    }

    static void Part(Transform parent, Vector3 position, Vector3 size, Hologram holo)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = holo.material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    void OnRevealStarted()
    {
        revealStart = Time.unscaledTime;
        for (int i = 0; i < 2; i++)
        {
            var holo = holograms[i];
            holo.appearAt = Time.unscaledTime + HoloAppearDelay + i * 0.25f;
            holo.root.gameObject.SetActive(true);
            holo.root.localScale = Vector3.zero;
            StartCoroutine(RevealCue(i, holo.appearAt));
        }
        AddBanner("ROUTE OPENED", "LEFT: RIFLE   ·   RIGHT: HAMMER", Color.white);
        Play(ProceduralSfx.SwingHeavy, centers[0] + Vector3.forward * 40f, 0.6f, 0.5f);
    }

    IEnumerator RevealCue(int route, float at)
    {
        while (Time.unscaledTime < at) yield return null;
        Vector3 floor = new Vector3(HoloPositions[route].x, 0f, HoloPositions[route].z);
        Pulse(floor, holograms[route].color, 8f, 0.9f);
        Burst(HoloPositions[route], holograms[route].color, 30, 5f, 0.35f, 0.9f, -0.1f);
        Play(ProceduralSfx.Upgrade, HoloPositions[route], 0.55f, route == 0 ? 1.15f : 0.9f);
    }

    void OnRouteSelected(int route)
    {
        var chosen = holograms[route];
        chosen.boost = 1f;
        Vector3 q = chosen.root.position;
        Pulse(new Vector3(q.x, 0f, q.z), chosen.color, 10f, 0.8f);
        AddBanner(route == 0 ? "ROUTE A  RIFLE" : "ROUTE B  HAMMER", "BOTH PATHS REMAIN OPEN", chosen.color);
    }

    void UpdateHolograms()
    {
        foreach (var holo in holograms)
        {
            if (holo == null || !holo.root.gameObject.activeSelf) continue;
            float now = Time.unscaledTime;
            float scale = 1f;
            if (holo.appearAt >= 0f)
            {
                float t = Mathf.Clamp01((now - holo.appearAt) / HoloAppearTime);
                float u = t - 1f;
                scale = t <= 0f ? 0f : 1f + 2.4f * u * u * u + 1.4f * u * u;
            }
            holo.boost = Mathf.MoveTowards(holo.boost, 0f, Time.unscaledDeltaTime * 0.8f);
            holo.root.localScale = Vector3.one * scale;
            var shape = holo.root.GetChild(0);
            shape.localRotation = Quaternion.Euler(0f, now * 70f, 0f);
            shape.localPosition = Vector3.up * Mathf.Sin(now * 2.2f) * 0.35f;
            float alpha = 0.45f + 0.15f * Mathf.Sin(now * 6f) + 0.4f * holo.boost;
            holo.material.color = new Color(holo.color.r, holo.color.g, holo.color.b, alpha);
        }
    }

    // ---- banners / letterbox --------------------------------------------------------------------------------------

    void AddBanner(string title, string subtitle, Color color)
    {
        banners.Add(new Banner { title = title, subtitle = subtitle, color = color, start = Time.unscaledTime });
        if (banners.Count > 3) banners.RemoveAt(0);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        DrawLetterbox();
        if (banners.Count == 0) return;
        if (font == null)
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
            titleStyle = new GUIStyle { font = font, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            subStyle = new GUIStyle { font = font, alignment = TextAnchor.MiddleCenter };
        }
        titleStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.055f);
        subStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.026f);
        float y = Screen.height * 0.16f;
        foreach (var b in banners)
        {
            float t = (Time.unscaledTime - b.start) / BannerTime;
            float alpha = Mathf.Clamp01(Mathf.Min(t / 0.12f, (1f - t) / 0.25f));
            float slide = (1f - Mathf.Clamp01(t / 0.15f)) * 40f;
            var rect = new Rect(0f, y, Screen.width, Screen.height * 0.07f);
            DrawText(rect, b.title, titleStyle, b.color, alpha, slide);
            y += rect.height;
            if (b.subtitle.Length > 0)
            {
                var sub = new Rect(0f, y - Screen.height * 0.01f, Screen.width, Screen.height * 0.04f);
                DrawText(sub, b.subtitle, subStyle, Color.white, alpha * 0.9f, slide);
                y += sub.height;
            }
            y += Screen.height * 0.01f;
        }
    }

    static void DrawText(Rect rect, string text, GUIStyle style, Color color, float alpha, float slide)
    {
        rect.x += slide;
        style.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.7f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = new Color(color.r, color.g, color.b, alpha);
        GUI.Label(rect, text, style);
    }

    void DrawLetterbox()
    {
        float t = (Time.unscaledTime - revealStart) / StageManager.MapRevealDuration;
        if (t < 0f || t > 1f) return;
        float k = Mathf.Clamp01(Mathf.Min(t / 0.2f, (1f - t) / 0.2f));
        k = k * k * (3f - 2f * k);
        float h = Screen.height * BarFraction * k;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, h), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, Screen.height - h, Screen.width, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}

// Self-driving timed visual: expanding ring, fading beam or fading light.
public class StageFxVisual : MonoBehaviour
{
    public enum Kind { Pulse, Beam, Light }
    Kind kind;
    Color color;
    float start, duration, size, height;
    Renderer[] renderers;
    Light lamp;

    public void Setup(Kind kind, Color color, float duration, float size, float height = 0f)
    {
        this.kind = kind; this.color = color; this.duration = Mathf.Max(duration, 0.01f); this.size = size; this.height = height;
        start = Time.unscaledTime;
        renderers = GetComponentsInChildren<Renderer>();
        lamp = GetComponent<Light>();
        Apply(0f);
    }

    void Update()
    {
        float t = (Time.unscaledTime - start) / duration;
        if (t >= 1f) { Destroy(gameObject); return; }
        Apply(t);
    }

    void Apply(float t)
    {
        float fade = 1f - t;
        switch (kind)
        {
            case Kind.Pulse:
                float e = 1f - fade * fade;
                transform.localScale = new Vector3(size * 2f * e, size * 2f * e, 1f);
                break;
            case Kind.Beam:
                foreach (Transform plane in transform) plane.localScale = new Vector3(size * (1f - t * 0.6f), height, 1f);
                break;
            case Kind.Light:
                lamp.intensity = size * fade * fade;
                return;
        }
        var tint = new Color(color.r, color.g, color.b, fade * (kind == Kind.Beam ? 0.7f : 1f));
        foreach (var r in renderers) StageFx.Tint(r, tint);
    }
}
