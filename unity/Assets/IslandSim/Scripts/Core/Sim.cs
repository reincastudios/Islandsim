using System;
using System.Collections.Generic;
using System.Linq;
using static IslandSim.Core.Defs;

namespace IslandSim.Core
{
    public enum ToastKind { Info, Good, Bad }
    public enum BoatState { Idle, Sail, Blocked }

    public sealed class Building
    {
        public int Id; public BType Type; public int X, Y;
        public float Res, Eff, Rate; public string Status = "";
        public float[] Stock;            // harbors only
        public bool Merged; public int Isl = -1;
        public bool HasRate;
    }

    public sealed class Island
    {
        public int Id, N; public string Name; public List<int> Parts = new List<int>();
        public Building Harbor; public float Res; public int Jobs; public float Eff = 1; public float Cx, Cy;
    }

    public sealed class Boat
    {
        public int Home; public float X, Y, Ang; public List<int> Path; public int Pi;
        public bool Out = true; public float[] Cargo = new float[GoodCount];
        public BoatState State = BoatState.Idle; public string Why = ""; public int Ver = -1, Trips;
        public bool HasCargo { get { foreach (var c in Cargo) if (c > 0) return true; return false; } }
    }

    public struct TerraResult { public bool Ok, Noop, Coast; public string Why; }
    public struct PlaceCheck { public bool Ok; public string Why; }

    public sealed class Goal
    {
        public string Id, Title, Hint; public Func<Sim, bool> Ok;
    }

    /// <summary>
    /// The whole game rules: atoll generation, islands and warehouses, boats, economy, terraforming and charter goals.
    /// Pure C# with no UnityEngine dependency, so it can be tested outside the editor.
    /// </summary>
    public sealed class Sim
    {
        public sbyte[] Hgt; public byte[] Tree; public int[] Bld; public byte[] Origin;
        public List<Building> Buildings = new List<Building>();
        public List<Island> Islands = new List<Island>();
        public short[] Comp;
        public List<Boat> Boats = new List<Boat>();
        public float Gold, T, Rate; public int NextId = 1, Capital;
        public float StatRum; public int StatSugarTrip;
        public HashSet<string> GoalsDone = new HashSet<string>();
        public int TerrainVer;

        // Change tracking for the view.
        public bool DirtyTrees = true, DirtyBuildings = true;
        public int DirtyX0 = 0, DirtyY0 = 0, DirtyX1 = W - 1, DirtyY1 = H - 1; // inclusive; X0 > X1 means clean

        public event Action<string, ToastKind> Toast;
        readonly Random rnd = new Random(7);

        public static int Idx(int x, int y) => y * W + x;
        public static bool Inb(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        static readonly int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };

        void Say(string msg, ToastKind k = ToastKind.Info) => Toast?.Invoke(msg, k);

        public void MarkDirty(int x, int y)
        {
            if (DirtyX0 > DirtyX1) { DirtyX0 = DirtyX1 = x; DirtyY0 = DirtyY1 = y; return; }
            DirtyX0 = Math.Min(DirtyX0, x); DirtyX1 = Math.Max(DirtyX1, x);
            DirtyY0 = Math.Min(DirtyY0, y); DirtyY1 = Math.Max(DirtyY1, y);
        }
        public void ClearDirty() { DirtyX0 = 1; DirtyX1 = 0; }
        public void MarkAllDirty() { DirtyX0 = 0; DirtyY0 = 0; DirtyX1 = W - 1; DirtyY1 = H - 1; }
        public bool TerrainDirty => DirtyX0 <= DirtyX1;

        // ---------------------------------------------------------------- map
        public static double RimR(double a)
        {
            double t = Noise.Rad(a);
            return AcR + 5.5 * Math.Sin(2 * t + 0.7) + 3.2 * Math.Sin(3 * t + 2.1) + 1.6 * Math.Sin(5 * t + 4)
                 + (Noise.Fbm(Math.Cos(t) * 1.6 + 5, Math.Sin(t) * 1.6 + 5, 4) - .5) * 6;
        }

        public static void IslandCentre(IslandSpec I, out double x, out double y)
        {
            double R = RimR(I.Ang) - I.Off;
            x = AcX + Math.Cos(Noise.Rad(I.Ang)) * R; y = AcY + Math.Sin(Noise.Rad(I.Ang)) * R;
        }

        int NearestSpec(int x, int y)
        {
            double bd = 1e9; int o = 0;
            for (int k = 0; k < Defs.Islands.Length; k++)
            {
                IslandCentre(Defs.Islands[k], out var cx, out var cy);
                double d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d < bd) { bd = d; o = k; }
            }
            return o;
        }

        public void Generate()
        {
            Hgt = new sbyte[W * H]; Tree = new byte[W * H]; Origin = new byte[W * H];
            var r32 = new Mulberry32(20261010);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    // domain-warp the coordinates so edges meander instead of tracing arcs
                    double wx = x + (Noise.Fbm(x * .035, y * .035, 40) - .5) * 12, wy = y + (Noise.Fbm(x * .035, y * .035, 41) - .5) * 12;
                    double dx = wx - AcX, dy = wy - AcY, d = Math.Sqrt(dx * dx + dy * dy);
                    double a = Math.Atan2(dy, dx) * 180 / Math.PI; if (a < 0) a += 360;
                    double r = d - RimR(a);
                    double inner = -2.5 - Noise.Fbm(a * .05, 1, 42) * 4, outer = 6 + Noise.Fbm(a * .05, 2, 43) * 6; // reef flat width varies around the ring
                    double v;
                    if (r > outer + 2.5) v = -3;
                    else if (r > outer) v = -2;
                    else if (r > inner) v = -1;                                           // reef flat on the rim
                    else v = -2 - (Noise.Fbm(x * .08, y * .08, 6) > .62 ? 1 : 0);         // deep lagoon
                    if (v == -2 && r < inner - 5 && Noise.Fbm(x * .21, y * .21, 8) > .74) v = -1;   // scattered coral heads
                    if (v == -1 && Math.Abs(r) < 2.4 && Noise.VNoise(a * .11, 3, 50) > .74) v = 0; // sandy motus along the rim
                    for (int k = 0; k < Defs.Islands.Length; k++)
                    {
                        var I = Defs.Islands[k];
                        double rw = I.Rw * (.8 + .45 * Noise.Fbm(a * .06, k * 3, 44));
                        double u = (r + I.Off) / rw, w = Noise.AngDiff(a, I.Ang) / I.Ext;
                        double e = Math.Sqrt(u * u + w * w) + (Noise.Fbm(x * .16, y * .16, 11 + k) - .5) * .5;
                        if (e < 1.15)
                        {
                            double t = Math.Max(0, 1 - e);
                            double hv0 = -1 + (I.Peak + 1.6) * Math.Pow(t, .7) + (Noise.Fbm(x * .3, y * .3, 21 + k) - .5) * (I.Peak > 3 ? 1.3 : .5);
                            if (hv0 > -0.5 && hv0 > v) v = hv0;                           // islands rise straight out of the lagoon
                        }
                        // Sugar Cay sits behind broad lagoon flats: too shallow for boats
                        if (k == 2 && r < 1 && r > -20 && Math.Abs(Noise.AngDiff(a, I.Ang)) < I.Ext + 10 && v < -1) v = -1;
                    }
                    if (Math.Abs(Noise.AngDiff(a, PassAng)) < 2.4 && v <= 0 && r > inner - 3 && r < outer + 3) v = -2; // natural pass to the ocean
                    int hv = Math.Max(MinH, Math.Min(MaxH, (int)Math.Floor(v + 0.5))); // JS Math.round: halves round up
                    Hgt[Idx(x, y)] = (sbyte)hv;
                    Origin[Idx(x, y)] = hv >= 0 ? (byte)NearestSpec(x, y) : (byte)255;
                }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = Idx(x, y), hv = Hgt[i], o = Origin[i];
                    double f = Noise.Fbm(x * .22, y * .22, 99);
                    double dens = o == 0 ? .44 : o == 1 ? .5 : .66;
                    if (hv >= 1 && hv <= 4 && f > dens) Tree[i] = (byte)(hv <= 1 && r32.Next() < .5 ? 2 : 1);
                    else if (hv == 0 && r32.Next() < .12) Tree[i] = 2;
                }
        }

        public void NewGame()
        {
            Generate();
            Bld = new int[W * H]; for (int i = 0; i < Bld.Length; i++) Bld[i] = -1;
            Buildings.Clear(); Boats.Clear(); GoalsDone.Clear();
            Gold = 1200; T = 0; NextId = 1; StatRum = 0; StatSugarTrip = 0; Capital = 0; Rate = 0;
            ComputeIslands();
            int bx = -1, by = -1; double bs = 1e9;
            var palm = Defs.Islands[0];
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = Idx(x, y); if (Origin[i] != 0 || Hgt[i] != 0 || !BerthOf(x, y, out _, out _)) continue;
                    double s = Math.Sqrt((x - AcX) * (x - AcX) + (y - AcY) * (y - AcY)) + Math.Abs(Noise.AngDiff(Math.Atan2(y - AcY, x - AcX) * 180 / Math.PI, palm.Ang + 8)) * 0.4;
                    if (s < bs) { bs = s; bx = x; by = y; }
                }
            if (bx < 0)
            { // guarantee a berth: take Palm's lagoon-side beach and dredge beside it
                for (int y = 1; y < H - 1; y++)
                    for (int x = 1; x < W - 1; x++)
                    {
                        int i = Idx(x, y); if (Origin[i] != 0 || Hgt[i] != 0 || !AdjWater(x, y)) continue;
                        double s = Math.Sqrt((x - AcX) * (x - AcX) + (y - AcY) * (y - AcY)); if (s < bs) { bs = s; bx = x; by = y; }
                    }
                for (int k = 0; k < 4; k++) { int i = Idx(bx + DX[k], by + DY[k]); if (Hgt[i] < 0) Hgt[i] = -2; }
            }
            var hb = AddBuilding(BType.Harbor, bx, by);
            hb.Stock[(int)Good.Wood] = 30; hb.Stock[(int)Good.Stone] = 24; hb.Stock[(int)Good.Fish] = 20;
            Capital = hb.Id; ComputeIslands();
            foreach (var type in new[] { BType.House, BType.House, BType.House, BType.House, BType.Fishery, BType.Lumber })
            {
                int px = -1, py = -1; double pd = 1e9;
                for (int y = by - 12; y <= by + 12; y++)
                    for (int x = bx - 12; x <= bx + 12; x++)
                    {
                        if (!CanPlace(type, x, y, true).Ok) continue;
                        double d = Math.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by));
                        double score = d - (type == BType.Lumber ? CountAround(x, y, 2, i => Tree[i] > 0) * 0.6 : 0)
                                         - (type == BType.Fishery ? CountAround(x, y, 2, i => Hgt[i] == -1) * 0.4 : 0);
                        if (score < pd) { pd = score; px = x; py = y; }
                    }
                if (px >= 0) { var b = AddBuilding(type, px, py); if (type == BType.House) b.Res = 3; }
            }
            ComputeIslands();
            DirtyTrees = true; DirtyBuildings = true; MarkAllDirty(); TerrainVer++;
        }

        Building AddBuilding(BType type, int x, int y)
        {
            int i = Idx(x, y);
            var b = new Building { Id = NextId++, Type = type, X = x, Y = y, Res = type == BType.House ? 2 : 0, Stock = type == BType.Harbor ? new float[GoodCount] : null };
            Buildings.Add(b); Bld[i] = b.Id; Tree[i] = 0; DirtyTrees = true; DirtyBuildings = true;
            return b;
        }

        public Building ById(int id) { foreach (var b in Buildings) if (b.Id == id) return b; return null; }
        public Building At(int x, int y) { int id = Bld[Idx(x, y)]; return id >= 0 ? ById(id) : null; }

        // ---------------------------------------------------------------- islands
        public void ComputeIslands()
        {
            Comp = new short[W * H]; for (int i = 0; i < Comp.Length; i++) Comp[i] = -1;
            Islands.Clear();
            var q = new int[W * H];
            for (int i = 0; i < W * H; i++) if (Hgt[i] >= 0 && Origin[i] == 255) Origin[i] = (byte)NearestSpec(i % W, i / W);
            for (int s = 0; s < W * H; s++)
            {
                if (Hgt[s] < 0 || Comp[s] >= 0) continue;
                int id = Islands.Count, qh = 0, qt = 0; q[qt++] = s; Comp[s] = (short)id;
                var cnt = new int[3]; int n = 0; long sx = 0, sy = 0;
                while (qh < qt)
                {
                    int c = q[qh++]; n++; int o = Origin[c]; if (o < 3) cnt[o]++;
                    int cx = c % W, cy = c / W; sx += cx; sy += cy;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + DX[k], ny = cy + DY[k]; if (!Inb(nx, ny)) continue;
                        int ni = Idx(nx, ny); if (Hgt[ni] >= 0 && Comp[ni] < 0) { Comp[ni] = (short)id; q[qt++] = ni; }
                    }
                }
                var isl = new Island { Id = id, N = n, Cx = (float)sx / n, Cy = (float)sy / n };
                for (int k = 0; k < 3; k++) if (cnt[k] > Math.Max(6, n * 0.08)) isl.Parts.Add(k);
                string name = isl.Parts.Count > 0 ? string.Join("-", isl.Parts.Select(k => Defs.Islands[k].Name.Split(' ')[0])) : "Islet";
                if (isl.Parts.Count == 1) name = Defs.Islands[isl.Parts[0]].Name;
                if (n < 60) name = "Islet";
                isl.Name = name;
                Islands.Add(isl);
            }
            foreach (var b in Buildings.Where(b => b.Type == BType.Harbor).OrderBy(b => b.Id))
            {
                var isl = IslandAt(b.X, b.Y); if (isl == null) continue;
                if (isl.Harbor == null) { isl.Harbor = b; b.Merged = false; }
                else { for (int g = 0; g < GoodCount; g++) { isl.Harbor.Stock[g] += b.Stock[g]; b.Stock[g] = 0; } b.Merged = true; }
            }
            foreach (var b in Buildings) b.Isl = Comp[Idx(b.X, b.Y)];
            SyncBoats();
        }

        public Island IslandAt(int x, int y) { int c = Comp[Idx(x, y)]; return c >= 0 ? Islands[c] : null; }
        public Island IslandOf(Building b) => b.Isl >= 0 && b.Isl < Islands.Count ? Islands[b.Isl] : null;
        public Building CapitalHarbor => ById(Capital);
        public Island CapitalIsland { get { var c = CapitalHarbor; return c != null ? IslandAt(c.X, c.Y) : null; } }
        public float[] StockOf(Island isl) => isl?.Harbor?.Stock;

        public int CountAround(int x, int y, int r, Func<int, bool> fn)
        {
            int n = 0;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, ny = y + dy; if (!Inb(nx, ny) || (dx == 0 && dy == 0)) continue;
                    if (fn(Idx(nx, ny))) n++;
                }
            return n;
        }

        public bool AdjWater(int x, int y)
        {
            for (int k = 0; k < 4; k++) { int nx = x + DX[k], ny = y + DY[k]; if (Inb(nx, ny) && Hgt[Idx(nx, ny)] < 0) return true; }
            return false;
        }

        public bool BerthOf(int x, int y, out int bx, out int by)
        {
            for (int k = 0; k < 4; k++)
            {
                int nx = x + DX[k], ny = y + DY[k];
                if (Inb(nx, ny) && Hgt[Idx(nx, ny)] <= -2) { bx = nx; by = ny; return true; }
            }
            bx = by = -1; return false;
        }

        public string IslandName(Building b) { var isl = b != null ? IslandAt(b.X, b.Y) : null; return isl != null ? isl.Name : "?"; }

        // ---------------------------------------------------------------- boats
        void SyncBoats()
        {
            var cap = CapitalHarbor; var capIsl = CapitalIsland; var keep = new List<Boat>();
            foreach (var isl in Islands)
            {
                var hb = isl.Harbor; if (hb == null || cap == null || isl == capIsl) continue;
                var bt = Boats.FirstOrDefault(b => b.Home == hb.Id);
                if (bt == null)
                {
                    int px = hb.X, py = hb.Y; if (BerthOf(hb.X, hb.Y, out var bx, out var by)) { px = bx; py = by; }
                    bt = new Boat { Home = hb.Id, X = px, Y = py };
                }
                keep.Add(bt);
            }
            foreach (var b in Boats)
                if (!keep.Contains(b))
                {
                    var hb = ById(b.Home);
                    if (hb != null && IslandOf(hb) == capIsl) Say($"Land bridge made the {IslandName(hb)} boat unnecessary; goods now move overland.", ToastKind.Good);
                }
            Boats = keep;
        }

        List<int> BfsPath(int sx, int sy, int tx, int ty)
        {
            if (!Inb(sx, sy) || Hgt[Idx(sx, sy)] > -2) return null;
            var prev = new int[W * H]; for (int i = 0; i < prev.Length; i++) prev[i] = -1;
            var q = new int[W * H]; int qh = 0, qt = 0; int s = Idx(sx, sy), t = Idx(tx, ty);
            q[qt++] = s; prev[s] = s;
            while (qh < qt)
            {
                int c = q[qh++]; if (c == t) break; int cx = c % W, cy = c / W;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + DX[k], ny = cy + DY[k]; if (!Inb(nx, ny)) continue;
                    int ni = Idx(nx, ny); if (prev[ni] < 0 && Hgt[ni] <= -2) { prev[ni] = c; q[qt++] = ni; }
                }
            }
            if (prev[t] < 0) return null;
            var path = new List<int>(); for (int c = t; c != s; c = prev[c]) path.Add(c); path.Add(s); path.Reverse();
            return path;
        }

        void PlanBoat(Boat bt)
        {
            var hb = ById(bt.Home); var cap = CapitalHarbor; bt.Ver = TerrainVer;
            if (!BerthOf(hb.X, hb.Y, out var hbx, out var hby)) { bt.State = BoatState.Blocked; bt.Why = $"No deep water beside the {IslandName(hb)} harbor"; bt.Path = null; return; }
            if (!BerthOf(cap.X, cap.Y, out var cbx, out var cby)) { bt.State = BoatState.Blocked; bt.Why = "No deep water beside the capital harbor"; bt.Path = null; return; }
            int sx = (int)Math.Round(bt.X), sy = (int)Math.Round(bt.Y);
            if (!Inb(sx, sy) || Hgt[Idx(sx, sy)] > -2)
            {
                if (bt.State == BoatState.Sail) { Say($"The {IslandName(hb)} boat ran aground and lost its cargo.", ToastKind.Bad); Array.Clear(bt.Cargo, 0, GoodCount); }
                sx = hbx; sy = hby; bt.X = sx; bt.Y = sy; bt.Out = true;
            }
            int tx = bt.Out ? cbx : hbx, ty = bt.Out ? cby : hby;
            var p = BfsPath(sx, sy, tx, ty);
            if (p == null) { bt.State = BoatState.Blocked; bt.Path = null; bt.Why = $"No deep-water route to {(bt.Out ? "the capital" : IslandName(hb))}. Shallows block boats."; return; }
            bt.Path = p; bt.Pi = 0; bt.State = BoatState.Sail; bt.Why = "";
        }

        static void Unload(float[] to, Boat bt) { for (int g = 0; g < GoodCount; g++) { to[g] += bt.Cargo[g]; bt.Cargo[g] = 0; } }
        static void Load(float[] from, float[] to, Boat bt)
        {
            for (int g = 0; g < GoodCount; g++)
            {
                float amt = Math.Min(25, (float)Math.Floor((from[g] - to[g]) / 2));
                if (amt > 0) { from[g] -= amt; bt.Cargo[g] = amt; }
            }
        }

        void StepBoats(float dt)
        {
            if (CapitalHarbor == null) return;
            foreach (var bt in Boats.ToArray())
            {
                var hb = ById(bt.Home); if (hb == null) continue;
                var home = IslandOf(hb)?.Harbor; var capH = CapitalIsland?.Harbor; if (home == null || capH == null) continue;
                if (bt.Ver != TerrainVer) PlanBoat(bt);
                if (bt.State != BoatState.Sail || bt.Path == null) continue;
                float move = 5 * dt;
                while (move > 0 && bt.Pi < bt.Path.Count - 1)
                {
                    int n = bt.Path[bt.Pi + 1]; float nx = n % W, ny = n / W;
                    float dx = nx - bt.X, dy = ny - bt.Y, d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > 0) bt.Ang = (float)Math.Atan2(dy, dx);
                    if (d <= move) { bt.X = nx; bt.Y = ny; bt.Pi++; move -= d; }
                    else { bt.X += dx / d * move; bt.Y += dy / d * move; move = 0; }
                }
                if (bt.Pi >= bt.Path.Count - 1)
                {
                    if (bt.Out) { Unload(capH.Stock, bt); Load(capH.Stock, home.Stock, bt); bt.Out = false; }
                    else { Unload(home.Stock, bt); Load(home.Stock, capH.Stock, bt); bt.Out = true; bt.Trips++; if (Origin[Idx(hb.X, hb.Y)] == 2) StatSugarTrip++; }
                    PlanBoat(bt);
                }
            }
        }

        // ---------------------------------------------------------------- placement
        public PlaceCheck CanPlace(BType type, int x, int y, bool free = false)
        {
            if (!Inb(x, y)) return new PlaceCheck { Why = "" };
            int i = Idx(x, y), hv = Hgt[i]; var d = Def(type);
            if (Bld[i] >= 0) return new PlaceCheck { Why = "Tile is occupied" };
            if (hv < 0) return new PlaceCheck { Why = "Needs land. Raise this tile first." };
            if (hv < d.MinH || hv > d.MaxH) return new PlaceCheck { Why = $"{d.Name} needs height {d.MinH}-{d.MaxH} (this tile is {hv})" };
            var isl = IslandAt(x, y);
            if (type == BType.Harbor)
            {
                if (!AdjWater(x, y)) return new PlaceCheck { Why = "Harbor must touch the sea" };
                if (isl.Harbor != null) return new PlaceCheck { Why = $"{isl.Name} already has a harbor" };
            }
            else
            {
                if (isl.Harbor == null) return new PlaceCheck { Why = $"{isl.Name} has no harbor yet. Build one first." };
                if (type == BType.Fishery && !AdjWater(x, y)) return new PlaceCheck { Why = "Fishery must touch the sea" };
            }
            if (free) return new PlaceCheck { Ok = true };
            var st = type == BType.Harbor ? CapitalIsland?.Harbor?.Stock : StockOf(isl);
            if (Gold < d.Gold) return new PlaceCheck { Why = $"Need {d.Gold} gold" };
            foreach (var g in new[] { Good.Wood, Good.Stone })
            {
                int c = d.Cost(g); if (c <= 0) continue;
                if (st == null || st[(int)g] < c) return new PlaceCheck { Why = $"Need {c} {GoodNames[(int)g].ToLower()} in {(type == BType.Harbor ? "the capital" : isl.Name)} stock" };
            }
            return new PlaceCheck { Ok = true };
        }

        public bool Place(BType type, int x, int y)
        {
            var r = CanPlace(type, x, y); if (!r.Ok) { if (!string.IsNullOrEmpty(r.Why)) Say(r.Why, ToastKind.Bad); return false; }
            var isl = IslandAt(x, y); var d = Def(type);
            var st = type == BType.Harbor ? CapitalIsland.Harbor.Stock : StockOf(isl);
            Gold -= d.Gold; st[(int)Good.Wood] -= d.Wood; st[(int)Good.Stone] -= d.Stone;
            var b = AddBuilding(type, x, y);
            if (type == BType.Harbor)
            {
                ComputeIslands();
                if (!BerthOf(x, y, out _, out _)) Say("Harbor built, but no deep water touches it. Lower a neighbouring shallow so boats can dock.", ToastKind.Bad);
                else Say($"Harbor founded on {isl.Name}. Its boat will try to sail for the capital.", ToastKind.Good);
            }
            else b.Isl = Comp[Idx(x, y)];
            return true;
        }

        public void Demolish(int x, int y)
        {
            var b = At(x, y); if (b == null) return;
            if (b.Id == Capital) { Say("The capital harbor cannot be demolished", ToastKind.Bad); return; }
            Gold += Def(b.Type).Gold / 2;
            if (b.Type == BType.Harbor && !b.Merged)
            {
                var other = Buildings.FirstOrDefault(o => o != b && o.Type == BType.Harbor && o.Isl == b.Isl);
                if (other != null) for (int g = 0; g < GoodCount; g++) other.Stock[g] += b.Stock[g];
            }
            Buildings.Remove(b); Bld[Idx(x, y)] = -1; DirtyTrees = true; DirtyBuildings = true;
            if (b.Type == BType.Harbor) ComputeIslands();
            Say($"{Def(b.Type).Name} demolished");
        }

        // ---------------------------------------------------------------- terraforming
        Island NearestHarborIsland(int x, int y)
        {
            Island best = null; int bd = int.MaxValue;
            foreach (var isl in Islands)
            {
                var h = isl.Harbor; if (h == null) continue;
                int d = Math.Abs(h.X - x) + Math.Abs(h.Y - y); if (d < bd) { bd = d; best = isl; }
            }
            return best;
        }

        public TerraResult Terraform(int x, int y, TerraMode mode, int target = 0)
        {
            if (!Inb(x, y)) return new TerraResult();
            int i = Idx(x, y), cur = Hgt[i], to = cur;
            if (mode == TerraMode.Raise) to = Math.Min(MaxH, cur + 1);
            else if (mode == TerraMode.Lower) to = Math.Max(MinH, cur - 1);
            else to = target;
            if (to == cur) return new TerraResult { Ok = true, Noop = true };
            var b = At(x, y); if (b != null) return new TerraResult { Why = $"Demolish the {Def(b.Type).Name} before reshaping its tile" };
            int steps = Math.Abs(to - cur); bool up = to > cur;
            var src = NearestHarborIsland(x, y); var st = src?.Harbor?.Stock; int gold = 4 * steps;
            if (Gold < gold) return new TerraResult { Why = "Not enough gold to move earth" };
            if (up && (st == null || st[(int)Good.Stone] < steps)) return new TerraResult { Why = $"Need {steps} stone at {(src != null ? src.Name : "a harbor")} to raise land" };
            Gold -= gold; if (up) st[(int)Good.Stone] -= steps; else if (st != null) st[(int)Good.Stone] += steps * 0.5f;
            bool wasLand = cur >= 0, isLand = to >= 0;
            Hgt[i] = (sbyte)to; if (Tree[i] != 0) { Tree[i] = 0; DirtyTrees = true; }
            if (wasLand != isLand) Origin[i] = 255;
            TerrainVer++; MarkDirty(x, y);
            return new TerraResult { Ok = true, Coast = wasLand != isLand };
        }

        /// <summary>Apply a brush stroke; recomputes islands if a coastline changed and reports joins and splits.</summary>
        public void TerraformBrush(int cx, int cy, int radius, TerraMode mode, int target, HashSet<int> done)
        {
            bool coast = false; string err = null;
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = cx + dx, y = cy + dy; if (!Inb(x, y)) continue; int k = Idx(x, y);
                    if (done != null && done.Contains(k)) continue;
                    var r = Terraform(x, y, mode, target);
                    if (r.Ok) { done?.Add(k); if (r.Coast) coast = true; }
                    else if (r.Why != null) err = r.Why;
                }
            if (err != null) Say(err, ToastKind.Bad);
            if (coast)
            {
                int before = Islands.Count; ComputeIslands(); DirtyBuildings = true;
                if (Islands.Count != before)
                    Say(Islands.Count < before ? "Islands joined by land. Their warehouses now share stock." : "Land split apart. Each piece needs its own harbor.",
                        Islands.Count < before ? ToastKind.Good : ToastKind.Info);
            }
        }

        // ---------------------------------------------------------------- simulation
        public void Step(float dt)
        {
            T += dt;
            foreach (var isl in Islands) { isl.Res = 0; isl.Jobs = 0; }
            foreach (var b in Buildings) { var isl = IslandOf(b); if (isl == null) continue; if (b.Type == BType.House) isl.Res += b.Res; else isl.Jobs += Def(b.Type).Jobs; }
            foreach (var isl in Islands) isl.Eff = isl.Jobs > 0 ? Math.Min(1, isl.Res / isl.Jobs) : 1;
            float income = 0, upkeep = 0;
            foreach (var b in Buildings)
            {
                var d = Def(b.Type); upkeep += d.Upkeep;
                var isl = IslandOf(b); var st = StockOf(isl);
                if (b.Type == BType.Harbor) { b.Status = b.Merged ? $"Merged into the {isl?.Name} warehouse" : (BerthOf(b.X, b.Y, out _, out _) ? "Berth open" : "No deep-water berth"); continue; }
                if (st == null) { b.Eff = 0; b.Status = "No harbor on this island"; continue; }
                float e = isl.Eff; b.Eff = e; b.HasRate = true;
                switch (b.Type)
                {
                    case BType.House:
                        {
                            b.HasRate = false;
                            float need = 0.012f * b.Res * dt; bool fed = false, lux = false;
                            if (st[(int)Good.Fish] >= need) { st[(int)Good.Fish] -= need; fed = true; }
                            float rn = 0.005f * b.Res * dt; if (fed && st[(int)Good.Rum] >= rn) { st[(int)Good.Rum] -= rn; lux = true; }
                            float cap = lux ? 8 : 4;
                            if (fed) b.Res = b.Res < cap ? Math.Min(cap, b.Res + 0.06f * dt) : Math.Max(cap, b.Res - 0.04f * dt);
                            else b.Res = Math.Max(1, b.Res - 0.04f * dt);
                            if (fed) income += 0.1f * b.Res * (lux ? 2 : 1);
                            b.Status = !fed ? "Hungry: needs fish" : lux ? "Fed and enjoying rum" : "Fed (rum would double tax)"; b.Eff = fed ? 1 : 0.3f;
                            break;
                        }
                    case BType.Fishery:
                        {
                            int n = CountAround(b.X, b.Y, 2, i => Hgt[i] == -1); float r = (0.06f + 0.035f * Math.Min(n, 10)) * e;
                            st[(int)Good.Fish] += r * dt; b.Rate = r; b.Status = $"{n} reef tiles nearby"; break;
                        }
                    case BType.Lumber:
                        {
                            int n = CountAround(b.X, b.Y, 2, i => Tree[i] > 0); float r = 0.03f * Math.Min(n, 12) * e;
                            st[(int)Good.Wood] += r * dt; b.Rate = r; b.Status = n > 0 ? $"{n} trees in reach" : "No trees in reach"; if (n == 0) b.Eff = 0; break;
                        }
                    case BType.Quarry:
                        {
                            int hv = Hgt[Idx(b.X, b.Y)]; float r = (0.15f + 0.05f * (hv - 3)) * e;
                            st[(int)Good.Stone] += r * dt; b.Rate = r; b.Status = $"Height {hv} rock"; break;
                        }
                    case BType.Plantation:
                        {
                            bool near = CountAround(b.X, b.Y, 2, i => Hgt[i] < 0) > 0; float r = 0.12f * (near ? 1.5f : 1) * e;
                            st[(int)Good.Sugar] += r * dt; b.Rate = r; b.Status = near ? "Coastal soil, +50%" : "Inland soil"; break;
                        }
                    case BType.Distillery:
                        {
                            float want = 0.2f * e * dt; float take = Math.Min(want, st[(int)Good.Sugar]);
                            st[(int)Good.Sugar] -= take; st[(int)Good.Rum] += take / 2; StatRum += take / 2; b.Rate = dt > 0 ? take / 2 / dt : 0;
                            bool idle = take < want * 0.5f; b.Status = idle ? "Waiting for sugar" : "Distilling"; if (idle) b.Eff = 0; break;
                        }
                }
                if (d.Jobs > 0 && e < 1) b.Status += $" · {Math.Round(e * 100)}% staffed";
            }
            Gold += (income - upkeep) * dt;
            Rate = Rate * 0.9f + (income - upkeep) * 0.1f;
            StepBoats(dt);
            if (rnd.NextDouble() < dt * 3)
            {
                int x = rnd.Next(W), y = rnd.Next(H), i = Idx(x, y);
                if (Tree[i] == 0 && Bld[i] < 0 && Hgt[i] >= 1 && Hgt[i] <= 4 && CountAround(x, y, 1, j => Tree[j] == 1) >= 2) { Tree[i] = 1; DirtyTrees = true; }
            }
        }

        // ---------------------------------------------------------------- goals
        public float TotalPop { get { float s = 0; foreach (var b in Buildings) if (b.Type == BType.House) s += b.Res; return s; } }

        public static readonly Goal[] Goals =
        {
            new Goal { Id = "grow",   Title = "Grow Palm Isle to 30 residents", Hint = "Houses need fish. Fisheries love reef shallows.", Ok = s => (s.CapitalIsland?.Res ?? 0) >= 30 },
            new Goal { Id = "basalt", Title = "Quarry stone on Basalt Isle", Hint = "Settle it with a harbor across the lagoon, or raise a land bridge along the reef.",
                       Ok = s => s.Buildings.Any(b => b.Type == BType.Quarry && s.Origin[Idx(b.X, b.Y)] == 1 && b.Eff > 0 && s.StockOf(s.IslandOf(b)) != null) },
            new Goal { Id = "sugar",  Title = "Link Sugar Cay to the capital", Hint = "Broad lagoon flats keep boats away. Dredge a channel to its harbor.",
                       Ok = s => s.StatSugarTrip > 0 || ((s.CapitalIsland?.Parts.Contains(2) ?? false) && s.Buildings.Any(b => b.Type == BType.Plantation && s.Origin[Idx(b.X, b.Y)] == 2)) },
            new Goal { Id = "rum",    Title = "Distil 25 barrels of rum", Hint = "Sugar fields feed distilleries. Rum doubles house tax.", Ok = s => s.StatRum >= 25 },
            new Goal { Id = "pop",    Title = "Reach 200 residents across the atoll", Hint = "", Ok = s => s.TotalPop >= 200 },
        };

        public void CheckGoals()
        {
            foreach (var g in Goals)
                if (!GoalsDone.Contains(g.Id) && g.Ok(this))
                {
                    GoalsDone.Add(g.Id); Say($"Charter goal met: {g.Title}", ToastKind.Good);
                    if (Goals.All(x => GoalsDone.Contains(x.Id))) Say("Every charter goal met. The atoll thrives.", ToastKind.Good);
                }
        }

        // ---------------------------------------------------------------- save / load
        [Serializable]
        public sealed class SaveBuilding { public int id, type, x, y; public float res; public float[] stock; }

        [Serializable]
        public sealed class SaveData
        {
            public int v = 1;
            public int[] h, tree, origin;   // int arrays keep JsonUtility happy
            public SaveBuilding[] buildings;
            public float gold, t, statRum; public int nextId, capital, statSugarTrip;
            public string[] goals;
        }

        public SaveData ToSave() => new SaveData
        {
            h = Hgt.Select(x => (int)x).ToArray(), tree = Tree.Select(x => (int)x).ToArray(), origin = Origin.Select(x => (int)x).ToArray(),
            buildings = Buildings.Select(b => new SaveBuilding { id = b.Id, type = (int)b.Type, x = b.X, y = b.Y, res = b.Res, stock = b.Stock != null ? (float[])b.Stock.Clone() : new float[0] }).ToArray(),
            gold = Gold, t = T, statRum = StatRum, nextId = NextId, capital = Capital, statSugarTrip = StatSugarTrip, goals = GoalsDone.ToArray(),
        };

        public bool FromSave(SaveData d)
        {
            if (d == null || d.h == null || d.h.Length != W * H || d.tree == null || d.tree.Length != W * H || d.origin == null || d.origin.Length != W * H || d.buildings == null) return false;
            Hgt = d.h.Select(x => (sbyte)x).ToArray(); Tree = d.tree.Select(x => (byte)x).ToArray(); Origin = d.origin.Select(x => (byte)x).ToArray();
            Bld = new int[W * H]; for (int i = 0; i < Bld.Length; i++) Bld[i] = -1;
            Buildings.Clear(); Boats.Clear();
            foreach (var sb in d.buildings)
            {
                var b = new Building { Id = sb.id, Type = (BType)sb.type, X = sb.x, Y = sb.y, Res = sb.res };
                if (b.Type == BType.Harbor) { b.Stock = new float[GoodCount]; if (sb.stock != null) Array.Copy(sb.stock, b.Stock, Math.Min(GoodCount, sb.stock.Length)); }
                Buildings.Add(b); Bld[Idx(b.X, b.Y)] = b.Id;
            }
            Gold = d.gold; T = d.t; StatRum = d.statRum; NextId = d.nextId; Capital = d.capital; StatSugarTrip = d.statSugarTrip;
            GoalsDone = new HashSet<string>(d.goals ?? new string[0]);
            ComputeIslands(); TerrainVer++;
            DirtyTrees = true; DirtyBuildings = true; MarkAllDirty();
            return true;
        }
    }
}
