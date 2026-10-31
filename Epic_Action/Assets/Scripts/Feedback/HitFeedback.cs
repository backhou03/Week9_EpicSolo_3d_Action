using System.Collections.Generic;
using UnityEngine;

public class HitFeedback : MonoBehaviour
{

    public static HitFeedback Instance { get; private set; }

    [SerializeField] ThirdPersonCamera cameraRig;

    AudioSource audioSource;
    Material particleMaterial;
    Material splatMaterial;
    readonly Queue<GameObject> splats = new Queue<GameObject>();

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        particleMaterial = new Material(Shader.Find("Sprites/Default"));
        splatMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = BuildSplatTexture() };
    }

    void OnDisable() => Time.timeScale = 1f;

    void OnDestroy()
    {
        Time.timeScale = 1f;
        if (Instance == this) Instance = null;
    }

    public void PlaySwing(bool heavy)
    {
        Play(heavy ? ProceduralSfx.SwingHeavy : ProceduralSfx.Swing, heavy ? 0.7f : 0.5f, Random.Range(0.92f, 1.08f));
    }

    public void PlayHit(HitInfo info, WeaponData weapon, SwingData swing, bool kill)
    {
        float boost = kill ? 1.5f : 1f;

        cameraRig.Shake(weapon.shake * swing.shakeScale * boost, 0.22f);
        cameraRig.FovPunch(weapon.fovPunch * (swing.heavy ? 1.6f : 1f) * boost, 0.2f);

        Vector3 spray = info.direction + Vector3.up * 0.35f;
        int heavyMul = swing.heavy || kill ? 2 : 1;
        Burst(info.point, spray, new Color(1f, 0.92f, 0.45f), 14 * heavyMul, 9f * boost, 0.07f, 0.35f, 38f, true);
        Burst(info.point, spray, Color.white, 1, 0f, 0.7f * (swing.heavy || kill ? 1.5f : 1f), 0.07f, 0f, false);

        Play(swing.heavy || kill ? ProceduralSfx.HitHeavy : ProceduralSfx.Hit, 0.9f, Random.Range(0.92f, 1.1f));
    }

    // One continuous-cut tick: a few sparks and a small rumble. The engine growl comes from ChainsawDriver.
    public void PlayTick(HitInfo info)
    {
        cameraRig.Shake(0.06f, 0.12f);
        Burst(info.point, info.direction + Vector3.up * 0.5f, new Color(1f, 0.8f, 0.3f), 5, 7f, 0.05f, 0.25f, 40f, true);
    }

    // The blades of shears just met: a bright flash and a ring of metal.
    public void PlaySnip(Vector3 point)
    {
        cameraRig.Shake(0.08f, 0.1f);
        Burst(point, Vector3.up, Color.white, 10, 6f, 0.05f, 0.18f, 180f, true);
        Burst(point, Vector3.up, Color.white, 1, 0f, 0.6f, 0.06f, 0f, false);
        Play(ProceduralSfx.Snip, 0.8f, Random.Range(0.95f, 1.1f));
    }

    // Red droplets, a wet thud and a stain on the ground.
    public void PlayBlood(Vector3 point, Vector3 direction, float amount)
    {
        Vector3 spray = direction + Vector3.up * 0.4f;
        Burst(point, spray, new Color(0.72f, 0.02f, 0.05f), Mathf.RoundToInt(16 * amount), 7f, 0.09f, 0.7f, 50f, false, 2.4f);
        Burst(point, spray, new Color(0.45f, 0f, 0.03f), Mathf.RoundToInt(8 * amount), 4f, 0.16f, 0.5f, 70f, false, 1.8f);
        Play(ProceduralSfx.Squelch, 0.5f * Mathf.Min(amount, 1.5f), Random.Range(0.9f, 1.15f));
        Splat(point, amount);
    }

    // The killing cut: the enemy has just been split in two.
    public void PlaySlice(Vector3 point, Vector3 direction, bool bloody = false)
    {
        if (bloody) PlayBlood(point, direction, 2.2f);
        cameraRig.Shake(0.45f, 0.35f);
        cameraRig.FovPunch(6f, 0.3f);
        Burst(point, direction + Vector3.up * 0.4f, new Color(1f, 0.85f, 0.35f), 48, 11f, 0.08f, 0.45f, 55f, true);
        Burst(point, Vector3.up, Color.white, 1, 0f, 1.6f, 0.09f, 0f, false);
        Play(ProceduralSfx.HitHeavy, 1f, 0.8f);
        Play(ProceduralSfx.Shatter, 0.6f, 1.15f);
    }

    public void PlaySonic(bool evolved)
    {
        Play(evolved ? ProceduralSfx.SonicHeavy : ProceduralSfx.Sonic, evolved ? 0.95f : 0.65f, 1f);
        cameraRig.Shake(evolved ? 0.28f : 0.07f, evolved ? 0.25f : 0.12f);
        cameraRig.FovPunch(evolved ? 3f : 0.7f, 0.2f);
    }

    public void PlaySonicImpact(Vector3 point, Vector3 direction, bool crush)
    {
        Burst(point, crush ? Vector3.up : direction, crush ? new Color(1f, 0.45f, 0.12f) : new Color(0.4f, 0.9f, 1f),
            crush ? 28 : 8, crush ? 7f : 4f, crush ? 0.14f : 0.06f, 0.3f, 65f, false);
        if (crush) Play(ProceduralSfx.HitHeavy, 0.55f, 0.8f);
    }

    public void PlaySonicExplosion(Vector3 point, Vector3 direction)
    {
        cameraRig.Shake(0.35f, 0.22f);
        Burst(point, Vector3.up, new Color(1f, 0.35f, 0.08f), 48, 10f, 0.16f, 0.45f, 180f, false);
        Burst(point, direction, Color.white, 2, 0f, 1.5f, 0.09f, 0f, false);
        Play(ProceduralSfx.HitHeavy, 0.8f, 0.7f);
        Play(ProceduralSfx.Shatter, 0.65f, 1.1f);
    }

    public void PlayNailShot(Vector3 point, Vector3 direction, bool piercing)
    {
        cameraRig.Shake(piercing ? 0.34f : 0.13f, piercing ? 0.22f : 0.12f);
        cameraRig.FovPunch(piercing ? 3.8f : 1.2f, piercing ? 0.24f : 0.12f);
        Color color = piercing ? new Color(1f, 0.78f, 0.32f) : new Color(1f, 0.65f, 0.18f);
        Burst(point, -direction, color, piercing ? 38 : 16, piercing ? 11f : 6f, 0.045f, 0.2f, 65f, true);
        if (piercing) Burst(point, direction, color, 14, 16f, 0.04f, 0.12f, 18f, true, 0.3f);
        Burst(point, direction, Color.white, 1, 0f, piercing ? 0.9f : 0.55f, 0.055f, 0f, false);
        var flash = new GameObject("NailgunMuzzleFlash");
        flash.transform.position = point - direction * 0.08f;
        var light = flash.AddComponent<Light>();
        light.color = color; light.range = piercing ? 2f : 1.1f; light.intensity = piercing ? 6f : 3f;
        Destroy(flash, 0.055f);
        Play(piercing ? ProceduralSfx.NailPierce : ProceduralSfx.NailShot, piercing ? 1f : 0.85f, Random.Range(0.95f,1.05f));
    }

    public void PlayNailPierce(Vector3 point, Vector3 direction)
    {
        PlayBlood(point, direction, 1.1f);
        cameraRig.Shake(0.12f, 0.1f);
        Burst(point, direction, new Color(0.72f, 0.02f, 0.05f), 8, 9f, 0.07f, 0.45f, 25f, false, 2f);
        Color spark = new Color(1f, 0.82f, 0.4f);
        Burst(point, direction, spark, 32, 16f, 0.055f, 0.3f, 30f, true, 1.6f);
        Burst(point, -direction, spark, 20, 9f, 0.045f, 0.25f, 70f, true, 1.6f);
        Burst(point, Vector3.up, new Color(1f, 0.55f, 0.15f), 12, 5f, 0.04f, 0.35f, 120f, true, 2.2f);
        Burst(point, direction, Color.white, 1, 0f, 0.6f, 0.05f, 0f, false);
    }

    public void PlayUpgrade()
    {
        Play(ProceduralSfx.Upgrade, 0.8f, 1f);
        cameraRig.FovPunch(5f, 0.6f);
        cameraRig.Shake(0.05f, 0.6f);
    }

    public void PlayUpgradeFlash(Vector3 position)
    {
        cameraRig.FovPunch(-8f, 0.3f);
        cameraRig.Shake(0.3f, 0.3f);
        Burst(position, Vector3.up, new Color(1f, 0.85f, 0.3f), 60, 8f, 0.1f, 0.6f, 180f, true);
        Burst(position, Vector3.up, Color.white, 1, 0f, 2.2f, 0.12f, 0f, false);
    }

    public void PlayBreak(Vector3 position)
    {
        cameraRig.Shake(0.55f, 0.45f);
        cameraRig.FovPunch(10f, 0.35f);
        Burst(position, Vector3.up, new Color(1f, 0.7f, 0.05f), 40, 7f, 0.13f, 1.1f, 180f, false, 1.6f);
        Burst(position, Vector3.up, new Color(0.8f, 0.9f, 1f), 24, 9f, 0.07f, 0.9f, 180f, true, 1.6f);
        Burst(position, Vector3.up, Color.white, 1, 0f, 2.6f, 0.1f, 0f, false);
        Play(ProceduralSfx.Shatter, 1f, 1f);
    }

    // A flat blotch lying on the ground below `point`; the oldest ones are removed past MaxSplats.
    void Splat(Vector3 point, float amount)
    {
        const int MaxSplats = 40;
        if (splats.Count >= MaxSplats)
        {
            var oldest = splats.Dequeue();
            if (oldest != null) Destroy(oldest);
        }

        Vector2 offset = Random.insideUnitCircle * (0.35f * amount);
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "FX_BloodSplat";
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = splatMaterial;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.transform.position = new Vector3(point.x + offset.x, 0.02f + 0.0005f * splats.Count, point.z + offset.y);
        go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        go.transform.localScale = Vector3.one * Random.Range(0.5f, 1.1f) * Mathf.Min(amount, 2f);
        splats.Enqueue(go);
    }

    // Irregular dark-red blob: a circle whose edge is pushed in and out by a few angular lobes.
    static Texture2D BuildSplatTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var rng = new System.Random(5);
        float[] lobePhase = new float[4];
        for (int i = 0; i < lobePhase.Length; i++) lobePhase[i] = (float)(rng.NextDouble() * Mathf.PI * 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float angle = Mathf.Atan2(dy, dx);
                float edge = 0.62f;
                for (int k = 0; k < lobePhase.Length; k++) edge += 0.1f * Mathf.Sin(angle * (k + 2) + lobePhase[k]);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((edge - dist) / 0.08f);
                tex.SetPixel(x, y, new Color(0.45f, 0.02f, 0.04f, 0.9f * alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    void Play(AudioClip clip, float volume, float pitch)
    {
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume);
    }

    void Burst(Vector3 position, Vector3 direction, Color color, int count, float speed, float size, float life, float spread, bool stretch, float gravity = 0.8f)
    {
        var go = new GameObject("FX_Burst");
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction : Vector3.up);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.maxParticles = 128;

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = spread > 0f;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = Mathf.Min(spread, 180f);
        shape.radius = 0.02f;

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = fade;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMaterial;
        r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        r.velocityScale = 0.05f;
        r.lengthScale = 2.5f;

        ps.Play();
        ps.Emit(count);
        Destroy(go, life + 0.5f);
    }
}
