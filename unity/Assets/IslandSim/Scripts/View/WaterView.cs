using UnityEngine;
using UnityEngine.Rendering;
using static IslandSim.Core.Defs;
using static IslandSim.View.World;

namespace IslandSim.View
{
    /// <summary>
    /// The sea, as one body of water. Out at sea it is one plane far past the map's edge. Round every coast it is a fine
    /// grid of its own, in 16×16-tile blocks, whose vertices the water shader moves: the swell rises as it shoals, the sea
    /// draws back, then runs up the sand as a thin sheet of the same water and drains away. Both are drawn with the same
    /// material, and the plane leaves out the blocks the fine grid covers, so there is no painted wave on the sand and no
    /// line between the sea and the shore.
    /// </summary>
    public sealed class WaterView
    {
        public readonly Material Mat;
        public readonly MeshRenderer Renderer;

        // the shore blocks: three grids (a quarter, half and whole tile), each block drawing the finest its distance from
        // the camera needs. A block's grid blends into the next coarser one (the shader collapses every other vertex) before
        // it switches, so neighbouring blocks always meet without a crack.
        const int FB = 16;
        const int BW = (W + FB - 1) / FB, BH = (H + FB - 1) / FB;
        static readonly float[] STEP = { .25f, .5f, 1f }, MORPH0 = { 60, 140, 1e6f }, MORPH1 = { 80, 180, 2e6f };
        readonly Transform shoreRoot;
        readonly MeshFilter[] blockF = new MeshFilter[BW * BH];
        readonly MeshRenderer[] blockR = new MeshRenderer[BW * BH];
        readonly sbyte[] blockLod = new sbyte[BW * BH];
        readonly Mesh[] lodMesh = new Mesh[3];
        readonly Material[] lodMat = new Material[3];
        // which blocks the fine grid draws (the plane leaves them out), and how much of the plane's open-sea swell may show
        // (none in or near a shore block, so the two surfaces meet at the same height)
        readonly byte[] maskData = new byte[BW * BH], calmData = new byte[BW * BH];
        readonly Texture2D maskTex = new Texture2D(BW, BH, TextureFormat.R8, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, name = "fine mask" };
        bool uploaded;
        readonly Texture2D calmTex = new Texture2D(BW, BH, TextureFormat.R8, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "calm" };

        public WaterView(Transform parent, TerrainView terrain)
        {
            Mat = Mats.Water();
            var go = new GameObject("water"); go.transform.SetParent(parent, false);
            var mesh = Geo.Plane(2400, 2400, 240, 240).RotateX(-Mathf.PI / 2).Translate(W / 2f, WATER_Y, H / 2f).ToMesh("water");
            mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + new Vector3(0, 1, 0));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = go.AddComponent<MeshRenderer>(); Renderer.sharedMaterial = Mat;
            Renderer.shadowCastingMode = ShadowCastingMode.Off; Renderer.receiveShadows = false;
            { var n = Mats.T("water_normal"); if (Mat != null) Mat.SetTexture("_WaterNormal", n); Shader.SetGlobalTexture("_IsWaterNormal", n); }

            shoreRoot = new GameObject("shore water").transform; shoreRoot.SetParent(parent, false);
            for (int l = 0; l < 3; l++)
            {
                lodMesh[l] = Grid(STEP[l]);
                if (Mat == null) continue;
                lodMat[l] = new Material(Mat) { name = "shore water " + l };
                lodMat[l].SetFloat("_Fine", 1); lodMat[l].SetVector("_FineLod", new Vector4(MORPH0[l], MORPH1[l], STEP[l], 0));
            }
            Shader.SetGlobalTexture("_IsFineMask", maskTex); Shader.SetGlobalTexture("_IsCalm", calmTex);
            Shader.SetGlobalVector("_IsBlocks", new Vector4(FB, BW, BH, 0));
            SyncShore(terrain, new RectInt(0, 0, W, H));
        }

        // one block's grid, in tile steps along +x and the map's rows (Unity's -z)
        static Mesh Grid(float step)
        {
            int n = Mathf.RoundToInt(FB / step), m = n + 1;
            var v = new Vector3[m * m]; var ix = new int[n * n * 6]; int k = 0;
            for (int j = 0; j <= n; j++) for (int i = 0; i <= n; i++) v[j * m + i] = new Vector3(i * step, 0, -j * step);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = j * m + i, b = a + 1, c = a + m, d = c + 1;
                    ix[k++] = a; ix[k++] = b; ix[k++] = d; ix[k++] = a; ix[k++] = d; ix[k++] = c;
                }
            var mesh = new Mesh { name = "shore water " + step };
            mesh.vertices = v; mesh.SetTriangles(ix, 0, false);
            mesh.bounds = new Bounds(new Vector3(FB / 2f, .4f, -FB / 2f), new Vector3(FB + 2, 3, FB + 2));
            return mesh;
        }

        /// <summary>Puts a fine shore block wherever the coast (or the shallows a wave runs over) lies within the changed
        /// tiles, and takes it away where the coast has gone.</summary>
        public void SyncShore(TerrainView t, RectInt r)
        {
            int bx0 = Mathf.Max(0, (r.xMin - 8) / FB), by0 = Mathf.Max(0, (r.yMin - 8) / FB);
            int bx1 = Mathf.Min(BW - 1, (r.xMax + 8) / FB), by1 = Mathf.Min(BH - 1, (r.yMax + 8) / FB);
            bool changed = false;
            for (int by = by0; by <= by1; by++)
                for (int bx = bx0; bx <= bx1; bx++)
                {
                    bool fine = false;
                    for (int y = by * FB; y < Mathf.Min(H, by * FB + FB) && !fine; y++)
                        for (int x = bx * FB; x < Mathf.Min(W, bx * FB + FB); x++)
                        {
                            float sd = t.ShoreAt(x + .5f, y + .5f);
                            if (sd > -2.5f && sd < 5.5f) { fine = true; break; }
                        }
                    int b = by * BW + bx;
                    if (fine == (maskData[b] != 0)) continue;
                    changed = true; maskData[b] = (byte)(fine ? 255 : 0);
                    if (fine)
                    {
                        var go = new GameObject($"shore water {bx},{by}"); go.transform.SetParent(shoreRoot, false);
                        go.transform.localPosition = U(bx * FB, 0, by * FB);
                        blockF[b] = go.AddComponent<MeshFilter>(); blockR[b] = go.AddComponent<MeshRenderer>();
                        blockR[b].shadowCastingMode = ShadowCastingMode.Off; blockR[b].receiveShadows = false;
                        blockLod[b] = -1;
                    }
                    else { Object.Destroy(blockF[b].gameObject); blockF[b] = null; blockR[b] = null; }
                }
            if (!changed && uploaded) return;
            uploaded = true;
            // the plane's swell fades out two blocks before any shore block, so the two meet flat at the sea's level
            for (int by = 0; by < BH; by++)
                for (int bx = 0; bx < BW; bx++)
                {
                    bool near = false;
                    for (int dy = -2; dy <= 2 && !near; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int x = bx + dx, y = by + dy;
                            if (x >= 0 && y >= 0 && x < BW && y < BH && maskData[y * BW + x] != 0) { near = true; break; }
                        }
                    calmData[by * BW + bx] = (byte)(near ? 0 : 255);
                }
            maskTex.SetPixelData(maskData, 0); maskTex.Apply(false);
            calmTex.SetPixelData(calmData, 0); calmTex.Apply(false);
        }

        /// <summary>Gives each shore block the grid its distance from the camera needs.</summary>
        public void UpdateLod(Vector3 cam)
        {
            if (lodMat[0] == null) return;
            for (int by = 0; by < BH; by++)
                for (int bx = 0; bx < BW; bx++)
                {
                    int b = by * BW + bx; if (blockR[b] == null) continue;
                    // nearest point of the block, at the sea's level, as the shader measures each vertex
                    float x = Mathf.Clamp(cam.x, bx * FB, bx * FB + FB), z = Mathf.Clamp(cam.z, -(by * FB + FB), -by * FB);
                    float d = new Vector3(cam.x - x, cam.y - SeaY, cam.z - z).magnitude;
                    sbyte l = (sbyte)(d < MORPH1[0] ? 0 : d < MORPH1[1] ? 1 : 2);
                    if (l == blockLod[b]) continue;
                    blockLod[b] = l; blockF[b].sharedMesh = lodMesh[l]; blockR[b].sharedMaterial = lodMat[l];
                }
        }

        static readonly int IdSeaY = Shader.PropertyToID("_IsSeaY");
        /// <summary>Lifts the sea by the tide (world units above WATER_Y). The shore water's vertices and the beach find the
        /// sea's edge from the ground's height against this level, so nothing else needs to move.</summary>
        public void SetTide(float h)
        {
            Renderer.transform.localPosition = new Vector3(0, h, 0);
            SeaY = WATER_Y + h; Shader.SetGlobalFloat(IdSeaY, SeaY);
        }

        /// <summary>Refraction reads the scene behind the water (High and Ultra); without it the sea is blended over the floor.</summary>
        public void SetRefraction(bool on)
        {
            foreach (var m in new[] { Mat, lodMat[0], lodMat[1], lodMat[2] })
            {
                if (m == null) continue;
                m.SetFloat("_UseRefr", on ? 1 : 0);
                m.SetFloat("_SrcBlend", on ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", on ? (float)BlendMode.Zero : (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", on ? 1 : 0);
            }
        }
    }
}
