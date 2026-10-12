using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandSim.View
{
    /// <summary>Flat-shaded low-poly mesh builder: boxes, cylinders, cones, icospheres, palms and fronds.</summary>
    public sealed class MeshKit
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<int> t = new List<int>();

        public bool Empty => t.Count == 0;

        /// <summary>Adds a triangle; winding is fixed so the face normal points away from <paramref name="inside"/>.</summary>
        void TriOut(Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            var nn = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(nn, (a + b + c) / 3f - inside) < 0) { var tmp = b; b = c; c = tmp; nn = -nn; }
            Tri(a, b, c, nn.normalized);
        }

        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 nrm)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); n.Add(nrm); n.Add(nrm); n.Add(nrm);
            // Unity treats (a, b, c) as front-facing when Cross(b - a, c - a) points at the viewer.
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        /// <summary>Double-sided triangle (two faces).</summary>
        public void TriBoth(Vector3 a, Vector3 b, Vector3 c)
        {
            var nn = Vector3.Cross(b - a, c - a).normalized;
            Tri(a, b, c, nn); Tri(a, c, b, -nn);
        }

        public MeshKit Box(float w, float h, float d, Matrix4x4 m)
        {
            var c = new Vector3[8];
            for (int k = 0; k < 8; k++) c[k] = m.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? -w / 2 : w / 2, (k & 2) == 0 ? -h / 2 : h / 2, (k & 4) == 0 ? -d / 2 : d / 2));
            var ctr = m.MultiplyPoint3x4(Vector3.zero);
            int[][] faces = { new[] { 0, 1, 3, 2 }, new[] { 4, 6, 7, 5 }, new[] { 0, 4, 5, 1 }, new[] { 2, 3, 7, 6 }, new[] { 0, 2, 6, 4 }, new[] { 1, 5, 7, 3 } };
            foreach (var f in faces) { TriOut(c[f[0]], c[f[1]], c[f[2]], ctr); TriOut(c[f[0]], c[f[2]], c[f[3]], ctr); }
            return this;
        }

        /// <summary>Cylinder or frustum centred on the origin (Three.js CylinderGeometry convention: top radius first).</summary>
        public MeshKit Cylinder(float rTop, float rBot, float h, int seg, Matrix4x4 m)
        {
            var ctr = m.MultiplyPoint3x4(Vector3.zero);
            var top = m.MultiplyPoint3x4(new Vector3(0, h / 2, 0)); var bot = m.MultiplyPoint3x4(new Vector3(0, -h / 2, 0));
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2 / seg, a1 = (k + 1) * Mathf.PI * 2 / seg;
                Vector3 t0 = m.MultiplyPoint3x4(new Vector3(Mathf.Cos(a0) * rTop, h / 2, Mathf.Sin(a0) * rTop));
                Vector3 t1 = m.MultiplyPoint3x4(new Vector3(Mathf.Cos(a1) * rTop, h / 2, Mathf.Sin(a1) * rTop));
                Vector3 b0 = m.MultiplyPoint3x4(new Vector3(Mathf.Cos(a0) * rBot, -h / 2, Mathf.Sin(a0) * rBot));
                Vector3 b1 = m.MultiplyPoint3x4(new Vector3(Mathf.Cos(a1) * rBot, -h / 2, Mathf.Sin(a1) * rBot));
                var mid = Vector3.Lerp(top, bot, .5f);
                if (rTop > 0) TriOut(t0, t1, b1, mid);
                TriOut(t0, b1, b0, mid);
                if (rTop > 0) TriOut(top, t0, t1, bot);
                if (rBot > 0) TriOut(bot, b0, b1, top);
            }
            return this;
        }

        public MeshKit Cone(float r, float h, int seg, Matrix4x4 m) => Cylinder(0, r, h, seg, m);

        public MeshKit Icosphere(float r, int subdiv, Matrix4x4 m)
        {
            float p = (1 + Mathf.Sqrt(5)) / 2;
            var pts = new List<Vector3> {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1) };
            var tris = new List<int[]> {
                new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11}, new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
                new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9}, new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1} };
            for (int s = 0; s < subdiv; s++)
            {
                var next = new List<int[]>(); var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out var idx)) return idx;
                    pts.Add((pts[a] + pts[b]) * .5f); cache[key] = pts.Count - 1; return pts.Count - 1;
                }
                foreach (var tr in tris)
                {
                    int ab = Mid(tr[0], tr[1]), bc = Mid(tr[1], tr[2]), ca = Mid(tr[2], tr[0]);
                    next.Add(new[] { tr[0], ab, ca }); next.Add(new[] { tr[1], bc, ab }); next.Add(new[] { tr[2], ca, bc }); next.Add(new[] { ab, bc, ca });
                }
                tris = next;
            }
            var ctr = m.MultiplyPoint3x4(Vector3.zero);
            foreach (var tr in tris)
                TriOut(m.MultiplyPoint3x4(pts[tr[0]].normalized * r), m.MultiplyPoint3x4(pts[tr[1]].normalized * r), m.MultiplyPoint3x4(pts[tr[2]].normalized * r), ctr);
            return this;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Matrix4x4 TRS(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0, float s = 1) =>
            Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(rx, ry, rz), Vector3.one * s);

        public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) => Matrix4x4.TRS(p, q, s);

        // ------------------------------------------------------------ vegetation shapes
        public static Mesh BroadleafTrunk() => new MeshKit().Cylinder(.05f, .08f, .55f, 6, TRS(0, .27f, 0)).ToMesh("trunk");

        public static Mesh BroadleafCrown() => new MeshKit()
            .Icosphere(.3f, 1, TRS(0, .78f, 0)).Icosphere(.24f, 1, TRS(.18f, .64f, .1f)).Icosphere(.22f, 1, TRS(-.15f, .67f, -.12f)).ToMesh("crown");

        public static readonly Vector3 PalmTop = new Vector3(.045f * 16 * .25f + .04f, 1.3f, 0);

        /// <summary>A bent, tapering palm trunk made of five leaning segments.</summary>
        public static Mesh PalmTrunk()
        {
            var k = new MeshKit(); float y = 0;
            for (int i = 0; i < 5; i++)
            {
                float r1 = .065f - i * .008f, r2 = .07f - i * .008f, bx = .045f * i * i * .25f;
                // Three's rotateZ(-.06k) leans the segment toward +x.
                k.Cylinder(r1, r2, .27f, 6, Matrix4x4.Translate(new Vector3(bx, y + .135f, 0)) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, -.06f * i * Mathf.Rad2Deg)));
                y += .26f;
            }
            return k.ToMesh("palmTrunk");
        }

        /// <summary>Eight drooping, tapering fronds, double-sided.</summary>
        public static Mesh PalmFronds()
        {
            var k = new MeshKit(); const int segs = 6; const float len = .95f, wid = .22f;
            for (int f = 0; f < 8; f++)
            {
                var rot = Quaternion.Euler(0, (f * 45f + (f % 2) * .2f * Mathf.Rad2Deg), 0);
                var pts = new Vector3[segs + 1, 2];
                for (int s = 0; s <= segs; s++)
                {
                    float x = len * s / segs, half = wid / 2 * ((1 - x / len) * .9f + .15f), yy = .16f * x - .62f * x * x;
                    pts[s, 0] = PalmTop + rot * new Vector3(x, yy, -half);
                    pts[s, 1] = PalmTop + rot * new Vector3(x, yy, half);
                }
                for (int s = 0; s < segs; s++)
                {
                    k.TriBoth(pts[s, 0], pts[s + 1, 0], pts[s + 1, 1]);
                    k.TriBoth(pts[s, 0], pts[s + 1, 1], pts[s, 1]);
                }
            }
            return k.ToMesh("palmFronds");
        }

        public static Mesh Bush() => new MeshKit().Icosphere(.17f, 1, TRS(new Vector3(0, .08f, 0), Quaternion.identity, new Vector3(1, .65f, 1))).ToMesh("bush");

        public static Mesh Rock() => new MeshKit().Icosphere(.15f, 0, TRS(Vector3.zero, Quaternion.identity, new Vector3(1, .6f, 1))).ToMesh("rock");

        public static Mesh Quad(float size)
        {
            var mesh = new Mesh { name = "quad" };
            float h = size / 2;
            mesh.vertices = new[] { new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
