using System;
using System.Collections.Generic;
using System.Linq;
using static IslandSim.Core.Defs;
using static IslandSim.Core.Noise;

namespace IslandSim.Core
{
    public sealed partial class Sim
    {
        // ------------------------------------------------------------ atoll geometry
        public struct Local { public double A, D, Sc; }

        // An atoll's own frame: undo its rotation, stretch and bend; A = bearing (deg), D = distance in ring units,
        // Sc = tiles per ring unit along that bearing (so reef widths stay in tiles on stretched atolls).
        public static Local AtollLocal(AtollSpec A, double x, double y)
        {
            double t = Rad(-A.Rot), dx = x - A.X, dy = y - A.Y;
            double lx = (dx * Math.Cos(t) - dy * Math.Sin(t)) / A.Sx, ly = (dx * Math.Sin(t) + dy * Math.Cos(t)) / A.Sy;
            if (A.Bend != 0) ly -= A.Bend * lx * lx / A.R;
            double a = Math.Atan2(ly, lx) * 180 / Math.PI; if (a < 0) a += 360;
            return new Local { A = a, D = Hypot(lx, ly), Sc = Hypot(Math.Cos(Rad(a)) * A.Sx, Math.Sin(Rad(a)) * A.Sy) };
        }
        public static (double x, double y) AtollWorld(AtollSpec A, double a, double d)
        {
            double lx0 = Math.Cos(Rad(a)) * d, ly = Math.Sin(Rad(a)) * d; if (A.Bend != 0) ly += A.Bend * lx0 * lx0 / A.R;
            double lx = lx0 * A.Sx; ly *= A.Sy; double t = Rad(A.Rot);
            return (A.X + lx * Math.Cos(t) - ly * Math.Sin(t), A.Y + lx * Math.Sin(t) + ly * Math.Cos(t));
        }
        static double ScAt(AtollSpec A, double a) => Hypot(Math.Cos(Rad(a)) * A.Sx, Math.Sin(Rad(a)) * A.Sy);
        // rim radius by bearing: each atoll's own lopsided, wobbly outline
        public static double RimR(AtollSpec A, double a)
        {
            double t = Rad(a), r = 1;
            foreach (var s in A.Shape) r += s[1] * Math.Sin(s[0] * t + s[2]);
            foreach (var dn in A.Dents) { double q = AngDiff(a, dn[0]) / dn[2]; r -= dn[1] * Math.Exp(-(q * q)); }
            return A.R * r + (Fbm(Math.Cos(t) * 1.6 + 5 + A.Seed * 7, Math.Sin(t) * 1.6 + 5, 4) - .5) * A.R * .12;
        }
        static (double x, double y) IslCentre(IslandSpec I)
        {
            var A = Atolls[I.At]; if (A.Lone) return (A.X, A.Y);
            return AtollWorld(A, I.Ang, RimR(A, I.Ang) - I.Off / ScAt(A, I.Ang));
        }
        public static readonly (double x, double y)[] IslC = Defs.Islands.Select(IslCentre).ToArray();
        // which atoll a point belongs to (the one whose rim it is furthest inside)
        public static int AtollAt(double x, double y)
        {
            int best = 0; double br = 1e9;
            for (int k = 0; k < Atolls.Length; k++)
            {
                var A = Atolls[k]; var l = AtollLocal(A, x, y); double r = (l.D - (A.Lone ? A.R : RimR(A, l.A))) * l.Sc;
                if (r < br) { br = r; best = k; }
            }
            return best;
        }
        static readonly int LoneIsl = Array.FindIndex(Defs.Islands, I => I.Kind == "lone");
        // a generous reach per atoll: past it a tile is open ocean for sure, so Generate() and SeaAt() skip that atoll
        static readonly double[] AtollReach = Atolls.Select(A => A.R * (A.Lone ? 1 : 1.3 + A.Bend) * Math.Max(A.Sx, A.Sy) + 40 * SC).ToArray();
        static bool NearAtoll(int k, double x, double y) { var A = Atolls[k]; double dx = x - A.X, dy = y - A.Y, r = AtollReach[k]; return dx * dx + dy * dy < r * r; }
        static readonly int[][] IslByAtoll = Atolls.Select((A, ai) => Enumerable.Range(0, Defs.Islands.Length).Where(k => Defs.Islands[k].At == ai).ToArray()).ToArray();
        struct Faro { public double X, Y, R; }

        // ------------------------------------------------------------ map: four atolls and a lone island
        public void Generate()
        {
            var h = new sbyte[W * H]; var tree = new byte[W * H]; var gt = new byte[W * H]; var lagoon = new byte[W * H]; var rnd = new Mulberry32(20261010);
            for (int i = 0; i < Origin.Length; i++) Origin[i] = 255;
            // ring reefs in the lagoons; lagoon islets sit on one of their own
            var faros = Atolls.Select((A, ai) =>
            {
                var o = new List<Faro>(); if (A.Lone) return o; var r = new Mulberry32((uint)(A.Seed * 101));
                for (int k = 0; k < A.Faros; k++) { double a = r.Next() * 360, fr = (3 + r.Next() * 3.5) * SC; var c = AtollWorld(A, a, RimR(A, a) * (.2 + r.Next() * .55)); o.Add(new Faro { X = c.x, Y = c.y, R = fr }); }
                foreach (var k in IslByAtoll[ai]) { var I = Defs.Islands[k]; if (I.Off > 15 * SC) { var c = IslCentre(I); o.Add(new Faro { X = c.x, Y = c.y, R = I.Rw + 3.5 * SC }); } }
                return o;
            }).ToArray();
            var lakes = new[] { 0.0, 180.0 }.Select(a => AtollWorld(Atolls[3], a, Atolls[3].R * .45)).ToArray();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    // domain-warp the coordinates so edges meander instead of tracing arcs
                    double wx = x + (Fbm(x / SC * .035, y / SC * .035, 40) - .5) * 12 * SC, wy = y + (Fbm(x / SC * .035, y / SC * .035, 41) - .5) * 12 * SC;
                    int ai = -1; Local L = default; double r = 1e9;
                    for (int k = 0; k < Atolls.Length; k++)
                    {
                        if (!NearAtoll(k, wx, wy)) continue; var A0 = Atolls[k]; var l = AtollLocal(A0, wx, wy); double rr0 = (l.D - (A0.Lone ? A0.R : RimR(A0, l.A))) * l.Sc;
                        if (rr0 < r) { r = rr0; ai = k; L = l; }
                    }
                    int i = Idx(x, y); if (ai < 0) { h[i] = MinH; continue; }   // open ocean, far from every atoll
                    var A = Atolls[ai]; double a = L.A;
                    double v = -3, best = 1e9; int owner = 255, g = 0;
                    if (A.Lone)
                    {
                        // one island, its own narrow reef, then straight down into the ocean
                        double rr = r + (Fbm(x / SC * .12, y / SC * .12, 90) - .5) * 3 * SC;
                        if (rr < 0) { double t = Math.Min(1, -rr / (10 * SC)); v = Math.Min(1.3, -1 + 3.4 * Math.Sqrt(t) + (Fbm(x / SC * .3, y / SC * .3, 91) - .5) * .4); owner = LoneIsl; best = 0; }
                        else if (rr < 3 * SC) v = -1; else if (rr < 4.5 * SC) v = -2;
                        if (owner != 255) foreach (var c in lakes)
                            {
                                double dd = Hypot(x - c.x, y - c.y) + (Fbm(x / SC * .3, y / SC * .3, 92) - .5) * 1.2 * SC;
                                if (dd < 2.4 * SC) { v = -1; g = GT.Pond; } else if (dd < 4.2 * SC && v >= 0) { v = 0; g = GT.Kulhi; }
                            }
                    }
                    else
                    {
                        double inner = (-2.5 - Fbm(a * .05, ai, 42) * 4) * SC, outer = (6 + Fbm(a * .05, ai + 2, 43) * 6) * SC; // reef flat width varies around the ring
                        if (r > outer + 2.5 * SC) v = -3;
                        else if (r > outer) v = -2;
                        else if (r > inner) v = -1;                                           // reef flat on the rim
                        else { v = -2 - (Fbm(x / SC * .08, y / SC * .08, 6) > .62 ? 1 : 0); lagoon[i] = 1; }  // deep lagoon
                        if (v <= -2 && r < inner - 5 * SC && Fbm(x / SC * .21, y / SC * .21, 8) > .74) v = -1;   // scattered coral heads (giri)
                        if (r < inner - 3 * SC) foreach (var f in faros[ai])
                            {
                                double dd = Hypot(x - f.X, y - f.Y) + (Fbm(x / SC * .25, y / SC * .25, 9) - .5) * 1.6 * SC;
                                if (dd < f.R) { v = dd > f.R - 1.8 * SC ? -1 : Math.Max(v, f.R < 4.5 * SC ? -1 : -2); break; }
                            }
                        if (v == -1 && Math.Abs(r) < 2.4 * SC && VNoise(a * .11, 3 + ai * 5, 50) > .74) v = 0; // sandy motus along the rim
                        foreach (var k in IslByAtoll[ai])
                        {
                            var I = Defs.Islands[k];
                            double da = AngDiff(a, I.Ang); if (Math.Abs(da) > I.Ext * 1.4 + 6) continue;
                            double rw = I.Rw * (.8 + .45 * Fbm(a * .06, k * 3, 44));
                            double u = (r + I.Off) / rw; if (Math.Abs(u) > 1.6) continue;
                            double e = Hypot(u, da / I.Ext) + (Fbm(x / SC * .16, y / SC * .16, 11 + k) - .5) * .5;
                            if (I.Flats && r < 1 && r > -20 * SC && Math.Abs(da) < I.Ext + 10 && v < -1) v = -1;   // Kaashi Cay sits behind broad lagoon flats: too shallow for boats
                            if (e >= 1.15) continue;
                            double t = Math.Max(0, 1 - e), nz = (Fbm(x / SC * .3, y / SC * .3, 21 + k) - .5) * .4;
                            // a white beach ring, then flat ground at height 1; only a few ridges reach height 2
                            double hv0 = Math.Min(1.3, -1 + 3.4 * Math.Sqrt(t) + nz); int kg = 0;
                            if (I.Kind == "vilu")
                            {
                                hv0 = e < .5 ? -1.6 : Math.Min(1.3, -1 + 3.4 * Math.Sqrt(Math.Max(0, 1 - Math.Abs(e - .82) / .33)) + nz);
                                if (e < .5) { if (e < best) { best = e; v = e < .32 ? -2 : -1; owner = 255; } continue; }
                            }
                            if (I.Kind == "finolhu") { hv0 = Math.Min(hv0, .4); kg = GT.Finolhu; }
                            if (I.Kind == "bird") kg = GT.Bird;
                            if (I.Kind == "kulhi" && t > .4) { double n = Fbm(x / SC * .22, y / SC * .22, 70 + k); if (t > .6 && n > .47) { hv0 = -1; kg = GT.Pond; } else if (n > .4) { hv0 = 0; kg = GT.Kulhi; } }
                            if (I.Hills != 0 && t > .35 && Fbm(x / SC * .11, y / SC * .11, 60 + k) > I.Hills) hv0 = 2;
                            if (kg == GT.Pond) { if (v < 0 || owner == k) { v = -1; g = kg; owner = 255; best = e; } continue; }
                            if (hv0 > -0.5 && (hv0 > v || e < best)) { v = Math.Max(v, hv0); if (e < best) { best = e; owner = k; g = kg; } }  // islands rise straight out of the lagoon
                        }
                        foreach (var p in A.Passes) if (Math.Abs(AngDiff(a, p[0])) < p[1] && v <= 0 && r > inner - 3 * SC && r < outer + 3 * SC) v = -2; // channels to the ocean
                    }
                    int hv = (int)Math.Max(MinH, Math.Min(MaxH, Round(v)));
                    h[i] = (sbyte)hv; Origin[i] = hv >= 0 ? (byte)(owner == 255 ? 254 : owner) : (byte)255; // 254: reef sand that belongs to no named island
                    gt[i] = (byte)(hv >= 0 || g == GT.Pond ? g : 0);
                }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = Idx(x, y);
                    // taro in wet ground beside a kulhi; seagrass on the lagoon's shallow flats
                    if (h[i] == 1 && gt[i] == 0 && CountAround2(gt, x, y, 2, GT.Kulhi) && Hash2(x, y, 71) < .6) gt[i] = GT.Taro;
                    if (h[i] == -1 && gt[i] == 0 && (lagoon[i] != 0 || CountAround2(lagoon, x, y, 3, 1)) && Fbm(x / SC * .09, y / SC * .09, 77) > .55) gt[i] = GT.Seagrass;
                }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = Idx(x, y), hv = h[i], o = Origin[i]; double f = Fbm(x / SC * .22, y / SC * .22, 99); var I = o < Defs.Islands.Length ? Defs.Islands[o] : null; int g = gt[i];
                    if (g == GT.Kulhi) { if (rnd.Next() < .75) tree[i] = 3; continue; }       // mangroves
                    if (g == GT.Taro || g == GT.Pond) continue;
                    double dens = I != null ? KindDens[I.Kind] : .5;
                    if (hv >= 1 && hv <= 4 && f > dens) tree[i] = (byte)(hv <= 1 && rnd.Next() < (I != null && (I.Kind == "wooded" || I.Kind == "bird") ? .15 : .5) ? 2 : 1);
                    else if (hv == 0 && rnd.Next() < (g == GT.Finolhu ? .015 : .12)) tree[i] = 2;
                }
            Hgt = h; Tree = tree; Gt = gt;
        }

        static bool CountAround2(byte[] arr, int x, int y, int r, int val)
        {
            for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++) { int nx = x + dx, ny = y + dy; if (Inb(nx, ny) && arr[Idx(nx, ny)] == val) return true; }
            return false;
        }

        /// <summary>Open ocean: beyond an atoll's outer reef edge (same warp and reef width as Generate), where nothing can be built out.</summary>
        public static bool SeaAt(int x, int y)
        {
            double wx = x + (Fbm(x / SC * .035, y / SC * .035, 40) - .5) * 12 * SC, wy = y + (Fbm(x / SC * .035, y / SC * .035, 41) - .5) * 12 * SC;
            int ai = 0; double r = 1e9, a = 0;
            for (int k = 0; k < Atolls.Length; k++)
            {
                if (!NearAtoll(k, wx, wy)) continue; var A = Atolls[k]; var l = AtollLocal(A, wx, wy); double rr = (l.D - (A.Lone ? A.R : RimR(A, l.A))) * l.Sc;
                if (rr < r) { r = rr; ai = k; a = l.A; }
            }
            if (r == 1e9) return true;
            return Atolls[ai].Lone ? r > 3.5 * SC : r > (6 + Fbm(a * .05, ai + 2, 43) * 6) * SC;
        }

        // ------------------------------------------------------------ a new game
        public void NewGame()
        {
            Generate();
            Road = new byte[W * H]; Bld = new int[W * H]; for (int i = 0; i < Bld.Length; i++) Bld[i] = -1;
            Buildings = new List<Building>(); Boats.Clear(); Trader = null; tradeCache = (-1, null); undoStack.Clear(); alerted.Clear();
            Gold = 800; T = 0; NextId = 1; StatRope = 0; StatLinkTrip = 0; Goals.Clear(); Capital = 0; Unrest = 0; Names.Clear();
            Age = 1; AgeDone = false; Trade = new TradeState(); Over = null; Rate = 0;
            ComputeIslands();
            int bx = -1, by = -1; double bs = 1e9;
            var A0 = Atolls[0];
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = Idx(x, y); if (Origin[i] != 0 || Hgt[i] != 0 || BerthOf(x, y) == null) continue;
                    double s = Hypot(x - A0.X, y - A0.Y) + Math.Abs(AngDiff(AtollLocal(A0, x, y).A, Defs.Islands[0].Ang + 8)) * 0.4;
                    if (s < bs) { bs = s; bx = x; by = y; }
                }
            if (bx < 0)
            { // guarantee a berth: take Palm's lagoon-side beach and dredge beside it
                for (int y = 1; y < H - 1; y++)
                    for (int x = 1; x < W - 1; x++)
                    {
                        int i = Idx(x, y); if (Origin[i] != 0 || Hgt[i] != 0 || !AdjWater(x, y)) continue;
                        double s = Hypot(x - A0.X, y - A0.Y); if (s < bs) { bs = s; bx = x; by = y; }
                    }
                for (int k = 0; k < 4; k++) { int i = Idx(bx + DX[k], by + DY[k]); if (Hgt[i] < 0) Hgt[i] = -2; }
            }
            var hb = AddBuilding(BType.Harbor, bx, by);
            hb.Stock[(int)Good.Wood] = 30; hb.Stock[(int)Good.Stone] = 24; hb.Stock[(int)Good.Fish] = 40; hb.Stock[(int)Good.Coconut] = 20;
            Capital = hb.Id; ComputeIslands();
            foreach (var type in new[] { BType.House, BType.House, BType.House, BType.Fishery, BType.Lumber })
            {
                Check pick = default; bool found = false; double pd = 1e9;
                for (int y = by - 14; y <= by + 14; y++)
                    for (int x = bx - 14; x <= bx + 14; x++)
                    {
                        var r = CanPlace(type, x, y, true); if (!r.Ok) continue;
                        double d = Hypot(x - bx, y - by);
                        double score = d - (type == BType.Lumber ? CountAround(x, y, 2, i => Tree[i] > 0) * 0.6 : 0) - (type == BType.Fishery ? CountAround(x, y, 2, i => Hgt[i] == -1) * 2 : 0);
                        if (score < pd) { pd = score; pick = r; found = true; }
                    }
                if (found) { var b = AddBuilding(type, pick.X, pick.Y, pick.W, pick.D); if (type == BType.House) b.Res = 4; }
            }
            ComputeIslands(); MarkAll();
        }
    }
}
