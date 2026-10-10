using IslandSim.Core;
using UnityEngine;
using UnityEngine.Rendering;
using static IslandSim.Core.Defs;

namespace IslandSim.View
{
    /// <summary>
    /// Heightfield mesh (shared corner vertices plus a centre vertex per tile) textured with a baked colour map:
    /// height bands blended between tiles, rock on steep slopes, a wet-sand band at the waterline and fine grain.
    /// </summary>
    public sealed class TerrainView
    {
        public const int Px = 6;                     // colour-map texels per tile
        const int NC = (W + 1) * (H + 1);

        static readonly string[] Band = { "#2b4f63", "#5f8f8c", "#d9c996", "#e6d3a0", "#86b35a", "#6a9c47", "#7d8a4b", "#8a7a5a", "#7c736b", "#9b958f", "#e9e8e4" }; // -3..7
        static readonly Color Rock = World.Hex("#857a6c");

        readonly Sim sim;
        readonly Mesh mesh;
        readonly MeshCollider col;
        readonly Vector3[] pos = new Vector3[NC + W * H];
        readonly Texture2D colorMap;
        readonly Color32[] pixels;
        readonly Color[] band = new Color[Band.Length];
        readonly float[] tileCol = new float[W * H * 3];
        public GameObject Go { get; }

        public TerrainView(Sim sim, Transform parent)
        {
            this.sim = sim;
            for (int k = 0; k < Band.Length; k++) band[k] = World.Hex(Band[k]);

            mesh = new Mesh { name = "atoll terrain", indexFormat = IndexFormat.UInt32 };
            var uv = new Vector2[pos.Length];
            for (int y = 0; y <= H; y++) for (int x = 0; x <= W; x++) { int v = y * (W + 1) + x; pos[v] = World.Pos(x, y, 0); uv[v] = new Vector2((float)x / W, 1 - (float)y / H); }
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { int v = NC + Sim.Idx(x, y); pos[v] = World.Pos(x + .5f, y + .5f, 0); uv[v] = new Vector2((x + .5f) / W, 1 - (y + .5f) / H); }
            var tri = new int[W * H * 12];
            int k2 = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int a = y * (W + 1) + x, b = a + 1, c = a + W + 1, d = c + 1, m = NC + Sim.Idx(x, y);
                    // Rows run toward -Z, so this order is clockwise seen from above (front-facing in Unity).
                    int[] q = { a, m, c, c, m, d, d, m, b, b, m, a };
                    System.Array.Copy(q, 0, tri, k2, 12); k2 += 12;
                }
            mesh.vertices = pos; mesh.uv = uv; mesh.triangles = tri;

            colorMap = new Texture2D(W * Px, H * Px, TextureFormat.RGBA32, true) { name = "atoll colour map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            pixels = new Color32[W * Px * H * Px];

            Go = new GameObject("Terrain");
            Go.transform.SetParent(parent, false);
            Go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = Go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit(Color.white, .06f, 0, colorMap);
            mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;
            col = Go.AddComponent<MeshCollider>();
        }

        void TileColor(int x, int y, out float r, out float g, out float b)
        {
            int i = Sim.Idx(x, y), h = sim.Hgt[i]; var c = band[h + 3];
            float n = ((float)Noise.Hash2(x, y, 5) - .5f) * 0.06f;
            r = c.r + n; g = c.g + n; b = c.b + n * .6f;
            if (h >= 1 && h <= 4 && sim.Tree[i] == 1) { r *= .82f; g *= .9f; b *= .8f; }
        }

        /// <summary>Rebuilds the mesh and the dirty part of the colour map.</summary>
        public void Rebuild(int x0, int y0, int x1, int y1)
        {
            for (int y = 0; y <= H; y++)
                for (int x = 0; x <= W; x++)
                {
                    float sh = 0, landSum = 0; int n = 0, landN = 0;
                    for (int dy = -1; dy <= 0; dy++)
                        for (int dx = -1; dx <= 0; dx++)
                        {
                            int tx = x + dx, ty = y + dy; if (!Sim.Inb(tx, ty)) continue;
                            int h = sim.Hgt[Sim.Idx(tx, ty)]; float top = World.TopY(h);
                            sh += top; n++; if (h >= 0) { landSum += top; landN++; }
                        }
                    float yy = n > 0 ? sh / n : World.TopY(-3);
                    if (landN >= 2) yy = Mathf.Max(yy, Mathf.Min(landSum / landN, 0.12f)); // keep coasts crisp
                    pos[y * (W + 1) + x].y = yy;
                }
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) pos[NC + Sim.Idx(x, y)].y = World.TopY(sim.Hgt[Sim.Idx(x, y)]);
            mesh.vertices = pos;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            col.sharedMesh = null; col.sharedMesh = mesh;

            // colour map, padded so blends at the edge of the change are refreshed too
            x0 = Mathf.Max(0, x0 - 2); y0 = Mathf.Max(0, y0 - 2); x1 = Mathf.Min(W - 1, x1 + 2); y1 = Mathf.Min(H - 1, y1 + 2);
            for (int y = Mathf.Max(0, y0 - 1); y <= Mathf.Min(H - 1, y1 + 1); y++)
                for (int x = Mathf.Max(0, x0 - 1); x <= Mathf.Min(W - 1, x1 + 1); x++)
                {
                    TileColor(x, y, out var r, out var g, out var b); int o = Sim.Idx(x, y) * 3;
                    tileCol[o] = r; tileCol[o + 1] = g; tileCol[o + 2] = b;
                }
            int tw = W * Px;
            for (int py = y0 * Px; py < (y1 + 1) * Px; py++)
                for (int px = x0 * Px; px < (x1 + 1) * Px; px++)
                {
                    float fx = (px + .5f) / Px, fy = (py + .5f) / Px;            // tile space
                    float cx = fx - .5f, cy = fy - .5f;
                    int ix = Mathf.Clamp(Mathf.FloorToInt(cx), 0, W - 2), iy = Mathf.Clamp(Mathf.FloorToInt(cy), 0, H - 2);
                    float ux = Mathf.Clamp01(cx - ix), uy = Mathf.Clamp01(cy - iy);
                    int i00 = Sim.Idx(ix, iy), i10 = i00 + 1, i01 = i00 + W, i11 = i01 + 1;
                    float w00 = (1 - ux) * (1 - uy), w10 = ux * (1 - uy), w01 = (1 - ux) * uy, w11 = ux * uy;
                    float r = tileCol[i00 * 3] * w00 + tileCol[i10 * 3] * w10 + tileCol[i01 * 3] * w01 + tileCol[i11 * 3] * w11;
                    float g = tileCol[i00 * 3 + 1] * w00 + tileCol[i10 * 3 + 1] * w10 + tileCol[i01 * 3 + 1] * w01 + tileCol[i11 * 3 + 1] * w11;
                    float b = tileCol[i00 * 3 + 2] * w00 + tileCol[i10 * 3 + 2] * w10 + tileCol[i01 * 3 + 2] * w01 + tileCol[i11 * 3 + 2] * w11;
                    float h00 = World.TopY(sim.Hgt[i00]), h10 = World.TopY(sim.Hgt[i10]), h01 = World.TopY(sim.Hgt[i01]), h11 = World.TopY(sim.Hgt[i11]);
                    float hgt = h00 * w00 + h10 * w10 + h01 * w01 + h11 * w11;
                    float gx = (h10 - h00) * (1 - uy) + (h11 - h01) * uy, gy = (h01 - h00) * (1 - ux) + (h11 - h10) * ux;
                    float slope = Mathf.Sqrt(gx * gx + gy * gy);
                    // steep ground shows rock
                    if (hgt > 0.05f) { float k = Mathf.Clamp01((slope - 0.75f) / 0.5f); r += (Rock.r - r) * k; g += (Rock.g - g) * k; b += (Rock.b - b) * k; }
                    // wet sand at the waterline
                    if (hgt > -0.2f && hgt < 0.4f) { float f = 1 - 0.3f * Mathf.Max(0, 1 - Mathf.Abs(hgt - 0.1f) / 0.3f); r *= f; g *= f; b *= f; }
                    // grain so ground reads as soil and grass, not flat paint
                    float grain = .86f + .1f * (float)Noise.VNoise(px * .5, py * .5, 1) + .08f * (float)Noise.Hash2(px, py, 3);
                    r *= grain; g *= grain; b *= grain * .98f;
                    int row = H * Px - 1 - py;                                        // texture rows run bottom-up
                    pixels[row * tw + px] = new Color32((byte)(Mathf.Clamp01(r) * 255), (byte)(Mathf.Clamp01(g) * 255), (byte)(Mathf.Clamp01(b) * 255), 255);
                }
            colorMap.SetPixels32(pixels);
            colorMap.Apply(true);
        }
    }
}
