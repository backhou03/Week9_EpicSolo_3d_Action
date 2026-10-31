using System.Collections.Generic;
using UnityEngine;

// Ribbon between the blade base and tip, recorded while Emitting and fading out afterwards.
public class BladeTrail : MonoBehaviour
{
    const float Life = 0.25f;
    const float MinStep = 0.02f;

    struct Sample
    {
        public Vector3 a, b;
        public float time;
        public bool start;
    }

    readonly List<Sample> samples = new List<Sample>();
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Color> colors = new List<Color>();
    readonly List<int> tris = new List<int>();

    Transform baseSpace, tipSpace;
    Vector3 baseLocal, tipLocal;
    Color color = Color.white;
    Mesh mesh;
    GameObject ribbon;
    bool emitting;
    bool nextIsStart;

    public bool Emitting
    {
        get => emitting;
        set
        {
            if (value && !emitting) nextIsStart = true;
            emitting = value;
        }
    }

    public void Configure(Vector3 blade0, Vector3 blade1, Color trailColor)
    {
        Configure(transform, blade0, transform, blade1, trailColor);
    }

    // The two edges of the ribbon may ride on different parts of the weapon (e.g. the two blade tips of shears).
    public void Configure(Transform space0, Vector3 blade0, Transform space1, Vector3 blade1, Color trailColor)
    {
        baseSpace = space0;
        tipSpace = space1;
        baseLocal = blade0;
        tipLocal = blade1;
        color = trailColor;
    }

    void Awake()
    {
        mesh = new Mesh { name = "BladeTrail" };
        mesh.MarkDynamic();

        ribbon = new GameObject("BladeTrail");
        ribbon.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = ribbon.AddComponent<MeshRenderer>();
        r.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    void OnDestroy()
    {
        if (ribbon != null) Destroy(ribbon);
        if (mesh != null) Destroy(mesh);
    }

    // Runs after WeaponSwinger.Update so the blade pose of this frame is final.
    void LateUpdate()
    {
        if (emitting) Record();

        while (samples.Count > 0 && Time.time - samples[0].time > Life) samples.RemoveAt(0);
        if (samples.Count > 0) samples[0] = new Sample { a = samples[0].a, b = samples[0].b, time = samples[0].time, start = true };

        Rebuild();
    }

    void Record()
    {
        Vector3 a = baseSpace.TransformPoint(baseLocal);
        Vector3 b = tipSpace.TransformPoint(tipLocal);
        if (!nextIsStart && samples.Count > 0)
        {
            var last = samples[samples.Count - 1];
            if ((b - last.b).sqrMagnitude < MinStep * MinStep && (a - last.a).sqrMagnitude < MinStep * MinStep) return;
        }

        samples.Add(new Sample { a = a, b = b, time = Time.time, start = nextIsStart });
        nextIsStart = false;
    }

    void Rebuild()
    {
        verts.Clear();
        colors.Clear();
        tris.Clear();

        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            float fade = Mathf.Clamp01(1f - (Time.time - s.time) / Life);
            fade *= fade;
            verts.Add(s.a);
            verts.Add(s.b);
            colors.Add(new Color(color.r, color.g, color.b, color.a * fade * 0.5f));
            colors.Add(new Color(color.r, color.g, color.b, color.a * fade));

            if (i > 0 && !s.start)
            {
                int v = i * 2;
                tris.Add(v - 2); tris.Add(v - 1); tris.Add(v);
                tris.Add(v - 1); tris.Add(v + 1); tris.Add(v);
            }
        }

        mesh.Clear();
        if (tris.Count == 0) return;
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }
}
