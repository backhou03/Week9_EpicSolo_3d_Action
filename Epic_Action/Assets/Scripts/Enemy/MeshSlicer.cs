using System.Collections.Generic;
using UnityEngine;

// Splits a CONVEX mesh with a plane (mesh local space). The cut cross-section of a convex mesh is a convex
// polygon, so each cap is a triangle fan around the polygon centre. Cap triangles go to submesh 1 so they can
// use their own material; everything else is submesh 0.
public static class MeshSlicer
{
    const float OnPlane = 1e-5f;

    struct Vert
    {
        public Vector3 pos, normal;
        public Vector2 uv;

        public static Vert Lerp(Vert a, Vert b, float t) => new Vert
        {
            pos = Vector3.Lerp(a.pos, b.pos, t),
            normal = Vector3.Lerp(a.normal, b.normal, t).normalized,
            uv = Vector2.Lerp(a.uv, b.uv, t),
        };
    }

    class MeshBuilder
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> body = new List<int>();
        public readonly List<int> cap = new List<int>();

        public int Add(Vert v)
        {
            verts.Add(v.pos);
            normals.Add(v.normal);
            uvs.Add(v.uv);
            return verts.Count - 1;
        }

        public void AddFan(List<Vert> poly)
        {
            if (poly.Count < 3) return;
            int first = Add(poly[0]);
            int prev = Add(poly[1]);
            for (int i = 2; i < poly.Count; i++)
            {
                int cur = Add(poly[i]);
                body.Add(first); body.Add(prev); body.Add(cur);
                prev = cur;
            }
        }

        // Adds the cap polygon facing `outward` (triangle winding is fixed up to match).
        public void AddCap(List<Vector3> ring, Vector3 centre, Vector3 outward)
        {
            int c = Add(new Vert { pos = centre, normal = outward, uv = new Vector2(0.5f, 0.5f) });
            for (int i = 0; i < ring.Count; i++)
            {
                Vector3 p0 = ring[i];
                Vector3 p1 = ring[(i + 1) % ring.Count];
                int a = Add(new Vert { pos = p0, normal = outward, uv = new Vector2(0.5f, 0.5f) });
                int b = Add(new Vert { pos = p1, normal = outward, uv = new Vector2(0.5f, 0.5f) });
                // Unity front faces have Cross(b - a, c - a) pointing at the viewer.
                if (Vector3.Dot(Vector3.Cross(p0 - centre, p1 - centre), outward) >= 0f) { cap.Add(c); cap.Add(a); cap.Add(b); }
                else { cap.Add(c); cap.Add(b); cap.Add(a); }
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(cap, 1);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // `above` is the side the plane normal points to. Returns false if the plane does not cut the mesh.
    public static bool Slice(Mesh source, Plane plane, out Mesh above, out Mesh below)
    {
        above = null;
        below = null;

        var verts = source.vertices;
        var normals = source.normals;
        var uvs = source.uv;
        var tris = source.triangles;

        var up = new MeshBuilder();
        var down = new MeshBuilder();
        var cutPoints = new List<Vector3>();
        var triangle = new Vert[3];
        var dist = new float[3];
        var polyUp = new List<Vert>(4);
        var polyDown = new List<Vert>(4);

        for (int t = 0; t < tris.Length; t += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int i = tris[t + k];
                triangle[k] = new Vert { pos = verts[i], normal = normals[i], uv = uvs.Length > i ? uvs[i] : Vector2.zero };
                dist[k] = plane.GetDistanceToPoint(verts[i]);
                if (Mathf.Abs(dist[k]) < OnPlane) { dist[k] = 0f; cutPoints.Add(verts[i]); }
            }

            polyUp.Clear();
            polyDown.Clear();
            for (int k = 0; k < 3; k++)
            {
                int n = (k + 1) % 3;
                if (dist[k] >= 0f) polyUp.Add(triangle[k]);
                if (dist[k] <= 0f) polyDown.Add(triangle[k]);
                if ((dist[k] > 0f && dist[n] < 0f) || (dist[k] < 0f && dist[n] > 0f))
                {
                    var hit = Vert.Lerp(triangle[k], triangle[n], dist[k] / (dist[k] - dist[n]));
                    polyUp.Add(hit);
                    polyDown.Add(hit);
                    cutPoints.Add(hit.pos);
                }
            }

            up.AddFan(polyUp);
            down.AddFan(polyDown);
        }

        if (up.body.Count == 0 || down.body.Count == 0) return false;

        if (!BuildRing(cutPoints, plane.normal, out List<Vector3> ring, out Vector3 centre)) return false;
        up.AddCap(ring, centre, -plane.normal);
        down.AddCap(ring, centre, plane.normal);

        above = up.ToMesh(source.name + "_above");
        below = down.ToMesh(source.name + "_below");
        return true;
    }

    // Sorts the cut points by angle around their centre and drops duplicates.
    static bool BuildRing(List<Vector3> points, Vector3 normal, out List<Vector3> ring, out Vector3 centre)
    {
        ring = null;
        centre = Vector3.zero;
        if (points.Count < 3) return false;

        foreach (var p in points) centre += p;
        centre /= points.Count;

        Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 v = Vector3.Cross(normal, u);

        var sorted = new List<(float angle, Vector3 point)>(points.Count);
        foreach (var p in points)
        {
            Vector3 d = p - centre;
            sorted.Add((Mathf.Atan2(Vector3.Dot(d, v), Vector3.Dot(d, u)), p));
        }
        sorted.Sort((a, b) => a.angle.CompareTo(b.angle));

        ring = new List<Vector3>(sorted.Count);
        foreach (var s in sorted)
            if (ring.Count == 0 || (s.point - ring[ring.Count - 1]).sqrMagnitude > 1e-8f) ring.Add(s.point);
        if (ring.Count > 1 && (ring[0] - ring[ring.Count - 1]).sqrMagnitude <= 1e-8f) ring.RemoveAt(ring.Count - 1);

        return ring.Count >= 3;
    }
}
