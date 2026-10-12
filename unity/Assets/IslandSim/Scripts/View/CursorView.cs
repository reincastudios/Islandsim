using System.Collections.Generic;
using IslandSim.Core;
using UnityEngine;
using UnityEngine.Rendering;
using static IslandSim.Core.Defs;
using static IslandSim.View.World;

namespace IslandSim.View
{
    /// <summary>
    /// What the pointer shows on the map, as in the web game: the brush ring draped over the ground (with refused tiles in
    /// red), the see-through ghost of a building about to be placed, its work-range square, and the outline of the
    /// selected building.
    /// </summary>
    public sealed class CursorView
    {
        readonly Sim S; readonly TerrainView terrain;
        const int N = 40;
        readonly Mesh ring; readonly Vector3[] ringPos = new Vector3[(N + 1) * (N + 1)];
        readonly Material ringMat; readonly Texture2D bad;
        readonly Mesh quad, box; readonly Material rangeMat, boxMat, ghostOk, ghostBad;
        readonly Dictionary<string, Mesh> ghosts = new Dictionary<string, Mesh>();
        readonly byte[] badData = new byte[11 * 11];

        public CursorView(Sim s, TerrainView t)
        {
            S = s; terrain = t;
            ring = Geo.Plane(1, 1, N, N).RotateX(-Mathf.PI / 2).ToMesh("cursor"); ring.MarkDynamic();
            ringMat = Mats.Cursor();
            bad = new Texture2D(11, 11, TextureFormat.R8, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "brush refused" };
            bad.SetPixelData(badData, 0); bad.Apply(false);
            if (ringMat != null) ringMat.SetTexture("_Bad", bad);

            quad = Geo.Plane(1, 1).RotateX(-Mathf.PI / 2).ToMesh("range");
            rangeMat = Mats.Overlay(Alpha(Lin("#fff6d8"), .16f), false, -1);
            ghostOk = Mats.Overlay(Alpha(Lin("#7ef0d8"), .55f), true); ghostBad = Mats.Overlay(Alpha(Lin("#ff6b6b"), .55f), true);
            boxMat = Mats.Overlay(Lin("#d9a441"), false, 1);
            box = BoxEdges();
        }
        static Color Alpha(Color c, float a) { c.a = a; return c; }

        // the 12 edges of a 1 × 0.5 × 1 box (the web game's EdgesGeometry outline)
        static Mesh BoxEdges()
        {
            var v = new List<Vector3>();
            for (int k = 0; k < 8; k++) v.Add(new Vector3((k & 1) - .5f, ((k >> 1) & 1) * .5f - .25f, ((k >> 2) & 1) - .5f));
            var idx = new List<int>();
            for (int a = 0; a < 8; a++) for (int bit = 1; bit < 8; bit <<= 1) { int b = a | bit; if (b != a) { idx.Add(a); idx.Add(b); } }
            var m = new Mesh { name = "selection" }; m.SetVertices(v); m.SetIndices(idx, MeshTopology.Lines, 0); m.RecalculateBounds();
            return m;
        }

        Mesh Ghost(BType type)
        {
            var d0 = Def(type); string key = type == BType.House ? type.ToString() + S.Plot : type.ToString();
            if (!ghosts.TryGetValue(key, out var m))
            {
                var sz = d0.Sizes != null ? d0.Sizes[S.Plot] : new[] { 3, 2 };
                ghosts[key] = m = Models.All(Models.Building(type, false, 0, sz[0], sz[1])).ToMesh("ghost " + key);
            }
            return m;
        }

        static void Draw(Mesh mesh, Material mat, Matrix4x4 m)
        {
            if (mat == null || mesh == null) return;
            Graphics.RenderMesh(new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false }, mesh, 0, m);
        }

        static Color ToolColour(Tool t) => Srgb(t == Tool.Raise ? "#ffcf5a" : t == Tool.Lower ? "#4fd1c5" : t == Tool.Demolish ? "#ff6b6b" : t == Tool.Smooth ? "#c9b6ff" : "#f3e6c4");

        void PlaceRing(int x, int y, float r, Color col)
        {
            float cx = x + .5f, cy = y + .5f, s0 = 2 * r + 1.4f;
            for (int j = 0; j <= N; j++)
                for (int i = 0; i <= N; i++)
                {
                    float px = cx + (i / (float)N - .5f) * s0, pz = cy + (j / (float)N - .5f) * s0;
                    ringPos[j * (N + 1) + i] = U(px, terrain.GroundY(px, pz) + .06f, pz);
                }
            ring.vertices = ringPos; ring.RecalculateBounds();
            ringMat.SetVector("_C", new Vector4(cx, cy, 0, 0)); ringMat.SetFloat("_R", r); ringMat.SetColor("_Col", col);
        }

        /// <summary>Draws this frame's cursor for the hovered tile (or none), and the selection outline.</summary>
        public void Draw(Vector2Int? hover, Building selected)
        {
            if (hover.HasValue)
            {
                int hx = hover.Value.x, hy = hover.Value.y;
                float gy = Mathf.Max(WATER_Y, TopY(S.Hgt[Sim.Idx(hx, hy)]));
                if (S.Tool == Tool.Build)
                {
                    var type = S.BuildType; var d0 = Def(type); var r = S.CanPlace(type, hx, hy);
                    var fp = r.Ok && r.W > 0 ? r : S.Footprint(type, hx, hy);
                    var m = U(Matrix4x4.Translate(new Vector3(fp.X + fp.W / 2f, gy, fp.Y + fp.D / 2f)) * RotY(d0.Size != null || d0.Sizes != null ? (fp.W >= fp.D ? 0 : Mathf.PI / 2) : 0));
                    Draw(Ghost(type), r.Ok ? ghostOk : ghostBad, m);
                    if (d0.Range > 0) { float s = 2 * d0.Range + 1; Draw(quad, rangeMat, U(Matrix4x4.Translate(new Vector3(hx + .5f, gy + .05f, hy + .5f)) * Sc(s, 1, s))); }
                }
                else if (ringMat != null)
                {
                    var t = S.Tool;
                    PlaceRing(hx, hy, t == Tool.Inspect || t == Tool.Demolish ? .5f : S.BrushSize / 2f, ToolColour(t));
                    var p = S.PreviewBrush(hx, hy);
                    for (int k = 0; k < badData.Length; k++) badData[k] = (byte)(p != null && p.Bad[k] ? 255 : 0);
                    bad.SetPixelData(badData, 0); bad.Apply(false);
                    Draw(ring, ringMat, Matrix4x4.identity);
                }
            }
            if (selected != null)
            {
                var c = Sim.PlotCentre(selected);
                Draw(box, boxMat, U(Matrix4x4.Translate(new Vector3((float)c.x, TopY(S.Hgt[Sim.Idx(selected.X, selected.Y)]) + .25f, (float)c.y)) * Sc(selected.W, 1, selected.D)));
            }
        }
    }
}
