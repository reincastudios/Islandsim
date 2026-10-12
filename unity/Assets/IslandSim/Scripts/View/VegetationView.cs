using System.Collections.Generic;
using IslandSim.Core;
using UnityEngine;
using UnityEngine.Rendering;
using static IslandSim.Core.Defs;
using static IslandSim.View.World;

namespace IslandSim.View
{
    /// <summary>
    /// Trees, palms, mangroves, bushes, taro, rocks and grass, as in the web game: instanced per 64-tile chunk and kind,
    /// rebuilt only where tiles changed, and dropped chunk by chunk with distance (grass first, then bushes, coconuts and roots).
    /// </summary>
    public sealed class VegetationView
    {
        readonly Sim S;
        sealed class Kind
        {
            public string Name; public Mesh Mesh; public Material Mat; public bool Shadow; public float Far;
            public PlantModel Plant; public float[] LodFar;   // a custom plant (see PlantModels) and where each of its LODs ends
            public bool Hidden;                               // part of a built-in plant that a custom one replaced
            public int[] Stand;                               // the built-in kinds drawn past the custom plant's last LOD
        }
        readonly Kind[] kinds;
        readonly Dictionary<string, int> kindIx = new Dictionary<string, int>();
        readonly Dictionary<int, List<int>> swap = new Dictionary<int, List<int>>();   // built-in kind -> its custom variants
        sealed class Batch { public Matrix4x4[] M; public int N; public MaterialPropertyBlock P; public Matrix4x4[][] PM; }   // PM: per custom-plant part
        sealed class Chunk { public int X0, Y0, W, H; public Bounds Box; public bool Dirty = true; public List<Batch>[] ByKind; }
        readonly List<Chunk> chunks = new List<Chunk>();
        public bool Grass = true;

        static Color[] L(params string[] hex) { var c = new Color[hex.Length]; for (int k = 0; k < hex.Length; k++) c[k] = Lin(hex[k]); return c; }
        static readonly Color[] LEAF = L("#d8ecc8", "#c8e0b0", "#e8f2d0", "#bcd8a8", "#d0e8c0");   // tints over the leaf texture
        static readonly Color[] MANGROVE = L("#7f9a6a", "#8aa673", "#748f62");
        static readonly Color[] TARO = L("#c8f0a0", "#b8e890", "#d0f4a8");
        static readonly Color[] FROND = L("#f0f4d8", "#e0ecc0", "#d8e8b8", "#f4f0c8");
        static readonly Color[] ROCKS = L("#c8c0b6", "#b0a89e", "#d8d0c4");

        public VegetationView(Sim s)
        {
            S = s;
            Kind K(string n, Geo g, string mat, bool shadow, float far) { var m = g.ToMesh(n); m.RecalculateTangents(); return new Kind { Name = n, Mesh = m, Mat = Mats.Get(mat), Shadow = shadow, Far = far }; }
            var list = new List<Kind>
            {
                K("trunk", Models.TreeTrunk, "bark", true, 620), K("crown", Models.TreeCrown, "leaf", true, 760),
                K("pTrunk", Models.PalmTrunk, "palmBark", true, 520), K("pCrown", Models.Fronds, "frond", true, 760),
                K("nut", Models.Coconuts, "nut", false, 160), K("mRoot", Models.MangroveRoots, "root", true, 300),
                K("mCrown", Models.MangroveCrown, "leaf", true, 760), K("bush", Models.Bush, "leaf", true, 320),
                K("taro", Models.Taro, "leaf", false, 260), K("rock", Models.Rock, "rock", false, 300), K("tuft", Models.Tuft, "tuft", false, 150),
            };
            for (int k = 0; k < list.Count; k++) kindIx[list[k].Name] = k;
            // custom plants from Resources/IslandSim/Plants, each fitted to the built-in plant it replaces
            var slots = new (string slot, string main, string[] hide, string leaf)[]
            {
                ("palm", "pTrunk", new[] { "pCrown", "nut" }, "frond"), ("tree", "trunk", new[] { "crown" }, "leaf"), ("mangrove", "mRoot", new[] { "mCrown" }, "leaf"),
                ("bush", "bush", new string[0], "leaf"), ("taro", "taro", new string[0], "leaf"), ("rock", "rock", new string[0], null), ("grass", "tuft", new string[0], "tuft"),
            };
            foreach (var (slot, main, hide, leaf) in slots)
            {
                var mk = list[kindIx[main]]; float hgt = mk.Mesh.bounds.max.y, far = mk.Far;
                foreach (var h in hide) { var hk = list[kindIx[h]]; hgt = Mathf.Max(hgt, hk.Mesh.bounds.max.y); far = Mathf.Max(far, hk.Far); }
                var vars = PlantModels.Variants(slot, hgt, leaf != null ? Mats.WindOf(leaf) : 0); if (vars.Count == 0) continue;
                var ix = new List<int>();
                foreach (var v in vars)
                {
                    // nearest level up close, the last one out to the plant's range; a heavy model with no LODs only up close,
                    // with the built-in plant standing in beyond (the two look alike from that far)
                    int n = v.Lods.Count; var lf = new float[n]; float reach = n == 1 && v.Tris > 5000 ? Mathf.Min(far, 110) : far;
                    for (int l = 0; l < n; l++) { float f = (l + 1f) / n; lf[l] = reach * f * f; }
                    var stand = new List<int> { kindIx[main] }; foreach (var h in hide) stand.Add(kindIx[h]);
                    ix.Add(list.Count); list.Add(new Kind { Name = slot + "#" + v.Name, Plant = v, LodFar = lf, Shadow = mk.Shadow, Far = far, Stand = reach < far ? stand.ToArray() : null });
                }
                swap[kindIx[main]] = ix;
                foreach (var h in hide) list[kindIx[h]].Hidden = true;
            }
            kinds = list.ToArray();
            for (int k = 0; k < kinds.Length; k++) kindIx[kinds[k].Name] = k;
            bufM = InitBuf<Matrix4x4>(kinds.Length); bufC = InitBuf<Vector4>(kinds.Length);
            for (int cy = 0; cy < CYN; cy++)
                for (int cx = 0; cx < CXN; cx++)
                {
                    int x0 = cx * CH, y0 = cy * CH, w = Mathf.Min(CH, W - x0), h = Mathf.Min(CH, H - y0);
                    var c = new Chunk { X0 = x0, Y0 = y0, W = w, H = h, Box = new Bounds(U(x0 + w / 2f, 1.5f, y0 + h / 2f), new Vector3(w + 4, 6, h + 4)), ByKind = new List<Batch>[kinds.Length] };
                    for (int k = 0; k < kinds.Length; k++) c.ByKind[k] = new List<Batch>();
                    chunks.Add(c);
                }
        }

        // instances gathered per kind while a chunk is rebuilt
        readonly List<Matrix4x4>[] bufM; readonly List<Vector4>[] bufC;
        static List<T>[] InitBuf<T>(int n) { var a = new List<T>[n]; for (int k = 0; k < a.Length; k++) a[k] = new List<T>(); return a; }
        Matrix4x4 cur; float curH;
        void Set(float px, float py, float pz, float rx, float ry, float sx, float sy, float sz)
        {
            cur = U(TRS(new Vector3(px, py, pz), rx, ry, 0, new Vector3(sx, sy, sz))); curH = Hash(Mathf.FloorToInt(px * 4), Mathf.FloorToInt(pz * 4), 31);
        }
        void Push(string k, Color? col = null)
        {
            int i = kindIx[k]; if (kinds[i].Hidden) return;
            if (swap.TryGetValue(i, out var vars))
            {
                // real textures keep their own colour, varied a little per plant (some lusher, some sun-bleached) so a
                // stand of one species never looks stamped
                i = vars[Mathf.Min(vars.Count - 1, (int)(curH * vars.Count))];
                float h = (curH * 7.31f) % 1f, v = .9f + .16f * h; col = new Color(v * (1.02f - .06f * h), v, v * (.92f + .1f * h));
            }
            bufM[i].Add(cur); var c = col ?? Color.white; bufC[i].Add(new Vector4(c.r, c.g, c.b, 1));
        }
        static Color Pick(Color[] a, float h) => a[(int)(h * a.Length)];
        int TreesAround(int x, int y)
        {
            int n = 0;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int tx = x + dx, ty = y + dy; if ((dx | dy) == 0 || tx < 0 || ty < 0 || tx >= W || ty >= H) continue;
                if (S.Tree[Sim.Idx(tx, ty)] != 0) n++;
            }
            return n;
        }

        void RebuildChunk(Chunk vc)
        {
            for (int y = vc.Y0; y < vc.Y0 + vc.H; y++)
                for (int x = vc.X0; x < vc.X0 + vc.W; x++)
                {
                    int i = Sim.Idx(x, y), t = S.Tree[i], hv = S.Hgt[i], g = S.Gt[i]; if (hv < 0) continue;
                    if (t != 0)
                    {
                        float s = .8f + Hash(x, y, 3) * .5f;
                        Set(x + .5f + (Hash(x, y, 8) - .5f) * .4f, TopY(hv), y + .5f + (Hash(x, y, 9) - .5f) * .4f, 0, Hash(x, y, 4) * 6, s, s * (.9f + Hash(x, y, 6) * .25f), s);
                        if (t == 1) { Push("trunk"); Push("crown", Pick(LEAF, Hash(x, y, 10))); }
                        else if (t == 2) { Push("pTrunk"); Push("pCrown", Pick(FROND, Hash(x, y, 11))); Push("nut"); }
                        else if (t == 3) { Push("mRoot"); Push("mCrown", Pick(MANGROVE, Hash(x, y, 12))); }
                        if (t == 1 || t == 2)
                        {
                            // jungle, not parkland: where the forest is thick a second, younger tree shares the tile, and
                            // undergrowth crowds round the trunks (look only; the sim still counts one tree per tile)
                            int nb = TreesAround(x, y);
                            if (nb >= 5 && Hash(x, y, 70) < (t == 1 ? .6f : .35f))
                            {
                                float s2 = .55f + Hash(x, y, 71) * .35f;
                                Set(x + .15f + Hash(x, y, 72) * .7f, TopY(hv), y + .15f + Hash(x, y, 73) * .7f, 0, Hash(x, y, 74) * 6, s2, s2 * (.9f + Hash(x, y, 75) * .3f), s2);
                                if (t == 1) { Push("trunk"); Push("crown", Pick(LEAF, Hash(x, y, 76))); } else { Push("pTrunk"); Push("pCrown", Pick(FROND, Hash(x, y, 77))); Push("nut"); }
                            }
                            for (int k = 0; k < (t == 1 ? 2 : 1); k++)
                                if (hv >= 1 && Hash(x, y, 80 + k) < .8f)
                                { float s3 = .6f + Hash(x, y, 82 + k) * .6f; Set(x + Hash(x, y, 84 + k), TopY(hv), y + Hash(x, y, 86 + k), 0, Hash(x, y, 88 + k) * 6, s3, s3, s3); Push("bush", Pick(LEAF, Hash(x, y, 90 + k))); }
                        }
                    }
                    if (S.Bld[i] >= 0 || S.Road[i] != 0) continue;                  // nothing grows on roads or inside plots
                    if (g == GT.Taro)
                    {
                        for (int k = 0; k < 2; k++) { float s = .8f + Hash(x, y, 60 + k) * .6f; Set(x + .25f + k * .5f, TopY(hv), y + .3f + Hash(x, y, 62 + k) * .4f, 0, Hash(x, y, 64 + k) * 6, s, s, s); Push("taro", Pick(TARO, Hash(x, y, 66 + k))); }
                        continue;
                    }
                    if (g == GT.Kulhi) continue;
                    // bushes thicken toward the forest, and beach naupaka walls the sand where it meets the trees
                    int near = t == 0 ? TreesAround(x, y) : 0;
                    float pb = hv >= 1 && hv <= 3 ? Mathf.Min(.9f, .35f + .12f * near) : hv == 0 && near >= 2 ? .45f : 0;
                    for (int k = 0; k < (near >= 3 ? 2 : 1); k++)
                        if (Hash(x, y, 12 + k * 100) < pb) { float s = .7f + Hash(x, y, 13 + k * 100) * .8f; Set(x + Hash(x, y, 14 + k * 100), TopY(hv), y + Hash(x, y, 15 + k * 100), 0, Hash(x, y, 16 + k * 100) * 6, s, s, s); Push("bush", Pick(LEAF, Hash(x, y, 17 + k * 100))); }
                    if (hv >= 4 && Hash(x, y, 18) < .45f || hv == 0 && Hash(x, y, 18) < .04f)
                    { float s = .6f + Hash(x, y, 19) * 1.1f; Set(x + Hash(x, y, 20), TopY(hv) + .01f, y + Hash(x, y, 21), Hash(x, y, 22) * .4f, Hash(x, y, 23) * 6, s, s, s); Push("rock", Pick(ROCKS, Hash(x, y, 24))); }
                    if (Grass && hv >= 1 && hv <= 3 && t != 1)
                        for (int k = 0; k < 3; k++) { float s = .7f + Hash(x, y, 40 + k) * .7f; Set(x + Hash(x, y, 43 + k), TopY(hv), y + Hash(x, y, 46 + k), 0, Hash(x, y, 49 + k) * 6, s, s * (hv >= 3 ? .7f : 1), s); Push("tuft", hv >= 3 ? FROND[3] : Color.white); }
                }
            for (int k = 0; k < kinds.Length; k++)
            {
                var list = vc.ByKind[k]; list.Clear(); var m = bufM[k]; var c = bufC[k];
                for (int at = 0; at < m.Count; at += 1023)
                {
                    int n = Mathf.Min(1023, m.Count - at); var b = new Batch { M = new Matrix4x4[n], N = n, P = new MaterialPropertyBlock() };
                    m.CopyTo(at, b.M, 0, n); var tint = new Vector4[n]; c.CopyTo(at, tint, 0, n); b.P.SetVectorArray("_Tint", tint);
                    var pl = kinds[k].Plant;
                    if (pl != null)
                    {
                        b.PM = new Matrix4x4[pl.PartCount][]; int g = 0;
                        foreach (var lod in pl.Lods) foreach (var part in lod) { var a = b.PM[g++] = new Matrix4x4[n]; for (int q = 0; q < n; q++) a[q] = b.M[q] * part.M; }
                    }
                    list.Add(b);
                }
                m.Clear(); c.Clear();
            }
            vc.Dirty = false;
        }

        public void Sync(bool all = false)
        {
            var r = S.DirtyTrees.Take();
            if (all) foreach (var vc in chunks) vc.Dirty = true;
            else if (!r.Empty) foreach (var vc in chunks) if (vc.X0 <= r.X1 + 1 && vc.X0 + vc.W > r.X0 - 1 && vc.Y0 <= r.Y1 + 1 && vc.Y0 + vc.H > r.Y0 - 1) vc.Dirty = true;   // +1: undergrowth looks at neighbouring trees
            foreach (var vc in chunks) if (vc.Dirty) RebuildChunk(vc);
        }

        readonly Plane[] planes = new Plane[6];
        /// <summary>Draws the chunks in view; far plants are dropped chunk by chunk.</summary>
        public void Draw(Camera cam)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, planes); var cp = cam.transform.position;
            foreach (var vc in chunks)
            {
                if (!GeometryUtility.TestPlanesAABB(planes, vc.Box)) continue;
                var c = vc.Box.center; c.y = 0; float d = Mathf.Max(0, Vector3.Distance(c, cp) - vc.W * .7f);
                for (int k = 0; k < kinds.Length; k++)
                {
                    var kd = kinds[k]; if (d >= kd.Far) continue;
                    if (kd.Plant != null) { DrawPlant(kd, vc, d); continue; }
                    if (kd.Mat == null) continue;
                    foreach (var b in vc.ByKind[k])
                    {
                        var rp = new RenderParams(kd.Mat) { shadowCastingMode = kd.Shadow ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows = true, worldBounds = vc.Box, matProps = b.P };
                        Graphics.RenderMeshInstanced(rp, kd.Mesh, 0, b.M, b.N);
                    }
                }
            }
        }

        void DrawPlant(Kind kd, Chunk vc, float d)
        {
            var batches = vc.ByKind[kindIx[kd.Name]];
            if (kd.Stand != null && d >= kd.LodFar[kd.LodFar.Length - 1])
            {
                foreach (int s in kd.Stand)
                {
                    var bk = kinds[s]; if (d >= bk.Far || bk.Mat == null) continue;
                    foreach (var b in batches)
                    {
                        var rp = new RenderParams(bk.Mat) { shadowCastingMode = bk.Shadow ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows = true, worldBounds = vc.Box, matProps = b.P };
                        Graphics.RenderMeshInstanced(rp, bk.Mesh, 0, b.M, b.N);
                    }
                }
                return;
            }
            int lod = 0; while (lod < kd.LodFar.Length - 1 && d >= kd.LodFar[lod]) lod++;
            int g0 = 0; for (int l = 0; l < lod; l++) g0 += kd.Plant.Lods[l].Length;
            foreach (var b in batches)
            {
                var parts = kd.Plant.Lods[lod];
                for (int p = 0; p < parts.Length; p++)
                {
                    var rp = new RenderParams(parts[p].Mat) { shadowCastingMode = kd.Shadow ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows = true, worldBounds = vc.Box, matProps = b.P };
                    Graphics.RenderMeshInstanced(rp, parts[p].Mesh, parts[p].Sub, b.PM[g0 + p], b.N);
                }
            }
        }
    }
}
