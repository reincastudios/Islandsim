using System;
using System.Collections.Generic;
using System.Linq;
using static IslandSim.Core.Defs;

namespace IslandSim.Core
{
    /// <summary>Trade post settings: which exports passing ships may buy from the capital, and whether to buy rice.</summary>
    public sealed class TradeState
    {
        public bool[] Sell = new bool[GoodCount];
        public bool Rice = true; public double Next = 45, Earned; public int Visits;
        public TradeVisit Last;
        public TradeState() { Sell[(int)Good.Rope] = Sell[(int)Good.Hikimas] = Sell[(int)Good.Cowrie] = true; }
    }
    public sealed class TradeVisit { public int Day, Rice; public string Sold; public double Gold; }

    /// <summary>A trading ship (odi) on its way in to the capital jetty, docked, or sailing out again.</summary>
    public sealed class TraderShip { public List<(int x, int y)> Path; public int Pi; public double X, Y, Ang, Wait; public string State = "in"; }

    public sealed class AgeItem { public string Title; public int Have, Need; }

    public sealed class Goal { public string Id, Title, Hint; public Func<Sim, bool> Ok; }

    public sealed partial class Sim
    {
        bool MosqueNear(Building h)
        {
            var c = PlotCentre(h);
            foreach (var m in Buildings) if (m.Type == BType.Mosque && m.Isl == h.Isl) { var q = PlotCentre(m); if (Hypot(q.x - c.x, q.y - c.y) <= MosqueWalk) return true; }
            return false;
        }
        // palms a toddy tapper can climb: wild coconut palms within 3 tiles, plus six for each coconut grove in reach
        int PalmsNear(Building b)
        {
            int n = CountAround(b.X, b.Y, 3, i => Tree[i] == 2);
            foreach (var g in Buildings) if (g.Type == BType.Grove && Math.Abs(g.X - b.X) <= 3 && Math.Abs(g.Y - b.Y) <= 3) n += 6;
            return n;
        }

        /// <summary>One simulation tick (the game runs 0.1 s ticks).</summary>
        public void Step(double dt)
        {
            T += dt;
            foreach (var isl in Islands) { isl.Res = 0; isl.Jobs = 0; }
            foreach (var b in Buildings) { var isl = IslandOfB(b); if (isl == null) continue; if (b.Type == BType.House) isl.Res += b.Res; else if (!b.Off) isl.Jobs += b.Def.Jobs; }
            AssignWorkers();
            foreach (var isl in Islands) isl.Filled = 0;
            foreach (var b in Buildings) { var d = b.Def; var isl = IslandOfB(b); if (isl != null && d.Jobs > 0 && !b.Off) isl.Filled += b.Staff * d.Jobs; }
            foreach (var isl in Islands) isl.Eff = isl.Jobs != 0 ? Math.Min(1, isl.Filled / isl.Jobs) : 1;
            double income = 0, upkeep = 0;
            foreach (var b in Buildings)
            {
                var d = b.Def; upkeep += d.Upkeep;
                var isl = IslandOfB(b); var st = StockOf(isl);
                if (b.Type == BType.Harbor) { b.Status = b.Merged ? $"Merged into the {isl?.Name} warehouse" : (BerthOf(b.X, b.Y) != null ? "Berth open" : "No deep-water berth"); continue; }
                if (st == null) { b.Eff = 0; b.Status = "No jetty on this island"; continue; }
                if (b.Off) { b.Eff = 0; b.Rate = 0; b.Status = "Paused"; continue; }
                bool road = d.Jobs > 0 && RoadBeside(b); double e = d.Jobs > 0 ? b.Staff * (road ? RoadBonus : 1) : 1; b.Eff = d.Jobs > 0 ? b.Staff : 1; b.Road = road;
                switch (b.Type)
                {
                    case BType.House:
                        {
                            // needs, in order: food (fish, else bought rice), coconuts, a mosque in walking distance; sweet toddy is the treat
                            double need = 0.012 * b.Res * dt, f = PlotFactor(b); var n = b.Need ?? (b.Need = new Needs());
                            n.Food = false; if (st[(int)Good.Fish] >= need) { st[(int)Good.Fish] -= need; n.Food = true; } else if (st[(int)Good.Rice] >= need) { st[(int)Good.Rice] -= need; n.Food = n.Rice = true; }
                            if (n.Food && st[(int)Good.Fish] >= need) n.Rice = false;
                            double cn = 0.006 * b.Res * dt; n.Coco = n.Food && st[(int)Good.Coconut] >= cn; if (n.Coco) st[(int)Good.Coconut] -= cn;
                            n.Faith = MosqueNear(b);
                            double tn = 0.004 * b.Res * dt; n.Toddy = n.Food && st[(int)Good.Toddy] >= tn; if (n.Toddy) st[(int)Good.Toddy] -= tn;
                            double cap = f * (4 + (n.Coco ? 2 : 0) + (n.Faith ? 2 : 0)); b.Cap = cap;
                            if (n.Food) b.Res = b.Res < cap ? Math.Min(cap, b.Res + 0.06 * dt) : Math.Max(cap, b.Res - 0.04 * dt); else b.Res = Math.Max(1, b.Res - 0.04 * dt);
                            b.Tax = n.Food ? Tax * b.Res * (1 + (n.Coco ? .25 : 0) + (n.Faith ? .25 : 0) + (n.Toddy ? .5 : 0)) : 0; income += b.Tax;
                            var miss = new List<string>(); if (!n.Coco) miss.Add("coconuts"); if (!n.Faith) miss.Add("a mosque nearby");
                            b.Status = !n.Food ? "Hungry: needs fish or rice" : miss.Count > 0 ? $"Fed; wants {string.Join(" and ", miss)}" : n.Toddy ? "Content, and enjoying sweet toddy" : "Content (sweet toddy would raise tax)";
                            b.Eff = n.Food ? 1 : 0.3; break;
                        }
                    case BType.Fishery:
                        {
                            int n = CountAround(b.X, b.Y, 2, i => Hgt[i] == -1 && Gt[i] != GT.Pond); int nur = Math.Min(8, CountAround(b.X, b.Y, 3, i => Gt[i] == GT.Kulhi || Gt[i] == GT.Pond || Gt[i] == GT.Seagrass));
                            double r = (0.1 + 0.035 * Math.Min(n, 10) + 0.012 * nur) * e; st[(int)Good.Fish] += r * dt; b.Rate = r;
                            b.Status = (n > 0 ? $"{n} reef tiles in reach" : "No reef in reach, so the catch is small") + (nur > 0 ? $"; {nur} nursery tiles (kulhi or seagrass)" : ""); break;
                        }
                    case BType.Lumber:
                        { int n = CountAround(b.X, b.Y, 2, i => Tree[i] > 0); double r = 0.03 * Math.Min(n, 12) * e; st[(int)Good.Wood] += r * dt; b.Rate = r; b.Status = n > 0 ? $"{n} trees in reach" : "No trees in reach"; if (n == 0) b.Eff = 0; break; }
                    case BType.Quarry:
                        {
                            int hv = Hgt[Idx(b.X, b.Y)], reef = CountAround(b.X, b.Y, 2, i => Hgt[i] == -1); double r = (0.06 + 0.012 * Math.Min(reef, 10) + 0.05 * Math.Max(0, hv - 1)) * e; st[(int)Good.Stone] += r * dt; b.Rate = r;
                            b.Status = reef > 0 ? $"{reef} reef-flat tiles in reach" + (hv >= 2 ? $" and a height {hv} ridge" : "") : hv >= 2 ? $"Height {hv} coral ridge" : "No reef flat in reach, so little stone"; break;
                        }
                    case BType.Grove:
                        { bool near = CountAround(b.X, b.Y, 2, i => Hgt[i] < 0) > 0; double r = 0.12 * (near ? 1.5 : 1) * e; st[(int)Good.Coconut] += r * dt; b.Rate = r; b.Status = near ? "Sea air, +50%" : "Inland palms"; break; }
                    case BType.Toddy:
                        {
                            int p = PalmsNear(b); double r = 0.02 * Math.Min(p, 12) * e; st[(int)Good.Toddy] += r * dt; b.Rate = r;
                            b.Status = p > 0 ? $"{Math.Min(p, 12)} palms to tap{(p > 12 ? " (12 is the most one tapper climbs)" : "")}" : "No coconut palms within 3 tiles"; if (p == 0) b.Eff = 0; break;
                        }
                    case BType.Drying:
                        {
                            double want = 0.2 * e * dt, take = Math.Min(want, st[(int)Good.Fish]), fw = take * 0.2;
                            if (take < want * 0.5 || st[(int)Good.Wood] < fw) { b.Rate = 0; b.Eff = 0; b.Status = take < want * 0.5 ? "Waiting for fish" : "Waiting for timber (firewood)"; break; }
                            st[(int)Good.Fish] -= take; st[(int)Good.Wood] -= fw; st[(int)Good.Hikimas] += take / 2; b.Rate = take / 2 / dt; b.Status = "Boiling, smoking and drying fish"; break;
                        }
                    case BType.Coir:
                        {
                            double want = 0.25 * e * dt, take = Math.Min(want, st[(int)Good.Coconut]);
                            if (take < want * 0.5) { b.Rate = 0; b.Eff = 0; b.Status = "Waiting for coconuts"; break; }
                            st[(int)Good.Coconut] -= take; st[(int)Good.Rope] += take * .4; StatRope += take * .4; b.Rate = take * .4 / dt; b.Status = "Soaking husks and twisting rope"; break;
                        }
                    case BType.Cowrie:
                        { int n = CountAround(b.X, b.Y, 2, i => Hgt[i] == -1); double r = (0.04 + 0.012 * Math.Min(n, 10)) * e; st[(int)Good.Cowrie] += r * dt; b.Rate = r; b.Status = n > 0 ? $"{n} shallow tiles to search" : "Few shallows, so few shells"; break; }
                    case BType.Mosque: b.Status = "Families within " + MosqueWalk + " tiles can pray here"; break;
                    case BType.Palace:
                        {
                            if (AgeDone) { b.Status = "The Age of Cowries is complete"; break; }
                            var items = AgeReady(out bool ok);
                            b.Status = ok ? "The charter is met: the age can advance" : $"{items.Count(it => it.Have >= it.Need)} of {items.Count} conditions met to end the age"; break;
                        }
                }
                if (d.Jobs > 0 && b.Staff <= 0) b.Status += " · no residents within walking distance";
                else if (d.Jobs > 0 && b.Staff < 1) b.Status += $" · {Noise.Round(b.Staff * 100)}% staffed";
                if (b.Wade > 0) b.Status += $" · {Noise.Round(b.Wade * 100)}% of the staff wade over the sandbank at low tide";
            }
            Gold += (income - upkeep) * dt;
            Rate = Rate * 0.9 + (income - upkeep) * 0.1;
            // unrest rises while more than 30% of residents are hungry; at 100% or past the debt limit the atoll is lost
            double pop = 0, hungry = 0; foreach (var b in Buildings) if (b.Type == BType.House) { pop += b.Res; if (b.Status.StartsWith("Hungry")) hungry += b.Res; }
            double share = pop != 0 ? hungry / pop : 0; Unrest = Math.Max(0, Math.Min(100, Unrest + (share > 0.3 ? share * 0.8 : -0.6) * dt));
            if (Over == null) { if (Gold < DebtLimit) GameOver("bankrupt"); else if (Unrest >= 100) GameOver("unrest"); }
            StepBoats(dt); StepTrader(dt);
            if (Random() < dt * 3)
            {
                int x = (int)(Random() * W), y = (int)(Random() * H), i = Idx(x, y);
                if (Tree[i] == 0 && Road[i] == 0 && Bld[i] < 0 && Hgt[i] >= 1 && Hgt[i] <= 4 && CountAround(x, y, 1, j => Tree[j] == 1) >= 2 && Gt[i] == 0) { Tree[i] = 1; MarkTrees(x, y); }
            }
        }

        public event Action<string> Ended;
        public void GameOver(string kind) { Over = kind; Ended?.Invoke(kind); }

        // residents walk to the nearest jobs on their own island, up to WALK tiles; older buildings are staffed first. At low
        // tide they also wade over sandbanks to jobs on another island of the same bank (own island first).
        void AssignWorkers()
        {
            var homes = Buildings.Where(b => b.Type == BType.House).Select(b => new Home { B = b, C = PlotCentre(b), Left = b.Res, Bk = Bank[Idx(b.X, b.Y)] }).ToList();
            bool low = Tide.IsLow(T);
            foreach (var p in Buildings)
            {
                var d = p.Def; p.Wade = 0; if (d.Jobs == 0) continue; p.Staff = 0; if (p.Off) continue;
                var c = PlotCentre(p); double need = d.Jobs, wade = 0; int bk = Bank[Idx(p.X, p.Y)];
                var near = homes.Select(h => (h, dd: Hypot(h.C.x - c.x, h.C.y - c.y))).Where(o => o.h.Left > 0 && (o.h.B.Isl == p.Isl || low && o.h.Bk == bk) && o.dd <= Walk)
                    .OrderBy(o => o.h.B.Isl == p.Isl ? 0 : 1).ThenBy(o => o.dd).ToList();
                foreach (var (h, _) in near) { double t = Math.Min(need, h.Left); h.Left -= t; need -= t; if (h.B.Isl != p.Isl) wade += t; if (need <= 1e-6) break; }
                p.Staff = Math.Max(0, (d.Jobs - need) / d.Jobs); p.Wade = wade / d.Jobs;
            }
        }
        sealed class Home { public Building B; public (double x, double y) C; public double Left; public int Bk; }

        // ------------------------------------------------------------ boats
        void SyncBoats()
        {
            var cap = CapitalHarbor; var capIsl = CapitalIsland; var keep = new List<Boat>();
            foreach (var isl in Islands)
            {
                var hb = isl.Harbor; if (hb == null || cap == null || isl == capIsl) continue;
                var bt = Boats.FirstOrDefault(b => b.Home == hb.Id);
                if (bt == null) { var p = BerthOf(hb.X, hb.Y) ?? (hb.X, hb.Y); bt = new Boat { Home = hb.Id, X = p.x, Y = p.y }; }
                keep.Add(bt);
            }
            foreach (var b in Boats) if (!keep.Contains(b)) { var hb = ById(b.Home); if (hb != null && IslandOfB(hb) == capIsl) Say($"Land bridge made the {IslandName(hb)} boat unnecessary; goods now move overland.", ToastKind.Good); }
            Boats = keep;
        }
        List<(int x, int y)> BfsPath(int sx, int sy, int tx, int ty, int maxH = -2)
        {
            if (!Inb(sx, sy) || Hgt[Idx(sx, sy)] > maxH) return null;
            var prev = new int[W * H]; for (int k = 0; k < prev.Length; k++) prev[k] = -1; var q = new int[W * H]; int qh = 0, qt = 0; int s = Idx(sx, sy), t = Idx(tx, ty);
            q[qt++] = s; prev[s] = s;
            while (qh < qt)
            {
                int c = q[qh++]; if (c == t) break; int cx = c % W, cy = c / W;
                for (int k = 0; k < 4; k++) { int nx = cx + DX[k], ny = cy + DY[k]; if (!Inb(nx, ny)) continue; int ni = Idx(nx, ny); if (prev[ni] < 0 && Hgt[ni] <= maxH) { prev[ni] = c; q[qt++] = ni; } }
            }
            if (prev[t] < 0) return null;
            var path = new List<(int, int)>(); for (int c = t; c != s; c = prev[c]) path.Add((c % W, c / W)); path.Add((sx, sy)); path.Reverse(); return path;
        }
        void PlanBoat(Boat bt)
        {
            var hb = ById(bt.Home); var cap = CapitalHarbor; bt.Ver = TerrainVer;
            var hbB = BerthOf(hb.X, hb.Y); var capB = BerthOf(cap.X, cap.Y);
            if (hbB == null) { bt.State = "blocked"; bt.Why = $"No deep water beside the {IslandName(hb)} jetty"; bt.Path = null; return; }
            if (capB == null) { bt.State = "blocked"; bt.Why = "No deep water beside the capital jetty"; bt.Path = null; return; }
            int sx = (int)Noise.Round(bt.X), sy = (int)Noise.Round(bt.Y);
            if (!Inb(sx, sy) || Hgt[Idx(sx, sy)] > -1)
            {
                if (bt.State == "sail") { Say($"The {IslandName(hb)} boat ran aground and lost its cargo.", ToastKind.Bad, (sx, sy)); bt.Cargo = new double[GoodCount]; }
                sx = hbB.Value.x; sy = hbB.Value.y; bt.X = sx; bt.Y = sy; bt.Out = true;
            }
            var tgt = bt.Out ? capB.Value : hbB.Value;
            // deep water if there is a way through; otherwise over the reef flats, which a dhoni crosses only at high tide
            var p = BfsPath(sx, sy, tgt.x, tgt.y) ?? BfsPath(sx, sy, tgt.x, tgt.y, -1);
            if (p == null) { bt.State = "blocked"; bt.Path = null; bt.Why = $"No water route to {(bt.Out ? "the capital" : IslandName(hb))}. Land or shallows block boats, even at high tide."; return; }
            bt.Path = p; bt.Pi = 0; bt.State = "sail"; bt.Why = "";
        }
        static void Unload(double[] to, Boat bt) { for (int g = 0; g < GoodCount; g++) to[g] += bt.Cargo[g]; bt.Cargo = new double[GoodCount]; }
        static void Load(double[] from, double[] to, Boat bt)
        {
            for (int g = 0; g < GoodCount; g++) { double amt = Math.Min(25, Math.Floor((from[g] - to[g]) / 2)); if (amt > 0) { from[g] -= amt; bt.Cargo[g] = amt; } }
        }
        void StepBoats(double dt)
        {
            if (CapitalHarbor == null) return;
            foreach (var bt in Boats)
            {
                var hb = ById(bt.Home); if (hb == null) continue;
                var home = IslandOfB(hb)?.Harbor; var capH = CapitalIsland?.Harbor; if (home == null || capH == null) continue;
                if (bt.Ver != TerrainVer) PlanBoat(bt);
                if (bt.State != "sail" || bt.Path == null) continue;
                // hold in deep water off a reef flat until the tide is high, then cross without stopping
                if (bt.Pi < bt.Path.Count - 1) { var (wx, wy) = bt.Path[bt.Pi + 1]; int cx = (int)Noise.Round(bt.X), cy = (int)Noise.Round(bt.Y);
                    bt.Wait = Hgt[Idx(wx, wy)] > -2 && Hgt[Idx(cx, cy)] <= -2 && !Tide.IsHigh(T); }
                if (bt.Wait) continue;
                double move = 5 * dt;
                while (move > 0 && bt.Pi < bt.Path.Count - 1)
                {
                    var (nx, ny) = bt.Path[bt.Pi + 1]; double dx = nx - bt.X, dy = ny - bt.Y, d = Hypot(dx, dy);
                    if (d > 0) bt.Ang = Math.Atan2(dy, dx);
                    if (d <= move) { bt.X = nx; bt.Y = ny; bt.Pi++; move -= d; } else { bt.X += dx / d * move; bt.Y += dy / d * move; move = 0; }
                }
                if (bt.Pi >= bt.Path.Count - 1)
                {
                    if (bt.Out) { Unload(capH.Stock, bt); Load(capH.Stock, home.Stock, bt); bt.Out = false; }
                    else { Unload(home.Stock, bt); Load(home.Stock, capH.Stock, bt); bt.Out = true; bt.Trips++; if (Origin[Idx(hb.X, hb.Y)] == 2) StatLinkTrip++; }
                    PlanBoat(bt);
                }
            }
        }

        // ------------------------------------------------------------ trade post: passing ships call at the capital jetty
        // a ship sails in from the open ocean along deep water (crossing the reef only if no channel exists), trades, and leaves
        public TraderShip Trader;
        (int ver, List<(int x, int y)> path) tradeCache = (-1, null);
        public List<(int x, int y)> TradeRoute()
        {
            if (tradeCache.ver == TerrainVer) return tradeCache.path;
            var cap = CapitalHarbor; var bth = cap != null ? BerthOf(cap.X, cap.Y) : null; List<(int, int)> path = null;
            if (bth.HasValue)
                foreach (var minH in new[] { -2, -1 })
                {
                    var prev = new int[W * H]; for (int k = 0; k < prev.Length; k++) prev[k] = -1; var q = new int[W * H]; int qh = 0, qt = 0, s = Idx(bth.Value.x, bth.Value.y), end = -1; q[qt++] = s; prev[s] = s;
                    while (qh < qt)
                    {
                        int c = q[qh++], cx = c % W, cy = c / W;
                        if (Hgt[c] == MinH && Hypot(cx - bth.Value.x, cy - bth.Value.y) > 24 && SeaAt(cx, cy)) { end = c; break; }
                        for (int k = 0; k < 4; k++) { int nx = cx + DX[k], ny = cy + DY[k]; if (!Inb(nx, ny)) continue; int ni = Idx(nx, ny); if (prev[ni] < 0 && Hgt[ni] <= minH) { prev[ni] = c; q[qt++] = ni; } }
                    }
                    if (end >= 0)
                    {
                        path = new List<(int, int)>(); for (int c = end; c != s; c = prev[c]) path.Add((c % W, c / W)); path.Add(bth.Value);
                        // start a little further out so the ship is seen arriving
                        var (x0, y0) = path[0]; var (x1, y1) = path[Math.Min(6, path.Count - 1)];
                        for (int k = 1; k <= 12; k++) { int x = (int)Noise.Round(x0 + (x0 - x1) * k / 2.0), y = (int)Noise.Round(y0 + (y0 - y1) * k / 2.0); if (!Inb(x, y)) break; path.Insert(0, (x, y)); }
                        break;
                    }
                }
            tradeCache = (TerrainVer, path); return path;
        }
        public void DoTrade()
        {
            var st = CapitalIsland?.Harbor?.Stock; if (st == null) return; var T0 = Trade; double gold = 0; var sold = new List<string>();
            foreach (var (g, price) in Price)
            {
                if (!T0.Sell[(int)g]) continue; double keep = g == Good.Rope && !AgeDone ? Age1Rope : g == Good.Fish || g == Good.Coconut ? 20 : 0;
                double n = Math.Floor(Math.Max(0, st[(int)g] - keep)); if (n <= 0) continue; st[(int)g] -= n; gold += n * price; sold.Add($"{n} {GoodLabel[(int)g].ToLowerInvariant()}");
            }
            int rice = 0; if (T0.Rice) { rice = (int)Math.Max(0, Math.Min(Math.Floor(RiceKeep - st[(int)Good.Rice]), Math.Floor((Gold + gold) / RicePrice))); st[(int)Good.Rice] += rice; gold -= rice * RicePrice; }
            Gold += gold; T0.Visits++; T0.Earned += gold + rice * RicePrice; T0.Last = new TradeVisit { Day = Day, Sold = sold.Count > 0 ? string.Join(", ", sold) : "nothing", Rice = rice, Gold = gold };
            var cap = CapitalHarbor; (int, int)? at = cap != null ? (cap.X, cap.Y) : ((int, int)?)null;
            if (sold.Count > 0 || rice > 0)
                Say($"A trading ship called at the capital{(sold.Count > 0 ? ", bought " + string.Join(", ", sold) : "")}{(rice > 0 ? (sold.Count > 0 ? " and" : ",") + $" sold you {rice} rice" : "")}. {(gold >= 0 ? "+" : "−")}{Math.Abs(Noise.Round(gold))} gold.", sold.Count > 0 ? ToastKind.Good : ToastKind.Info, at);
            else Say("A trading ship called at the capital but found nothing to buy. Make coir rope, dried fish or cowries, and let the trade post sell them.", ToastKind.Info, at);
        }
        void StepTrader(double dt)
        {
            var T0 = Trade; if (T0 == null || CapitalHarbor == null) return;
            if (Trader == null)
            {
                if (T < T0.Next) return;
                var p = TradeRoute(); T0.Next = T + TradeEvery;
                if (p == null) { DoTrade(); return; }                      // no water route at all: the ship anchors off the reef and sends boats in
                Trader = new TraderShip { Path = p, X = p[0].x, Y = p[0].y }; return;
            }
            if (Trader.State == "dock") { Trader.Wait -= dt; if (Trader.Wait <= 0) { Trader.Path.Reverse(); Trader.Pi = 0; Trader.State = "out"; } return; }
            double move = 6 * dt; var P = Trader.Path;
            while (move > 0 && Trader.Pi < P.Count - 1)
            {
                var (nx, ny) = P[Trader.Pi + 1]; double dx = nx - Trader.X, dy = ny - Trader.Y, d = Hypot(dx, dy);
                if (d > 0) Trader.Ang = Math.Atan2(dy, dx); if (d <= move) { Trader.X = nx; Trader.Y = ny; Trader.Pi++; move -= d; } else { Trader.X += dx / d * move; Trader.Y += dy / d * move; move = 0; }
            }
            if (Trader.Pi >= P.Count - 1) { if (Trader.State == "in") { DoTrade(); Trader.State = "dock"; Trader.Wait = 6; } else Trader = null; }
        }
        public int TradeIn => (int)Math.Max(0, Math.Ceiling(Trade.Next - T));

        // ------------------------------------------------------------ the age: what Age 1 still needs before it can advance
        public double ContentFishers() { double n = 0; foreach (var b in Buildings) if (b.Type == BType.House && b.Need != null && b.Need.Food && b.Need.Coco && b.Need.Faith) n += b.Res; return n; }
        public List<AgeItem> AgeReady(out bool ok)
        {
            var st = CapitalIsland?.Harbor?.Stock ?? NoStock(); bool Has(BType t) => Buildings.Any(b => b.Type == t);
            var items = new List<AgeItem>
            {
                new AgeItem { Title = $"{Age1Fishers} fishers with all needs met", Have = (int)Math.Floor(ContentFishers()), Need = Age1Fishers },
                new AgeItem { Title = "A coir rope yard", Have = Has(BType.Coir) ? 1 : 0, Need = 1 },
                new AgeItem { Title = "A trading ship has called at the jetty", Have = Trade.Visits > 0 ? 1 : 0, Need = 1 },
                new AgeItem { Title = "The Sultan's palace", Have = Has(BType.Palace) ? 1 : 0, Need = 1 },
                new AgeItem { Title = $"{Age1Gold} gold", Have = (int)Math.Floor(Gold), Need = Age1Gold },
                new AgeItem { Title = $"{Age1Rope} coir rope at the capital", Have = (int)Math.Floor(st[(int)Good.Rope]), Need = Age1Rope },
                new AgeItem { Title = $"{Age1Stone} coral stone at the capital", Have = (int)Math.Floor(st[(int)Good.Stone]), Need = Age1Stone },
            };
            ok = items.All(i => i.Have >= i.Need); return items;
        }
        public void AdvanceAge()
        {
            if (AgeDone) return; AgeReady(out bool ok); if (!ok) { Refuse("The charter is not met yet"); return; }
            var st = CapitalIsland.Harbor.Stock; Gold -= Age1Gold; st[(int)Good.Rope] -= Age1Rope; st[(int)Good.Stone] -= Age1Stone; AgeDone = true;
            var p = Buildings.FirstOrDefault(b => b.Type == BType.Palace);
            Say("The Sultan proclaims the end of the Age of Cowries. Portuguese sails are sighted on the horizon: the Age of Forts and Sail comes in the next update.", ToastKind.Good, p != null ? (p.X, p.Y) : ((int, int)?)null);
        }

        // ------------------------------------------------------------ goals: the Age 1 charter
        public static readonly Goal[] GoalList =
        {
            new Goal { Id = "grow",    Title = "Grow the capital to 30 residents", Hint = "Compounds need fish. Dhoni berths catch most beside reef shallows.", Ok = s => (s.CapitalIsland?.Res ?? 0) >= 30 },
            new Goal { Id = "coconut", Title = "Feed coconuts to a compound", Hint = "Plant a coconut grove on low ground near the water. Fed families with coconuts grow bigger.", Ok = s => s.Buildings.Any(b => b.Type == BType.House && b.Need != null && b.Need.Coco) },
            new Goal { Id = "mosque",  Title = "Build an island mosque", Hint = "Cut coral stone from the reef flat first. Families within 10 tiles get their faith need met.", Ok = s => s.Buildings.Any(b => b.Type == BType.Mosque) },
            new Goal { Id = "trade",   Title = "Sell goods to a passing ship", Hint = "Traders call at the capital jetty every two days. Coir rope, dried fish and cowries sell best.", Ok = s => s.Trade.Earned > 0 },
            new Goal { Id = "rope",    Title = "Twist 40 coir rope", Hint = "A rope yard by the lagoon turns coconuts into rope, the great export of the age.", Ok = s => s.StatRope >= 40 },
            new Goal { Id = "palace",  Title = "Build the Sultan's palace", Hint = "It stands on the capital island and needs 40 coral stone and 30 timber.", Ok = s => s.Buildings.Any(b => b.Type == BType.Palace) },
            new Goal { Id = "fishers", Title = $"House {Age1Fishers} fishers with all needs met", Hint = "Fish or rice, coconuts and a mosque within walking distance.", Ok = s => s.ContentFishers() >= Age1Fishers },
            new Goal { Id = "advance", Title = "Complete the Age of Cowries", Hint = "Open the palace and advance once its list is met.", Ok = s => s.AgeDone },
        };
        /// <summary>Marks newly met charter goals; returns the ones met this call so the HUD can celebrate them.</summary>
        public List<Goal> CheckGoals()
        {
            var met = new List<Goal>();
            foreach (var g in GoalList)
                if (!Goals.Contains(g.Id) && g.Ok(this))
                {
                    Goals.Add(g.Id); met.Add(g); Say($"Charter goal met: {g.Title}", ToastKind.Good);
                    if (GoalList.All(x => Goals.Contains(x.Id))) Say("Every charter goal met. The atoll thrives.", ToastKind.Good);
                }
            return met;
        }
    }
}
