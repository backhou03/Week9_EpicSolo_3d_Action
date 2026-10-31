using UnityEngine;

// Hammer-only ground FX: shock rings, cracks, dust, sparks, glow smears and a pose-hold hit stop.
// Five world-space particle systems fed by manual Emit calls.
public class HammerFx : MonoBehaviour
{
    public static readonly Color Ember = new Color(1f, 0.55f, 0.15f);
    public static readonly Color Hot = new Color(1f, 0.9f, 0.55f);
    static readonly Color DustColor = new Color(0.72f, 0.66f, 0.56f, 0.6f);

    Material spriteMaterial, softMaterial, ringMaterial, crackMaterial;
    Texture2D softTexture, ringTexture, crackTexture;
    ParticleSystem sparks, dust, rings, cracks, glow;
    const float MaxHold = 0.1f;
    float holdUntil;

    // True while the weapon pose should freeze on contact; the world keeps running.
    public bool Holding => Time.time < holdUntil;

    public void Configure()
    {
        softTexture = BuildSoftTexture();
        ringTexture = BuildRingTexture();
        crackTexture = BuildCrackTexture();
        spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        softMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = softTexture };
        ringMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = ringTexture };
        crackMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = crackTexture };

        sparks = Build("FX_HammerSparks", spriteMaterial, ParticleSystemRenderMode.Stretch, 1.2f, 800,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)), false);
        dust = Build("FX_HammerDust", softMaterial, ParticleSystemRenderMode.Billboard, -0.04f, 600,
            new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.4f, 0.9f), new Keyframe(1f, 1.2f)), true);
        rings = Build("FX_HammerRings", ringMaterial, ParticleSystemRenderMode.HorizontalBillboard, 0f, 80,
            new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(0.2f, 0.62f), new Keyframe(1f, 1f)), false);
        cracks = Build("FX_HammerCracks", crackMaterial, ParticleSystemRenderMode.HorizontalBillboard, 0f, 60,
            new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.08f, 1f), new Keyframe(1f, 1f)), false, 0.55f);
        glow = Build("FX_HammerGlow", softMaterial, ParticleSystemRenderMode.Billboard, 0f, 500,
            new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.4f)), false);
    }

    void OnDestroy()
    {
        foreach (var ps in new[] { sparks, dust, rings, cracks, glow }) if (ps != null) Destroy(ps.gameObject);
        foreach (var m in new[] { spriteMaterial, softMaterial, ringMaterial, crackMaterial }) if (m != null) Destroy(m);
        foreach (var t in new[] { softTexture, ringTexture, crackTexture }) if (t != null) Destroy(t);
    }

    // Whole particles owed this frame for a per-second rate.
    public static int Steps(ref float accumulator, float rate, float dt)
    {
        accumulator += rate * dt;
        int n = (int)accumulator;
        accumulator -= n;
        return n;
    }

    // ---- Time ---------------------------------------------------------------

    // Holds the weapon pose for a moment; overlapping calls extend the same hold.
    public void Hitstop(float seconds) => holdUntil = Mathf.Max(holdUntil, Time.time + Mathf.Min(seconds, MaxHold));

    // ---- Emitters -----------------------------------------------------------

    // Flat shock ring expanding to `radius` (ease-out), lying on the ground.
    public void Ring(Vector3 center, float radius, float life, Color color)
    {
        var ep = new ParticleSystem.EmitParams
        {
            position = center + Vector3.up * 0.04f,
            velocity = Vector3.zero,
            startSize = radius * 2f,
            startLifetime = life,
            startColor = color,
            rotation = Random.Range(0f, 360f),
        };
        rings.Emit(ep, 1);
    }

    // A fissure star that stays on the ground long after the hit.
    public void Crack(Vector3 center, float radius, float life)
    {
        var ep = new ParticleSystem.EmitParams
        {
            position = center + Vector3.up * 0.05f,
            velocity = Vector3.zero,
            startSize = radius * 2f,
            startLifetime = life,
            startColor = Color.white,
            rotation = Random.Range(0f, 360f),
        };
        cracks.Emit(ep, 1);
    }

    // Soft puffs rolling outward from a ring of `radius`.
    public void Dust(Vector3 center, int count, float speed, float radius, float size = 0.7f, float life = 0.8f)
    {
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            ep.position = center + outward * (radius * Random.Range(0.5f, 1f)) + Vector3.up * Random.Range(0.05f, 0.2f);
            ep.velocity = outward * (speed * Random.Range(0.5f, 1f)) + Vector3.up * Random.Range(0.2f, 0.9f);
            ep.startSize = size * Random.Range(0.6f, 1.2f);
            ep.startLifetime = life * Random.Range(0.7f, 1f);
            ep.startColor = DustColor;
            ep.rotation = Random.Range(0f, 360f);
            dust.Emit(ep, 1);
        }
    }

    // Dust thrown along one direction (dash and rush wake).
    public void DustStream(Vector3 position, Vector3 direction, int count, float speed, float size = 0.6f)
    {
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            ep.position = position + Random.insideUnitSphere * 0.25f;
            ep.velocity = (direction + Random.insideUnitSphere * 0.5f + Vector3.up * 0.25f) * (speed * Random.Range(0.4f, 1f));
            ep.startSize = size * Random.Range(0.6f, 1.2f);
            ep.startLifetime = Random.Range(0.4f, 0.8f);
            ep.startColor = DustColor;
            ep.rotation = Random.Range(0f, 360f);
            dust.Emit(ep, 1);
        }
    }

    public void Sparks(Vector3 position, Vector3 direction, int count, float speed, float spread, float size = 0.07f, float life = 0.45f)
    {
        if (count <= 0) return;
        Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
        var ep = new ParticleSystem.EmitParams { position = position };
        for (int i = 0; i < count; i++)
        {
            ep.velocity = (forward + Random.insideUnitSphere * spread).normalized * (speed * Random.Range(0.4f, 1f));
            ep.startSize = size * Random.Range(0.6f, 1f);
            ep.startLifetime = life * Random.Range(0.6f, 1f);
            ep.startColor = Color.Lerp(Ember, Hot, Random.value);
            sparks.Emit(ep, 1);
        }
    }

    // Flat outward spray along the ground.
    public void SparkRing(Vector3 center, int count, float speed, float size = 0.07f)
    {
        var ep = new ParticleSystem.EmitParams { position = center + Vector3.up * 0.08f };
        for (int i = 0; i < count; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            ep.velocity = new Vector3(Mathf.Cos(angle), Random.Range(0.15f, 0.6f), Mathf.Sin(angle)) * (speed * Random.Range(0.5f, 1f));
            ep.startSize = size * Random.Range(0.6f, 1f);
            ep.startLifetime = Random.Range(0.3f, 0.55f);
            ep.startColor = Color.Lerp(Ember, Hot, Random.value);
            sparks.Emit(ep, 1);
        }
    }

    // A soft glow blob that stays put and fades: strung along a path it reads as an afterimage.
    public void Smear(Vector3 position, float size, float life, Color color)
    {
        var ep = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = Vector3.zero,
            startSize = size,
            startLifetime = life,
            startColor = color,
            rotation = Random.Range(0f, 360f),
        };
        glow.Emit(ep, 1);
    }

    // The whole ground hit: rings, crack, rolling dust, sparks and a white flash. power ~0.2 (tap) to 1.5 (finisher).
    public void Impact(Vector3 ground, float radius, float power)
    {
        Ring(ground, radius, 0.3f + 0.22f * power, Color.Lerp(Ember, Hot, 0.45f));
        if (power >= 0.5f) Ring(ground, radius * 0.6f, 0.22f + 0.1f * power, new Color(1f, 1f, 1f, 0.8f));
        if (power >= 1f) Ring(ground, radius * 1.15f, 0.5f, new Color(Ember.r, Ember.g, Ember.b, 0.55f));
        if (power >= 0.3f) Crack(ground, radius * 0.75f, 1.6f + power);
        Dust(ground, 6 + (int)(20f * power), 2.5f + 4.5f * power, radius * 0.5f, 0.6f + 0.35f * power);
        SparkRing(ground, 8 + (int)(26f * power), 4f + 7f * power);
        Sparks(ground + Vector3.up * 0.1f, Vector3.up, 6 + (int)(18f * power), 5f + 6f * power, 0.9f);
        Smear(ground + Vector3.up * 0.2f, radius * (0.7f + 0.3f * power), 0.14f, new Color(1f, 0.95f, 0.8f, 0.7f));
    }

    // ---- Setup --------------------------------------------------------------

    ParticleSystem Build(string objectName, Material material, ParticleSystemRenderMode mode, float gravity, int max,
        AnimationCurve size, bool slowDown, float fadeStart = 0.5f)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.maxParticles = max;

        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = fade;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, size);

        if (slowDown)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.4f;
            limit.dampen = 0.12f;
        }

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.renderMode = mode;
        r.velocityScale = 0.05f;
        r.lengthScale = 2.5f;

        ps.Play();
        return ps;
    }

    static Texture2D NewTexture(int size) => new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

    static Texture2D BuildSoftTexture()
    {
        const int size = 32;
        var tex = NewTexture(size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply();
        return tex;
    }

    // Bright thin front edge with a faint filled glow behind it.
    static Texture2D BuildRingTexture()
    {
        const int size = 128;
        var tex = NewTexture(size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.07f, 2f));
                float fill = d < 0.86f ? 0.22f * Mathf.Pow(d / 0.86f, 3f) : 0f;
                float a = Mathf.Clamp01(edge + fill) * Mathf.Clamp01((1f - d) / 0.05f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    // Dark fissures radiating from the centre, tapering and wobbling toward their tips.
    static Texture2D BuildCrackTexture()
    {
        const int size = 128;
        const int count = 11;
        var tex = NewTexture(size);
        var rng = new System.Random(11);
        float[] angle = new float[count], length = new float[count];
        for (int i = 0; i < count; i++)
        {
            angle[i] = (i + (float)rng.NextDouble() * 0.6f) / count * Mathf.PI * 2f;
            length[i] = 0.45f + (float)rng.NextDouble() * 0.5f;
        }
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float theta = Mathf.Atan2(dy, dx);
                float a = d < 0.1f ? 0.8f : 0f;
                for (int i = 0; i < count; i++)
                {
                    if (d >= length[i]) continue;
                    float delta = Mathf.Repeat(theta - angle[i] - 0.07f * Mathf.Sin(d * 13f + i) + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
                    float perp = d * Mathf.Abs(Mathf.Sin(delta));
                    float half = 0.028f * (1f - d / length[i]) + 0.004f;
                    if (Mathf.Abs(delta) < Mathf.PI * 0.5f) a = Mathf.Max(a, Mathf.Clamp01(1f - perp / half));
                }
                tex.SetPixel(x, y, new Color(0.1f, 0.06f, 0.04f, a * 0.9f));
            }
        }
        tex.Apply();
        return tex;
    }
}
