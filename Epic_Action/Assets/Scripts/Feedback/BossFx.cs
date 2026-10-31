using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Boss presentation: presence (aura, silhouette, footsteps), pattern telegraphs, impacts, roars, phase colours and the defeat sequence.
// Driven only by Boss events and state; replaces the basic BossTelegraph lines at runtime. StageFx adds it and triggers the intro roar.
[RequireComponent(typeof(Boss))]
public class BossFx : MonoBehaviour
{
    static readonly Color[] PhaseColors = { new Color(1f, 0.5f, 0.15f), new Color(1f, 0.2f, 0.1f), new Color(0.85f, 0.2f, 1f) };
    static readonly Color ShieldColor = new Color(0.55f, 0.8f, 1f);
    static readonly Color WarmColor = new Color(1f, 0.55f, 0.1f);
    static readonly Color WarnColor = new Color(1f, 0.1f, 0.06f);
    static readonly Color RockColor = new Color(0.5f, 0.42f, 0.35f);
    const float StepDistance = 2.4f;
    const float SlowScale = 0.35f;
    const float SlowHold = 0.35f;
    const float SlowEase = 0.15f;
    const float FanFlashTime = 0.2f;
    const float LineWidth = 0.1f;
    const int MaxMarkers = 4;
    const int ArcSteps = 16;

    static Mesh fanMesh;
    static Texture2D discTexture, vignetteTexture;
    static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    static readonly int ColorId = Shader.PropertyToID("_Color");

    Boss boss;
    Renderer body;
    Transform root, decor, shell;
    Material flat, disc, dark, eyeMat, shellMat;
    GameObject rectOuter, rectFill, fanOuter, fanFill;
    readonly GameObject[] discs = new GameObject[MaxMarkers];
    readonly GameObject[] rings = new GameObject[MaxMarkers];
    readonly GameObject[] pillars = new GameObject[MaxMarkers];
    readonly List<Vector3> linePoints = new List<Vector3>();
    LineRenderer edge;
    Light aura;
    GameObject groundGlow, groundRing;
    ParticleSystem embers;
    ThirdPersonCamera cameraRig;
    Color shown = PhaseColors[0];
    Vector3 lastPos;
    float stepDistance, ghostTimer, dustTimer, pulseTimer, roarGlow, whiteFlash;
    bool introQueued, dead, slowing, rightFoot;

    Color PhaseColor => boss.IsInvulnerable ? ShieldColor : PhaseColors[Mathf.Clamp(boss.Phase - 1, 0, 2)];

    void Awake()
    {
        boss = GetComponent<Boss>();
        body = GetComponentInChildren<MeshFilter>(true) != null ? GetComponentInChildren<MeshFilter>(true).GetComponent<Renderer>() : null;
        var basic = GetComponent<BossTelegraph>();
        if (basic != null) basic.enabled = false;
        flat = new Material(Shader.Find("Sprites/Default"));
        disc = new Material(Shader.Find("Sprites/Default")) { mainTexture = DiscTexture() };
        shellMat = new Material(Shader.Find("Sprites/Default"));
        dark = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.07f, 0.06f, 0.08f) };
        eyeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = PhaseColors[0] };
        root = new GameObject("BossFxRoot").transform;
        BuildTelegraphs();
        BuildPresence();
        BuildSilhouette();
        lastPos = transform.position;
    }

    void Start()
    {
        if (introQueued) RunIntro();
    }

    void OnEnable()
    {
        boss.TelegraphStarted += OnTelegraphStarted;
        boss.AttackFired += OnAttackFired;
        boss.AttackCancelled += OnAttackCancelled;
        boss.AttackEnded += OnAttackEnded;
        boss.VolleyFired += OnVolley;
        boss.PhaseTransitionStarted += OnTransitionStarted;
        boss.PhaseTransitionEnded += OnTransitionEnded;
        boss.Roared += OnRoared;
        boss.Defeated += OnDefeated;
    }

    void OnDisable()
    {
        boss.TelegraphStarted -= OnTelegraphStarted;
        boss.AttackFired -= OnAttackFired;
        boss.AttackCancelled -= OnAttackCancelled;
        boss.AttackEnded -= OnAttackEnded;
        boss.VolleyFired -= OnVolley;
        boss.PhaseTransitionStarted -= OnTransitionStarted;
        boss.PhaseTransitionEnded -= OnTransitionEnded;
        boss.Roared -= OnRoared;
        boss.Defeated -= OnDefeated;
        EndSlow();
        HideTelegraphs();
    }

    void OnDestroy()
    {
        EndSlow();
        foreach (var m in new[] { flat, disc, shellMat, dark, eyeMat }) if (m != null) Destroy(m);
        if (root != null) Destroy(root.gameObject);
        if (shell != null) Destroy(shell.gameObject);
    }

    // ---- build ----------------------------------------------------------------------------------------------------

    static Texture2D DiscTexture()
    {
        if (discTexture != null) return discTexture;
        discTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float r = Vector2.Distance(new Vector2((x + 0.5f) / 64f, (y + 0.5f) / 64f), new Vector2(0.5f, 0.5f)) * 2f;
                discTexture.SetPixel(x, y, new Color(1f, 1f, 1f, (1f - Mathf.SmoothStep(0.9f, 1f, r)) * (0.65f + 0.35f * r)));
            }
        discTexture.Apply();
        return discTexture;
    }

    static Texture2D VignetteTexture()
    {
        if (vignetteTexture != null) return vignetteTexture;
        vignetteTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float r = Vector2.Distance(new Vector2((x + 0.5f) / 64f, (y + 0.5f) / 64f), new Vector2(0.5f, 0.5f)) * 2f;
                vignetteTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0.35f, 1.1f, r)));
            }
        vignetteTexture.Apply();
        return vignetteTexture;
    }

    // Unit-radius fan along +Z, matching the barrage spread.
    static Mesh FanMesh()
    {
        if (fanMesh != null) return fanMesh;
        var vertices = new Vector3[ArcSteps + 2];
        var triangles = new int[ArcSteps * 3];
        for (int i = 0; i <= ArcSteps; i++)
            vertices[i + 1] = Quaternion.AngleAxis(Mathf.Lerp(-Boss.BarrageHalfAngle, Boss.BarrageHalfAngle, i / (float)ArcSteps), Vector3.up) * Vector3.forward;
        for (int i = 0; i < ArcSteps; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 2; triangles[i * 3 + 2] = i + 1; }
        fanMesh = new Mesh { name = "BossFan", vertices = vertices, triangles = triangles };
        fanMesh.RecalculateBounds();
        return fanMesh;
    }

    GameObject Flat(string name, Material material)
    {
        var go = StageFx.MakeQuad(name, material, Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
        go.transform.SetParent(root, true);
        go.SetActive(false);
        return go;
    }

    GameObject Fan(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, true);
        go.AddComponent<MeshFilter>().sharedMesh = FanMesh();
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = flat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        go.SetActive(false);
        return go;
    }

    void BuildTelegraphs()
    {
        for (int i = 0; i < MaxMarkers; i++)
        {
            discs[i] = Flat("Disc" + i, disc);
            rings[i] = Flat("Ring" + i, StageFx.RingMaterial);
            var pillar = new GameObject("Pillar" + i);
            pillar.transform.SetParent(root, true);
            for (int q = 0; q < 2; q++)
            {
                var plane = StageFx.MakeQuad("Plane", EnemyTelegraphFx.GlowMaterial, Vector3.zero, Quaternion.Euler(0f, q * 90f, 0f));
                plane.transform.SetParent(pillar.transform, false);
                plane.transform.localPosition = Vector3.up * 0.5f;
                plane.transform.localRotation = Quaternion.Euler(0f, q * 90f, 0f);
            }
            pillar.SetActive(false);
            pillars[i] = pillar;
        }
        rectOuter = Flat("ChargeOuter", flat);
        rectFill = Flat("ChargeFill", flat);
        fanOuter = Fan("FanOuter");
        fanFill = Fan("FanFill");
        var lineGo = new GameObject("BossEdge");
        lineGo.transform.SetParent(root, true);
        edge = lineGo.AddComponent<LineRenderer>();
        edge.sharedMaterial = flat; edge.useWorldSpace = true; edge.widthMultiplier = LineWidth;
        edge.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; edge.receiveShadows = false;
        edge.enabled = false;
    }

    void BuildPresence()
    {
        var lightGo = new GameObject("BossAura");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = Vector3.up * 1.2f;
        aura = lightGo.AddComponent<Light>();
        aura.type = LightType.Point; aura.range = 16f; aura.intensity = 2f; aura.color = PhaseColors[0]; aura.shadows = LightShadows.None;
        groundGlow = StageFx.MakeQuad("BossGroundGlow", EnemyTelegraphFx.GlowMaterial, transform.position, Quaternion.Euler(90f, 0f, 0f));
        groundGlow.transform.SetParent(root, true);
        groundRing = StageFx.MakeQuad("BossGroundRing", StageFx.RingMaterial, transform.position, Quaternion.Euler(90f, 0f, 0f));
        groundRing.transform.SetParent(root, true);

        var go = new GameObject("BossEmbers");
        go.transform.SetParent(transform, false);
        embers = go.AddComponent<ParticleSystem>();
        embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = embers.main;
        main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.startColor = PhaseColors[0]; main.gravityModifier = -0.12f;
        var emission = embers.emission; emission.rateOverTime = 12f;
        var shape = embers.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = 0.8f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
        var over = embers.colorOverLifetime; over.enabled = true; over.color = fade;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = EnemyTelegraphFx.GlowMaterial;
        embers.Play();

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.name = "BossShield";
        var shieldRenderer = sphere.GetComponent<Renderer>();
        shieldRenderer.sharedMaterial = shellMat; shieldRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; shieldRenderer.receiveShadows = false;
        shell = sphere.transform;
        sphere.SetActive(false);
    }

    // Horns, pauldrons, back spikes and a glowing eye bar to give the body a recognisable silhouette.
    void BuildSilhouette()
    {
        if (body == null) return;
        Bounds b = body.bounds;
        float h = b.size.y;
        Vector3 c = b.center, fwd = transform.forward, right = transform.right;
        decor = new GameObject("BossSilhouette").transform;
        decor.SetParent(transform, false);
        for (int s = -1; s <= 1; s += 2)
        {
            Part(dark, c + right * s * b.extents.x * 0.5f + Vector3.up * b.extents.y * 1.0f, transform.rotation * Quaternion.Euler(-12f, 0f, -s * 28f), new Vector3(0.09f, 0.26f, 0.09f) * h);
            Part(dark, c + right * s * b.extents.x * 0.92f + Vector3.up * h * 0.2f, transform.rotation * Quaternion.Euler(0f, 0f, -s * 18f), new Vector3(0.2f, 0.09f, 0.22f) * h);
        }
        for (int i = 0; i < 3; i++)
            Part(dark, c - fwd * b.extents.z * 0.85f + Vector3.up * h * (0.12f + 0.14f * i) + right * (i - 1) * b.extents.x * 0.3f,
                transform.rotation * Quaternion.Euler(-35f, 0f, 0f), new Vector3(0.05f, 0.2f, 0.05f) * h);
        Part(eyeMat, c + fwd * b.extents.z * 0.92f + Vector3.up * h * 0.26f, transform.rotation, new Vector3(b.size.x * 0.4f / h, 0.035f, 0.04f) * h);
    }

    void Part(Material material, Vector3 position, Quaternion rotation, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(decor, false);
        go.transform.SetPositionAndRotation(position, rotation);
        float scale = transform.lossyScale.x;
        go.transform.localScale = size / (scale > 0.01f ? scale : 1f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    // ---- per-frame --------------------------------------------------------------------------------------------------

    void Update()
    {
        float dt = Time.deltaTime;
        roarGlow = Mathf.MoveTowards(roarGlow, 0f, Time.unscaledDeltaTime * 0.9f);
        whiteFlash = Mathf.MoveTowards(whiteFlash, 0f, Time.unscaledDeltaTime * 2.2f);
        shown = Color.Lerp(shown, PhaseColor, 1f - Mathf.Exp(-7f * dt));
        UpdatePresence(dt);
        if (dead) return;
        Footsteps();
        if (boss.State == BossState.Attacking && boss.Pattern == BossPattern.Charge) ChargeTrail(dt);
        if (boss.State == BossState.Transition) TransitionPulse(dt);
    }

    void LateUpdate()
    {
        UpdateTelegraph();
        UpdateShell();
    }

    void UpdatePresence(float dt)
    {
        float alive = dead ? 0f : 1f;
        float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * (2.5f + boss.Phase));
        float charge = boss.IsTelegraphing ? boss.TelegraphProgress : 0f;
        aura.color = shown;
        aura.intensity = Mathf.MoveTowards(aura.intensity, alive * (1.8f + beat * 1.2f + charge * 6f + (boss.IsInvulnerable ? 3f : 0f)), dt * 20f);
        Vector3 p = transform.position;
        groundGlow.transform.position = p + Vector3.up * 0.06f;
        groundGlow.transform.localScale = new Vector3(12f, 12f, 1f) * (1f + 0.08f * beat);
        Tint(groundGlow, new Color(shown.r, shown.g, shown.b, alive * (0.22f + 0.12f * beat + 0.25f * charge)));
        groundRing.transform.position = p + Vector3.up * 0.07f;
        groundRing.transform.localScale = new Vector3(8.5f, 8.5f, 1f);
        groundRing.transform.rotation = Quaternion.Euler(90f, Time.time * 25f, 0f);
        Tint(groundRing, new Color(shown.r, shown.g, shown.b, alive * (0.35f + 0.25f * beat)));
        eyeMat.color = Color.Lerp(shown, Color.white, boss.IsTelegraphing ? charge * 0.8f : 0.1f * beat);
        var main = embers.main; main.startColor = shown;
        var emission = embers.emission; emission.rateOverTime = alive * (8f + boss.Phase * 10f + (boss.IsInvulnerable ? 40f : 0f));
        if (dead && embers.isPlaying) embers.Stop();
    }

    void Footsteps()
    {
        Vector3 pos = transform.position;
        Vector3 d = pos - lastPos;
        d.y = 0f;
        lastPos = pos;
        if (d.magnitude > 3f || boss.IsInvulnerable) return;
        stepDistance += d.magnitude;
        if (stepDistance < StepDistance) return;
        stepDistance -= StepDistance;
        rightFoot = !rightFoot;
        Vector3 foot = pos + transform.right * (rightFoot ? 0.9f : -0.9f);
        StageFx.Shake(foot, 0.09f, 0.14f, 40f);
        StageFx.Burst(foot + Vector3.up * 0.1f, new Color(0.8f, 0.75f, 0.65f, 0.6f), 10, 2.2f, 0.6f, 0.6f, -0.1f);
        StageFx.Pulse(foot, new Color(shown.r, shown.g, shown.b, 0.6f), 1.8f, 0.3f);
        StageFx.Play(ProceduralSfx.HitHeavy, foot, 0.4f, Random.Range(0.36f, 0.48f));
    }

    void ChargeTrail(float dt)
    {
        ghostTimer -= dt; dustTimer -= dt;
        if (ghostTimer <= 0f) { ghostTimer = 0.04f; SpawnGhost(); }
        if (dustTimer <= 0f)
        {
            dustTimer = 0.06f;
            Vector3 p = transform.position;
            StageFx.Dust(p + Vector3.up * 0.1f, new Vector3(1.4f, 0.05f, 1.4f));
            StageFx.Burst(p + Vector3.up * 0.2f - boss.AimDirection * 1f, shown, 6, 3f, 0.4f, 0.4f, 0.2f);
        }
    }

    void SpawnGhost()
    {
        var filter = body != null ? body.GetComponent<MeshFilter>() : null;
        if (filter == null) return;
        var go = new GameObject("BossGhost");
        go.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
        go.transform.localScale = body.transform.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = flat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        go.AddComponent<BossFxFade>().Setup(shown, 0.35f, 0.5f, 0f);
    }

    void TransitionPulse(float dt)
    {
        pulseTimer -= dt;
        if (pulseTimer > 0f) return;
        pulseTimer = 0.32f;
        StageFx.Pulse(transform.position, ShieldColor, 7f, 0.5f);
        StageFx.Burst(transform.position + Vector3.up * 0.5f, ShieldColor, 10, 3f, 0.3f, 0.7f, -0.3f);
    }

    void UpdateShell()
    {
        bool show = boss.IsInvulnerable && !dead;
        if (shell.gameObject.activeSelf != show) shell.gameObject.SetActive(show);
        if (!show) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
        shell.position = body != null ? body.bounds.center : transform.position + Vector3.up * 2f;
        shell.localScale = Vector3.one * (5.2f + 0.25f * pulse);
        shell.Rotate(0f, 80f * Time.deltaTime, 0f);
        Tint(shell.gameObject, new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, 0.16f + 0.12f * pulse));
    }

    // ---- telegraphs -------------------------------------------------------------------------------------------------

    void HideTelegraphs()
    {
        if (root == null) return;
        foreach (var go in discs) if (go != null) go.SetActive(false);
        foreach (var go in rings) if (go != null) go.SetActive(false);
        foreach (var go in pillars) if (go != null) go.SetActive(false);
        if (rectOuter != null) rectOuter.SetActive(false);
        if (rectFill != null) rectFill.SetActive(false);
        if (fanOuter != null) fanOuter.SetActive(false);
        if (fanFill != null) fanFill.SetActive(false);
        if (edge != null) edge.enabled = false;
    }

    static void Put(GameObject go, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
    {
        go.SetActive(true);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;
        Tint(go, color);
    }

    static void Tint(GameObject go, Color color)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) { foreach (var child in go.GetComponentsInChildren<Renderer>()) Tint(child.gameObject, color); return; }
        r.GetPropertyBlock(block);
        block.SetColor(ColorId, color);
        r.SetPropertyBlock(block);
    }

    // Orange warns early and turns red then white as the hit lands, so the dash window is the last bright flash.
    void UpdateTelegraph()
    {
        HideTelegraphs();
        if (dead || !boss.IsTelegraphing) return;
        float p = boss.TelegraphProgress;
        float blink = p > 0.85f ? Mathf.PingPong(Time.time * 18f, 1f) : 0f;
        Color c = Color.Lerp(Color.Lerp(WarmColor, WarnColor, p), Color.white, blink * 0.6f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (8f + p * 20f));
        Vector3 aim = boss.AimDirection;
        Quaternion flatRot = Quaternion.Euler(90f, 0f, 0f);
        switch (boss.Pattern)
        {
            case BossPattern.Slam: ShowSlam(p, c, pulse, flatRot); break;
            case BossPattern.Charge: ShowCharge(p, c, pulse, aim); break;
            case BossPattern.Barrage: ShowBarrage(p, c, pulse, aim); break;
            default: ShowSummon(p, c, pulse, flatRot); break;
        }
    }

    void ShowSlam(float p, Color c, float pulse, Quaternion flatRot)
    {
        Vector3 centre = boss.ImpactPoint;
        float d = Boss.SlamRadius * 2f;
        Put(discs[0], centre, flatRot, new Vector3(d, d, 1f) * Mathf.Max(0.02f, p), new Color(c.r, c.g, c.b, 0.22f + 0.28f * pulse * p + 0.2f * p));
        Put(rings[0], centre + Vector3.up * 0.02f, flatRot, new Vector3(d, d, 1f), new Color(c.r, c.g, c.b, 0.9f));
        // Converging ring touches the rim exactly when the slam lands.
        float k = 1f + (1f - p) * 0.8f;
        Put(rings[1], centre + Vector3.up * 0.03f, flatRot, new Vector3(d, d, 1f) * k, new Color(1f, 1f, 1f, 0.2f + 0.7f * p));
    }

    void ShowCharge(float p, Color c, float pulse, Vector3 aim)
    {
        Vector3 a = boss.AttackOrigin, b = boss.ChargeEnd;
        b.y = a.y;
        Vector3 along = b - a;
        float length = along.magnitude;
        if (length < 0.1f) return;
        float width = Boss.ChargeHalfWidth * 2f;
        Quaternion rot = Quaternion.LookRotation(along / length, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        Vector3 dir = along / length;
        Put(rectOuter, a + along * 0.5f + Vector3.up * 0.01f, rot, new Vector3(width, length, 1f), new Color(c.r, c.g, c.b, 0.1f + 0.08f * pulse));
        float fill = length * Mathf.Max(0.02f, p);
        Put(rectFill, a + dir * fill * 0.5f + Vector3.up * 0.02f, rot, new Vector3(width, fill, 1f), new Color(c.r, c.g, c.b, 0.3f + 0.3f * p));
        Vector3 side = Vector3.Cross(Vector3.up, dir) * Boss.ChargeHalfWidth;
        Vector3 lift = Vector3.up * 0.04f;
        SetEdge(c, 0.95f, a - side + lift, b - side + lift, b + side + lift, a + side + lift, a - side + lift);
        float cap = width * 1.5f;
        Put(rings[0], b + Vector3.up * 0.03f, Quaternion.Euler(90f, 0f, 0f), new Vector3(cap, cap, 1f) * (1f + 0.1f * pulse), new Color(c.r, c.g, c.b, 0.6f));
    }

    void ShowBarrage(float p, Color c, float pulse, Vector3 aim)
    {
        Vector3 origin = boss.AttackOrigin;
        Quaternion rot = Quaternion.LookRotation(aim, Vector3.up);
        float range = Boss.BarrageRange;
        Put(fanOuter, origin, rot, Vector3.one * range, new Color(c.r, c.g, c.b, 0.1f + 0.07f * pulse));
        Put(fanFill, origin + Vector3.up * 0.01f, rot, Vector3.one * range * Mathf.Max(0.02f, p), new Color(c.r, c.g, c.b, 0.28f + 0.3f * p));
        int shots = 5 + (boss.Phase - 1) * 2;
        linePoints.Clear();
        linePoints.Add(origin);
        for (int i = 0; i <= ArcSteps; i++)
            linePoints.Add(origin + Quaternion.AngleAxis(Mathf.Lerp(-Boss.BarrageHalfAngle, Boss.BarrageHalfAngle, i / (float)ArcSteps), Vector3.up) * aim * range);
        linePoints.Add(origin);
        // One thin lane per projectile so the gaps between bullets are readable.
        for (int i = 0; i < shots; i++)
        {
            linePoints.Add(origin + Quaternion.AngleAxis(Mathf.Lerp(-Boss.BarrageHalfAngle, Boss.BarrageHalfAngle, i / (float)(shots - 1)), Vector3.up) * aim * range);
            linePoints.Add(origin);
        }
        SetEdge(c, 0.8f, linePoints.ToArray());
    }

    void ShowSummon(float p, Color c, float pulse, Quaternion flatRot)
    {
        int count = Mathf.Min(boss.SummonPoints.Count, MaxMarkers);
        float d = 2.6f;
        for (int i = 0; i < count; i++)
        {
            Vector3 point = boss.SummonPoints[i] + Vector3.up * 0.06f;
            Put(discs[i], point, flatRot, new Vector3(d, d, 1f) * Mathf.Max(0.05f, p), new Color(0.75f, 0.35f, 1f, 0.3f + 0.3f * pulse * p));
            Put(rings[i], point + Vector3.up * 0.02f, flatRot, new Vector3(d, d, 1f), new Color(0.85f, 0.5f, 1f, 0.9f));
            pillars[i].SetActive(true);
            pillars[i].transform.position = point;
            pillars[i].transform.localScale = new Vector3(0.6f + 1.2f * p, 7f, 0.6f + 1.2f * p);
            Tint(pillars[i], new Color(0.75f, 0.35f, 1f, 0.12f + 0.4f * p));
        }
    }

    void SetEdge(Color c, float alpha, params Vector3[] points)
    {
        edge.enabled = true;
        edge.startColor = edge.endColor = new Color(c.r, c.g, c.b, alpha);
        edge.positionCount = points.Length;
        edge.SetPositions(points);
    }

    // ---- events -----------------------------------------------------------------------------------------------------

    void OnTelegraphStarted(Boss source)
    {
        Vector3 p = transform.position;
        StageFx.Flash(p + Vector3.up * 3f, WarmColor, 3f, 9f, 0.25f);
        StageFx.Burst(p + Vector3.up * 1.2f, WarmColor, 14, 3f, 0.3f, 0.5f, -0.2f);
        StageFx.Play(ProceduralSfx.SwingHeavy, p, 0.6f, boss.Pattern == BossPattern.Charge ? 0.4f : boss.Pattern == BossPattern.Slam ? 0.5f : 0.6f);
    }

    void OnAttackFired(Boss source)
    {
        switch (boss.Pattern)
        {
            case BossPattern.Slam: SlamImpact(); break;
            case BossPattern.Charge: ChargeStart(); break;
            case BossPattern.Barrage: break;
            default: SummonImpact(); break;
        }
    }

    void OnAttackCancelled(Boss source)
    {
        StageFx.Burst(transform.position + Vector3.up * 1.5f, new Color(0.6f, 0.6f, 0.65f, 0.8f), 12, 3f, 0.5f, 0.5f, -0.1f);
    }

    void OnAttackEnded(Boss source)
    {
        if (boss.Pattern != BossPattern.Charge) return;
        Vector3 p = transform.position;
        StageFx.Dust(p + Vector3.up * 0.1f, new Vector3(2.4f, 0.05f, 2.4f));
        StageFx.Burst(p + Vector3.up * 0.3f + boss.AimDirection * 1.2f, RockColor, 20, 6f, 0.3f, 0.7f, 1.4f);
        StageFx.Pulse(p, WarmColor, 5f, 0.4f);
        StageFx.Shake(p, 0.35f, 0.3f, 50f);
        StageFx.Play(ProceduralSfx.Hit, p, 0.8f, 0.5f);
    }

    void SlamImpact()
    {
        Vector3 hit = boss.ImpactPoint;
        Vector3 ground = new Vector3(hit.x, transform.position.y, hit.z);
        float r = Boss.SlamRadius;
        Color c = PhaseColor;
        StageFx.Pulse(ground, c, r * 1.8f, 0.5f);
        StageFx.Pulse(ground, Color.white, r * 1.1f, 0.3f);
        StageFx.Dust(ground + Vector3.up * 0.1f, new Vector3(r * 0.8f, 0.05f, r * 0.8f));
        StageFx.Burst(ground + Vector3.up * 0.3f, RockColor, 45, 9f, 0.35f, 1f, 1.6f);
        StageFx.Burst(ground + Vector3.up * 0.2f, c, 30, 7f, 0.3f, 0.7f, 0.4f);
        StageFx.Flash(ground + Vector3.up * 1.5f, Color.Lerp(c, Color.white, 0.5f), 9f, 16f, 0.35f);
        StageFx.Shake(ground, 0.75f, 0.55f, 55f);
        FovPunch(4f, 0.2f);
        StageFx.Play(ProceduralSfx.HitHeavy, ground, 1f, 0.5f);
        StageFx.Play(ProceduralSfx.Shatter, ground, 0.5f, 0.8f);
        StartCoroutine(Cracks(ground, r, c));
    }

    IEnumerator Cracks(Vector3 centre, float radius, Color color)
    {
        for (int k = 0; k < 9; k++)
        {
            float angle = k * 40f + Random.Range(-12f, 12f);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            float length = radius * Random.Range(0.8f, 1.2f);
            var go = new GameObject("BossCrack");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = flat; line.useWorldSpace = true; line.widthMultiplier = Random.Range(0.12f, 0.22f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            line.startColor = line.endColor = Color.white;
            const int steps = 6;
            line.positionCount = steps;
            for (int i = 0; i < steps; i++)
            {
                float t = i / (steps - 1f);
                Vector3 point = centre + dir * Mathf.Lerp(0.6f, length, t) + side * (i == 0 ? 0f : Random.Range(-0.3f, 0.3f)) + Vector3.up * 0.07f;
                line.SetPosition(i, point);
            }
            go.AddComponent<BossFxFade>().Setup(Color.Lerp(color, Color.white, 0.3f), 2.4f, 0.95f, 0f);
            if (k % 3 == 2) yield return null;
        }
    }

    void ChargeStart()
    {
        Vector3 p = transform.position;
        StageFx.Dust(p + Vector3.up * 0.1f, new Vector3(2f, 0.05f, 2f));
        StageFx.Burst(p + Vector3.up * 0.4f - boss.AimDirection * 0.8f, shown, 24, 8f, 0.4f, 0.5f, 0.2f);
        StageFx.Pulse(p, shown, 4f, 0.35f);
        StageFx.Flash(p + Vector3.up * 2f, shown, 6f, 12f, 0.3f);
        StageFx.Shake(p, 0.3f, 0.3f, 50f);
        FovPunch(-6f, 0.3f);
        StageFx.Play(ProceduralSfx.SwingHeavy, p, 0.9f, 0.35f);
        ghostTimer = 0f; dustTimer = 0f;
    }

    void OnVolley(Boss source)
    {
        Vector3 origin = transform.position + Vector3.up;
        Vector3 muzzle = origin + boss.AimDirection * 1.4f;
        Color c = shown;
        StageFx.Flash(muzzle, Color.Lerp(c, Color.white, 0.5f), 8f, 12f, 0.18f);
        StageFx.Burst(muzzle, c, 20, 9f, 0.3f, 0.35f, 0f);
        StageFx.Burst(muzzle, Color.white, 8, 6f, 0.25f, 0.2f, 0f);
        var flash = new GameObject("BossFanFlash");
        flash.transform.SetPositionAndRotation(boss.AttackOrigin, Quaternion.LookRotation(boss.AimDirection, Vector3.up));
        flash.transform.localScale = Vector3.one * Boss.BarrageRange * 0.35f;
        flash.AddComponent<MeshFilter>().sharedMesh = FanMesh();
        var r = flash.AddComponent<MeshRenderer>();
        r.sharedMaterial = flat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        flash.AddComponent<BossFxFade>().Setup(Color.Lerp(c, Color.white, 0.4f), FanFlashTime, 0.6f, 2f);
        StageFx.Shake(muzzle, 0.14f, 0.12f, 40f);
        StageFx.Play(ProceduralSfx.NailShot, muzzle, 0.7f, 0.6f);
    }

    void SummonImpact()
    {
        Color c = new Color(0.8f, 0.4f, 1f);
        foreach (var point in boss.SummonPoints)
        {
            StageFx.Pulse(point, c, 3f, 0.5f);
            StageFx.Beam(point, c, 10f, 1.2f, 0.5f);
            StageFx.Burst(point + Vector3.up * 0.3f, c, 20, 5f, 0.35f, 0.7f, 0.3f);
            StageFx.Dust(point + Vector3.up * 0.1f, new Vector3(0.8f, 0.05f, 0.8f));
            StageFx.Flash(point + Vector3.up * 1.5f, c, 5f, 8f, 0.3f);
        }
        Vector3 p = transform.position;
        StageFx.Pulse(p, c, 6f, 0.5f);
        StageFx.Shake(p, 0.3f, 0.3f, 50f);
        StageFx.Play(ProceduralSfx.Upgrade, p, 0.7f, 0.5f);
        StageFx.Play(ProceduralSfx.SwingHeavy, p, 0.7f, 0.4f);
    }

    void OnTransitionStarted(Boss source)
    {
        Vector3 p = transform.position;
        StageFx.Flash(p + Vector3.up * 3f, ShieldColor, 8f, 18f, 0.6f);
        StageFx.Pulse(p, ShieldColor, 9f, 0.6f);
        pulseTimer = 0.2f;
    }

    void OnRoared(Boss source)
    {
        if (!dead) StartCoroutine(Roar(0.8f, PhaseColors[Mathf.Clamp(boss.Phase - 1, 0, 2)]));
    }

    void OnTransitionEnded(Boss source)
    {
        Vector3 p = transform.position;
        Color c = PhaseColors[Mathf.Clamp(boss.Phase - 1, 0, 2)];
        StageFx.Pulse(p, c, 14f, 0.7f);
        StageFx.Pulse(p, Color.white, 7f, 0.4f);
        StageFx.Beam(p, c, 16f, 2.5f, 0.6f);
        StageFx.Burst(p + Vector3.up * 1.2f, c, 80, 11f, 0.5f, 1f, 0.3f);
        StageFx.Burst(p + Vector3.up * 0.2f, ShieldColor, 40, 9f, 0.4f, 0.8f, 0.5f);
        StageFx.Flash(p + Vector3.up * 3f, c, 10f, 22f, 0.6f);
        StageFx.Shake(p, 0.6f, 0.6f, 60f);
        FovPunch(5f, 0.3f);
        StageFx.Play(ProceduralSfx.HitHeavy, p, 1f, 0.45f);
        StageFx.Play(ProceduralSfx.Shatter, p, 0.7f, 0.6f);
        if (StageFx.Instance != null) StageFx.Instance.Announce("PHASE " + boss.Phase, boss.Phase == 3 ? "THE CRUSHER IS ENRAGED" : "THE CRUSHER GROWS FIERCE", c);
    }

    // Entry roar: called by StageFx when the final arena opens.
    public void PlayIntro()
    {
        introQueued = true;
        if (isActiveAndEnabled && !dead) RunIntro();
    }

    void RunIntro()
    {
        introQueued = false;
        StartCoroutine(Roar(1f, PhaseColors[0]));
    }

    IEnumerator Roar(float power, Color c)
    {
        Vector3 p = transform.position;
        StageFx.Play(ProceduralSfx.HitHeavy, p, 1f, 0.3f);
        StageFx.Play(ProceduralSfx.SwingHeavy, p, 0.9f, 0.3f);
        StageFx.Shake(p, 0.9f * power, 1.3f, 70f);
        FovPunch(-9f * power, 0.5f);
        roarGlow = power;
        for (int i = 0; i < 4 && !dead; i++)
        {
            StageFx.Pulse(p, i % 2 == 0 ? c : Color.white, 9f + i * 4f, 0.8f);
            StageFx.Burst(p + Vector3.up * 0.3f, c, 20, 8f, 0.4f, 0.8f, 0.2f);
            StageFx.Dust(p + Vector3.up * 0.1f, new Vector3(3f, 0.05f, 3f));
            StageFx.Flash(p + Vector3.up * 3.5f, c, 6f, 20f, 0.3f);
            StageFx.Shake(p, 0.35f * power, 0.25f, 60f);
            yield return new WaitForSecondsRealtime(0.28f);
        }
    }

    // ---- defeat -----------------------------------------------------------------------------------------------------

    void OnDefeated(Boss source)
    {
        dead = true;
        HideTelegraphs();
        if (decor != null) decor.gameObject.SetActive(false);
        MonoBehaviour host = StageFx.Instance != null ? (MonoBehaviour)StageFx.Instance : this;
        host.StartCoroutine(DefeatRoutine(transform.position, body != null ? body.bounds.center : transform.position + Vector3.up * 2f, PhaseColors[2]));
    }

    IEnumerator DefeatRoutine(Vector3 feet, Vector3 centre, Color c)
    {
        // Short global slow-motion on the killing blow (kept within 0.3..1 timescale and under 0.6 s real time).
        StartCoroutine(SlowMo());
        StageFx.Play(ProceduralSfx.Shatter, feet, 1f, 0.5f);
        StageFx.Shake(feet, 0.8f, 0.6f, 70f);
        float end = Time.unscaledTime + 1.25f;
        while (Time.unscaledTime < end)
        {
            Vector3 p = centre + Random.insideUnitSphere * 1.6f;
            Color blast = Random.value < 0.5f ? WarmColor : c;
            StageFx.Burst(p, blast, 30, 8f, 0.5f, 0.8f, 0.5f);
            StageFx.Burst(p, Color.white, 8, 5f, 0.3f, 0.4f, 0f);
            StageFx.Flash(p, blast, 7f, 14f, 0.3f);
            StageFx.Pulse(new Vector3(p.x, feet.y, p.z), blast, 3.5f, 0.4f);
            StageFx.Shake(p, 0.28f, 0.2f, 60f);
            StageFx.Play(ProceduralSfx.Hit, p, 0.7f, Random.Range(0.4f, 0.7f));
            StageFx.Dust(feet + Vector3.up * 0.1f, new Vector3(2f, 0.05f, 2f));
            yield return new WaitForSecondsRealtime(0.11f);
        }
        // Final collapse blast as the body disappears.
        StageFx.Pulse(feet, Color.white, 12f, 0.6f);
        StageFx.Pulse(feet, c, 20f, 1f);
        StageFx.Pulse(feet, WarmColor, 28f, 1.3f);
        StageFx.Beam(feet, Color.white, 26f, 5f, 0.9f);
        StageFx.Burst(centre, WarmColor, 140, 14f, 0.7f, 1.4f, 0.6f);
        StageFx.Burst(centre, c, 90, 10f, 0.5f, 1.2f, 0.4f);
        StageFx.Burst(feet + Vector3.up * 0.3f, RockColor, 60, 12f, 0.4f, 1.2f, 1.6f);
        StageFx.Dust(feet + Vector3.up * 0.1f, new Vector3(4f, 0.05f, 4f));
        StageFx.Flash(centre, Color.white, 14f, 30f, 0.9f);
        StageFx.Shake(feet, 1.1f, 0.9f, 80f);
        StageFx.Play(ProceduralSfx.HitHeavy, feet, 1f, 0.3f);
        StageFx.Play(ProceduralSfx.Upgrade, feet, 0.6f, 0.5f);
        whiteFlash = 0.8f;
        FovPunch(8f, 0.4f);
    }

    IEnumerator SlowMo()
    {
        if (Time.timeScale < 0.9f) yield break;
        slowing = true;
        Time.timeScale = SlowScale;
        yield return new WaitForSecondsRealtime(SlowHold);
        for (float t = 0f; t < SlowEase; t += Time.unscaledDeltaTime)
        {
            if (!slowing) yield break;
            Time.timeScale = Mathf.Lerp(SlowScale, 1f, t / SlowEase);
            yield return null;
        }
        EndSlow();
    }

    void EndSlow()
    {
        if (!slowing) return;
        slowing = false;
        if (Time.timeScale > 0f && Time.timeScale < 1f) Time.timeScale = 1f;
    }

    void FovPunch(float amount, float duration)
    {
        if (cameraRig == null) cameraRig = FindFirstObjectByType<ThirdPersonCamera>();
        if (cameraRig != null) cameraRig.FovPunch(amount, duration);
    }

    // Red edge glow while roaring and a white flash on the final blast.
    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || (roarGlow <= 0.01f && whiteFlash <= 0.01f)) return;
        var full = new Rect(0f, 0f, Screen.width, Screen.height);
        Color c = PhaseColor;
        if (roarGlow > 0.01f)
        {
            GUI.color = new Color(c.r, c.g, c.b, Mathf.Min(1f, roarGlow) * 0.65f);
            GUI.DrawTexture(full, VignetteTexture());
        }
        if (whiteFlash > 0.01f)
        {
            GUI.color = new Color(1f, 1f, 1f, whiteFlash);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }
}

// Self-driving fade for ghosts, cracks and flashes; optional growth.
public class BossFxFade : MonoBehaviour
{
    static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    static readonly int ColorId = Shader.PropertyToID("_Color");
    Renderer target;
    Color color;
    float start, duration, alpha, grow;
    Vector3 baseScale;

    public void Setup(Color color, float duration, float alpha, float grow)
    {
        this.color = color; this.duration = Mathf.Max(duration, 0.01f); this.alpha = alpha; this.grow = grow;
        target = GetComponent<Renderer>();
        baseScale = transform.localScale;
        start = Time.time;
        Apply(0f);
    }

    void Update()
    {
        float t = (Time.time - start) / duration;
        if (t >= 1f) { Destroy(gameObject); return; }
        Apply(t);
    }

    void Apply(float t)
    {
        if (grow > 0f) transform.localScale = baseScale * (1f + grow * t);
        if (target == null) return;
        target.GetPropertyBlock(block);
        block.SetColor(ColorId, new Color(color.r, color.g, color.b, alpha * (1f - t)));
        target.SetPropertyBlock(block);
    }
}
