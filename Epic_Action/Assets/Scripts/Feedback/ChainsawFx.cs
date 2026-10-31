using UnityEngine;

// Chainsaw-only sparks and glow smears. Two world-space particle systems fed by manual Emit calls.
public class ChainsawFx : MonoBehaviour
{
    static readonly Color Hot = new Color(1f, 0.9f, 0.5f);
    static readonly Color Orange = new Color(1f, 0.5f, 0.12f);

    Transform blade;
    Vector3 tipLocal;
    Material sparkMaterial, smearMaterial;
    Texture2D softTexture;
    ParticleSystem sparks, smear;

    public Vector3 Tip => blade.TransformPoint(tipLocal);

    public void Configure(Transform visual, Vector3 bladeTip)
    {
        blade = visual;
        tipLocal = bladeTip;
        sparkMaterial = new Material(Shader.Find("Sprites/Default"));
        softTexture = BuildSoftTexture();
        smearMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = softTexture };
        sparks = Build("FX_ChainsawSparks", sparkMaterial, true, 0.8f);
        smear = Build("FX_ChainsawSmear", smearMaterial, false, 0f);
    }

    void OnDestroy()
    {
        if (sparks != null) Destroy(sparks.gameObject);
        if (smear != null) Destroy(smear.gameObject);
        if (sparkMaterial != null) Destroy(sparkMaterial);
        if (smearMaterial != null) Destroy(smearMaterial);
        if (softTexture != null) Destroy(softTexture);
    }

    // Whole particles owed this frame for a per-second rate.
    public static int Steps(ref float accumulator, float rate, float dt)
    {
        accumulator += rate * dt;
        int n = (int)accumulator;
        accumulator -= n;
        return n;
    }

    public void Sparks(Vector3 position, Vector3 direction, int count, float speed, float spread, float size = 0.05f, float life = 0.3f)
    {
        if (count <= 0 || sparks == null) return;
        Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
        var ep = new ParticleSystem.EmitParams { position = position };
        for (int i = 0; i < count; i++)
        {
            ep.velocity = (forward + Random.insideUnitSphere * spread).normalized * (speed * Random.Range(0.4f, 1f));
            ep.startSize = size * Random.Range(0.6f, 1f);
            ep.startLifetime = life * Random.Range(0.6f, 1f);
            ep.startColor = Color.Lerp(Orange, Hot, Random.value);
            sparks.Emit(ep, 1);
        }
    }

    // Flat outward spray around a point.
    public void Ring(Vector3 center, int count, float speed, float size = 0.06f, float life = 0.35f)
    {
        if (sparks == null) return;
        var ep = new ParticleSystem.EmitParams { position = center };
        for (int i = 0; i < count; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            ep.velocity = new Vector3(Mathf.Cos(angle), Random.Range(0.1f, 0.45f), Mathf.Sin(angle)) * (speed * Random.Range(0.5f, 1f));
            ep.startSize = size * Random.Range(0.6f, 1f);
            ep.startLifetime = life * Random.Range(0.6f, 1f);
            ep.startColor = Color.Lerp(Orange, Hot, Random.value);
            sparks.Emit(ep, 1);
        }
    }

    // A soft glow blob that stays put and fades: strung along a path it reads as an afterimage.
    public void Smear(Vector3 position, float size, float life, Color color)
    {
        if (smear == null) return;
        var ep = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = Vector3.zero,
            startSize = size,
            startLifetime = life,
            startColor = color,
            rotation = Random.Range(0f, 360f),
        };
        smear.Emit(ep, 1);
    }

    ParticleSystem Build(string objectName, Material material, bool stretch, float gravity)
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
        main.maxParticles = 600;

        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = fade;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, stretch
            ? new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f))
            : new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.4f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        r.velocityScale = 0.05f;
        r.lengthScale = 2.5f;

        ps.Play();
        return ps;
    }

    static Texture2D BuildSoftTexture()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
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
}
