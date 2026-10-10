using IslandSim.Core;
using UnityEngine;
using UnityEngine.Rendering;
using static IslandSim.Core.Defs;

namespace IslandSim.View
{
    /// <summary>Ocean surface (custom shader fed by a per-tile depth texture) and the deep seabed beyond the map.</summary>
    public sealed class WaterView
    {
        readonly Sim sim;
        readonly Texture2D depthTex;
        readonly Color32[] depth = new Color32[W * H];
        public Material Material { get; }

        public WaterView(Sim sim, Transform parent)
        {
            this.sim = sim;
            depthTex = new Texture2D(W, H, TextureFormat.RGBA32, false, true) { name = "lagoon depth", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

            var seabed = new GameObject("Seabed");
            seabed.transform.SetParent(parent, false);
            seabed.transform.position = new Vector3(W / 2f, World.TopY(-3) - 0.05f, H / 2f);
            seabed.AddComponent<MeshFilter>().sharedMesh = MeshKit.Quad(1800);
            var sr = seabed.AddComponent<MeshRenderer>();
            sr.sharedMaterial = Mats.Lit(World.Hex("#1d3d52"), 0);
            sr.shadowCastingMode = ShadowCastingMode.Off;

            var go = new GameObject("Ocean");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(W / 2f, World.WaterY, H / 2f);
            go.AddComponent<MeshFilter>().sharedMesh = Grid(1400, 220);
            var mr = go.AddComponent<MeshRenderer>();
            var sh = Shader.Find("IslandSim/Water");
            Material = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { name = "isim water" };
            Material.SetTexture("_DepthTex", depthTex);
            Material.SetVector("_MapSize", new Vector4(W, H, 0, 0));
            mr.sharedMaterial = Material;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        static Mesh Grid(float size, int segs)
        {
            int n = segs + 1; var v = new Vector3[n * n]; var t = new int[segs * segs * 6];
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) v[j * n + i] = new Vector3(-size / 2 + size * i / segs, 0, -size / 2 + size * j / segs);
            int k = 0;
            for (int j = 0; j < segs; j++)
                for (int i = 0; i < segs; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    t[k++] = a; t[k++] = c; t[k++] = d; t[k++] = a; t[k++] = d; t[k++] = b;
                }
            var m = new Mesh { name = "ocean grid", indexFormat = IndexFormat.UInt32, vertices = v, triangles = t };
            m.RecalculateNormals();
            m.bounds = new Bounds(Vector3.zero, new Vector3(size, 4, size));
            return m;
        }

        /// <summary>Refresh the depth texture: R = normalised depth below the surface, G = land flag.</summary>
        public void Rebuild()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int h = sim.Hgt[Sim.Idx(x, y)];
                    byte d = h >= 0 ? (byte)0 : (byte)Mathf.RoundToInt(Mathf.Min(1, (World.WaterY - World.TopY(h)) / 2.2f) * 255);
                    // texture rows run bottom-up and world Z = H - row, so row (H-1-y) holds map row y
                    depth[(H - 1 - y) * W + x] = new Color32(d, h >= 0 ? (byte)255 : (byte)0, 0, 255);
                }
            depthTex.SetPixels32(depth);
            depthTex.Apply(false);
        }
    }
}
