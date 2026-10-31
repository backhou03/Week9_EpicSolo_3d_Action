using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Rebuilds the game's capsule enemy as capsule minus an infinite cylinder.
// Marching tetrahedra includes both the outside skin and the visible inner tunnel.
public static class NailHoleMesh
{
    public sealed class Geometry
    {
        public Vector3[] vertices, normals;
        public Vector2[] uv;
        public int[] skin, tunnel;
    }

    public static Mesh Create(Bounds bounds, Vector3 point, Vector3 direction, float holeRadius)
    {
        Geometry data = Generate(bounds, point, direction, holeRadius);
        var mesh = new Mesh { name = "EnemyCapsule_NailThroughHole", indexFormat = IndexFormat.UInt32 };
        mesh.vertices = data.vertices;
        mesh.normals = data.normals;
        mesh.uv = data.uv;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(data.skin, 0);
        mesh.SetTriangles(data.tunnel, 1);
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Geometry Generate(Bounds bounds, Vector3 point, Vector3 direction, float holeRadius)
        => new Builder(bounds, point, direction.normalized, holeRadius).Build();

    sealed class Builder
    {
        const int X = 18, Y = 32, Z = 18;
        static readonly int[,] Corners = { {0,0,0}, {1,0,0}, {1,1,0}, {0,1,0}, {0,0,1}, {1,0,1}, {1,1,1}, {0,1,1} };
        static readonly int[,] Tetrahedra = { {0,5,1,6}, {0,1,2,6}, {0,2,3,6}, {0,3,7,6}, {0,7,4,6}, {0,4,5,6} };
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> skin = new List<int>(), tunnel = new List<int>();
        readonly Vector3 center, linePoint, lineDirection, min, step;
        readonly float radius, segment, bore;
        readonly Vector3[] p = new Vector3[8];
        readonly float[] value = new float[8];
        readonly int[] inside = new int[4], outside = new int[4];

        public Builder(Bounds bounds, Vector3 point, Vector3 direction, float holeRadius)
        {
            center = bounds.center;
            radius = Math.Min(bounds.extents.x, bounds.extents.z);
            segment = Math.Max(0f, bounds.extents.y - radius);
            linePoint = point;
            lineDirection = direction;
            bore = holeRadius;
            min = bounds.min - Vector3.one * 0.03f;
            Vector3 size = bounds.size + Vector3.one * 0.06f;
            step = new Vector3(size.x / X, size.y / Y, size.z / Z);
        }

        float Sample(Vector3 at)
        {
            Vector3 q = at - center;
            q.y -= Math.Max(-segment, Math.Min(segment, q.y));
            Vector3 radial = at - linePoint;
            radial -= lineDirection * Vector3.Dot(radial, lineDirection);
            return Math.Max(q.magnitude - radius, bore - radial.magnitude);
        }

        Vector3 Normal(Vector3 at, out bool inner)
        {
            Vector3 q = at - center;
            q.y -= Math.Max(-segment, Math.Min(segment, q.y));
            Vector3 radial = at - linePoint;
            radial -= lineDirection * Vector3.Dot(radial, lineDirection);
            inner = bore - radial.magnitude > q.magnitude - radius;
            return inner ? -radial.normalized : q.normalized;
        }

        public Geometry Build()
        {
            int Index(int x, int y, int z) => (x * (Y + 1) + y) * (Z + 1) + z;
            Vector3 Position(int x, int y, int z) => min + new Vector3(x * step.x, y * step.y, z * step.z);
            var field = new float[(X + 1) * (Y + 1) * (Z + 1)];
            for (int x = 0; x <= X; x++)
                for (int y = 0; y <= Y; y++)
                    for (int z = 0; z <= Z; z++) field[Index(x,y,z)] = Sample(Position(x,y,z));
            for (int x = 0; x < X; x++)
                for (int y = 0; y < Y; y++)
                    for (int z = 0; z < Z; z++)
                    {
                        int negative = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            int cx = x + Corners[c,0], cy = y + Corners[c,1], cz = z + Corners[c,2];
                            p[c] = Position(cx,cy,cz);
                            value[c] = field[Index(cx,cy,cz)];
                            if (value[c] < 0f) negative++;
                        }
                        if (negative == 0 || negative == 8) continue;
                        for (int t = 0; t < 6; t++) Polygonize(t);
                    }
            return new Geometry { vertices = vertices.ToArray(), normals = normals.ToArray(), uv = uv.ToArray(), skin = skin.ToArray(), tunnel = tunnel.ToArray() };
        }

        Vector3 Edge(int a, int b)
        {
            float f = value[a] / (value[a] - value[b]);
            return p[a] + (p[b] - p[a]) * f;
        }

        void Polygonize(int t)
        {
            int n = 0, o = 0;
            for (int i = 0; i < 4; i++)
            {
                int c = Tetrahedra[t,i];
                if (value[c] < 0f) inside[n++] = c;
                else outside[o++] = c;
            }
            if (n == 0 || n == 4) return;
            if (n == 1) Add(Edge(inside[0],outside[0]), Edge(inside[0],outside[1]), Edge(inside[0],outside[2]));
            else if (n == 3) Add(Edge(outside[0],inside[0]), Edge(outside[0],inside[1]), Edge(outside[0],inside[2]));
            else
            {
                Vector3 a = Edge(inside[0],outside[0]), b = Edge(inside[0],outside[1]);
                Vector3 c = Edge(inside[1],outside[0]), d = Edge(inside[1],outside[1]);
                Add(a,b,c); Add(b,d,c);
            }
        }

        void Add(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 cross = Vector3.Cross(b-a,c-a);
            if (cross.sqrMagnitude < 1e-12f) return;
            Vector3 normal = Normal((a+b+c)/3f, out bool inner);
            if (Vector3.Dot(cross,normal) < 0f) { Vector3 swap = b; b = c; c = swap; }
            List<int> triangles = inner ? tunnel : skin;
            AddVertex(a, triangles); AddVertex(b, triangles); AddVertex(c, triangles);
        }

        void AddVertex(Vector3 at, List<int> triangles)
        {
            triangles.Add(vertices.Count);
            vertices.Add(at);
            normals.Add(Normal(at, out _));
            uv.Add(new Vector2((float)Math.Atan2(at.x-center.x,at.z-center.z)/(2f*(float)Math.PI)+0.5f, (at.y-min.y)/(Y*step.y)));
        }
    }
}
