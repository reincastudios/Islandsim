using System.Collections.Generic;
using IslandSim.Core;
using UnityEngine;
using static IslandSim.View.Geo;

namespace IslandSim.View
{
    /// <summary>Parts collected per material, as the web game's kit(): each building costs a handful of draw calls, fewer once batched.</summary>
    public sealed class Kit
    {
        public readonly Dictionary<string, List<Geo>> Parts = new Dictionary<string, List<Geo>>();
        public Kit Add(string mat, Geo g)
        {
            if (!Parts.TryGetValue(mat, out var l)) Parts[mat] = l = new List<Geo>();
            l.Add(Mats.IsFoliage(mat) ? g.KeepHeight() : g); return this;
        }
        /// <summary>Merges each material's parts and applies the model's own scale, in tile space around the model's origin.</summary>
        public Dictionary<string, Geo> Build(float scale = 1)
        {
            var o = new Dictionary<string, Geo>();
            foreach (var kv in Parts) { var g = Merge(kv.Value); if (scale != 1) g.Scale(scale); o[kv.Key] = g; }
            return o;
        }
    }

    /// <summary>
    /// The web game's models, line for line: buildings (makeModel), plants, boats and the trading ship. Numbers are the
    /// web game's, in tile space; Kit and Geo turn them into Unity meshes.
    /// </summary>
    public static class Models
    {
        const float PI = Mathf.PI;

        // ------------------------------------------------------------ vegetation
        public static readonly Vector3 PalmTop = new Vector3(.04f * 25 * .18f + .03f, 1.34f, 0);

        public static readonly Geo PalmTrunk = Make(() =>
        {
            var segs = new List<Geo>(); float y = 0;
            for (int k = 0; k < 6; k++)
            {
                float r1 = .058f - k * .006f, r2 = .064f - k * .006f, bx = .04f * k * k * .18f;
                var g = Cylinder(r1, r2, .23f, 8, 1, true);
                for (int i = 0; i < g.UV.Count; i++) g.UV[i] = new Vector2(g.UV[i].x, g.UV[i].y * .6f + k * .6f);
                segs.Add(g.RotateZ(-.055f * k).Translate(bx, y + .115f, 0)); y += .225f;
            }
            return Merge(segs);
        });

        public static readonly Geo Fronds = Make(() =>
        {
            var fr = new List<Geo>();
            for (int k = 0; k < 11; k++)
            {
                bool young = k >= 8; float len = young ? .62f : .95f + ((k * 37) % 5) * .04f;
                var g = Plane(len, .36f, 10, 2).RotateX(-PI / 2).Translate(len / 2, 0, 0);
                for (int i = 0; i < g.P.Count; i++)
                {
                    var p = g.P[i]; float t = p.x / len, z = p.z;
                    p.y = (young ? .5f * t - .15f * t * t : .18f * t - .75f * t * t) * len - Mathf.Abs(z) * .35f;   // droop and a V-fold along the rachis
                    p.z = z * (1 - .25f * t); g.P[i] = p;
                }
                g.ComputeNormals();
                float ang = k * (PI * 2 / 8) + (k % 2) * .25f + (young ? .4f : 0);
                g.RotateZ(young ? .9f : .12f).RotateY(ang).Translate(PalmTop.x, PalmTop.y, PalmTop.z); fr.Add(g);
            }
            return Merge(fr);
        });

        public static readonly Geo Coconuts = Make(() =>
        {
            var l = new List<Geo>();
            for (int k = 0; k < 4; k++) l.Add(Sph(.045f, 8, 6).Translate(PalmTop.x + Mathf.Cos(k * 1.7f) * .06f, PalmTop.y - .07f, PalmTop.z + Mathf.Sin(k * 1.7f) * .06f));
            return Merge(l);
        });

        public static readonly Geo TreeTrunk = Make(() => Merge(C(.04f, .075f, .62f, 0, 0, 0, 7), C(.02f, .035f, .32f, 0, 0, 0, 5).RotateZ(.7f).Translate(.06f, .34f, 0), C(.02f, .03f, .28f, 0, 0, 0, 5).RotateX(-.8f).Translate(0, .38f, -.06f)));
        public static readonly Geo TreeCrown = Make(() => CrownCards(16, .3f, 0, .82f, 0, .5f, 7));
        public static readonly Geo Bush = Make(() => CrownCards(7, .12f, 0, .14f, 0, .32f, 9));
        public static readonly Geo Tuft = Make(() =>
        {
            var a = Plane(.34f, .26f).Translate(0, .13f, 0); var b = Plane(.34f, .26f).RotateY(PI / 2).Translate(0, .13f, 0);
            foreach (var g in new[] { a, b }) for (int i = 0; i < g.N.Count; i++) g.N[i] = Vector3.up;
            return Merge(a, b);
        });
        public static readonly Geo Rock = Make(() =>
        {
            var g = Icosahedron(.16f, 2);
            for (int i = 0; i < g.P.Count; i++)
            {
                var v = g.P[i]; float f = .75f + .5f * (float)Noise.VNoise(v.x * 9 + 3, v.z * 9 + v.y * 5, 77); v *= f; v.y *= .62f; g.P[i] = v;
            }
            return g.ComputeNormals().WorldUV(2.5f);
        });
        // mangroves: a low dark crown on a tangle of arching stilt roots; taro: a clump of broad leaves
        public static readonly Geo MangroveRoots = Make(() =>
        {
            var l = new List<Geo>();
            for (int k = 0; k < 6; k++) l.Add(C(.012f, .018f, .42f, 0, 0, 0, 4).RotateZ(.55f).Translate(.22f, -.02f, 0).RotateY(k * 1.05f + .3f));
            l.Add(C(.028f, .036f, .36f, 0, .3f, 0, 6)); return Merge(l);
        });
        public static readonly Geo MangroveCrown = Make(() => CrownCards(12, .3f, 0, .62f, 0, .5f, 13));
        public static readonly Geo Taro = Make(() => CrownCards(6, .08f, 0, .16f, 0, .3f, 17));

        static Geo Make(System.Func<Geo> f) => f();

        // ------------------------------------------------------------ buildings
        static readonly string[] WALLS = { "wall0", "wall1", "wall2", "wall3", "wall4", "wall5", "wall6" };
        static readonly string[] SHUTTERS = { "shut0", "shut1", "shut2", "shut3", "shut4" };

        // dark panes with wooden frames and coloured shutters on the long walls
        static void WindowsOn(Kit k, float w, float h, float d, float y, string shut, int count = 1)
        {
            foreach (var sz in new[] { -1f, 1f })
                for (int i = 0; i < count; i++)
                {
                    float x = count == 1 ? 0 : ((float)i / (count - 1) - .5f) * w * .55f;
                    k.Add("glass", B(.1f, .11f, .012f, x, y, sz * (d / 2 + .004f))).Add("plank", B(.12f, .012f, .02f, x, y - .008f, sz * (d / 2 + .006f)))
                     .Add(shut, B(.04f, .12f, .01f, x - .075f, y - .005f, sz * (d / 2 + .008f))).Add(shut, B(.04f, .12f, .01f, x + .075f, y - .005f, sz * (d / 2 + .008f)));
                }
        }

        static readonly Dictionary<string, Dictionary<string, Geo>> cache = new Dictionary<string, Dictionary<string, Geo>>();

        /// <summary>A building's parts per material, in tile space around its plot centre, scaled as the web game draws it.</summary>
        public static Dictionary<string, Geo> Building(BType type, bool capital, int seed = 0, int pw = 3, int pd = 2)
        {
            string key = $"{type}|{capital}|{seed % WALLS.Length}|{(seed >> 2) % SHUTTERS.Length}|{pw}|{pd}";
            if (!cache.TryGetValue(key, out var m)) cache[key] = m = MakeModel(type, capital, seed, pw, pd);
            return m;
        }

        static Dictionary<string, Geo> MakeModel(BType type, bool capital, int seed, int pw, int pd)
        {
            var k = new Kit(); string wall = WALLS[seed % WALLS.Length], shut = SHUTTERS[(seed >> 2) % SHUTTERS.Length];
            switch (type)
            {
                case BType.Harbor:
                    { // warehouse on a timber deck, with a pier running out toward the berth (+z)
                        k.Add("plank", B(.98f, .06f, .98f, 0, 0, 0, 2)).Add("plank", B(.24f, .05f, .7f, .22f, 0, .78f, 2));
                        foreach (var (x, z) in new[] { (-.45f, .45f), (.45f, -.45f), (.45f, .45f), (-.45f, -.45f), (.12f, .7f), (.32f, .7f), (.12f, 1.1f), (.32f, 1.1f) }) k.Add("wood", C(.025f, .03f, .5f, x, -.44f, z, 6));
                        k.Add(wall, B(.5f, .36f, .42f, -.16f, .06f, -.18f, 2.5f)).Add("roof", Gable(.5f, .42f, .2f).Translate(-.16f, .42f, -.18f));
                        k.Add("wood", B(.16f, .22f, .012f, -.16f, .06f, .031f)); WindowsOn(k, .5f, .36f, .42f, .26f, shut);
                        foreach (var (x, z) in new[] { (.25f, -.05f), (.33f, .07f), (.27f, .18f) }) k.Add("plank", B(.1f, .1f, .1f, x, .06f, z, 4));
                        k.Add("wood", C(.04f, .04f, .12f, .08f, .06f, .25f, 10)).Add("wood", C(.04f, .04f, .12f, .16f, .06f, .31f, 10));
                        k.Add("dark", C(.012f, .015f, .85f, -.4f, .06f, .38f, 6)).Add(capital ? "capFlag" : "flag", Plane(.24f, .14f, 4, 1).Translate(-.4f + .12f, .82f, .38f));
                        foreach (var z in new[] { .6f, .9f, 1.15f }) k.Add("dark", C(.018f, .02f, .06f, .36f, .06f, z, 6));
                        break;
                    }
                case BType.House:
                    { // goathi: a walled plot (faaru, 3×2 or 4×3 tiles), gate on +z, thatched coral-stone house at the back
                        float hx = pw / 2f - .05f, hz = pd / 2f - .05f, t = .06f, wh = .16f, gx = -.35f, gw = .36f; bool big = pw * pd > 6;
                        void WallSeg(float x0, float x1, float z0, float z1)
                        {
                            float w = Mathf.Abs(x1 - x0); if (w == 0) w = t; float d = Mathf.Abs(z1 - z0); if (d == 0) d = t; float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
                            k.Add("coral", B(w, wh, d, cx, 0, cz, 3)).Add("lime", B(w + .02f, .03f, d + .02f, cx, wh, cz, 3));
                        }
                        WallSeg(-hx, hx, -hz, -hz); WallSeg(-hx, -hx, -hz, hz); WallSeg(hx, hx, -hz, hz);
                        WallSeg(-hx, gx - gw / 2, hz, hz); WallSeg(gx + gw / 2, hx, hz, hz);
                        foreach (var x in new[] { gx - gw / 2 - .02f, gx + gw / 2 + .02f }) k.Add("lime", B(.09f, .25f, .09f, x, 0, hz, 3)).Add("lime", B(.11f, .025f, .11f, x, .25f, hz, 3));
                        // the house: lime-washed coral walls on a plinth, wooden door and lattice windows, deep hipped cadjan thatch roof
                        void House(float cx, float cz, float hw, float hd)
                        {
                            k.Add("coral", B(hw + .08f, .06f, hd + .08f, cx, 0, cz, 3)).Add("lime", B(hw, .4f, hd, cx, .06f, cz, 3));
                            k.Add("thatch", Hip(hw, hd, .36f, .13f).Translate(cx, .46f, cz));
                            k.Add("wood", B(.16f, .25f, .014f, cx, .06f, cz + hd / 2 + .004f)).Add("dark", B(.012f, .012f, .012f, cx + .05f, .17f, cz + hd / 2 + .012f));
                            foreach (var x in hw > 1 ? new[] { cx - .42f, cx + .42f } : new[] { cx - .26f })
                            {
                                k.Add("dark", B(.13f, .11f, .01f, x, .2f, cz + hd / 2 + .003f));
                                for (int i = 0; i < 3; i++) k.Add("wood", B(.012f, .11f, .014f, x + (i - 1) * .04f, .2f, cz + hd / 2 + .006f));
                                k.Add("wood", B(.15f, .014f, .016f, x, .2f, cz + hd / 2 + .006f)).Add("wood", B(.15f, .014f, .016f, x, .31f, cz + hd / 2 + .006f));
                            }
                        }
                        float H0 = -hz + .53f; House(-.2f - (big ? .4f : 0), H0, 1.3f, .72f);
                        if (big)
                        {
                            House(hx - .5f, .05f, .72f, .62f);                                 // a second house for a married son's family
                            // a little vegetable patch and drying mat in the open yard
                            k.Add("soil", B(.7f, .03f, .42f, -hx + .62f, 0, hz - .95f, 1.6f));
                            for (int i = 0; i < 6; i++) k.Add("cane", Plane(.12f, .14f).Translate(-hx + .36f + (i % 3) * .26f, .1f, hz - 1.05f + (i / 3) * .2f));
                            k.Add("rope", B(.5f, .012f, .36f, .25f, 0, hz - .45f, 6));
                        }
                        // kitchen hut (badige) in a back corner
                        k.Add("coral", B(.4f, .24f, .34f, hx - .45f, 0, -hz + .37f, 3)).Add("thatch", Gable(.4f, .34f, .15f, .06f, 2.5f).Translate(hx - .45f, .24f, -hz + .37f)).Add("dark", B(.1f, .16f, .01f, hx - .45f, 0, -hz + .54f + .002f));
                        // well (valu) with a little bucket frame
                        float wx = -hx + .4f, wz = hz - .47f;
                        k.Add("coral", C(.11f, .12f, .12f, wx, 0, wz, 14)).Add("dark", C(.085f, .085f, .005f, wx, .12f, wz, 14));
                        k.Add("wood", C(.008f, .008f, .24f, wx - .12f, 0, wz, 5)).Add("wood", C(.008f, .008f, .24f, wx + .12f, 0, wz, 5)).Add("wood", B(.28f, .016f, .016f, wx, .23f, wz)).Add("rope", C(.004f, .004f, .08f, wx, .15f, wz, 4));
                        // two joali, rope-net seats on wooden frames, in front of the house
                        foreach (var x in big ? new[] { -.5f, -.25f } : new[] { .25f, .5f })
                        {
                            float z = hz - .89f;
                            foreach (var (dx, dz) in new[] { (-.06f, -.05f), (.06f, -.05f), (-.06f, .05f), (.06f, .05f) }) k.Add("wood", C(.006f, .006f, .14f, x + dx, 0, z + dz, 4));
                            k.Add("rope", B(.12f, .012f, .12f, x, .07f, z).RotateX(-.25f)).Add("wood", B(.14f, .012f, .012f, x, .14f, z - .05f));
                        }
                        break;
                    }
                case BType.Fishery:
                    {
                        k.Add("plank", B(.62f, .05f, .5f, 0, 0, 0, 2)).Add("wood", B(.42f, .28f, .34f, -.12f, .05f, -.05f, 2)).Add("thatch", Gable(.42f, .34f, .2f, .08f, 2.5f).Translate(-.12f, .33f, -.05f));
                        foreach (var x in new[] { .14f, .32f }) k.Add("wood", C(.012f, .014f, .36f, x, .05f, .12f, 5)).Add("wood", C(.012f, .014f, .36f, x, .05f, -.14f, 5));
                        k.Add("wood", C(.008f, .008f, .28f, 0, 0, 0, 4).RotateX(PI / 2).Translate(.14f, .38f, -.01f)).Add("wood", C(.008f, .008f, .28f, 0, 0, 0, 4).RotateX(PI / 2).Translate(.32f, .38f, -.01f));
                        for (int i = 0; i < 6; i++) k.Add("fish", B(.02f, .07f, .008f, .14f + (i % 2) * .18f, .3f, -.11f + (i / 2) * .1f));
                        k.Add("rope", B(.22f, .015f, .18f, .2f, .05f, .2f, 6));
                        break;
                    }
                case BType.Lumber:
                    {
                        k.Add("wood", B(.42f, .26f, .36f, -.15f, 0, -.12f, 2)).Add("thatch", Gable(.42f, .36f, .16f, .07f, 2.5f).Translate(-.15f, .26f, -.12f));
                        for (int i = 0; i < 5; i++) { int row = i < 3 ? 0 : 1; k.Add("bark", C(.055f, .055f, .5f, 0, 0, 0, 8).RotateZ(PI / 2).Translate(.12f, .055f + row * .1f, .12f + (i % 3) * .11f - row * .05f + (row != 0 ? .05f : 0))); }
                        k.Add("plank", B(.3f, .02f, .06f, .15f, .16f, -.25f)).Add("wood", B(.02f, .16f, .02f, .05f, 0, -.25f)).Add("wood", B(.02f, .16f, .02f, .25f, 0, -.25f));
                        break;
                    }
                case BType.Quarry:
                    {
                        k.Add("stoneD", B(.86f, .06f, .86f, 0, 0, 0, 2));
                        foreach (var (x, z, w, h, d) in new[] { (-.2f, -.18f, .3f, .24f, .26f), (.22f, .16f, .22f, .16f, .26f), (.24f, -.22f, .18f, .13f, .16f), (-.18f, .22f, .2f, .1f, .14f), (0f, 0f, .12f, .08f, .12f) }) k.Add("stone", B(w, h, d, x, .06f, z, 2.5f));
                        k.Add("wood", C(.015f, .015f, .6f, -.36f, .06f, .34f, 5)).Add("wood", C(.012f, .012f, .38f, 0, 0, 0, 5).RotateZ(1.25f).Translate(-.2f, .6f, .34f)).Add("rope", C(.004f, .004f, .3f, -.04f, .34f, .34f, 3));
                        break;
                    }
                case BType.Grove:
                    { // a few young coconut palms on swept sand, with a heap of husked nuts
                        k.Add("soil", B(.96f, .03f, .96f, 0, 0, 0, 1.6f));
                        foreach (var (x, z, r, sc) in new[] { (-.26f, -.24f, 0f, .44f), (.24f, -.2f, 2.1f, .4f), (-.2f, .24f, 4.2f, .42f), (.27f, .27f, 1.2f, .46f) })
                        {
                            k.Add("palmBark", PalmTrunk.Clone().Scale(sc).RotateY(r).Translate(x, .03f, z)).Add("frond", Fronds.Clone().Scale(sc).RotateY(r).Translate(x, .03f, z)).Add("nut", Coconuts.Clone().Scale(sc).RotateY(r).Translate(x, .03f, z));
                        }
                        for (int i = 0; i < 7; i++) k.Add("nut", Sph(.035f, 8, 6).Translate(.02f + (i % 3) * .06f - (i > 4 ? .03f : 0), .06f + (i > 4 ? .05f : 0), .0f + (i / 3) * .05f));
                        break;
                    }
                case BType.Toddy:
                    { // the tapper's hut beside a notched palm, clay pots (raa) hung under its crown
                        k.Add("wood", B(.36f, .24f, .3f, -.2f, 0, -.18f, 2)).Add("thatch", Gable(.36f, .3f, .15f, .07f, 2.5f).Translate(-.2f, .24f, -.18f));
                        const float sc = .5f; k.Add("palmBark", PalmTrunk.Clone().Scale(sc).Translate(.2f, 0, .12f)).Add("frond", Fronds.Clone().Scale(sc).Translate(.2f, 0, .12f));
                        for (int i = 0; i < 5; i++) k.Add("dark", B(.05f, .01f, .02f, .2f + i * .004f, .08f + i * .11f, .12f + .03f));
                        foreach (var (x, y, z) in new[] { (.23f, .6f, .16f), (.18f, .58f, .08f), (.16f, .05f, -.04f), (.04f, .05f, .05f), (.1f, .05f, -.02f) }) k.Add("clay", Sph(.035f, 10, 8).Scale(1, 1.2f, 1).Translate(x, y + .04f, z));
                        break;
                    }
                case BType.Drying:
                    { // a cooking shed with a boiling pot, then racks of fish smoked and drying in the sun
                        k.Add("plank", B(.9f, .04f, .7f, 0, 0, 0, 2)).Add("wood", B(.3f, .24f, .28f, -.28f, .04f, -.16f, 2)).Add("thatch", Gable(.3f, .28f, .14f, .07f, 2.5f).Translate(-.28f, .28f, -.16f));
                        k.Add("dark", C(.07f, .06f, .08f, -.28f, .04f, .12f, 12)).Add("stoneD", C(.09f, .1f, .03f, -.28f, .04f, .12f, 10));
                        foreach (var z in new[] { -.2f, .02f, .24f })
                        {
                            foreach (var x in new[] { .02f, .38f }) k.Add("wood", C(.01f, .012f, .26f, x, .04f, z, 5));
                            k.Add("wood", C(.008f, .008f, .38f, 0, 0, 0, 4).RotateZ(PI / 2).Translate(.2f, .28f, z));
                            for (int i = 0; i < 5; i++) k.Add("fish", B(.018f, .09f, .006f, .07f + i * .07f, .17f, z));
                        }
                        k.Add("rope", B(.28f, .012f, .2f, .2f, .04f, .36f, 6));
                        break;
                    }
                case BType.Coir:
                    { // husks soak in a pit, then fibre is twisted into rope along a walk of posts
                        k.Add("plank", B(.34f, .04f, .3f, -.3f, 0, -.28f, 2)).Add("wood", B(.3f, .22f, .26f, -.3f, .04f, -.28f, 2)).Add("thatch", Gable(.3f, .26f, .14f, .07f, 2.5f).Translate(-.3f, .26f, -.28f));
                        for (int i = 0; i < 9; i++) k.Add("husk", Sph(.045f, 7, 5).Scale(1.2f, .8f, 1).Translate(.12f + (i % 3) * .08f, .035f + (i / 6) * .05f, -.32f + ((i / 3) % 2) * .07f));
                        foreach (var x in new[] { -.42f, .42f }) foreach (var z in new[] { .08f, .22f, .36f }) k.Add("wood", C(.012f, .014f, .2f, x, 0, z, 5));
                        foreach (var z in new[] { .08f, .22f, .36f }) k.Add("rope", C(.006f, .006f, .84f, 0, 0, 0, 4).RotateZ(PI / 2).Translate(0, .16f, z));
                        k.Add("rope", C(.06f, .06f, .06f, .3f, 0, -.05f, 10)).Add("rope", C(.05f, .05f, .05f, .3f, .06f, -.05f, 10));
                        break;
                    }
                case BType.Cowrie:
                    { // a palm-leaf shade on the beach, woven baskets of shells
                        foreach (var (x, z) in new[] { (-.3f, -.25f), (.1f, -.25f), (-.3f, .05f), (.1f, .05f) }) k.Add("wood", C(.012f, .014f, .26f, x, 0, z, 5));
                        k.Add("thatch", Gable(.48f, .4f, .1f, .04f, 2.5f).Translate(-.1f, .26f, -.1f));
                        foreach (var (x, z) in new[] { (.28f, .2f), (.36f, .06f), (-.1f, .25f) })
                        {
                            k.Add("rope", C(.06f, .045f, .07f, x, 0, z, 12));
                            for (int i = 0; i < 4; i++) k.Add("shell", Sph(.014f, 6, 5).Translate(x + (i % 2 - .5f) * .04f, .075f, z + (i / 2) * .03f - .015f));
                        }
                        for (int i = 0; i < 10; i++) k.Add("shell", Sph(.012f, 6, 5).Scale(1, .6f, 1.4f).Translate(-.4f + World.Hash(i, 1, 5) * .8f, .01f, .2f + World.Hash(i, 2, 5) * .2f));
                        break;
                    }
                case BType.Mosque:
                    { // 2×2: a small lime-washed coral mosque with a carved wooden hipped roof, in a walled yard with a well
                        const float Y = 0; k.Add("coral", B(1.9f, .05f, 1.9f, 0, Y, 0, 3));
                        void Wl(float x0, float x1, float z0, float z1)
                        {
                            float ww = Mathf.Abs(x1 - x0); if (ww == 0) ww = .06f; float dd = Mathf.Abs(z1 - z0); if (dd == 0) dd = .06f;
                            k.Add("coral", B(ww, .14f, dd, (x0 + x1) / 2, .05f, (z0 + z1) / 2, 3)).Add("lime", B(ww + .02f, .025f, dd + .02f, (x0 + x1) / 2, .19f, (z0 + z1) / 2, 3));
                        }
                        Wl(-.92f, .92f, -.92f, -.92f); Wl(-.92f, -.92f, -.92f, .92f); Wl(.92f, .92f, -.92f, .92f); Wl(-.92f, -.2f, .92f, .92f); Wl(.2f, .92f, .92f, .92f);
                        k.Add("coral", B(1.1f, .1f, .8f, 0, .05f, -.25f, 3)).Add("lime", B(1, .5f, .7f, 0, .15f, -.25f, 3));
                        k.Add("wood", Hip(1, .7f, .32f, .12f).Translate(0, .65f, -.25f)).Add("wood", B(.06f, .06f, .06f, 0, .97f, -.25f)).Add("copper", C(0, .03f, .1f, 0, 1.03f, -.25f, 8));
                        foreach (var x in new[] { -.32f, 0f, .32f }) k.Add("wood", B(.14f, .26f, .012f, x, .17f, .105f));
                        k.Add("lime", B(1.1f, .04f, .3f, 0, .15f, .25f, 3)); foreach (var x in new[] { -.5f, -.17f, .17f, .5f }) k.Add("wood", C(.022f, .025f, .42f, x, .19f, .38f, 8));
                        k.Add("wood", B(1.16f, .03f, .36f, 0, .61f, .25f, 3));
                        k.Add("coral", C(.1f, .11f, .11f, .6f, .05f, .55f, 14)).Add("dark", C(.08f, .08f, .005f, .6f, .16f, .55f, 14));
                        foreach (var (x, z) in new[] { (-.6f, .5f), (-.7f, .3f), (-.5f, .3f) }) k.Add("lime", B(.05f, .12f, .02f, x, .05f, z));
                        break;
                    }
                case BType.Palace:
                    { // 3×3: the Sultan's palace, a walled compound with a two-storey hall, tiled roof and a red flag over the gate
                        const float hx = 1.42f;
                        void Ww(float x0, float x1, float z0, float z1)
                        {
                            float a = Mathf.Abs(x1 - x0); if (a == 0) a = .08f; float d = Mathf.Abs(z1 - z0); if (d == 0) d = .08f;
                            k.Add("coral", B(a, .3f, d, (x0 + x1) / 2, 0, (z0 + z1) / 2, 3)).Add("lime", B(a + .03f, .04f, d + .03f, (x0 + x1) / 2, .3f, (z0 + z1) / 2, 3));
                        }
                        Ww(-hx, hx, -hx, -hx); Ww(-hx, -hx, -hx, hx); Ww(hx, hx, -hx, hx); Ww(-hx, -.3f, hx, hx); Ww(.3f, hx, hx, hx);
                        foreach (var x in new[] { -.34f, .34f }) k.Add("lime", B(.16f, .6f, .16f, x, 0, hx, 3)).Add("coral", B(.2f, .05f, .2f, x, .6f, hx, 3));
                        k.Add("lime", B(.84f, .12f, .14f, 0, .5f, hx, 3));
                        k.Add("coral", B(2.0f, .1f, 1.2f, 0, 0, -.5f, 3)).Add(wall, B(1.8f, .55f, 1.0f, 0, .1f, -.5f, 3)).Add("wood", B(1.84f, .04f, 1.04f, 0, .65f, -.5f, 3)).Add(wall, B(1.4f, .45f, .8f, 0, .69f, -.5f, 3));
                        k.Add("roof", Hip(1.4f, .8f, .45f, .16f).Translate(0, 1.14f, -.5f));
                        WindowsOn(k, 1.8f, .55f, 1.0f, .36f, shut, 4); WindowsOn(k, 1.4f, .45f, .8f, .9f, shut, 3);
                        k.Add("wood", B(.3f, .36f, .014f, 0, .1f, 0 + .004f));
                        foreach (var x in new[] { -.75f, -.45f, .45f, .75f }) k.Add("lime", C(.04f, .045f, .55f, x, .1f, .12f, 10));
                        k.Add("roof", B(1.8f, .04f, .34f, 0, .65f, .08f, 3));
                        k.Add("dark", C(.015f, .018f, 1.3f, -1.2f, 0, 1.2f, 6)).Add("capFlag", Plane(.36f, .22f, 4, 1).Translate(-1.2f + .18f, 1.18f, 1.2f));
                        foreach (var (x, z) in new[] { (-1.0f, .5f), (1.0f, .6f), (.95f, -1.15f) }) k.Add("palmBark", PalmTrunk.Clone().Scale(.55f).Translate(x, 0, z)).Add("frond", Fronds.Clone().Scale(.55f).Translate(x, 0, z));
                        break;
                    }
            }
            return k.Build(type == BType.House || Defs.Def(type).Unit ? 1 : 1.25f);   // plots are drawn at tile scale
        }

        // ------------------------------------------------------------ boats
        // a planked hull that narrows to a bow, mast and a gently curved sail
        public static readonly Geo Hull = Make(() =>
        {
            var g = Box(.8f, .16f, .3f, 8, 1, 2);
            for (int i = 0; i < g.P.Count; i++)
            {
                var p = g.P[i]; float t = (p.x + .4f) / .8f, taper = t > .6f ? 1 - (t - .6f) / .4f * .92f : 1 - (.6f - t) * .25f;
                g.P[i] = new Vector3(p.x, p.y + (t > .75f ? (t - .75f) * .25f : 0), p.z * taper * (p.y < 0 ? .7f : 1));
            }
            return g.ComputeNormals().NI().WorldUV(3);
        });
        public static readonly Geo Sail = Make(() =>
        {
            var g = Plane(.38f, .6f, 6, 8);
            for (int i = 0; i < g.P.Count; i++)
            {
                var p = g.P[i]; float x = p.x + .19f, y = p.y + .3f, w = .38f * (1 - y / .6f * .95f);
                if (x > w) p.x = w - .19f; p.z = Mathf.Sin(Mathf.Min(x, w) / .38f * PI) * .05f; g.P[i] = p;
            }
            return g.Translate(.19f, .55f, 0).ComputeNormals();
        });

        /// <summary>A dhoni: hull, deck and mast per material; the sail is drawn on its own so its colour can change.</summary>
        public static Dictionary<string, Geo> Boat()
        {
            var k = new Kit(); k.Add("wood", Hull.Clone()).Add("plank", B(.5f, .02f, .2f, -.04f, .08f, 0, 3)).Add("dark", C(.012f, .015f, .8f, .02f, .08f, 0, 6));
            var o = k.Build(1.3f); o["sail"] = Sail.Clone().Translate(.03f, 0, 0).Scale(1.3f); return o;
        }

        /// <summary>A trading ship (odi): a long, deep hull, a deckhouse and two masts with ochre sails.</summary>
        public static Dictionary<string, Geo> Ship()
        {
            var k = new Kit();
            k.Add("wood", Hull.Clone().Scale(2.3f, 1.7f, 1.7f)).Add("plank", B(1.2f, .03f, .34f, -.08f, .13f, 0, 3)).Add("wood", B(.3f, .16f, .26f, -.42f, .15f, 0, 2)).Add("thatch", Gable(.3f, .26f, .1f, .04f, 2.5f).Translate(-.42f, .31f, 0));
            foreach (var x in new[] { .12f, .48f }) k.Add("dark", C(.016f, .02f, 1.15f, x, .13f, 0, 6));
            for (int i = 0; i < 4; i++) k.Add("plank", B(.12f, .1f, .12f, .25f + (i % 2) * .14f, .15f, (i / 2) * .14f - .07f, 4));
            foreach (var (x, s) in new[] { (.12f, 1.35f), (.48f, 1.1f) }) k.Add("sailOchre", Sail.Clone().Scale(s).Translate(x, .08f, 0));
            return k.Build(1.3f);
        }

        /// <summary>Everything in a model merged into one geometry, for the see-through placement ghost.</summary>
        public static Geo All(Dictionary<string, Geo> parts) => Merge(parts.Values);
    }
}
