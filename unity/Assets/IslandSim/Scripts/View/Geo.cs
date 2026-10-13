using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandSim.View
{
    /// <summary>
    /// A small port of the three.js geometry the web game builds its models from: the same primitives (box, cylinder,
    /// sphere, plane, icosahedron) with the same vertex order, normals and UVs, and the same helpers (worldUV, gable, hip,
    /// crown cards). Everything is in tile space (see World); ToMesh converts to Unity once.
    /// </summary>
    public sealed class Geo
    {
        public List<Vector3> P = new List<Vector3>(), N = new List<Vector3>();
        public List<Vector2> UV = new List<Vector2>();
        /// <summary>Optional second UV set; foliage keeps its own height above the model's origin here, for wind once batched.</summary>
        public List<Vector2> UV2;
        public List<int> I;                    // null = non-indexed (every three vertices a triangle)

        public int Count => P.Count;
        public Geo Clone() => new Geo { P = new List<Vector3>(P), N = new List<Vector3>(N), UV = new List<Vector2>(UV), UV2 = UV2 != null ? new List<Vector2>(UV2) : null, I = I != null ? new List<int>(I) : null };

        // ------------------------------------------------------------ transforms
        public Geo Apply(Matrix4x4 m)
        {
            var nm = m.inverse.transpose;
            for (int k = 0; k < P.Count; k++) P[k] = m.MultiplyPoint3x4(P[k]);
            for (int k = 0; k < N.Count; k++) N[k] = nm.MultiplyVector(N[k]).normalized;
            if (m.determinant < 0 && I != null) for (int k = 0; k < I.Count; k += 3) { int t = I[k + 1]; I[k + 1] = I[k + 2]; I[k + 2] = t; }
            else if (m.determinant < 0) for (int k = 0; k < P.Count; k += 3) { Swap(P, k + 1, k + 2); Swap(N, k + 1, k + 2); Swap(UV, k + 1, k + 2); if (UV2 != null) Swap(UV2, k + 1, k + 2); }
            return this;
        }
        static void Swap<T>(List<T> l, int a, int b) { if (l.Count <= b) return; var t = l[a]; l[a] = l[b]; l[b] = t; }
        public Geo Translate(float x, float y, float z) { var d = new Vector3(x, y, z); for (int k = 0; k < P.Count; k++) P[k] += d; return this; }
        public Geo RotateX(float a) => Apply(World.RotX(a));
        public Geo RotateY(float a) => Apply(World.RotY(a));
        public Geo RotateZ(float a) => Apply(World.RotZ(a));
        public Geo Scale(float x, float y, float z) => Apply(World.Sc(x, y, z));
        public Geo Scale(float s) => Scale(s, s, s);

        /// <summary>three.js toNonIndexed.</summary>
        public Geo NI()
        {
            if (I == null) return this;
            var g = new Geo { UV2 = UV2 != null ? new List<Vector2>() : null };
            foreach (var i in I) { g.P.Add(P[i]); g.N.Add(N.Count > i ? N[i] : Vector3.zero); g.UV.Add(UV.Count > i ? UV[i] : Vector2.zero); g.UV2?.Add(UV2[i]); }
            return g;
        }

        /// <summary>three.js computeVertexNormals: area-weighted per shared vertex when indexed, flat per face otherwise.</summary>
        public Geo ComputeNormals()
        {
            var acc = new Vector3[P.Count];
            int n = I != null ? I.Count : P.Count;
            for (int k = 0; k < n; k += 3)
            {
                int a = I != null ? I[k] : k, b = I != null ? I[k + 1] : k + 1, c = I != null ? I[k + 2] : k + 2;
                var f = Vector3.Cross(P[c] - P[b], P[a] - P[b]);
                acc[a] += f; acc[b] += f; acc[c] += f;
            }
            N = new List<Vector3>(P.Count); foreach (var v in acc) N.Add(v.normalized);
            return this;
        }

        // ------------------------------------------------------------ three.js primitives
        public static Geo Box(float w, float h, float d, int ws = 1, int hs = 1, int ds = 1)
        {
            var g = new Geo { I = new List<int>() };
            void Plane(int u, int v, int wAx, float udir, float vdir, float width, float height, float depth, int gx, int gy)
            {
                float segW = width / gx, segH = height / gy, wh = width / 2, hh = height / 2, dh = depth / 2; int start = g.P.Count;
                for (int iy = 0; iy <= gy; iy++)
                    for (int ix = 0; ix <= gx; ix++)
                    {
                        var p = Vector3.zero; p[u] = (ix * segW - wh) * udir; p[v] = (iy * segH - hh) * vdir; p[wAx] = dh;
                        var nn = Vector3.zero; nn[wAx] = depth > 0 ? 1 : -1;
                        g.P.Add(p); g.N.Add(nn); g.UV.Add(new Vector2((float)ix / gx, 1 - (float)iy / gy));
                    }
                for (int iy = 0; iy < gy; iy++)
                    for (int ix = 0; ix < gx; ix++)
                    {
                        int a = start + ix + (gx + 1) * iy, b = start + ix + (gx + 1) * (iy + 1), c = start + ix + 1 + (gx + 1) * (iy + 1), e = start + ix + 1 + (gx + 1) * iy;
                        g.I.AddRange(new[] { a, b, e, b, c, e });
                    }
            }
            Plane(2, 1, 0, -1, -1, d, h, w, ds, hs);
            Plane(2, 1, 0, 1, -1, d, h, -w, ds, hs);
            Plane(0, 2, 1, 1, 1, w, d, h, ws, ds);
            Plane(0, 2, 1, 1, -1, w, d, -h, ws, ds);
            Plane(0, 1, 2, 1, -1, w, h, d, ws, hs);
            Plane(0, 1, 2, -1, -1, w, h, -d, ws, hs);
            return g;
        }

        public static Geo Cylinder(float rt, float rb, float h, int rs = 32, int hs = 1, bool open = false)
        {
            var g = new Geo { I = new List<int>() }; float half = h / 2, slope = (rb - rt) / h;
            var grid = new int[hs + 1, rs + 1];
            for (int y = 0; y <= hs; y++)
            {
                float v = (float)y / hs, r = v * (rb - rt) + rt;
                for (int x = 0; x <= rs; x++)
                {
                    float u = (float)x / rs, th = u * Mathf.PI * 2, s = Mathf.Sin(th), c = Mathf.Cos(th);
                    grid[y, x] = g.P.Count; g.P.Add(new Vector3(r * s, -v * h + half, r * c));
                    g.N.Add(new Vector3(s, slope, c).normalized); g.UV.Add(new Vector2(u, 1 - v));
                }
            }
            for (int x = 0; x < rs; x++)
                for (int y = 0; y < hs; y++)
                {
                    int a = grid[y, x], b = grid[y + 1, x], c = grid[y + 1, x + 1], d = grid[y, x + 1];
                    if (rt > 0 || y != 0) g.I.AddRange(new[] { a, b, d });
                    if (rb > 0 || y != hs - 1) g.I.AddRange(new[] { b, c, d });
                }
            if (!open)
            {
                void Cap(bool top)
                {
                    float r = top ? rt : rb, sign = top ? 1 : -1; int cs = g.P.Count;
                    for (int x = 1; x <= rs; x++) { g.P.Add(new Vector3(0, half * sign, 0)); g.N.Add(new Vector3(0, sign, 0)); g.UV.Add(new Vector2(.5f, .5f)); }
                    int ce = g.P.Count;
                    for (int x = 0; x <= rs; x++)
                    {
                        float u = (float)x / rs, th = u * Mathf.PI * 2, c = Mathf.Cos(th), s = Mathf.Sin(th);
                        g.P.Add(new Vector3(r * s, half * sign, r * c)); g.N.Add(new Vector3(0, sign, 0)); g.UV.Add(new Vector2(c * .5f + .5f, s * .5f * sign + .5f));
                    }
                    for (int x = 0; x < rs; x++) { int c = cs + x, i = ce + x; if (top) g.I.AddRange(new[] { i, i + 1, c }); else g.I.AddRange(new[] { i + 1, i, c }); }
                }
                if (rt > 0) Cap(true);
                if (rb > 0) Cap(false);
            }
            return g;
        }

        public static Geo Sphere(float r, int ws = 32, int hs = 16)
        {
            var g = new Geo { I = new List<int>() }; var grid = new int[hs + 1, ws + 1];
            for (int iy = 0; iy <= hs; iy++)
            {
                float v = (float)iy / hs, uo = iy == 0 ? .5f / ws : iy == hs ? -.5f / ws : 0;
                for (int ix = 0; ix <= ws; ix++)
                {
                    float u = (float)ix / ws;
                    var p = new Vector3(-r * Mathf.Cos(u * Mathf.PI * 2) * Mathf.Sin(v * Mathf.PI), r * Mathf.Cos(v * Mathf.PI), r * Mathf.Sin(u * Mathf.PI * 2) * Mathf.Sin(v * Mathf.PI));
                    grid[iy, ix] = g.P.Count; g.P.Add(p); g.N.Add(p.normalized); g.UV.Add(new Vector2(u + uo, 1 - v));
                }
            }
            for (int iy = 0; iy < hs; iy++)
                for (int ix = 0; ix < ws; ix++)
                {
                    int a = grid[iy, ix + 1], b = grid[iy, ix], c = grid[iy + 1, ix], d = grid[iy + 1, ix + 1];
                    if (iy != 0) g.I.AddRange(new[] { a, b, d });
                    if (iy != hs - 1) g.I.AddRange(new[] { b, c, d });
                }
            return g;
        }

        public static Geo Plane(float w, float h, int ws = 1, int hs = 1)
        {
            var g = new Geo { I = new List<int>() }; float sw = w / ws, sh = h / hs;
            for (int iy = 0; iy <= hs; iy++)
                for (int ix = 0; ix <= ws; ix++)
                { g.P.Add(new Vector3(ix * sw - w / 2, -(iy * sh - h / 2), 0)); g.N.Add(Vector3.forward); g.UV.Add(new Vector2((float)ix / ws, 1 - (float)iy / hs)); }
            for (int iy = 0; iy < hs; iy++)
                for (int ix = 0; ix < ws; ix++)
                {
                    int a = ix + (ws + 1) * iy, b = ix + (ws + 1) * (iy + 1), c = ix + 1 + (ws + 1) * (iy + 1), d = ix + 1 + (ws + 1) * iy;
                    g.I.AddRange(new[] { a, b, d, b, c, d });
                }
            return g;
        }

        /// <summary>three.js IcosahedronGeometry (PolyhedronGeometry): each face split into (detail+1)² triangles, pushed out to the sphere.</summary>
        public static Geo Icosahedron(float r, int detail)
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            float[] V = { -1, t, 0, 1, t, 0, -1, -t, 0, 1, -t, 0, 0, -1, t, 0, 1, t, 0, -1, -t, 0, 1, -t, t, 0, -1, t, 0, 1, -t, 0, -1, -t, 0, 1 };
            int[] F = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            var g = new Geo(); int cols = detail + 1;
            Vector3 Vx(int i) => new Vector3(V[i * 3], V[i * 3 + 1], V[i * 3 + 2]);
            for (int f = 0; f < F.Length; f += 3)
            {
                Vector3 a = Vx(F[f]), b = Vx(F[f + 1]), c = Vx(F[f + 2]);
                var vs = new List<Vector3>[cols + 1];
                for (int i = 0; i <= cols; i++)
                {
                    vs[i] = new List<Vector3>(); var aj = Vector3.Lerp(a, c, (float)i / cols); var bj = Vector3.Lerp(b, c, (float)i / cols); int rows = cols - i;
                    for (int j = 0; j <= rows; j++) vs[i].Add(j == 0 && i == cols ? aj : Vector3.Lerp(aj, bj, (float)j / rows));
                }
                for (int i = 0; i < cols; i++)
                    for (int j = 0; j < 2 * (cols - i) - 1; j++)
                    {
                        int k = j / 2;
                        if (j % 2 == 0) { g.P.Add(vs[i][k + 1]); g.P.Add(vs[i + 1][k]); g.P.Add(vs[i][k]); }
                        else { g.P.Add(vs[i][k + 1]); g.P.Add(vs[i + 1][k + 1]); g.P.Add(vs[i + 1][k]); }
                    }
            }
            for (int k = 0; k < g.P.Count; k++) { var n = g.P[k].normalized; g.P[k] = n * r; g.N.Add(n); g.UV.Add(Vector2.zero); }
            if (detail == 0) g.ComputeNormals();
            return g;
        }

        // ------------------------------------------------------------ the web game's helpers
        /// <summary>Box-projected UVs in world units, so textures keep their scale on any part.</summary>
        public Geo WorldUV(float s = 1)
        {
            UV = new List<Vector2>(P.Count);
            for (int i = 0; i < P.Count; i++)
            {
                var n = N[i]; float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z); var p = P[i];
                Vector2 uv = ay >= ax && ay >= az ? new Vector2(p.x, p.z) : ax >= az ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
                UV.Add(uv * s);
            }
            return this;
        }

        public static Geo B(float w, float h, float d, float x = 0, float y = 0, float z = 0, float s = 1) => Box(w, h, d).NI().Translate(x, y + h / 2, z).WorldUV(s);
        public static Geo C(float rt, float rb, float h, float x = 0, float y = 0, float z = 0, int seg = 10) => Cylinder(rt, rb, h, seg).NI().Translate(x, y + h / 2, z);
        public static Geo Sph(float r, int ws, int hs) => Sphere(r, ws, hs).NI();

        /// <summary>Gable roof (ridge along x) with UVs running down the slopes, so tiles line up.</summary>
        public static Geo Gable(float w, float d, float rise, float over = .06f, float s = 3.2f)
        {
            float hw = w / 2 + over, hd = d / 2 + over, slope = Mathf.Sqrt(hd * hd + rise * rise);
            float[] P = { -hw, 0, -hd, hw, 0, -hd, hw, rise, 0, -hw, 0, -hd, hw, rise, 0, -hw, rise, 0, -hw, 0, hd, -hw, rise, 0, hw, rise, 0, -hw, 0, hd, hw, rise, 0, hw, 0, hd,
                -hw * .98f, 0, -hd * .98f, -hw * .98f, rise * .98f, 0, -hw * .98f, 0, hd * .98f, hw * .98f, 0, -hd * .98f, hw * .98f, 0, hd * .98f, hw * .98f, rise * .98f, 0 };
            float[] U = { 0, 0, 2 * hw * s, 0, 2 * hw * s, slope * s, 0, 0, 2 * hw * s, slope * s, 0, slope * s, 0, 0, 0, slope * s, 2 * hw * s, slope * s, 0, 0, 2 * hw * s, slope * s, 2 * hw * s, 0,
                0, 0, .5f, .5f, 1, 0, 0, 0, 1, 0, .5f, .5f };
            var g = new Geo();
            for (int k = 0; k < P.Length / 3; k++) { g.P.Add(new Vector3(P[k * 3], P[k * 3 + 1], P[k * 3 + 2])); g.UV.Add(new Vector2(U[k * 2], U[k * 2 + 1])); }
            return g.ComputeNormals();
        }

        /// <summary>Hipped roof: ridge along x, all four sides sloping.</summary>
        public static Geo Hip(float w, float d, float rise, float over = .08f)
        {
            float hw = w / 2 + over, hd = d / 2 + over, r = Mathf.Max(0, hw - hd);
            Vector3 A = new Vector3(-hw, 0, -hd), Bq = new Vector3(hw, 0, -hd), Cq = new Vector3(hw, 0, hd), D = new Vector3(-hw, 0, hd), R1 = new Vector3(-r, rise, 0), R2 = new Vector3(r, rise, 0);
            var tris = new[] { new[] { A, R1, R2 }, new[] { A, R2, Bq }, new[] { D, Cq, R2 }, new[] { D, R2, R1 }, new[] { A, D, R1 }, new[] { Bq, R2, Cq } };
            var g = new Geo();
            foreach (var t in tris)
            {
                Vector3 a = t[0], b = t[1], c = t[2], u = b - a, v = c - a;
                if (u.z * v.x - u.x * v.z < 0) { var tmp = b; b = c; c = tmp; }
                g.P.Add(a); g.P.Add(b); g.P.Add(c);
            }
            return g.ComputeNormals().WorldUV(7);
        }

        /// <summary>three.js BufferGeometry.lookAt: turns the geometry so its +z faces the direction.</summary>
        public Geo LookAt(Vector3 dir)
        {
            var z = dir.sqrMagnitude < 1e-12f ? Vector3.forward : dir.normalized;
            var x = Vector3.Cross(Vector3.up, z);
            if (x.sqrMagnitude < 1e-12f) { z.z += .0001f; z = z.normalized; x = Vector3.Cross(Vector3.up, z); }
            x = x.normalized; var y = Vector3.Cross(z, x);
            var m = Matrix4x4.identity; m.SetColumn(0, x); m.SetColumn(1, y); m.SetColumn(2, z);
            return Apply(m);
        }

        /// <summary>Quads arranged on a sphere with normals pointing outward, so a crown shades like a volume.</summary>
        public static Geo CrownCards(int count, float radius, float cx, float cy, float cz, float size, uint seed)
        {
            var parts = new List<Geo>(); var r = new Core.Mulberry32(seed);
            for (int k = 0; k < count; k++)
            {
                float u = (float)r.Next() * 2 - 1, a = (float)r.Next() * Mathf.PI * 2, s = Mathf.Sqrt(1 - u * u);
                var dir = new Vector3(s * Mathf.Cos(a), u * .7f + .15f, s * Mathf.Sin(a)).normalized;
                var g = Plane(size, size).LookAt(dir).Translate(cx + dir.x * radius, cy + dir.y * radius * .8f, cz + dir.z * radius);
                for (int i = 0; i < g.P.Count; i++) g.N[i] = new Vector3(g.P[i].x - cx, (g.P[i].y - cy) * 1.3f, g.P[i].z - cz).normalized;
                parts.Add(g);
            }
            return Merge(parts);
        }

        /// <summary>Non-indexed concatenation (three.js mergeGeometries after the web game's merge()).</summary>
        public static Geo Merge(IEnumerable<Geo> list)
        {
            var o = new Geo(); var all = new List<Geo>(list); bool uv2 = all.Exists(g => g.UV2 != null);
            if (uv2) o.UV2 = new List<Vector2>();
            foreach (var g0 in all)
            {
                var g = g0.NI(); o.P.AddRange(g.P); o.N.AddRange(g.N);
                if (g.UV.Count == g.P.Count) o.UV.AddRange(g.UV); else for (int k = 0; k < g.P.Count; k++) o.UV.Add(Vector2.zero);
                if (uv2) { if (g.UV2 != null) o.UV2.AddRange(g.UV2); else for (int k = 0; k < g.P.Count; k++) o.UV2.Add(Vector2.zero); }
            }
            return o;
        }

        /// <summary>Stores each vertex's own height (above the model's origin) in UV2, so wind still sways foliage once it is baked into a batch.</summary>
        public Geo KeepHeight() { var g = NI(); g.UV2 = new List<Vector2>(g.P.Count); foreach (var p in g.P) g.UV2.Add(new Vector2(Mathf.Max(p.y, 0), 0)); return g; }
        public static Geo Merge(params Geo[] list) => Merge((IEnumerable<Geo>)list);

        // ------------------------------------------------------------ to Unity
        /// <summary>Appends this geometry to Unity-space buffers: z flipped, winding reversed.</summary>
        public void AppendTo(List<Vector3> pos, List<Vector3> nrm, List<Vector2> uv, List<int> idx, List<Vector2> uv2 = null)
        {
            var g = I != null ? NI() : this; int b = pos.Count;
            for (int k = 0; k < g.P.Count; k++)
            {
                pos.Add(World.U(g.P[k])); nrm.Add(World.U(g.N.Count > k ? g.N[k] : Vector3.up)); uv.Add(g.UV.Count > k ? g.UV[k] : Vector2.zero);
                uv2?.Add(g.UV2 != null ? g.UV2[k] : Vector2.zero);
            }
            for (int k = 0; k < g.P.Count; k += 3) { idx.Add(b + k); idx.Add(b + k + 2); idx.Add(b + k + 1); }
        }

        public Mesh ToMesh(string name)
        {
            var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var idx = new List<int>(); var uv2 = UV2 != null ? new List<Vector2>() : null;
            AppendTo(pos, nrm, uv, idx, uv2);
            return Build(name, pos, nrm, uv, idx, uv2);
        }

        public static Mesh Build(string name, List<Vector3> pos, List<Vector3> nrm, List<Vector2> uv, List<int> idx, List<Vector2> uv2 = null)
        {
            var m = new Mesh { name = name };
            if (pos.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(pos); m.SetNormals(nrm); m.SetUVs(0, uv); if (uv2 != null) m.SetUVs(1, uv2); m.SetTriangles(idx, 0, true);
            return m;
        }
    }
}
