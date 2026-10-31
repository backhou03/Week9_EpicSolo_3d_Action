using System.Collections.Generic;
using UnityEngine;

// Readability: flowing chevrons on the road floor (colored per route) and glowing arena boundary lines that
// turn red while locked and green once cleared.
public class StageGuideFx : MonoBehaviour
{
    static readonly Color TrunkColor = new Color(0.55f, 1f, 0.65f);
    static readonly Color RifleColor = new Color(0.3f, 0.9f, 1f);
    static readonly Color HammerColor = new Color(1f, 0.55f, 0.15f);
    static readonly Color ArenaIdle = new Color(1f, 0.7f, 0.2f);
    static readonly Color ArenaLocked = new Color(1f, 0.12f, 0.08f);
    static readonly Color ArenaCleared = new Color(0.35f, 1f, 0.55f);
    static readonly Vector3[] Trunk = { new Vector3(0, 0, -16), new Vector3(0, 0, 53) };
    static readonly Vector3[] RouteA = { new Vector3(0, 0, 53), new Vector3(-28, 0, 53), new Vector3(-28, 0, 113), new Vector3(0, 0, 113) };
    static readonly Vector3[] RouteB = { new Vector3(0, 0, 53), new Vector3(28, 0, 53), new Vector3(28, 0, 113), new Vector3(0, 0, 113) };
    static readonly Vector3[] Finish = { new Vector3(0, 0, 113), new Vector3(0, 0, 150) };
    const float LaneWidth = 2.2f;
    const float ChevronLength = 3f;
    const float FlowSpeed = 0.7f;
    const float Floor = 0.05f;
    const float ArenaHalf = 13.6f;
    const float LineWidth = 0.45f;
    const float ClearFade = 4f;

    class Lane { public Mesh mesh; public float length; public Vector3[] verts; }
    class Outline { public Material material; public int state; public float changed; public Vector3 center; }

    static Texture2D chevronTexture;
    readonly List<Lane> lanes = new List<Lane>();
    readonly List<Material> materials = new List<Material>();
    readonly Outline[] outlines = new Outline[4];
    StageManager manager;

    void Start()
    {
        manager = GetComponent<StageManager>();
        AddPath(Trunk, TrunkColor);
        AddPath(RouteA, RifleColor);
        AddPath(RouteB, HammerColor);
        AddPath(Finish, TrunkColor);
        for (int i = 0; i < outlines.Length; i++)
        {
            var entrance = transform.Find("Arena" + i + "_Entrance");
            if (entrance == null) continue;
            var o = new Outline { center = new Vector3(entrance.position.x, 0f, entrance.position.z + 14f), material = new Material(Shader.Find("Sprites/Default")) };
            materials.Add(o.material);
            for (int s = 0; s < 4; s++)
            {
                bool horizontal = s < 2;
                float sign = s % 2 == 0 ? 1f : -1f;
                var pos = o.center + (horizontal ? new Vector3(0, 0, sign * ArenaHalf) : new Vector3(sign * ArenaHalf, 0, 0)) + Vector3.up * Floor;
                var q = StageFx.MakeQuad("ArenaLine", o.material, pos, Quaternion.Euler(90f, 0f, 0f));
                q.transform.SetParent(transform, true);
                q.transform.localScale = horizontal ? new Vector3(ArenaHalf * 2f, LineWidth, 1f) : new Vector3(LineWidth, ArenaHalf * 2f, 1f);
            }
            outlines[i] = o;
        }
        manager.ArenaEntered += OnEntered;
        manager.ArenaCleared += OnCleared;
    }

    void OnDestroy()
    {
        foreach (var m in materials) if (m != null) Destroy(m);
        if (manager == null) return;
        manager.ArenaEntered -= OnEntered;
        manager.ArenaCleared -= OnCleared;
    }

    void OnEntered(int index) => SetState(index, 1);
    void OnCleared(int index) => SetState(index, 2);

    void SetState(int index, int state)
    {
        if (outlines[index] == null) return;
        outlines[index].state = state;
        outlines[index].changed = Time.time;
    }

    void AddPath(Vector3[] points, Color color)
    {
        var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = Chevron(), color = new Color(color.r, color.g, color.b, 0.55f) };
        materials.Add(material);
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 a = points[i], b = points[i + 1];
            Vector3 dir = (b - a).normalized, side = Vector3.Cross(Vector3.up, dir) * LaneWidth * 0.5f;
            var lane = new Lane { length = Vector3.Distance(a, b), mesh = new Mesh { name = "RoadChevron" } };
            lane.verts = new[] { a - side, a + side, b + side, b - side };
            for (int v = 0; v < 4; v++) lane.verts[v] += Vector3.up * Floor;
            lane.mesh.MarkDynamic();
            lane.mesh.vertices = lane.verts;
            lane.mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            lane.mesh.uv = new Vector2[4];
            lane.mesh.RecalculateBounds();
            var go = new GameObject("RoadChevron"); go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = lane.mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            lanes.Add(lane);
        }
    }

    // Chevron points toward +v so a falling v offset makes the arrows flow forward.
    static Texture2D Chevron()
    {
        if (chevronTexture != null) return chevronTexture;
        chevronTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / 64f - 0.5f), v = (y + 0.5f) / 64f;
                float edge = 0.5f - u - 0.0f;
                float arm = Mathf.Abs(v - (0.78f - u * 0.9f));
                float a = Mathf.Clamp01(1f - arm / 0.07f) * Mathf.Clamp01(edge * 10f);
                chevronTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        chevronTexture.Apply();
        return chevronTexture;
    }

    void Update()
    {
        float scroll = Time.time * FlowSpeed;
        foreach (var lane in lanes)
        {
            float tiles = lane.length / ChevronLength;
            lane.mesh.uv = new[] { new Vector2(0, -scroll), new Vector2(1, -scroll), new Vector2(1, tiles - scroll), new Vector2(0, tiles - scroll) };
        }
        foreach (var o in outlines)
        {
            if (o == null) continue;
            float since = Time.time - o.changed;
            Color c; float a;
            switch (o.state)
            {
                case 1: c = ArenaLocked; a = 0.55f + 0.35f * Mathf.Sin(Time.time * 7f); break;
                case 2: c = ArenaCleared; a = Mathf.Lerp(0.9f, 0.18f, since / ClearFade); break;
                default: c = ArenaIdle; a = 0.28f + 0.1f * Mathf.Sin(Time.time * 2f); break;
            }
            if (o.state == 1 && since < 0.4f) { c = Color.white; a = 1f; }
            o.material.color = new Color(c.r, c.g, c.b, a);
        }
    }
}
