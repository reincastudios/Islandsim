using System.Collections.Generic;
using IslandSim.Core;
using UnityEngine;
using UnityEngine.Rendering;
using static IslandSim.Core.Defs;
using static IslandSim.View.World;

namespace IslandSim.View
{
    /// <summary>
    /// The ground, as in the web game: 64×64-tile chunks of corner-and-centre vertices with per-vertex tint and seven layer
    /// weights, rebuilt only where tiles changed; the coastline distance field the waves follow; the water-depth texture the
    /// sea reads; and the seabed beyond the map.
    /// </summary>
    public sealed class TerrainView
    {
        readonly Sim S;
        sealed class Chunk
        {
            public int X0, Y0, W, H, NC, NV; public Mesh Mesh; public bool Dirty = true;
            public Vector3[] Pos, Nrm; public Color[] Col; public Vector4[] S0, S1;
        }
        readonly List<Chunk> chunks = new List<Chunk>();
        public readonly float[] CornerY = new float[(W + 1) * (H + 1)];
        readonly byte[] oceanMask = new byte[W * H];
        HashSet<int> yardIds = new HashSet<int>();
        public readonly Material Mat;

        // coastline distance in tiles (negative on land) at 4× tile resolution, so surf follows a smooth shore
        public const int SR = 4, SW = W * SR, SH = H * SR;
        readonly byte[] shoreData = new byte[SW * SH];
        public readonly Texture2D ShoreTex = new Texture2D(SW, SH, TextureFormat.R8, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "shore" };
        // water depth (r), land (g) and open ocean (b) per tile, for the sea
        readonly Color32[] depthData = new Color32[W * H];
        public readonly Texture2D DepthTex = new Texture2D(W, H, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "depth" };
        int shoreVer = -1;

        public TerrainView(Sim s, Transform parent)
        {
            S = s;
            Mat = Mats.Terrain();
            if (Mat != null) { Mat.SetTexture("_Alb", Mats.Array("_albedo")); Mat.SetTexture("_Nor", Mats.Array("_normal")); }
            for (int cy = 0; cy < CYN; cy++)
                for (int cx = 0; cx < CXN; cx++)
                {
                    int x0 = cx * CH, y0 = cy * CH, w = Mathf.Min(CH, W - x0), h = Mathf.Min(CH, H - y0), nc = (w + 1) * (h + 1), nv = nc + w * h;
                    var c = new Chunk { X0 = x0, Y0 = y0, W = w, H = h, NC = nc, NV = nv, Pos = new Vector3[nv], Nrm = new Vector3[nv], Col = new Color[nv], S0 = new Vector4[nv], S1 = new Vector4[nv] };
                    var ix = new int[w * h * 12]; int k = 0;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int a = y * (w + 1) + x, b = a + 1, cc = a + w + 1, d = cc + 1, m = nc + y * w + x;
                            // the web game's fan [a,c,m, c,d,m, d,b,m, b,a,m], wound the other way for Unity
                            ix[k++] = a; ix[k++] = m; ix[k++] = cc; ix[k++] = cc; ix[k++] = m; ix[k++] = d; ix[k++] = d; ix[k++] = m; ix[k++] = b; ix[k++] = b; ix[k++] = m; ix[k++] = a;
                        }
                    for (int y = 0; y <= h; y++) for (int x = 0; x <= w; x++) c.Pos[y * (w + 1) + x] = U(x0 + x, 0, y0 + y);
                    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) c.Pos[nc + y * w + x] = U(x0 + x + .5f, 0, y0 + y + .5f);
                    c.Mesh = new Mesh { name = $"terrain {cx},{cy}" }; c.Mesh.MarkDynamic();
                    c.Mesh.vertices = c.Pos; c.Mesh.normals = c.Nrm; c.Mesh.colors = c.Col; c.Mesh.SetUVs(0, c.S0); c.Mesh.SetUVs(1, c.S1);
                    c.Mesh.SetTriangles(ix, 0, false);
                    c.Mesh.bounds = new Bounds(U(x0 + w / 2f, -1, y0 + h / 2f), new Vector3(w + 2, 6, h + 2));
                    var go = new GameObject(c.Mesh.name); go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = c.Mesh;
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Mat; mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;
                    chunks.Add(c);
                }
            // outer seabed beyond the map: the same sand, seen through deep water
            var sb = new GameObject("seabed"); sb.transform.SetParent(parent, false);
            sb.AddComponent<MeshFilter>().sharedMesh = Geo.Plane(2400, 2400).RotateX(-Mathf.PI / 2).Translate(W / 2f, TopY(-3) - .05f, H / 2f).ToMesh("seabed");
            var smr = sb.AddComponent<MeshRenderer>(); smr.sharedMaterial = Mats.Seabed(); smr.shadowCastingMode = ShadowCastingMode.Off; smr.receiveShadows = true;
        }

        // ------------------------------------------------------------ per-tile look
        static readonly Dictionary<int, Color> Tint = new Dictionary<int, Color>
        {
            [-3] = Lin("#d4d6c8"), [-2] = Lin("#ece6d2"),   // lagoon floors are white coral sand, so the water over them glows turquoise
            [-1] = Lin("#f2ead6"), [0] = Lin("#ffffff"), [1] = Lin("#ffffff"), [2] = Lin("#f2f4e8"), [3] = Lin("#f0eadc"),
        };
        static readonly Dictionary<int, Color> GtTint = new Dictionary<int, Color>
        { [GT.Kulhi] = Lin("#b9b48a"), [GT.Pond] = Lin("#8c9a74"), [GT.Taro] = Lin("#e6f0c4"), [GT.Seagrass] = Lin("#a9b88e") };

        Color TileTint(int x, int y)
        {
            int i = Sim.Idx(x, y), h = S.Hgt[i];
            if (!GtTint.TryGetValue(S.Gt[i], out var c)) c = Tint.TryGetValue(h, out var t) ? t : Color.white;
            float n = (Hash(x, y, 5) - .5f) * 0.06f; c.r += n; c.g += n; c.b += n * .6f;
            return c;
        }
        bool NearLand(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) if (Sim.Inb(x + dx, y + dy) && S.Hgt[Sim.Idx(x + dx, y + dy)] >= 0) return true;
            return false;
        }
        // ground layer weights for one tile: sand, grass, forest, rock, reef, soil, path
        void TileSplat(int x, int y, float[] o)
        {
            int i = Sim.Idx(x, y), h = S.Hgt[i], g = S.Gt[i]; System.Array.Clear(o, 0, 7);
            if (S.Road[i] != 0 && h >= 0) { o[6] = 1; return; }
            if (g == GT.Kulhi) { o[5] = .55f; o[2] = .45f; return; }       // mangrove mud and leaf litter
            if (g == GT.Pond) { o[5] = .6f; o[4] = .4f; return; }
            if (g == GT.Seagrass) { o[1] = .7f; o[4] = .3f; return; }
            if (g == GT.Taro && S.Bld[i] < 0) { o[5] = .6f; o[1] = .4f; return; }
            if (h <= -2) o[0] = 1;                                        // lagoon floor: clean sand
            else if (h == -1 && S.Sandbank[i] != 0) { o[0] = .9f; o[4] = .1f; }   // sandbank: clean sand with a little rubble
            else if (h == -1) { if (NearLand(x, y)) { o[0] = .85f; o[4] = .15f; } else o[4] = 1; }   // reef flat: rubble, seagrass; clean sand on the shelf off a beach
            else if (h == 0) o[0] = 1;
            else if (h <= 2) { if (S.Tree[i] == 1) o[2] = 1; else { o[1] = 1; o[5] = Hash(x, y, 31) < .18f ? .6f : 0; } }
            else if (h <= 4) { if (S.Tree[i] == 1) { o[2] = .7f; o[5] = .3f; } else { o[5] = .6f; o[1] = .4f; } }
            else o[3] = 1;
            if (yardIds.Contains(S.Bld[i]) && h >= 0) { System.Array.Clear(o, 0, 7); o[0] = 1; return; }   // compound yards: swept white sand
            if (S.Bld[i] >= 0 && h >= 1) { o[1] *= .4f; o[5] += .8f; }    // trampled ground around buildings
        }

        // Sandbanks lie just under the still sea, so they dry when the tide falls below Tide.LOW and residents can wade
        const float BANK_Y = WATER_Y - 0.055f;
        float TileY(int i) => S.Hgt[i] == -1 && S.Sandbank[i] != 0 ? BANK_Y : TopY(S.Hgt[i]);
        float CornerHeight(int x, int y)
        {
            float sh = 0, landSum = 0; int n = 0, landN = 0, bankN = 0;
            for (int ty = y - 1; ty <= y; ty++)
                for (int tx = x - 1; tx <= x; tx++)
                {
                    if (!Sim.Inb(tx, ty)) continue; int i = Sim.Idx(tx, ty), h = S.Hgt[i]; float t = TileY(i); sh += t; n++; if (h >= 0) { landSum += t; landN++; } else if (S.Sandbank[i] != 0) bankN++;
                }
            float yy = n > 0 ? sh / n : TopY(-3); if (landN >= 2) yy = Mathf.Max(yy, Mathf.Min(landSum / landN, 0.05f)); // the beach runs down to meet the water almost flush, with no lip
            if (landN == 0 && bankN == 0) yy = SeaFloor(x, y, yy);
            else if (landN == 0 && bankN == n) yy += (Hash(x, y, 61) - .5f) * .045f;   // the bank's crest rises and dips, so it dries in bars and pools
            return Coast(x, y, yy);
        }
        // Under water the floor is averaged over a wider 4×4 patch of tiles, so a reef flat slopes down into the lagoon over
        // two tiles instead of dropping off a tile edge, and every corner gets a small random rise or dip so the floor rolls
        // in sandy mounds and hollows (look only; the sim's depths are unchanged)
        static readonly float[] SF_W = { 1, 3, 3, 1 };
        float SeaFloor(int x, int y, float near)
        {
            float s = 0, wsum = 0;
            for (int j = 0; j < 4; j++)
                for (int i = 0; i < 4; i++)
                {
                    int tx = x - 2 + i, ty = y - 2 + j; if (!Sim.Inb(tx, ty)) continue; int h = S.Hgt[Sim.Idx(tx, ty)];
                    float w = SF_W[i] * SF_W[j]; s += (h >= 0 ? TopY(-1) : TopY(h)) * w; wsum += w;
                }
            float f = wsum > 0 ? Mathf.Lerp(near, s / wsum, .7f) : near;
            return f + (Hash(x, y, 57) - .5f) * Mathf.Clamp01((WATER_Y - f) * .3f) * .28f;
        }

        // The coast follows the smooth shoreline field (the one the waves use), not the tile grid: near the waterline every
        // vertex takes its height from its distance to that line, so the water's edge curves round the island instead of
        // stepping tile by tile. The beach rises quickly to the tile height inland, and seaward the sand shelves gently for
        // three to four tiles, so the water thins to clear over it (look only; the sim's tiles are unchanged).
        const float SHELF_TOP = WATER_Y - 0.04f, SHELF_SLOPE = 0.22f, SHELF_REACH = 3.6f, BEACH_SLOPE = 0.8f, DUG_SLOPE = 1.5f;
        float Coast(float fx, float fy, float y)
        {
            float sd = ShoreAt(fx, fy);
            if (sd >= 0)
            {
                if (sd >= SHELF_REACH) return y;
                // ground dug deeper than a reef flat (a dredged canal, a harbour basin, a lagoon drop-off) keeps its depth:
                // the shelf drops steeply there instead of filling the hole back up with sand
                float slope = Mathf.Lerp(SHELF_SLOPE, DUG_SLOPE, Mathf.Clamp01((TopY(-1) - y) / (TopY(-1) - TopY(-2))));
                float shelf = WATER_Y - slope * sd - 0.04f * Mathf.Min(sd / 0.25f, 1);
                return y > WATER_Y ? shelf : Mathf.Max(y, shelf);
            }
            float beach = WATER_Y + BEACH_SLOPE * -sd;
            return Mathf.Min(y > WATER_Y ? y : TopY(0), beach);
        }
        // signed distance to the coastline in tiles at a point in tile space, bilinear like the shaders' ShoreD
        float ShoreAt(float fx, float fy)
        {
            float sx = Mathf.Clamp(fx * SR - .5f, 0, SW - 1.001f), sy = Mathf.Clamp(fy * SR - .5f, 0, SH - 1.001f);
            int x0 = (int)sx, y0 = (int)sy; float tx = sx - x0, ty = sy - y0; int i = y0 * SW + x0;
            float v = (shoreData[i] * (1 - tx) + shoreData[i + 1] * tx) * (1 - ty) + (shoreData[i + SW] * (1 - tx) + shoreData[i + SW + 1] * tx) * ty;
            return v / 255f * 9f - 3f;
        }
        public float CAt(int x, int y) => CornerY[Mathf.Clamp(y, 0, H) * (W + 1) + Mathf.Clamp(x, 0, W)];

        /// <summary>Height of the ground (or the sea surface) at any point, from the corner heights.</summary>
        public float GroundY(float fx, float fy)
        {
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy); float tx = fx - x0, ty = fy - y0;
            float a = CAt(x0, y0), b = CAt(x0 + 1, y0), c = CAt(x0, y0 + 1), d = CAt(x0 + 1, y0 + 1);
            return Mathf.Max(WATER_Y, a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty);
        }

        readonly float[] spl = new float[7], acc = new float[7];
        void RebuildChunk(Chunk c)
        {
            int x0 = c.X0, y0 = c.Y0, w = c.W, h = c.H, nc = c.NC;
            for (int y = 0; y <= h; y++)
                for (int x = 0; x <= w; x++)
                {
                    int X = x0 + x, Y = y0 + y, v = y * (w + 1) + x, n = 0; float r = 0, g = 0, b = 0; System.Array.Clear(acc, 0, 7);
                    for (int ty = Y - 1; ty <= Y; ty++)
                        for (int tx = X - 1; tx <= X; tx++)
                        {
                            if (!Sim.Inb(tx, ty)) continue; n++; var t = TileTint(tx, ty); r += t.r; g += t.g; b += t.b;
                            TileSplat(tx, ty, spl); for (int k = 0; k < 7; k++) acc[k] += spl[k];
                        }
                    c.Pos[v].y = CAt(X, Y); c.Col[v] = new Color(r / n, g / n, b / n, 1);
                    c.S0[v] = new Vector4(acc[0] / n, acc[1] / n, acc[2] / n, acc[3] / n); c.S1[v] = new Vector4(acc[4] / n, acc[5] / n, acc[6] / n, 0);
                    float nx = -(CAt(X + 1, Y) - CAt(X - 1, Y)) / 2, nz = -(CAt(X, Y + 1) - CAt(X, Y - 1)) / 2;
                    c.Nrm[v] = U(new Vector3(nx, 1, nz).normalized);
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int X = x0 + x, Y = y0 + y, v = nc + y * w + x;
                    int hh = S.Hgt[Sim.Idx(X, Y)]; float ty = TileY(Sim.Idx(X, Y));
                    if (hh < 0)
                    {
                        // out in open water the floor rolls in mounds and hollows instead of tile-sized steps; a tile dug into
                        // the land (its corners on the beach) keeps its own depth so the hole shows
                        float c0 = CAt(X, Y), c1 = CAt(X + 1, Y), c2 = CAt(X, Y + 1), c3 = CAt(X + 1, Y + 1);
                        if (Mathf.Max(Mathf.Max(c0, c1), Mathf.Max(c2, c3)) < WATER_Y - .1f) ty = Mathf.Lerp(ty, (c0 + c1 + c2 + c3) * .25f, .6f);
                    }
                    c.Pos[v].y = Coast(X + .5f, Y + .5f, ty); c.Col[v] = TileTint(X, Y);
                    TileSplat(X, Y, spl); c.S0[v] = new Vector4(spl[0], spl[1], spl[2], spl[3]); c.S1[v] = new Vector4(spl[4], spl[5], spl[6], 0);
                    float nx = -(CAt(X + 1, Y) + CAt(X + 1, Y + 1) - CAt(X, Y) - CAt(X, Y + 1)) / 2, nz = -(CAt(X, Y + 1) + CAt(X + 1, Y + 1) - CAt(X, Y) - CAt(X + 1, Y)) / 2;
                    c.Nrm[v] = U(new Vector3(nx, 1, nz).normalized);
                }
            // the shelf is pale sand however deep the tile under it: it lightens and turns sandy as it rises toward the surface
            var pale = Tint[-1];
            for (int v = 0; v < c.NV; v++)
            {
                float y = c.Pos[v].y; if (y >= WATER_Y || y < TopY(-1) - 0.05f) continue;
                float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(TopY(-1), SHELF_TOP, y));
                var col = c.Col[v]; c.Col[v] = new Color(Mathf.Max(col.r, Mathf.Lerp(col.r, pale.r, k)), Mathf.Max(col.g, Mathf.Lerp(col.g, pale.g, k)), Mathf.Max(col.b, Mathf.Lerp(col.b, pale.b, k)), 1);
                float ks = k * .8f; var s0 = c.S0[v] * (1 - ks); var s1 = c.S1[v] * (1 - ks); s0.x += ks; c.S0[v] = s0; c.S1[v] = s1;
            }
            // steep faces become rock whatever their height band (only real cliffs: beach slopes stay sand)
            for (int v = 0; v < c.NV; v++)
            {
                float y = c.Pos[v].y, ny = c.Nrm[v].y;
                if (y > 0.05f)
                {
                    float k = Mathf.Min(1, Mathf.Max(0, (0.6f - ny) / 0.2f));
                    if (k > 0) { var s0 = c.S0[v]; var s1 = c.S1[v]; s0 *= 1 - k; s1.x *= 1 - k; s1.y *= 1 - k; s0.w += k; c.S0[v] = s0; c.S1[v] = s1; }
                }
            }
            c.Mesh.vertices = c.Pos; c.Mesh.normals = c.Nrm; c.Mesh.colors = c.Col; c.Mesh.SetUVs(0, c.S0); c.Mesh.SetUVs(1, c.S1);
            c.Dirty = false;
        }

        // open ocean: the deepest water reachable from the map's edge without crossing a reef (lagoon holes stay lagoon-coloured)
        void ComputeOcean()
        {
            System.Array.Clear(oceanMask, 0, oceanMask.Length); var q = new int[W * H]; int qh = 0, qt = 0;
            void Seed(int x, int y) { int i = Sim.Idx(x, y); if (S.Hgt[i] <= -3 && oceanMask[i] == 0) { oceanMask[i] = 1; q[qt++] = i; } }
            for (int x = 0; x < W; x++) { Seed(x, 0); Seed(x, H - 1); }
            for (int y = 0; y < H; y++) { Seed(0, y); Seed(W - 1, y); }
            while (qh < qt)
            {
                int c = q[qh++], cx = c % W, cy = c / W;
                for (int k = 0; k < 4; k++) { int nx = cx + Sim.DX[k], ny = cy + Sim.DY[k]; if (!Sim.Inb(nx, ny)) continue; int ni = Sim.Idx(nx, ny); if (oceanMask[ni] == 0 && S.Hgt[ni] <= -3) { oceanMask[ni] = 1; q[qt++] = ni; } }
            }
        }

        /// <summary>Rebuilds whatever the game changed since the last frame. Returns true if anything was redone.</summary>
        public bool Sync()
        {
            var r = S.DirtyLook.Take(); var sd = S.DirtyShore.Take();
            if (r.Empty && sd.Empty) return false;
            if (r.Empty) r = sd;
            yardIds = new HashSet<int>(); foreach (var b in S.Buildings) if (b.Type == BType.House) yardIds.Add(b.Id);
            bool full = r.X0 == 0 && r.Y0 == 0 && r.X1 == W - 1 && r.Y1 == H - 1;
            // the shoreline field first: the coast's vertex heights are read from it
            if (full && shoreVer != S.TerrainVer) ComputeShore(0, 0, W - 1, H - 1);
            else if (!sd.Empty) ComputeShore(sd.X0, sd.Y0, sd.X1, sd.Y1);
            shoreVer = S.TerrainVer;
            if (full) { foreach (var c in chunks) c.Dirty = true; ComputeOcean(); }
            else foreach (var c in chunks) if (c.X0 <= r.X1 + 6 && c.X0 + c.W >= r.X0 - 6 && c.Y0 <= r.Y1 + 6 && c.Y0 + c.H >= r.Y0 - 6) c.Dirty = true;  // a tile's look reaches its corners and the sand shelf off its coast, and their normals one corner further
            for (int y = Mathf.Max(0, r.Y0 - 5); y <= Mathf.Min(H, r.Y1 + 6); y++) for (int x = Mathf.Max(0, r.X0 - 5); x <= Mathf.Min(W, r.X1 + 6); x++) CornerY[y * (W + 1) + x] = CornerHeight(x, y);
            foreach (var c in chunks) if (c.Dirty) RebuildChunk(c);
            for (int y = Mathf.Max(0, r.Y0 - 1); y <= Mathf.Min(H - 1, r.Y1 + 1); y++)
                for (int x = Mathf.Max(0, r.X0 - 1); x <= Mathf.Min(W - 1, r.X1 + 1); x++)
                {
                    int i = Sim.Idx(x, y), h = S.Hgt[i];
                    depthData[i] = new Color32((byte)(h >= 0 ? 0 : Mathf.Floor(Mathf.Min(1, (WATER_Y - TileY(i)) / 2.2f) * 255 + .5f)), (byte)(h >= 0 ? 255 : 0), (byte)(h <= -3 && oceanMask[i] != 0 ? 255 : 0), 255);
                }
            DepthTex.SetPixels32(depthData); DepthTex.Apply(false);
            return true;
        }

        // The field only matters within a few tiles of the coast (it is clamped to -3..6), so an edit recomputes a window
        // around it with a margin wide enough that the clamped values inside come out the same as a full pass.
        void ComputeShore(int tx0, int ty0, int tx1, int ty1)
        {
            const int M = 8;
            int wx0 = Mathf.Max(0, (tx0 - M) * SR), wy0 = Mathf.Max(0, (ty0 - M) * SR), wx1 = Mathf.Min(SW, (tx1 + 1 + M) * SR), wy1 = Mathf.Min(SH, (ty1 + 1 + M) * SR);
            int ww = wx1 - wx0, wh = wy1 - wy0, N = ww * wh;
            var land = new byte[N]; var din = new float[N]; var dout = new float[N];
            int Mk(int x, int y) => S.Hgt[Sim.Idx(Mathf.Clamp(x, 0, W - 1), Mathf.Clamp(y, 0, H - 1))] >= 0 ? 1 : 0;
            float Bl(int x, int y) => (4 * Mk(x, y) + 2 * (Mk(x - 1, y) + Mk(x + 1, y) + Mk(x, y - 1) + Mk(x, y + 1)) + Mk(x - 1, y - 1) + Mk(x + 1, y - 1) + Mk(x - 1, y + 1) + Mk(x + 1, y + 1)) / 16f;
            for (int sy = 0; sy < wh; sy++)
                for (int sx = 0; sx < ww; sx++)
                {
                    float wx = (sx + wx0 + .5f) / SR - .5f, wy = (sy + wy0 + .5f) / SR - .5f; int x0 = Mathf.FloorToInt(wx), y0 = Mathf.FloorToInt(wy); float fx = wx - x0, fy = wy - y0;
                    float v = (Mk(x0, y0) * (1 - fx) + Mk(x0 + 1, y0) * fx) * (1 - fy) + (Mk(x0, y0 + 1) * (1 - fx) + Mk(x0 + 1, y0 + 1) * fx) * fy;
                    float b = (Bl(x0, y0) * (1 - fx) + Bl(x0 + 1, y0) * fx) * (1 - fy) + (Bl(x0, y0 + 1) * (1 - fx) + Bl(x0 + 1, y0 + 1) * fx) * fy;
                    land[sy * ww + sx] = (byte)((v + b) * .5f >= .5f ? 1 : 0);   // half blurred: corners round off, single tiles stay
                }
            const float INF = 1e6f, D = 1.41421356f;
            for (int i = 0; i < N; i++) { din[i] = land[i] != 0 ? INF : 0; dout[i] = land[i] != 0 ? 0 : INF; }
            foreach (var d in new[] { din, dout })   // two-pass chamfer distance transform
            {
                for (int y = 0; y < wh; y++)
                    for (int x = 0; x < ww; x++)
                    {
                        int i = y * ww + x; float v = d[i]; if (v == 0) continue;
                        if (x > 0) v = Mathf.Min(v, d[i - 1] + 1);
                        if (y > 0) { v = Mathf.Min(v, d[i - ww] + 1); if (x > 0) v = Mathf.Min(v, d[i - ww - 1] + D); if (x < ww - 1) v = Mathf.Min(v, d[i - ww + 1] + D); }
                        d[i] = v;
                    }
                for (int y = wh - 1; y >= 0; y--)
                    for (int x = ww - 1; x >= 0; x--)
                    {
                        int i = y * ww + x; float v = d[i]; if (v == 0) continue;
                        if (x < ww - 1) v = Mathf.Min(v, d[i + 1] + 1);
                        if (y < wh - 1) { v = Mathf.Min(v, d[i + ww] + 1); if (x < ww - 1) v = Mathf.Min(v, d[i + ww + 1] + D); if (x > 0) v = Mathf.Min(v, d[i + ww - 1] + D); }
                        d[i] = v;
                    }
            }
            // write back only the part the margin makes exact
            int ix0 = Mathf.Max(0, (tx0 - 4) * SR) - wx0, iy0 = Mathf.Max(0, (ty0 - 4) * SR) - wy0, ix1 = Mathf.Min(SW, (tx1 + 5) * SR) - wx0, iy1 = Mathf.Min(SH, (ty1 + 5) * SR) - wy0;   // wide enough for the coast heights that read it
            bool full = wx0 == 0 && wy0 == 0 && wx1 == SW && wy1 == SH;
            for (int y = full ? 0 : iy0; y < (full ? wh : iy1); y++)
                for (int x = full ? 0 : ix0; x < (full ? ww : ix1); x++)
                {
                    int i = y * ww + x; float sd = (land[i] != 0 ? -(din[i] - .5f) : dout[i] - .5f) / SR;
                    shoreData[(y + wy0) * SW + x + wx0] = (byte)Mathf.Floor(Mathf.Clamp01((sd + 3) / 9) * 255 + .5f);
                }
            ShoreTex.SetPixelData(shoreData, 0); ShoreTex.Apply(false);
        }
    }
}
