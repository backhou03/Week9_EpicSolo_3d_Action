using System.Collections.Generic;
using UnityEngine;

// Enemy telegraph and attack visuals, driven by EnemyAttack events. Melee: short ground fan that fills fast, then a slash.
// Ranged: dashed aim line that tightens, thickens and whitens, then a muzzle flash. All procedural, one mesh per enemy.
public class EnemyTelegraphFx : MonoBehaviour
{
    public static readonly Color RangedCore = new Color(1f, 0.25f, 0.8f);
    static readonly Color MeleeCold = new Color(1f, 0.18f, 0.08f);
    static readonly Color MeleeHot = new Color(1f, 0.75f, 0.2f);
    static readonly Color RangedCold = new Color(0.65f, 0.35f, 1f);

    const float FlashTime = 0.14f;
    const float GroundY = 0.06f;
    const int ArcSegments = 14;
    const float DashLength = 0.55f;
    const float SlashHeight = 1f;

    static Material glowMaterial;
    static Texture2D glowTexture;
    static readonly RaycastHit[] rayHits = new RaycastHit[8];

    EnemyAttack attack;
    Enemy enemy;
    Transform body;
    Mesh mesh;
    MeshRenderer meshRenderer;
    Camera cam;
    float fireT;
    Vector3 fireOrigin, fireAim;
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Color> colors = new List<Color>();
    readonly List<Vector2> uvs = new List<Vector2>();
    readonly List<int> tris = new List<int>();

    // Soft round material shared by every enemy effect and the projectile trail.
    public static Material GlowMaterial
    {
        get
        {
            if (glowMaterial != null) return glowMaterial;
            glowTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float r = Vector2.Distance(new Vector2((x + 0.5f) / 32f, (y + 0.5f) / 32f), new Vector2(0.5f, 0.5f));
                    glowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0.12f, 0.5f, r)));
                }
            glowTexture.Apply();
            glowMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = glowTexture };
            return glowMaterial;
        }
    }

    public static void Attach(EnemyAttack source)
    {
        var fx = new GameObject("EnemyAttackFx").AddComponent<EnemyTelegraphFx>();
        fx.Configure(source);
    }

    void Configure(EnemyAttack source)
    {
        attack = source;
        enemy = source.GetComponent<Enemy>();
        body = source.transform.Find("Body");
        mesh = new Mesh { name = "EnemyAttackFx" };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = GlowMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.enabled = false;
        attack.AttackFired += OnFired;
        attack.AttackCancelled += OnCancelled;
        PlayerHurtFx.Ensure();
    }

    void OnDestroy()
    {
        if (attack != null)
        {
            attack.AttackFired -= OnFired;
            attack.AttackCancelled -= OnCancelled;
        }
        if (mesh != null) Destroy(mesh);
    }

    void OnFired(EnemyAttack a)
    {
        fireT = FlashTime;
        fireOrigin = a.AttackOrigin;
        fireAim = a.AimDirection;
        bool melee = a.Kind == EnemyAttackKind.Melee;
        AudioSource.PlayClipAtPoint(melee ? ProceduralSfx.Swing : ProceduralSfx.NailShot, fireOrigin, melee ? 0.5f : 0.35f);
    }

    void OnCancelled(EnemyAttack a)
    {
        fireT = 0f;
        meshRenderer.enabled = false;
    }

    void LateUpdate()
    {
        if (attack == null || enemy == null) { Destroy(gameObject); return; }
        cam = Camera.main;
        bool melee = attack.Kind == EnemyAttackKind.Melee;
        mesh.Clear();
        verts.Clear();
        colors.Clear();
        uvs.Clear();
        tris.Clear();

        if (enemy.IsDead)
        {
            fireT = 0f;
        }
        else if (attack.IsTelegraphing)
        {
            float p = attack.TelegraphProgress;
            if (melee) DrawMeleeTelegraph(p);
            else DrawRangedTelegraph(p);
            enemy.TelegraphColor = TintFor(melee, p);
            AnimateBody(melee, p, 0f);
        }
        else if (fireT > 0f)
        {
            fireT -= Time.deltaTime;
            float u = Mathf.Clamp01(fireT / FlashTime);
            if (melee) DrawMeleeFlash(u);
            else DrawRangedFlash(u);
            AnimateBody(melee, 0f, u);
        }

        meshRenderer.enabled = verts.Count > 0;
        if (verts.Count == 0) return;
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    // ---- Body ---------------------------------------------------------------

    static Color TintFor(bool melee, float p)
    {
        Color c = Color.Lerp(melee ? MeleeCold : RangedCold, melee ? MeleeHot : RangedCore, p * p);
        bool blink = Mathf.Sin(Time.time * Mathf.Lerp(20f, 55f, p)) > 0f;
        if (p > 0.55f && blink) c = Color.Lerp(c, Color.white, 0.6f);
        if (p > 0.9f) c = Color.white;
        return c;
    }

    // Windup leans the body back and swells it; the release snaps it forward (melee) or recoils (ranged).
    void AnimateBody(bool melee, float p, float fireU)
    {
        if (body == null) return;
        float e = 1f - (1f - p) * (1f - p);
        Vector3 right = Vector3.Cross(Vector3.up, fireU > 0f ? fireAim : attack.AimDirection);
        float lean = melee ? -28f * e + 40f * fireU : -8f * e - 16f * fireU;
        float swell = melee ? 1f + 0.16f * e + 0.18f * fireU : 1f + 0.1f * p;
        body.rotation = Quaternion.AngleAxis(lean, right) * body.rotation;
        body.localScale *= swell;
        if (!melee && p > 0.6f) body.localPosition += Random.insideUnitSphere * (0.035f * p * p);
    }

    // ---- Melee --------------------------------------------------------------

    void DrawMeleeTelegraph(float p)
    {
        Vector3 origin = attack.transform.position + Vector3.up * GroundY;
        Vector3 aim = attack.AimDirection;
        float reach = EnemyAttack.MeleeRange + 0.4f;
        float half = EnemyAttack.MeleeHalfAngle;
        float e = 1f - (1f - p) * (1f - p);
        Color heat = Color.Lerp(MeleeCold, MeleeHot, p * p);
        if (p > 0.85f) heat = Color.Lerp(heat, Color.white, (p - 0.85f) / 0.15f);

        Sector(origin, aim, half, 0f, reach, WithAlpha(heat, 0.1f), WithAlpha(heat, 0.16f));
        Sector(origin, aim, half, 0f, reach * e, WithAlpha(heat, 0.25f), WithAlpha(heat, 0.7f));
        Sector(origin, aim, half, reach - 0.14f, reach, WithAlpha(heat, 0.6f + 0.4f * p), WithAlpha(heat, 0.6f + 0.4f * p));
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 tip = origin + Quaternion.Euler(0f, half * s, 0f) * aim * reach;
            Quad(origin, tip, GroundSide(tip - origin), 0.05f, 0.05f, WithAlpha(heat, 0.9f), WithAlpha(heat, 0.9f));
        }
    }

    // A bright flash of the whole fan plus a slash sweeping across it at chest height.
    void DrawMeleeFlash(float u)
    {
        Vector3 origin = fireOrigin;
        origin.y = attack.transform.position.y + GroundY;
        float reach = EnemyAttack.MeleeRange + 0.4f;
        float half = EnemyAttack.MeleeHalfAngle;
        Sector(origin, fireAim, half, 0f, reach, WithAlpha(Color.white, 0.5f * u), WithAlpha(MeleeHot, 0.7f * u));

        float sweep = 1f - u;
        float tail = Mathf.Max(-half, Mathf.Lerp(-half, half, sweep) - 40f);
        float head = Mathf.Lerp(-half, half, Mathf.Min(1f, sweep * 1.4f));
        Vector3 chest = fireOrigin;
        for (int i = 0; i < ArcSegments; i++)
        {
            float a0 = Mathf.Lerp(tail, head, i / (float)ArcSegments);
            float a1 = Mathf.Lerp(tail, head, (i + 1) / (float)ArcSegments);
            Vector3 p0 = chest + Quaternion.Euler(0f, a0, 0f) * fireAim * (reach * 0.85f);
            Vector3 p1 = chest + Quaternion.Euler(0f, a1, 0f) * fireAim * (reach * 0.85f);
            float f0 = (i / (float)ArcSegments), f1 = (i + 1) / (float)ArcSegments;
            Quad(p0, p1, Vector3.up, 0.32f * f0, 0.32f * f1, WithAlpha(Color.white, f0 * u), WithAlpha(Color.white, f1 * u));
        }
    }

    // ---- Ranged -------------------------------------------------------------

    void DrawRangedTelegraph(float p)
    {
        Vector3 aim = attack.AimDirection;
        Vector3 eye = attack.AttackOrigin;
        float length = BeamLength(eye, aim);
        Vector3 ground = attack.transform.position + Vector3.up * GroundY;
        Color c = Color.Lerp(RangedCold, RangedCore, Mathf.SmoothStep(0f, 1f, p * 1.4f));
        if (p > 0.88f) c = Color.Lerp(c, Color.white, (p - 0.88f) / 0.12f);
        float alpha = Mathf.Lerp(0.5f, 1f, p);
        float width = Mathf.Lerp(0.05f, 0.19f, p);

        // Dashes close up into a solid lane as the shot nears.
        float gap = Mathf.Lerp(0.8f, 0f, Mathf.SmoothStep(0.4f, 0.9f, p));
        Vector3 side = GroundSide(aim);
        float start = 0.9f;
        for (float s = start; s < length; s += DashLength + gap)
        {
            float end = Mathf.Min(s + DashLength, length);
            Quad(ground + aim * s, ground + aim * end, side, width, width, WithAlpha(c, alpha), WithAlpha(c, alpha));
            if (verts.Count > 3000) break;
        }

        // Thin chest-height beam so the line also reads from a low camera angle.
        Vector3 tip = eye + aim * length;
        Vector3 ribbonSide = FaceCamera(eye, tip);
        float line = Mathf.Lerp(0.015f, 0.07f, p);
        Quad(eye + aim * start, tip, ribbonSide, line, line, WithAlpha(c, 0.25f + 0.55f * p), WithAlpha(c, 0.25f + 0.55f * p));

        // Charge orb at the muzzle.
        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 40f) * p;
        Disc(eye + aim * 0.6f, Mathf.Lerp(0.25f, 1.1f, p) * pulse, WithAlpha(c, 0.5f + 0.5f * p));
    }

    void DrawRangedFlash(float u)
    {
        Vector3 eye = fireOrigin;
        float length = BeamLength(eye, fireAim);
        Vector3 ground = new Vector3(eye.x, attack.transform.position.y + GroundY, eye.z);
        float width = Mathf.Lerp(0.02f, 0.4f, u);
        Quad(ground, ground + fireAim * length, GroundSide(fireAim), width, width, WithAlpha(Color.white, u), WithAlpha(RangedCore, 0.6f * u));
        Vector3 tip = eye + fireAim * length;
        Quad(eye, tip, FaceCamera(eye, tip), 0.1f * u, 0.1f * u, WithAlpha(Color.white, u), WithAlpha(RangedCore, 0.4f * u));
        Disc(eye + fireAim * 0.5f, Mathf.Lerp(0.6f, 1.8f, u), WithAlpha(Color.white, u));
    }

    // Same stop rule as the projectile: first non-enemy collider.
    static float BeamLength(Vector3 origin, Vector3 dir)
    {
        int n = Physics.RaycastNonAlloc(origin, dir, rayHits, EnemyAttack.RangedRange, ~0, QueryTriggerInteraction.Ignore);
        float best = EnemyAttack.RangedRange;
        for (int i = 0; i < n; i++)
        {
            if (rayHits[i].collider.GetComponentInParent<Enemy>() != null) continue;
            best = Mathf.Min(best, rayHits[i].distance);
        }
        return best;
    }

    // ---- Mesh helpers -------------------------------------------------------

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    static Vector3 GroundSide(Vector3 dir)
    {
        Vector3 s = Vector3.Cross(Vector3.up, dir);
        return s.sqrMagnitude < 1e-4f ? Vector3.right : s.normalized;
    }

    Vector3 FaceCamera(Vector3 a, Vector3 b)
    {
        if (cam == null) return Vector3.up;
        Vector3 s = Vector3.Cross((b - a).normalized, cam.transform.position - (a + b) * 0.5f);
        return s.sqrMagnitude < 1e-4f ? Vector3.up : s.normalized;
    }

    void Quad(Vector3 a, Vector3 b, Vector3 side, float wa, float wb, Color ca, Color cb)
    {
        int i = verts.Count;
        verts.Add(a - side * wa); colors.Add(ca);
        verts.Add(a + side * wa); colors.Add(ca);
        verts.Add(b + side * wb); colors.Add(cb);
        verts.Add(b - side * wb); colors.Add(cb);
        for (int k = 0; k < 4; k++) uvs.Add(new Vector2(0.5f, 0.5f));
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
    }

    // Annular sector on the ground, r0..r1, spanning +-halfAngle around dir.
    void Sector(Vector3 origin, Vector3 dir, float halfAngle, float r0, float r1, Color c0, Color c1)
    {
        int first = verts.Count;
        for (int i = 0; i <= ArcSegments; i++)
        {
            Vector3 d = Quaternion.Euler(0f, Mathf.Lerp(-halfAngle, halfAngle, i / (float)ArcSegments), 0f) * dir;
            verts.Add(origin + d * r0); colors.Add(c0);
            verts.Add(origin + d * r1); colors.Add(c1);
            uvs.Add(new Vector2(0.5f, 0.5f));
            uvs.Add(new Vector2(0.5f, 0.5f));
        }
        for (int i = 0; i < ArcSegments; i++)
        {
            int k = first + i * 2;
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 3);
            tris.Add(k); tris.Add(k + 3); tris.Add(k + 2);
        }
    }

    // Camera-facing soft glow.
    void Disc(Vector3 center, float size, Color c)
    {
        Vector3 right = cam != null ? cam.transform.right : Vector3.right;
        Vector3 up = cam != null ? cam.transform.up : Vector3.up;
        int i = verts.Count;
        verts.Add(center - right * size - up * size); uvs.Add(new Vector2(0f, 0f));
        verts.Add(center - right * size + up * size); uvs.Add(new Vector2(0f, 1f));
        verts.Add(center + right * size + up * size); uvs.Add(new Vector2(1f, 1f));
        verts.Add(center + right * size - up * size); uvs.Add(new Vector2(1f, 0f));
        for (int k = 0; k < 4; k++) colors.Add(c);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
    }
}

// Enemy projectile look: hot core, point light and a fading trail that lingers after the hit.
public static class EnemyProjectileFx
{
    public static void Decorate(GameObject shot)
    {
        var light = shot.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = EnemyTelegraphFx.RangedCore;
        light.range = 4f;
        light.intensity = 2.5f;
        light.shadows = LightShadows.None;

        var go = new GameObject("Trail");
        go.transform.SetParent(shot.transform, false);
        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = EnemyTelegraphFx.GlowMaterial;
        trail.time = 0.3f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 0f));
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(EnemyTelegraphFx.RangedCore, 0.35f), new GradientColorKey(new Color(0.6f, 0.2f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
    }

    // Leaves the trail behind to fade out on its own.
    public static void ReleaseTrail(GameObject shot)
    {
        var trail = shot.GetComponentInChildren<TrailRenderer>();
        if (trail == null) return;
        trail.transform.SetParent(null, true);
        Object.Destroy(trail.gameObject, trail.time + 0.05f);
    }
}

// Player hurt / dodge feedback. Watches WeaponAvatar.Hp so no gameplay file needs a hook.
public class PlayerHurtFx : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly Color HurtRed = new Color(1f, 0.12f, 0.1f);
    static PlayerHurtFx instance;

    const float VignetteTime = 0.45f;
    const float DodgeTime = 0.25f;
    const float StrongTintTime = 0.15f;

    WeaponAvatar avatar;
    Texture2D vignette;
    int lastHp;
    float hurtTime = -10f, dodgeTime = -10f;
    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<MaterialPropertyBlock> saved = new List<MaterialPropertyBlock>();
    readonly MaterialPropertyBlock scratch = new MaterialPropertyBlock();

    public static void Ensure()
    {
        if (instance == null) instance = new GameObject("PlayerHurtFx").AddComponent<PlayerHurtFx>();
    }

    // Called when an enemy attack lands while the avatar is invulnerable (dash, respawn grace).
    public static void Dodge()
    {
        if (instance != null) instance.OnDodge();
    }

    void Awake()
    {
        instance = this;
        vignette = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float r = Vector2.Distance(new Vector2((x + 0.5f) / 64f, (y + 0.5f) / 64f), new Vector2(0.5f, 0.5f)) * 2f;
                vignette.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0.45f, 1.1f, r)));
            }
        vignette.Apply();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (vignette != null) Destroy(vignette);
    }

    void Update()
    {
        if (avatar == null)
        {
            avatar = FindFirstObjectByType<WeaponAvatar>();
            if (avatar == null) return;
            lastHp = avatar.Hp;
        }
        int hp = avatar.Hp;
        if (hp < lastHp) OnHurt(lastHp - hp, hp <= 0);
        else if (hp > lastHp) Restore();
        lastHp = hp;
        UpdateTint(hp);
    }

    void OnHurt(int damage, bool died)
    {
        hurtTime = Time.time;
        Restore();
        var rig = avatar.CameraRig;
        if (rig == null) return;
        rig.Shake((0.16f + damage * 0.012f) * (died ? 2f : 1f), died ? 0.5f : 0.3f);
        rig.FovPunch(-2.5f, 0.2f);
    }

    void OnDodge()
    {
        if (avatar == null || avatar.IsDead || Time.time - hurtTime < WeaponAvatar.HurtInvulnerability || Time.time - dodgeTime < 0.2f) return;
        dodgeTime = Time.time;
        if (avatar.CameraRig != null) avatar.CameraRig.FovPunch(2.5f, 0.2f);
        AudioSource.PlayClipAtPoint(ProceduralSfx.Snip, avatar.Center, 0.3f);
    }

    // Body flashes red on the hit, then blinks through the invulnerability window.
    void UpdateTint(int hp)
    {
        float t = Time.time - hurtTime;
        if (hp <= 0 || t >= WeaponAvatar.HurtInvulnerability)
        {
            Restore();
            return;
        }
        if (renderers.Count == 0) Collect();
        float strength = t < StrongTintTime ? 1f : (Mathf.Sin(t * 50f) > 0f ? 0.55f : 0f);
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] == null) continue;
            if (strength <= 0f)
            {
                renderers[i].SetPropertyBlock(saved[i]);
                continue;
            }
            Color original = saved[i].HasColor(BaseColorId) ? saved[i].GetColor(BaseColorId) : renderers[i].sharedMaterial.GetColor(BaseColorId);
            renderers[i].GetPropertyBlock(scratch);
            scratch.SetColor(BaseColorId, Color.Lerp(original, HurtRed, strength));
            renderers[i].SetPropertyBlock(scratch);
        }
    }

    void Collect()
    {
        foreach (var r in avatar.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (r.sharedMaterial == null || !r.sharedMaterial.HasProperty(BaseColorId)) continue;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            renderers.Add(r);
            saved.Add(block);
        }
    }

    void Restore()
    {
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i] != null) renderers[i].SetPropertyBlock(saved[i]);
        renderers.Clear();
        saved.Clear();
    }

    // Red edge vignette on hit, cyan on a dodge.
    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || avatar == null) return;
        float hurt = 1f - (Time.time - hurtTime) / VignetteTime;
        float dodge = 1f - (Time.time - dodgeTime) / DodgeTime;
        var screen = new Rect(0f, 0f, Screen.width, Screen.height);
        if (hurt > 0f)
        {
            GUI.color = new Color(1f, 0.05f, 0.05f, Mathf.Clamp01(hurt) * 0.8f);
            GUI.DrawTexture(screen, vignette);
        }
        if (dodge > 0f)
        {
            GUI.color = new Color(0.3f, 0.9f, 1f, Mathf.Clamp01(dodge) * 0.5f);
            GUI.DrawTexture(screen, vignette);
        }
        GUI.color = Color.white;
    }
}
