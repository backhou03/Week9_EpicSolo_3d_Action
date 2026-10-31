using System.Collections.Generic;
using UnityEngine;

// Shears-only visuals: a fan that follows the blade, flat arc and ring flashes, dust puffs.
// Everything is built at runtime; shared feedback (hit stop, shake, sparks) stays in HitFeedback.
public class ShearsFx : MonoBehaviour
{
    const int Segments = 28;
    const float SweepMinStep = 0.03f;
    static readonly float[] ArcRadii = { 0f, 0.7f, 1f };
    static readonly float[] ArcAlphas = { 0f, 0.14f, 0.9f };
    static readonly float[] RingRadii = { 0.84f, 1f };
    static readonly float[] RingAlphas = { 0f, 1f };

    struct Sample
    {
        public Vector3 a, b;
        public float time;
        public bool start;
    }

    class Flash
    {
        public GameObject go;
        public Material material;
        public Mesh mesh;
        public Color color;
        public float start, life, from, radius;
    }

    readonly List<Sample> samples = new List<Sample>();
    readonly List<Flash> flashes = new List<Flash>();
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Color> colors = new List<Color>();
    readonly List<int> tris = new List<int>();

    Transform rootSpace, tipSpace;
    Vector3 rootLocal, tipLocal;
    Color sweepColor = Color.white;
    float sweepLife = 0.3f;
    bool emitting, nextIsStart;
    Mesh sweepMesh;
    GameObject sweepObject;
    Material sweepMaterial, particleMaterial;
    AudioSource audioSource;

    void Awake()
    {
        var shader = Shader.Find("Sprites/Default");
        sweepMaterial = new Material(shader);
        particleMaterial = new Material(shader);
        sweepMesh = new Mesh { name = "ShearsSweep" };
        sweepMesh.MarkDynamic();
        sweepObject = new GameObject("ShearsSweep");
        sweepObject.AddComponent<MeshFilter>().sharedMesh = sweepMesh;
        var r = sweepObject.AddComponent<MeshRenderer>();
        r.sharedMaterial = sweepMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
    }

    void OnDestroy()
    {
        foreach (var f in flashes) Dispose(f);
        flashes.Clear();
        if (sweepObject != null) Destroy(sweepObject);
        if (sweepMesh != null) Destroy(sweepMesh);
        if (sweepMaterial != null) Destroy(sweepMaterial);
        if (particleMaterial != null) Destroy(particleMaterial);
    }

    // ---- Sweep fan: the area the blade just cut through, from its root to its tip ----

    public void ConfigureSweep(Transform root, Vector3 rootPoint, Transform tip, Vector3 tipPoint)
    {
        rootSpace = root;
        rootLocal = rootPoint;
        tipSpace = tip;
        tipLocal = tipPoint;
    }

    public void BeginSweep(Color color, float life)
    {
        sweepColor = color;
        sweepLife = life;
        if (!emitting) nextIsStart = true;
        emitting = true;
    }

    public void EndSweep() => emitting = false;

    // Runs after WeaponSwinger.Update so the blade pose of this frame is final.
    void LateUpdate()
    {
        if (emitting && rootSpace != null && tipSpace != null) Record();
        while (samples.Count > 0 && Time.time - samples[0].time > sweepLife) samples.RemoveAt(0);
        if (samples.Count > 0) samples[0] = new Sample { a = samples[0].a, b = samples[0].b, time = samples[0].time, start = true };
        RebuildSweep();
    }

    void Record()
    {
        Vector3 a = rootSpace.TransformPoint(rootLocal);
        Vector3 b = tipSpace.TransformPoint(tipLocal);
        if (!nextIsStart && samples.Count > 0)
        {
            var last = samples[samples.Count - 1];
            if ((b - last.b).sqrMagnitude < SweepMinStep * SweepMinStep) return;
        }
        samples.Add(new Sample { a = a, b = b, time = Time.time, start = nextIsStart });
        nextIsStart = false;
    }

    void RebuildSweep()
    {
        verts.Clear();
        colors.Clear();
        tris.Clear();
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            float fade = Mathf.Clamp01(1f - (Time.time - s.time) / sweepLife);
            fade *= fade;
            verts.Add(s.a);
            verts.Add(s.b);
            colors.Add(new Color(sweepColor.r, sweepColor.g, sweepColor.b, sweepColor.a * fade * 0.04f));
            colors.Add(new Color(sweepColor.r, sweepColor.g, sweepColor.b, sweepColor.a * fade * 0.8f));
            if (i > 0 && !s.start)
            {
                int v = i * 2;
                tris.Add(v - 2); tris.Add(v - 1); tris.Add(v);
                tris.Add(v - 1); tris.Add(v + 1); tris.Add(v);
            }
        }
        sweepMesh.Clear();
        if (tris.Count == 0) return;
        sweepMesh.SetVertices(verts);
        sweepMesh.SetColors(colors);
        sweepMesh.SetTriangles(tris, 0);
        sweepMesh.RecalculateBounds();
    }

    // ---- Flat flashes ----

    // A fan of `halfAngle` degrees either side of `yaw`, growing from startScale to full size while it fades.
    public void Arc(Vector3 center, float yaw, float radius, float halfAngle, Color color, float life, float startScale = 0.5f)
    {
        AddFlash(BuildBand(halfAngle, ArcRadii, ArcAlphas), center, yaw, radius, color, life, startScale);
    }

    // A full circle edge; a startScale above 1 makes it contract.
    public void Ring(Vector3 center, float radius, Color color, float life, float startScale = 0.3f)
    {
        AddFlash(BuildBand(180f, RingRadii, RingAlphas), center, 0f, radius, color, life, startScale);
    }

    void AddFlash(Mesh mesh, Vector3 center, float yaw, float radius, Color color, float life, float startScale)
    {
        var go = new GameObject("ShearsFlash");
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * radius * startScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Sprites/Default")) { color = color };
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        flashes.Add(new Flash
        {
            go = go, material = material, mesh = mesh, color = color,
            start = Time.time, life = Mathf.Max(0.02f, life), from = startScale, radius = radius,
        });
    }

    void Update()
    {
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            var f = flashes[i];
            float u = (Time.time - f.start) / f.life;
            if (u >= 1f)
            {
                Dispose(f);
                flashes.RemoveAt(i);
                continue;
            }
            float ease = 1f - (1f - u) * (1f - u) * (1f - u);
            f.go.transform.localScale = Vector3.one * (f.radius * Mathf.Lerp(f.from, 1f, ease));
            var c = f.color;
            c.a = f.color.a * (1f - u) * (1f - u);
            f.material.color = c;
        }
    }

    static void Dispose(Flash f)
    {
        if (f.go != null) Destroy(f.go);
        if (f.mesh != null) Destroy(f.mesh);
        if (f.material != null) Destroy(f.material);
    }

    // Rows run from the centre outwards; each row gets its own alpha so the outer edge reads brightest.
    static Mesh BuildBand(float halfAngle, float[] radii, float[] alphas)
    {
        int rows = radii.Length;
        var v = new Vector3[(Segments + 1) * rows];
        var c = new Color[v.Length];
        var t = new List<int>();
        for (int i = 0; i <= Segments; i++)
        {
            float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)Segments) * Mathf.Deg2Rad;
            for (int j = 0; j < rows; j++)
            {
                int k = i * rows + j;
                v[k] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radii[j];
                c[k] = new Color(1f, 1f, 1f, alphas[j]);
                if (i == 0 || j == rows - 1) continue;
                int p = (i - 1) * rows + j;
                t.Add(p); t.Add(p + 1); t.Add(k);
                t.Add(p + 1); t.Add(k + 1); t.Add(k);
            }
        }
        var mesh = new Mesh { name = "ShearsBand" };
        mesh.vertices = v;
        mesh.colors = c;
        mesh.triangles = t.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---- Dust and sparks ----

    public void Puff(Vector3 position, Vector3 direction, Color color, int count, float speed, float size, float life, float spread, float gravity = 0.3f)
    {
        var go = new GameObject("ShearsPuff");
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
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = Mathf.Min(spread, 180f);
        shape.radius = 0.15f;

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = fade;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMaterial;
        r.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
        ps.Emit(count);
        Destroy(go, life + 0.5f);
    }

    public void Play(AudioClip clip, float volume, float pitch)
    {
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume);
    }
}
