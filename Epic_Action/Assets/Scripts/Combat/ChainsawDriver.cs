using System.Collections.Generic;
using UnityEngine;

// The chain model is static, so the loop is faked: the right and left halves slide in opposite directions
// and wrap every tooth pitch, which reads as endless circulation along the straight runs.
public class ChainsawDriver : MonoBehaviour
{
    const string ChainRootName = "RulerChainsaw_Chain";
    const float ToothPitch = 0.0743f;   // measured on the model, uniform on the straight runs
    const float EndCapThreshold = 0.03f; // links whose centre is within this x of the axis are the tip/base caps
    const float MaxChainSpeed = 2f;      // m/s at Rev 1; keeps the step per frame under half a pitch (no strobing)

    // 0 = idle, 1 = full revs.
    public float Rev { get; set; } = 0.3f;

    Transform pivotRight, pivotLeft;
    AudioSource engine;
    Vector3 basePosition;
    float offset;
    float smoothRev;
    float contact;

    void Awake()
    {
        basePosition = transform.localPosition;
        SplitChain();

        engine = gameObject.AddComponent<AudioSource>();
        engine.clip = ProceduralSfx.ChainsawLoop;
        engine.loop = true;
        engine.spatialBlend = 0f;
        engine.Play();
    }

    // Call on every continuous-cut tick; makes the engine bog down and the saw shake harder.
    public void NotifyContact() => contact = 1f;

    void Update()
    {
        float dt = Time.deltaTime;
        smoothRev = Mathf.Lerp(smoothRev, Rev, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        contact = Mathf.MoveTowards(contact, 0f, 8f * Time.unscaledDeltaTime);

        if (pivotRight != null)
        {
            float step = Mathf.Min(smoothRev * MaxChainSpeed * dt, ToothPitch * 0.45f);
            offset = Mathf.Repeat(offset + step + ToothPitch * 0.5f, ToothPitch) - ToothPitch * 0.5f;
            pivotRight.localPosition = new Vector3(0f, offset, 0f);
            pivotLeft.localPosition = new Vector3(0f, -offset, 0f);
        }

        // Skip the shake while time is frozen for hit-stop so the impact frame stays crisp.
        float shake = Time.timeScale > 0.1f ? 0.004f * smoothRev + 0.012f * contact : 0f;
        transform.localPosition = basePosition + Random.insideUnitSphere * shake;

        engine.volume = Mathf.Lerp(0.12f, 0.35f, smoothRev) + 0.2f * contact;
        engine.pitch = Mathf.Lerp(0.8f, 1.35f, smoothRev) - 0.25f * contact;
    }

    void SplitChain()
    {
        Transform chainRoot = null;
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == ChainRootName) { chainRoot = t; break; }
        if (chainRoot == null)
        {
            Debug.LogWarning("ChainsawDriver: no '" + ChainRootName + "' child; chain will not animate.");
            return;
        }

        pivotRight = new GameObject("ChainPivotRight").transform;
        pivotLeft = new GameObject("ChainPivotLeft").transform;
        pivotRight.SetParent(chainRoot, false);
        pivotLeft.SetParent(chainRoot, false);

        foreach (var filter in chainRoot.GetComponentsInChildren<MeshFilter>())
        {
            var source = filter.sharedMesh;
            if (source == null) continue;
            if (!source.isReadable)
            {
                Debug.LogWarning("ChainsawDriver: mesh '" + source.name + "' is not readable (enable Read/Write on the model); chain will not animate.");
                return;
            }

            var renderer = filter.GetComponent<MeshRenderer>();
            SplitMesh(source, out Mesh right, out Mesh left, out Mesh still);
            AddPart(filter, renderer, right, pivotRight);
            AddPart(filter, renderer, left, pivotLeft);
            AddPart(filter, renderer, still, filter.transform.parent);
            renderer.enabled = false;
        }
    }

    static void AddPart(MeshFilter origin, MeshRenderer originRenderer, Mesh mesh, Transform parent)
    {
        if (mesh == null) return;
        var go = new GameObject(origin.name + "_" + parent.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = origin.transform.localPosition;
        go.transform.localRotation = origin.transform.localRotation;
        go.transform.localScale = origin.transform.localScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = originRenderer.sharedMaterials;
    }

    // Groups triangles into links (connected via welded vertex positions), then sorts each link by its centre x.
    static void SplitMesh(Mesh source, out Mesh right, out Mesh left, out Mesh still)
    {
        var verts = source.vertices;
        var normals = source.normals;
        var uvs = source.uv;
        var tris = source.triangles;

        var weldedId = new Dictionary<Vector3Int, int>();
        var parent = new int[verts.Length];
        var rep = new int[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(verts[i].x * 10000f), Mathf.RoundToInt(verts[i].y * 10000f), Mathf.RoundToInt(verts[i].z * 10000f));
            if (!weldedId.TryGetValue(key, out int id)) { id = i; weldedId[key] = id; }
            rep[i] = id;
            parent[i] = i;
        }

        int Find(int a)
        {
            while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; }
            return a;
        }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }

        for (int t = 0; t < tris.Length; t += 3)
        {
            Union(rep[tris[t]], rep[tris[t + 1]]);
            Union(rep[tris[t]], rep[tris[t + 2]]);
        }

        var sumX = new Dictionary<int, float>();
        var count = new Dictionary<int, int>();
        for (int i = 0; i < verts.Length; i++)
        {
            int root = Find(rep[i]);
            sumX.TryGetValue(root, out float s);
            count.TryGetValue(root, out int c);
            sumX[root] = s + verts[i].x;
            count[root] = c + 1;
        }

        var rightTris = new List<int>();
        var leftTris = new List<int>();
        var stillTris = new List<int>();
        for (int t = 0; t < tris.Length; t += 3)
        {
            int root = Find(rep[tris[t]]);
            float cx = sumX[root] / count[root];
            var target = cx > EndCapThreshold ? rightTris : cx < -EndCapThreshold ? leftTris : stillTris;
            target.Add(tris[t]); target.Add(tris[t + 1]); target.Add(tris[t + 2]);
        }

        right = Build(source.name + "_R", verts, normals, uvs, rightTris);
        left = Build(source.name + "_L", verts, normals, uvs, leftTris);
        still = Build(source.name + "_S", verts, normals, uvs, stillTris);
    }

    static Mesh Build(string name, Vector3[] verts, Vector3[] normals, Vector2[] uvs, List<int> tris)
    {
        if (tris.Count == 0) return null;

        var remap = new Dictionary<int, int>();
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var indices = new int[tris.Count];
        for (int i = 0; i < tris.Count; i++)
        {
            int old = tris[i];
            if (!remap.TryGetValue(old, out int idx))
            {
                idx = v.Count;
                remap[old] = idx;
                v.Add(verts[old]);
                n.Add(normals.Length > old ? normals[old] : Vector3.up);
                uv.Add(uvs.Length > old ? uvs[old] : Vector2.zero);
            }
            indices[i] = idx;
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
