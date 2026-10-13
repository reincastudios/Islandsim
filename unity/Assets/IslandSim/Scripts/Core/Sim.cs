using System;
using System.Collections.Generic;
using System.Linq;
using static IslandSim.Core.Defs;

namespace IslandSim.Core
{
    public enum ToastKind { Info, Good, Bad }

    /// <summary>A compound's needs this tick: food (fish, or rice when fish runs out), coconuts, a mosque nearby, and sweet toddy.</summary>
    public sealed class Needs { public bool Food, Rice, Coco, Faith, Toddy; }

    public sealed class Building
    {
        public int Id; public BType Type; public int X, Y, W = 1, D = 1;
        public double Res, Eff, Rate, Staff, Cap, Tax; public string Status = "";
        public double[] Stock;            // jetties only
        public bool Merged, Off, Road; public int Isl = -1;
        public double Wade;               // share of the staff who wade over a sandbank at low tide (not saved)
        public Needs Need;
        public BuildingDef Def => Defs.Def(Type);
    }

    public sealed class Island
    {
        public int Id, N, Atoll; public string Name, AutoName, Key; public List<int> Parts = new List<int>();
        public Building Harbor; public double Res, Jobs, Filled, Eff = 1, Cx, Cy;
    }

    public sealed class Boat
    {
        public int Home; public double X, Y, Ang; public List<(int x, int y)> Path; public int Pi;
        public bool Out = true; public double[] Cargo = new double[GoodCount];
        public string State = "idle", Why = ""; public int Ver = -1, Trips;
        public bool Wait;                 // holding off a reef flat until high tide
        public bool HasCargo { get { foreach (var c in Cargo) if (c > 0) return true; return false; } }
    }

    public struct Check
    {
        public bool Ok; public string Why; public int X, Y, W, D;
        public static Check No(string why) => new Check { Ok = false, Why = why };
    }

    public struct Flow { public double Make, Use; }

    /// <summary>
    /// The whole game rules: the five-atoll map, islands and their warehouses, placement, roads, terraforming, the Age 1
    /// economy, dhoni boats, trading ships, the charter and saves. A line-by-line port of the web game (index.html), pure C#
    /// with no UnityEngine, so it can be tested outside the editor and checked against the browser tile for tile.
    /// </summary>
    public sealed partial class Sim
    {
        // ------------------------------------------------------------ state (the web game's S)
        public sbyte[] Hgt; public byte[] Tree, Gt, Road, Origin = new byte[W * H]; public int[] Bld;
        public List<Building> Buildings = new List<Building>();
        public double Gold, T, Rate, Unrest; public int NextId = 1, Capital, Age = 1; public bool AgeDone;
        public double StatRope; public int StatLinkTrip;
        public HashSet<string> Goals = new HashSet<string>();
        public Dictionary<string, string> Names = new Dictionary<string, string>();
        public TradeState Trade = new TradeState();
        public string Over;                  // null, "bankrupt" or "unrest"

        public List<Island> Islands = new List<Island>();
        public short[] Comp;
        /// <summary>Land joined by sandbanks, labelled together. At low tide residents wade across to work on another island
        /// of the same bank; goods and warehouses stay with their own island.</summary>
        public short[] Bank;
        /// <summary>Sandbanks: the reef flats (height -1) on the short ways between two islands, at most SANDBANK_REACH
        /// flats long, and one tile either side. They lie just under the sea and dry at low tide.</summary>
        public byte[] Sandbank;
        public const int SANDBANK_REACH = 8;
        public (int x, int y)?[] IslAnchor = new (int, int)?[Defs.Islands.Length];
        public List<Boat> Boats = new List<Boat>();
        public int TerrainVer;

        /// <summary>Compound plot size picked in the Homes cards: 0 = 3×2, 1 = 4×3.</summary>
        public int Plot;
        /// <summary>Gold and stone spent by the current stroke, for the cursor tag.</summary>
        public double SpendGold, SpendStone;

        // Change tracking for the view: tiles whose look changed (roads, plots, trees), tiles whose height changed (also redo
        // the coastline), a new buildings list, and the minimap's ground layer.
        public DirtyRect DirtyLook = DirtyRect.All(), DirtyShore = DirtyRect.All(), DirtyTrees = DirtyRect.All();
        public bool MiniDirty = true; public int BuildingsVer;

        public event Action<string, ToastKind, (int x, int y)?> Toast;
        /// <summary>Called when a tool is refused, with the reason for the cursor tag.</summary>
        public event Action<string> Refused;
        /// <summary>Random numbers for tree regrowth; tests swap it for a fixed source.</summary>
        public Func<double> Random;
        /// <summary>Real time in seconds, for the 10-second undo window.</summary>
        public Func<double> Now = () => Environment.TickCount / 1000.0;

        public Sim() { var r = new System.Random(); Random = r.NextDouble; }

        public static int Idx(int x, int y) => y * W + x;
        public static bool Inb(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public static readonly int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };
        static double Hypot(double a, double b) => Noise.Hypot(a, b);

        public void Say(string msg, ToastKind k = ToastKind.Info, (int x, int y)? at = null) => Toast?.Invoke(msg, k, at);
        public void Refuse(string why) => Refused?.Invoke(why);

        public void MarkTrees(int x0, int y0, int x1, int y1) { DirtyTrees.Add(x0, y0, x1, y1); MiniDirty = true; }
        public void MarkTrees(int x, int y) => MarkTrees(x, y, x, y);
        public void MarkTerrain(int x0, int y0, int x1, int y1) { DirtyLook.Add(x0, y0, x1, y1); MiniDirty = true; }
        public void MarkTerrain(int x, int y) => MarkTerrain(x, y, x, y);
        public void MarkShore(int x, int y) { DirtyShore.Add(x, y, x, y); DirtyLook.Add(x - 1, y - 1, x + 1, y + 1); MiniDirty = true; }
        public void MarkAll() { DirtyLook = DirtyRect.All(); DirtyShore = DirtyRect.All(); DirtyTrees = DirtyRect.All(); MiniDirty = true; BuildingsVer++; }

        // ------------------------------------------------------------ buildings
        public Building ById(int id) { foreach (var b in Buildings) if (b.Id == id) return b; return null; }
        public Building BAt(int x, int y) { int id = Bld[Idx(x, y)]; return id >= 0 ? ById(id) : null; }
        public Building CapitalHarbor => ById(Capital);
        public Island CapitalIsland { get { var c = CapitalHarbor; return c != null ? IslandAtComp(Comp[Idx(c.X, c.Y)]) : null; } }
        Island IslandAtComp(int c) => c >= 0 && c < Islands.Count ? Islands[c] : null;
        public Island IslandOf(int x, int y) => IslandAtComp(Comp[Idx(x, y)]);
        public Island IslandOfB(Building b) => IslandAtComp(b.Isl);
        public static double[] NoStock() => new double[GoodCount];
        public double[] StockOf(Island isl) => isl != null && isl.Harbor != null ? isl.Harbor.Stock : null;
        public string IslandName(Building b) { var isl = IslandOf(b.X, b.Y); return isl != null ? isl.Name : "?"; }
        public double TotalPop { get { double n = 0; foreach (var b in Buildings) if (b.Type == BType.House) n += b.Res; return n; } }
        public int Day => 1 + (int)Math.Floor(T / 30);

        public Building AddBuilding(BType type, int x, int y, int w = 1, int d = 1)
        {
            MiniDirty = true; BuildingsVer++;
            var b = new Building { Id = NextId++, Type = type, X = x, Y = y, W = w, D = d, Res = type == BType.House ? 2 : 0, Stock = type == BType.Harbor ? NoStock() : null };
            Buildings.Add(b);
            for (int ty = y; ty < y + d; ty++) for (int tx = x; tx < x + w; tx++) { int i = Idx(tx, ty); Bld[i] = b.Id; Tree[i] = 0; Road[i] = 0; }
            if (type == BType.House) { var t = YardPalmTile(b); if (t.HasValue) Tree[Idx(t.Value.x, t.Value.y)] = 2; }   // a coconut palm in the yard
            MarkTrees(x, y, x + w - 1, y + d - 1); MarkTerrain(x - 1, y - 1, x + w, y + d);
            return b;
        }

        // Compounds face their gate along local +z; long plots turn 0/180°, deep plots ±90°.
        public static double PlotRot(Building b) { double flip = Noise.Hash2(b.X, b.Y, 1) < .5 ? 0 : Math.PI; return (b.W >= b.D ? 0 : Math.PI / 2) + flip; }
        public static (double x, double y) PlotCentre(Building b) => (b.X + b.W / 2.0, b.Y + b.D / 2.0);
        public static double PlotFactor(Building b) => b.W * b.D / 6.0;   // a 4×3 compound holds twice the families of a 3×2 one
        (int x, int y)? YardPalmTile(Building b)
        {
            var c = PlotCentre(b); double r = PlotRot(b); bool big = b.W * b.D > 6; double lx = big ? .55 : 1, lz = big ? Math.Min(b.W, b.D) / 2.0 - .45 : .5; // front, away from the well
            double wx = c.x + lx * Math.Cos(r) + lz * Math.Sin(r), wz = c.y - lx * Math.Sin(r) + lz * Math.Cos(r); int tx = (int)Math.Floor(wx), ty = (int)Math.Floor(wz);
            return Inb(tx, ty) && Bld[Idx(tx, ty)] == b.Id ? (tx, ty) : ((int, int)?)null;
        }

        // ------------------------------------------------------------ islands
        // Grows every island out over the reef flats at once; where two islands' growth meets within SANDBANK_REACH flats,
        // the way back to each shore is a sandbank. Changed tiles are marked so the ground there is redrawn.
        void ComputeSandbanks(int[] q)
        {
            var own = new int[W * H]; var dist = new int[W * H]; var par = new int[W * H]; int qh = 0, qt = 0;
            for (int i = 0; i < W * H; i++) { own[i] = Hgt[i] >= 0 ? Comp[i] : -1; par[i] = -1; if (own[i] >= 0) q[qt++] = i; }
            while (qh < qt)
            {
                int c = q[qh++], cx = c % W, cy = c / W; if (dist[c] >= SANDBANK_REACH) continue;
                for (int k = 0; k < 4; k++) { int nx = cx + DX[k], ny = cy + DY[k]; if (!Inb(nx, ny)) continue; int ni = Idx(nx, ny); if (Hgt[ni] == -1 && own[ni] < 0) { own[ni] = own[c]; dist[ni] = dist[c] + 1; par[ni] = c; q[qt++] = ni; } }
            }
            var path = new byte[W * H];
            void Trace(int c) { for (; c >= 0 && Hgt[c] == -1 && path[c] == 0; c = par[c]) path[c] = 1; }
            for (int c = 0; c < W * H; c++)
            {
                if (own[c] < 0) continue; int cx = c % W, cy = c / W;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + DX[k], ny = cy + DY[k]; if (!Inb(nx, ny)) continue; int n = Idx(nx, ny);
                    if (own[n] < 0 || own[n] == own[c] || dist[c] + dist[n] > SANDBANK_REACH) continue;
                    Trace(c); Trace(n);
                }
            }
            var nbk = new byte[W * H];
            for (int c = 0; c < W * H; c++)
            {
                if (path[c] == 0) continue; nbk[c] = 1; int cx = c % W, cy = c / W;
                for (int k = 0; k < 4; k++) { int nx = cx + DX[k], ny = cy + DY[k]; if (Inb(nx, ny) && Hgt[Idx(nx, ny)] == -1) nbk[Idx(nx, ny)] = 1; }
            }
            if (Sandbank != null)
            {
                int x0 = W, y0 = H, x1 = -1, y1 = -1;
                for (int i = 0; i < W * H; i++) if (nbk[i] != Sandbank[i]) { int x = i % W, y = i / W; x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
                if (x1 >= 0) MarkTerrain(x0, y0, x1, y1);
            }
            Sandbank = nbk;
        }

        public void ComputeIslands()
        {
            Comp = new short[W * H]; for (int i = 0; i < Comp.Length; i++) Comp[i] = -1;
            Islands = new List<Island>();
            var q = new int[W * H]; int nIsl = Defs.Islands.Length;
            for (int i = 0; i < W * H; i++)
                if (Hgt[i] >= 0 && Origin[i] == 255)
                {
                    int x = i % W, y = i / W; double bd = 1e9; int o = 0;
                    for (int k = 0; k < nIsl; k++) { var c = IslC[k]; double d = Hypot(x - c.x, y - c.y); if (d < bd) { bd = d; o = k; } }
                    Origin[i] = (byte)o;
                }
            var counts = new List<int[]>(); var starts = new List<int>();
            for (int s = 0; s < W * H; s++)
            {
                if (Hgt[s] < 0 || Comp[s] >= 0) continue;
                int id = Islands.Count, qh = 0, qt = 0; q[qt++] = s; Comp[s] = (short)id; var cnt = new int[nIsl]; int n = 0; double sx = 0, sy = 0;
                while (qh < qt)
                {
                    int c = q[qh++]; n++; int o = Origin[c]; if (o < nIsl) cnt[o]++; int cx = c % W, cy = c / W; sx += cx; sy += cy;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + DX[k], ny = cy + DY[k]; if (!Inb(nx, ny)) continue; int ni = Idx(nx, ny);
                        if (Hgt[ni] >= 0 && Comp[ni] < 0) { Comp[ni] = (short)id; q[qt++] = ni; }
                    }
                }
                Islands.Add(new Island { Id = id, N = n, Cx = sx / n, Cy = sy / n, Atoll = AtollAt(sx / n, sy / n) });
                counts.Add(cnt); starts.Add(s);
            }
            // a named island's name goes to the piece holding most of it; stray fragments are islets
            var owner = new int[nIsl];
            for (int k = 0; k < nIsl; k++) { int b = -1, bc = 0; foreach (var isl in Islands) if (counts[isl.Id][k] > bc) { bc = counts[isl.Id][k]; b = isl.Id; } owner[k] = b; }
            foreach (var isl in Islands)
            {
                int n = isl.N; var cnt = counts[isl.Id];
                var parts = new List<int>(); for (int k = 0; k < nIsl; k++) if (owner[k] == isl.Id && cnt[k] > Math.Max(6, n * 0.08)) parts.Add(k);
                string name = parts.Count > 0 ? string.Join("–", parts.Select(k => Defs.Islands[k].Name.Split(' ')[0])) : "Islet";
                if (parts.Count == 1) name = Defs.Islands[parts[0]].Name;
                if (n < 12) name = "Islet";
                // a player's name sticks to the island's biggest named piece (or, for an islet, its first tile)
                int main = -1; foreach (var k in parts) if (main < 0 || cnt[k] > cnt[main]) main = k;
                isl.Key = main >= 0 ? "k" + main : "t" + starts[isl.Id]; isl.AutoName = name;
                isl.Name = Names.TryGetValue(isl.Key, out var nm) ? nm : name; isl.Parts = parts;
            }
            // each named island's label sits on its land tile nearest the island's centre
            IslAnchor = new (int, int)?[nIsl]; var ad = new double[nIsl]; for (int k = 0; k < nIsl; k++) ad[k] = 1e9;
            for (int i = 0; i < W * H; i++)
            {
                int o = Origin[i]; if (o >= nIsl || Hgt[i] < 0) continue; int x = i % W, y = i / W; var c = IslC[o];
                double d = Hypot(x - c.x, y - c.y); if (d < ad[o]) { ad[o] = d; IslAnchor[o] = (x, y); }
            }
            foreach (var b in Buildings.Where(b => b.Type == BType.Harbor).OrderBy(b => b.Id))
            {
                var isl = IslandAtComp(Comp[Idx(b.X, b.Y)]); if (isl == null) continue;
                if (isl.Harbor == null) { isl.Harbor = b; b.Merged = false; }
                else { for (int g = 0; g < GoodCount; g++) { isl.Harbor.Stock[g] += b.Stock[g]; b.Stock[g] = 0; } b.Merged = true; }
            }
            foreach (var b in Buildings) b.Isl = Comp[Idx(b.X, b.Y)];
            ComputeSandbanks(q);
            Bank = new short[W * H]; for (int i = 0; i < Bank.Length; i++) Bank[i] = -1; short nb = 0;
            for (int s = 0; s < W * H; s++)
            {
                if ((Hgt[s] < 0 && Sandbank[s] == 0) || Bank[s] >= 0) continue;
                int qh = 0, qt = 0; q[qt++] = s; Bank[s] = nb;
                while (qh < qt)
                {
                    int c = q[qh++], cx = c % W, cy = c / W;
                    for (int k = 0; k < 4; k++) { int nx = cx + DX[k], ny = cy + DY[k]; if (!Inb(nx, ny)) continue; int ni = Idx(nx, ny); if ((Hgt[ni] >= 0 || Sandbank[ni] != 0) && Bank[ni] < 0) { Bank[ni] = nb; q[qt++] = ni; } }
                }
                nb++;
            }
            SyncBoats();
        }

        public int CountAround(int x, int y, int r, Func<int, bool> fn)
        {
            int n = 0;
            for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
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
        public (int x, int y)? BerthOf(int x, int y)
        {
            for (int k = 0; k < 4; k++) { int nx = x + DX[k], ny = y + DY[k]; if (Inb(nx, ny) && Hgt[Idx(nx, ny)] <= -2) return (nx, ny); }
            return null;
        }
        public Island NearestHarborIsland(double x, double y)
        {
            Island best = null; double bd = 1e9;
            foreach (var isl in Islands) { var h = isl.Harbor; if (h == null) continue; double d = Math.Abs(h.X - x) + Math.Abs(h.Y - y); if (d < bd) { bd = d; best = isl; } }
            return best;
        }

        // ------------------------------------------------------------ placement
        public Cost CostOf(BType type) => type == BType.House && Plot == 1 ? BigHouse : Def(type).Cost;
        public static Cost CostOfB(Building b) => b.Type == BType.House && b.W * b.D > 6 ? BigHouse : b.Def.Cost;

        /// <summary>A building's tiles: the hovered tile sits at (or just before) the middle; multi-tile plots may be turned 90°.</summary>
        public Check Footprint(BType type, int x, int y, bool turned = false)
        {
            var d0 = Def(type); var sz = d0.Sizes != null ? d0.Sizes[Plot] : d0.Size;
            if (sz == null) return new Check { X = x, Y = y, W = 1, D = 1 };
            int w = turned ? sz[1] : sz[0], d = turned ? sz[0] : sz[1];
            return new Check { X = x - ((w - 1) >> 1), Y = y - ((d - 1) >> 1), W = w, D = d };
        }

        public Check CanPlace(BType type, int x, int y, bool free = false)
        {
            if (Def(type).Size == null) return CanPlaceAt(type, Footprint(type, x, y), free);
            var a = CanPlaceAt(type, Footprint(type, x, y, false), free); if (a.Ok) return a;    // try both ways round
            var b = CanPlaceAt(type, Footprint(type, x, y, true), free); return b.Ok ? b : a;
        }

        Check CanPlaceAt(BType type, Check fp, bool free)
        {
            int x = fp.X, y = fp.Y; var d = Def(type);
            if (!Inb(x, y) || !Inb(x + fp.W - 1, y + fp.D - 1)) return Check.No("");
            int hv = Hgt[Idx(x, y)];
            for (int ty = y; ty < y + fp.D; ty++) for (int tx = x; tx < x + fp.W; tx++)
                {
                    int i = Idx(tx, ty), h = Hgt[i];
                    if (Bld[i] >= 0) return Check.No(fp.W > 1 ? "The plot overlaps another building" : "Tile is occupied");
                    int g = Gt[i];
                    if (g == GT.Kulhi || g == GT.Pond) return Check.No("Kulhi wetland. Raise it to fill it in, or leave it as a fish nursery.");
                    if (g == GT.Bird) return Check.No("Birds nest here. The island is protected.");
                    if (g == GT.Finolhu && type != BType.Fishery && type != BType.Harbor) return Check.No("Finolhu sand shifts with the monsoon. Only a fishery or harbor can stand here.");
                    if (h < 0) return Check.No("Needs land. Raise this tile first.");
                    if (h < d.MinH || h > d.MaxH) return Check.No($"{d.Name} needs height {d.MinH}–{d.MaxH} (this tile is {h})");
                    if (h != hv) return Check.No($"The {fp.W}×{fp.D} plot must be level. Use Level first.");
                }
            var isl = IslandOf(x, y);
            if (type == BType.Harbor)
            {
                if (!AdjWater(x, y)) return Check.No("Harbor must touch the sea");
                if (isl.Harbor != null) return Check.No($"{isl.Name} already has a jetty");
            }
            else
            {
                if (isl.Harbor == null) return Check.No($"{isl.Name} has no jetty yet. Build one first.");
                if (type == BType.Fishery && !AdjWater(x, y)) return Check.No("A dhoni berth must touch the sea");
                if (type == BType.Coir && CountAround(x, y, 2, i => Hgt[i] < 0) == 0) return Check.No("The rope yard soaks husks in the lagoon: build it within 2 tiles of water");
                if (type == BType.Palace)
                {
                    if (Buildings.Any(b => b.Type == BType.Palace)) return Check.No("There is only one Sultan's palace");
                    if (isl != CapitalIsland) return Check.No("The palace must stand on the capital island");
                }
            }
            fp.Ok = true;
            if (free) return fp;
            var st = type == BType.Harbor ? CapitalIsland?.Harbor?.Stock : StockOf(isl); var cost = CostOf(type);
            if (Gold < cost.Gold) return Check.No($"Need {cost.Gold} gold");
            foreach (var g in new[] { Good.Wood, Good.Stone })
            {
                int c = cost.Of(g); if (c == 0) continue;
                if (st == null || st[(int)g] < c) return Check.No($"Need {c} {GoodLabel[(int)g].ToLowerInvariant()} in {(type == BType.Harbor ? "the capital" : isl.Name)} stock");
            }
            return fp;
        }

        public bool Place(BType type, int x, int y)
        {
            var r = CanPlace(type, x, y); if (!r.Ok) { if (!string.IsNullOrEmpty(r.Why)) Refuse(r.Why); return false; }
            var isl = IslandOf(x, y); var cost = CostOf(type);
            var st = type == BType.Harbor ? CapitalIsland.Harbor.Stock : StockOf(isl);
            Gold -= cost.Gold; st[(int)Good.Wood] -= cost.Wood; st[(int)Good.Stone] -= cost.Stone;
            var b = AddBuilding(type, r.X, r.Y, r.W, r.D);
            if (type == BType.Harbor)
            {
                ComputeIslands();
                if (BerthOf(x, y) == null) Say("Jetty built, but no deep water touches it. Lower a neighbouring shallow so boats can dock.", ToastKind.Bad, (x, y));
                else Say($"Jetty founded on {isl.Name}. Its dhoni will try to sail for the capital.", ToastKind.Good, (x, y));
            }
            else b.Isl = Comp[Idx(x, y)];
            return true;
        }

        public void Demolish(int x, int y)
        {
            var b = BAt(x, y); if (b == null) return;
            if (b.Id == Capital) { Refuse("The capital jetty cannot be demolished"); return; }
            Gold += Math.Floor(CostOfB(b).Gold / 2.0);
            if (b.Type == BType.Harbor && !b.Merged)
            {
                var other = Buildings.FirstOrDefault(o => o != b && o.Type == BType.Harbor && o.Isl == b.Isl);
                if (other != null) for (int g = 0; g < GoodCount; g++) other.Stock[g] += b.Stock[g];
            }
            Buildings.Remove(b); BuildingsVer++;
            for (int ty = b.Y; ty < b.Y + b.D; ty++) for (int tx = b.X; tx < b.X + b.W; tx++) Bld[Idx(tx, ty)] = -1;
            MarkTrees(b.X, b.Y, b.X + b.W - 1, b.Y + b.D - 1); MarkTerrain(b.X - 1, b.Y - 1, b.X + b.W, b.Y + b.D);
            if (b.Type == BType.Harbor) ComputeIslands();
        }

        // ------------------------------------------------------------ sand roads
        public Check BuildRoad(int x, int y)
        {
            if (!Inb(x, y)) return Check.No(null); int i = Idx(x, y);
            if (Road[i] != 0) return new Check { Ok = true };
            if (Hgt[i] < 0) return Check.No("Roads need land");
            if (Gt[i] == GT.Kulhi) return Check.No("Roads cannot cross a kulhi. Fill it in first.");
            if (Gt[i] == GT.Bird) return Check.No("Birds nest here. The island is protected.");
            if (Bld[i] >= 0) return new Check { Ok = true };                    // roads simply run up to a building
            if (Gold < 2) return Check.No("Need 2 gold per road tile");
            Gold -= 2; SpendGold += 2; Road[i] = 1; if (Tree[i] != 0) Tree[i] = 0; MarkTrees(x, y); MarkTerrain(x, y); return new Check { Ok = true };
        }
        public void RemoveRoad(int x, int y)
        {
            if (!Inb(x, y)) return; int i = Idx(x, y); if (Road[i] == 0) return;
            Road[i] = 0; Gold += 1; MarkTrees(x, y); MarkTerrain(x, y);
        }
        /// <summary>A road tile touching any side of the footprint.</summary>
        public bool RoadBeside(Building b)
        {
            for (int x = b.X; x < b.X + b.W; x++) foreach (var y in new[] { b.Y - 1, b.Y + b.D }) if (Inb(x, y) && Road[Idx(x, y)] != 0) return true;
            for (int y = b.Y; y < b.Y + b.D; y++) foreach (var x in new[] { b.X - 1, b.X + b.W }) if (Inb(x, y) && Road[Idx(x, y)] != 0) return true;
            return false;
        }

        // ------------------------------------------------------------ what the HUD reads
        /// <summary>What an island's buildings make and use, per second.</summary>
        public Flow[] Flows(Island isl)
        {
            var f = new Flow[GoodCount]; if (isl == null) return f;
            foreach (var b in Buildings)
            {
                if (b.Isl != isl.Id || b.Off) continue; double r = b.Rate;
                switch (b.Type)
                {
                    case BType.Fishery: f[(int)Good.Fish].Make += r; break;
                    case BType.Lumber: f[(int)Good.Wood].Make += r; break;
                    case BType.Quarry: f[(int)Good.Stone].Make += r; break;
                    case BType.Grove: f[(int)Good.Coconut].Make += r; break;
                    case BType.Toddy: f[(int)Good.Toddy].Make += r; break;
                    case BType.Cowrie: f[(int)Good.Cowrie].Make += r; break;
                    case BType.Drying: f[(int)Good.Hikimas].Make += r; f[(int)Good.Fish].Use += r * 2; f[(int)Good.Wood].Use += r * .4; break;
                    case BType.Coir: f[(int)Good.Rope].Make += r; f[(int)Good.Coconut].Use += r / .4; break;
                    case BType.House:
                        {
                            var n = b.Need ?? new Needs();
                            if (n.Food) f[(int)(n.Rice ? Good.Rice : Good.Fish)].Use += 0.012 * b.Res; else f[(int)Good.Fish].Use += 0.012 * b.Res;
                            if (n.Coco) f[(int)Good.Coconut].Use += 0.006 * b.Res;
                            if (n.Toddy) f[(int)Good.Toddy].Use += 0.004 * b.Res;
                            break;
                        }
                }
            }
            return f;
        }

        /// <summary>Residents of an island's compounds within walking distance of a tile (the cursor tag shows it before placing).</summary>
        public double WorkersNear(int x, int y, int isl)
        {
            double n = 0; bool low = Tide.IsLow(T); int bk = Inb(x, y) ? Bank[Idx(x, y)] : -1;
            foreach (var b in Buildings) if (b.Type == BType.House && (b.Isl == isl || low && bk >= 0 && Bank[Idx(b.X, b.Y)] == bk)) { var c = PlotCentre(b); if (Hypot(c.x - x - .5, c.y - y - .5) <= Walk) n += b.Res; }
            return n;
        }

        readonly Dictionary<string, double> alerted = new Dictionary<string, double>();
        void WarnOnce(string k, string msg, (int, int)? at) { if (alerted.TryGetValue(k, out var t0) && T - t0 < 120) return; alerted[k] = T; Say(msg, ToastKind.Bad, at); }
        public void ClearAlerts() => alerted.Clear();
        /// <summary>The katheeb's warnings, checked every 3 seconds: empty stores, idle rope yards, unstaffed buildings, debt, stuck boats.</summary>
        public void CheckAlerts()
        {
            foreach (var isl in Islands)
            {
                var hb = isl.Harbor; if (hb == null) continue; var st = hb.Stock; var f = Flows(isl); var at = (hb.X, hb.Y);
                if (f[(int)Good.Fish].Use > 0 && st[(int)Good.Fish] < 1) WarnOnce(isl.Name + ":fish", $"{isl.Name} is out of fish. Its compounds go hungry and stop paying tax.", at);
                if (Buildings.Any(b => b.Isl == isl.Id && b.Type == BType.Coir && !b.Off && b.Status.Contains("Waiting"))) WarnOnce(isl.Name + ":coco", $"The rope yard on {isl.Name} is waiting for coconuts. Plant more coconut groves.", at);
                if (Buildings.Any(b => b.Isl == isl.Id && b.Type == BType.House && b.Need != null && b.Need.Food && !b.Need.Coco) && st[(int)Good.Coconut] < 1) WarnOnce(isl.Name + ":cocoh", $"Families on {isl.Name} have no coconuts. A coconut grove would let their compounds grow.", at);
            }
            var cap = CapitalHarbor; (int, int)? cat = cap != null ? (cap.X, cap.Y) : ((int, int)?)null;
            if (Gold < 0) WarnOnce("debt", $"The treasury is empty. If the debt passes {-DebtLimit} gold, the sultan takes the atoll from you.", cat);
            if (Unrest >= 40) WarnOnce("unrest", $"Unrest is at {Noise.Round(Unrest)}%. Hungry families are angry; feed them before it reaches 100%.", cat);
            foreach (var b in Buildings) if (b.Def.Jobs > 0 && !b.Off && b.Staff <= 0) WarnOnce("staff" + b.Id, $"The {b.Def.Name.ToLowerInvariant()} on {IslandName(b)} has no one within walking distance. Build a compound nearby.", (b.X, b.Y));
            foreach (var bt in Boats) if (bt.State == "blocked") { var hb = ById(bt.Home); if (hb != null) WarnOnce("boat" + bt.Home, $"The {IslandName(hb)} boat is stuck. {bt.Why}", ((int)Noise.Round(bt.X), (int)Noise.Round(bt.Y))); }
        }
    }

    /// <summary>An inclusive tile rectangle of pending changes; empty when X0 > X1.</summary>
    public struct DirtyRect
    {
        public int X0, Y0, X1, Y1;
        public bool Empty => X0 > X1;
        public static DirtyRect None() => new DirtyRect { X0 = 1, X1 = 0 };
        public static DirtyRect All() => new DirtyRect { X0 = 0, Y0 = 0, X1 = Defs.W - 1, Y1 = Defs.H - 1 };
        public void Add(int x0, int y0, int x1, int y1)
        {
            x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(Defs.W - 1, x1); y1 = Math.Min(Defs.H - 1, y1);
            if (x0 > x1 || y0 > y1) return;
            if (Empty) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; return; }
            X0 = Math.Min(X0, x0); Y0 = Math.Min(Y0, y0); X1 = Math.Max(X1, x1); Y1 = Math.Max(Y1, y1);
        }
        /// <summary>Hands the rectangle to the view and starts a fresh one.</summary>
        public DirtyRect Take() { var r = this; this = None(); return r; }
    }
}
