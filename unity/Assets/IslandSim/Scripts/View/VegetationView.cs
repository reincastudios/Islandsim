using System.Collections.Generic;
using IslandSim.Core;
using UnityEngine;
using UnityEngine.Rendering;
using static IslandSim.Core.Defs;

namespace IslandSim.View
{
    /// <summary>Broadleaf trees, palms, bushes and rocks drawn with GPU instancing, a few colour variants each.</summary>
    public sealed class VegetationView
    {
        const int Batch = 1023;

        sealed class Layer
        {
            public Mesh Mesh; public Material[] Variants; public ShadowCastingMode Shadows;
            public List<Matrix4x4>[] Items;
            public List<Matrix4x4[]>[] Batches;
            public List<int>[] Counts;
        }

        static readonly string[] Leaf = { "#2f6f2e", "#3b7d2f", "#2a6234", "#4a8a34", "#356f3a" };
        static readonly string[] Frond = { "#4c8f34", "#5c9a38", "#467f2f", "#6aa33f" };
        static readonly string[] Rocks = { "#8d877f", "#7a746c", "#a19a90" };

        readonly Sim sim;
        readonly Layer trunk, crown, palmTrunk, fronds, bush, rock;
        readonly Layer[] layers;

        public VegetationView(Sim sim)
        {
            this.sim = sim;
            trunk = MakeLayer(MeshKit.BroadleafTrunk(), new[] { "#6b4a2c" }, ShadowCastingMode.On);
            crown = MakeLayer(MeshKit.BroadleafCrown(), Leaf, ShadowCastingMode.On);
            palmTrunk = MakeLayer(MeshKit.PalmTrunk(), new[] { "#8a7356" }, ShadowCastingMode.On);
            fronds = MakeLayer(MeshKit.PalmFronds(), Frond, ShadowCastingMode.On);
            bush = MakeLayer(MeshKit.Bush(), Leaf, ShadowCastingMode.Off);
            rock = MakeLayer(MeshKit.Rock(), Rocks, ShadowCastingMode.Off);
            layers = new[] { trunk, crown, palmTrunk, fronds, bush, rock };
        }

        static Layer MakeLayer(Mesh mesh, string[] colours, ShadowCastingMode shadows)
        {
            var l = new Layer { Mesh = mesh, Shadows = shadows, Variants = new Material[colours.Length], Items = new List<Matrix4x4>[colours.Length], Batches = new List<Matrix4x4[]>[colours.Length], Counts = new List<int>[colours.Length] };
            for (int k = 0; k < colours.Length; k++)
            {
                l.Variants[k] = Mats.Lit(World.Hex(colours[k]), .1f);
                l.Items[k] = new List<Matrix4x4>(); l.Batches[k] = new List<Matrix4x4[]>(); l.Counts[k] = new List<int>();
            }
            return l;
        }

        static double Hs(int x, int y, int s) => Noise.Hash2(x, y, s);
        static int Pick(int x, int y, int s, int n) => Mathf.Min(n - 1, (int)(Hs(x, y, s) * n));

        public void Rebuild()
        {
            foreach (var l in layers) foreach (var list in l.Items) list.Clear();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = Sim.Idx(x, y), t = sim.Tree[i], hv = sim.Hgt[i]; if (hv < 0) continue;
                    float top = World.TopY(hv);
                    if (t != 0)
                    {
                        float s = .8f + (float)Hs(x, y, 3) * .5f;
                        var p = World.Pos(x + .5f + ((float)Hs(x, y, 8) - .5f) * .4f, y + .5f + ((float)Hs(x, y, 9) - .5f) * .4f, top);
                        var m = Matrix4x4.TRS(p, Quaternion.Euler(0, (float)Hs(x, y, 4) * 6 * Mathf.Rad2Deg, 0), Vector3.one * s);
                        if (t == 1) { trunk.Items[0].Add(m); crown.Items[Pick(x, y, 10, Leaf.Length)].Add(m); }
                        else { palmTrunk.Items[0].Add(m); fronds.Items[Pick(x, y, 11, Frond.Length)].Add(m); }
                    }
                    if (sim.Bld[i] >= 0) continue;
                    if (hv >= 1 && hv <= 3 && Hs(x, y, 12) < .35)
                    {
                        float s = .7f + (float)Hs(x, y, 13) * .8f;
                        var p = World.Pos(x + (float)Hs(x, y, 14), y + (float)Hs(x, y, 15), top);
                        bush.Items[Pick(x, y, 17, Leaf.Length)].Add(Matrix4x4.TRS(p, Quaternion.Euler(0, (float)Hs(x, y, 16) * 6 * Mathf.Rad2Deg, 0), Vector3.one * s));
                    }
                    if ((hv >= 4 && Hs(x, y, 18) < .45) || (hv == 0 && Hs(x, y, 18) < .04))
                    {
                        float s = .6f + (float)Hs(x, y, 19) * 1.1f;
                        var p = World.Pos(x + (float)Hs(x, y, 20), y + (float)Hs(x, y, 21), top + .02f);
                        rock.Items[Pick(x, y, 24, Rocks.Length)].Add(Matrix4x4.TRS(p, Quaternion.Euler((float)Hs(x, y, 22) * Mathf.Rad2Deg, (float)Hs(x, y, 23) * 6 * Mathf.Rad2Deg, 0), Vector3.one * s));
                    }
                }
            foreach (var l in layers)
                for (int k = 0; k < l.Items.Length; k++)
                {
                    l.Batches[k].Clear(); l.Counts[k].Clear();
                    var src = l.Items[k];
                    for (int o = 0; o < src.Count; o += Batch)
                    {
                        int n = Mathf.Min(Batch, src.Count - o); var arr = new Matrix4x4[n];
                        src.CopyTo(o, arr, 0, n); l.Batches[k].Add(arr); l.Counts[k].Add(n);
                    }
                }
        }

        public int Count(int kind) => kind == 1 ? trunk.Items[0].Count : palmTrunk.Items[0].Count;

        public void Draw()
        {
            foreach (var l in layers)
                for (int k = 0; k < l.Variants.Length; k++)
                    for (int b = 0; b < l.Batches[k].Count; b++)
                        Graphics.DrawMeshInstanced(l.Mesh, 0, l.Variants[k], l.Batches[k][b], l.Counts[k][b], null, l.Shadows, true);
        }
    }
}
