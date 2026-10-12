using System;
using System.Collections.Generic;
using static IslandSim.Core.Defs;

namespace IslandSim.Core
{
    public struct TerraResult { public bool Ok, Noop, Coast; public string Why; public double Gold, Stone; }

    /// <summary>What one pass of the brush would cost here, and which tiles it would refuse (drawn red under the ring).</summary>
    public sealed class BrushPreview { public double Gold, Stone; public int N; public readonly bool[] Bad = new bool[11 * 11]; }

    /// <summary>One press of the mouse: the tiles already done, the hold timer, and each tile's first state for Ctrl+Z.</summary>
    public sealed class Stroke
    {
        public readonly HashSet<int> Done = new HashSet<int>();
        public int Last = -1; public double Hold, Gold, Stone; public double[] Src;
        public Dictionary<int, TileState> Undo;
    }
    public struct TileState { public sbyte H; public byte Tree, Gt, Road, O; }

    public sealed partial class Sim
    {
        // ------------------------------------------------------------ the tool in hand
        public Tool Tool = Tool.Inspect; public BType BuildType;
        /// <summary>Brush size, 1–9 tiles across.</summary>
        public int BrushSize = 1;
        /// <summary>The height Level flattens to (one of Defs.Levels).</summary>
        public int LevelTo = 1;
        public Stroke CurStroke;
        /// <summary>What Plant puts down: 2 coconut palm, 1 shade tree, 3 mangrove (the Tree values).</summary>
        public byte PlantKind = 2;
        public const int PlantGold = 3;
        public static bool IsTerra(Tool t) => t == Tool.Raise || t == Tool.Lower || t == Tool.Flatten || t == Tool.Smooth;

        // one round brush, 1–9 tiles across: the tiles whose centres sit within half the size of the hovered tile's centre
        bool InBrush(int dx, int dy) => dx * dx + dy * dy <= (BrushSize / 2.0) * (BrushSize / 2.0) + .01;
        public void ForBrush(int cx, int cy, Action<int, int> fn)
        {
            int r = BrushSize >> 1;
            for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++) if (InBrush(dx, dy) && Inb(cx + dx, cy + dy)) fn(cx + dx, cy + dy);
        }
        public void SetBrush(int n) => BrushSize = Math.Max(1, Math.Min(9, n));

        // ------------------------------------------------------------ terraforming
        // dry = only check and price the change; while a stroke is open each tile's first state is kept for Ctrl+Z
        public TerraResult Terraform(int x, int y, Tool mode, int target, bool dry = false)
        {
            if (!Inb(x, y)) return new TerraResult();
            int i = Idx(x, y), cur = Hgt[i], to = cur;
            if (mode == Tool.Raise) to = Math.Min(MaxH, cur + 1); else if (mode == Tool.Lower) to = Math.Max(MinH, cur - 1); else if (mode == Tool.Flatten || mode == Tool.Smooth) to = target;
            if (to == cur) return new TerraResult { Ok = true, Noop = true };
            var b = BAt(x, y); if (b != null) return new TerraResult { Why = $"Demolish the {b.Def.Name} before reshaping its tile" };
            if (Gt[i] == GT.Bird) return new TerraResult { Why = "Birds nest here. The island is protected." };
            if (cur <= -2 && SeaAt(x, y)) return new TerraResult { Why = "Open ocean: too deep to dredge or build out into" };
            int steps = Math.Abs(to - cur); bool up = to > cur;
            var src = NearestHarborIsland(x, y); var st = src?.Harbor?.Stock; double gold = 4 * steps;
            if (Gold < gold) return new TerraResult { Why = "Not enough gold to move earth" };
            if (up && (st == null || st[(int)Good.Stone] < steps)) return new TerraResult { Why = $"Need {steps} stone at {(src != null ? src.Name : "a jetty")} to raise land" };
            if (dry) return new TerraResult { Ok = true, Gold = gold, Stone = up ? steps : -steps * 0.5 };
            var s = CurStroke;
            if (s?.Undo != null && !s.Undo.ContainsKey(i)) s.Undo[i] = new TileState { H = Hgt[i], Tree = Tree[i], Gt = Gt[i], Road = Road[i], O = Origin[i] };
            Gold -= gold; SpendGold += gold; if (up) { st[(int)Good.Stone] -= steps; SpendStone += steps; } else if (st != null) { st[(int)Good.Stone] += steps * 0.5; SpendStone -= steps * 0.5; }
            if (s != null) { s.Gold += gold; if (st != null) { s.Src = st; s.Stone += up ? steps : -steps * 0.5; } }
            bool wasLand = cur >= 0, isLand = to >= 0;
            Hgt[i] = (sbyte)to; Tree[i] = 0; if (to < 0) Road[i] = 0;
            if (Gt[i] != GT.Bird) Gt[i] = 0;                    // filling a kulhi or a taro field turns it into plain ground
            if (wasLand != isLand) Origin[i] = 255;
            TerrainVer++; MarkShore(x, y); MarkTrees(x, y);
            return new TerraResult { Ok = true, Coast = wasLand != isLand };
        }

        // smoothing: each tile steps once toward the rounded mean of its 3×3 neighbourhood (worked out before any tile moves)
        List<(int x, int y, int to)> SmoothTargets(int cx, int cy)
        {
            var o = new List<(int, int, int)>();
            ForBrush(cx, cy, (x, y) =>
            {
                double sum = 0; int n = 0;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int nx = x + dx, ny = y + dy; if (Inb(nx, ny)) { sum += Hgt[Idx(nx, ny)]; n++; } }
                int cur = Hgt[Idx(x, y)], m = (int)Noise.Round(sum / n); if (m != cur) o.Add((x, y, cur + Math.Sign(m - cur)));
            });
            return o;
        }
        List<(int x, int y, int to)> BrushJobs(int cx, int cy)
        {
            if (Tool == Tool.Smooth) return SmoothTargets(cx, cy);
            var jobs = new List<(int, int, int)>(); ForBrush(cx, cy, (x, y) => jobs.Add((x, y, LevelTo))); return jobs;
        }

        // Ctrl+Z: undo the last terraforming stroke within 10 seconds, with the gold and stone given back
        sealed class UndoEntry { public Dictionary<int, TileState> Undo; public double Gold, Stone, At; public double[] Src; }
        readonly List<UndoEntry> undoStack = new List<UndoEntry>();
        public void UndoTerraform()
        {
            double now = Now(); while (undoStack.Count > 0 && now - undoStack[undoStack.Count - 1].At > 10) undoStack.Clear();
            if (undoStack.Count == 0) { Say("Nothing to undo. Only reshaping from the last 10 seconds can be undone."); return; }
            var u = undoStack[undoStack.Count - 1]; undoStack.RemoveAt(undoStack.Count - 1);
            bool coast = false; int x0 = W, y0 = H, x1 = 0, y1 = 0;
            foreach (var kv in u.Undo)
            {
                int i = kv.Key, x = i % W, y = i / W; var o = kv.Value; if (Bld[i] >= 0) continue; if ((Hgt[i] >= 0) != (o.H >= 0)) coast = true;
                Hgt[i] = o.H; Tree[i] = o.Tree; Gt[i] = o.Gt; Road[i] = o.Road; Origin[i] = o.O; MarkShore(x, y);
                x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y);
            }
            Gold += u.Gold; if (u.Src != null) u.Src[(int)Good.Stone] += u.Stone; TerrainVer++; MarkTrees(x0, y0, x1, y1);
            if (coast) ComputeIslands();
            Say($"Undone: {u.Undo.Count} tile{(u.Undo.Count > 1 ? "s" : "")} put back, {Noise.Round(u.Gold)} gold returned.", ToastKind.Good);
        }

        // ------------------------------------------------------------ using the tool
        public void BeginStroke(int x, int y)
        {
            SpendGold = SpendStone = 0;
            CurStroke = new Stroke { Undo = IsTerra(Tool) ? new Dictionary<int, TileState>() : null };
            if (Inb(x, y)) { CurStroke.Last = Idx(x, y); ApplyAt(x, y, true); }
        }
        /// <summary>The pointer moved onto another tile while the button is held.</summary>
        public void DragStroke(int x, int y)
        {
            if (CurStroke == null || !Inb(x, y)) return; int k = Idx(x, y);
            if (k != CurStroke.Last) { CurStroke.Last = k; CurStroke.Hold = 0; ApplyAt(x, y, false); }
        }
        // holding the mouse still keeps raising, lowering or smoothing, a step every 0.3 seconds
        public void RepeatStroke(double dt, int hx, int hy, bool hovering)
        {
            if (CurStroke == null || !hovering || !(Tool == Tool.Raise || Tool == Tool.Lower || Tool == Tool.Smooth)) return;
            CurStroke.Hold += dt; if (CurStroke.Hold < .3) return; CurStroke.Hold = 0; CurStroke.Done.Clear(); ApplyAt(hx, hy, false);
        }
        public void EndStroke()
        {
            var s = CurStroke;
            if (s?.Undo != null && s.Undo.Count > 0) undoStack.Add(new UndoEntry { Undo = s.Undo, Gold = s.Gold, Stone = s.Stone, Src = s.Src, At = Now() });
            if (undoStack.Count > 20) undoStack.RemoveAt(0);
            CurStroke = null;
        }

        void ApplyAt(int tx, int ty, bool first)
        {
            if (Tool == Tool.Build) { if (first) Place(BuildType, tx, ty); return; }
            if (Tool == Tool.Demolish) { if (BAt(tx, ty) != null) { if (first) Demolish(tx, ty); } else RemoveRoad(tx, ty); return; }
            if (Tool == Tool.Inspect) return;
            if (Tool == Tool.Plant)
            {
                string err = null; int n = 0;
                ForBrush(tx, ty, (x, y) => { var r = PlantTree(x, y, PlantKind); if (r.Ok && !r.Noop) n++; else if (!r.Ok && !string.IsNullOrEmpty(r.Why)) err = r.Why; });
                if (n == 0 && err != null) Refuse(err); return;
            }
            if (Tool == Tool.Road) { string err = null; ForBrush(tx, ty, (x, y) => { var r = BuildRoad(x, y); if (!r.Ok && !string.IsNullOrEmpty(r.Why)) err = r.Why; }); if (err != null) Refuse(err); return; }
            bool coast = false; string why = null;
            foreach (var (x, y, target) in BrushJobs(tx, ty))
            {
                int k = Idx(x, y); if (CurStroke.Done.Contains(k)) continue;
                var r = Terraform(x, y, Tool, target);
                if (r.Ok) { if (!r.Noop) CurStroke.Done.Add(k); if (r.Coast) coast = true; } else if (!string.IsNullOrEmpty(r.Why)) why = r.Why;
            }
            if (why != null) Refuse(why);
            if (coast)
            {
                int before = Islands.Count; ComputeIslands();
                if (Islands.Count != before) Say(Islands.Count < before ? "Islands joined by land. Their warehouses now share stock." : "Land split apart. Each piece needs its own jetty.", Islands.Count < before ? ToastKind.Good : ToastKind.Info);
            }
        }

        // ------------------------------------------------------------ planting
        /// <summary>Puts a tree on one tile: palms from the beach up, shade trees on land, mangroves in a kulhi. Tiles that already hold a tree are left alone (Noop). dry = only check and price it.</summary>
        public TerraResult PlantTree(int x, int y, byte kind, bool dry = false)
        {
            if (!Inb(x, y)) return new TerraResult(); int i = Idx(x, y), h = Hgt[i];
            if (Tree[i] != 0) return new TerraResult { Ok = true, Noop = true };
            if (Bld[i] >= 0 || Road[i] != 0) return new TerraResult { Noop = true };      // buildings and roads simply keep their tile
            if (Gt[i] == GT.Bird) return new TerraResult { Why = "Birds nest here. The island is protected." };
            if (kind == 3)
            {
                if (Gt[i] != GT.Kulhi || h < 0) return new TerraResult { Why = "Mangroves only take root in a kulhi, the island's wetland" };
            }
            else
            {
                if (h < 0) return new TerraResult { Why = "Trees need land" };
                if (kind == 1 && h < 1) return new TerraResult { Why = "Shade trees need land above the beach. Palms grow on sand." };
                if (Gt[i] == GT.Kulhi) return new TerraResult { Why = "A kulhi takes mangroves, not dry-land trees" };
                if (Gt[i] == GT.Pond || Gt[i] == GT.Taro) return new TerraResult { Why = $"Not in the {(Gt[i] == GT.Pond ? "pond" : "taro field")}" };
            }
            if (Gold < PlantGold) return new TerraResult { Why = $"Need {PlantGold} gold per tree" };
            if (dry) return new TerraResult { Ok = true, Gold = PlantGold };
            Gold -= PlantGold; SpendGold += PlantGold; Tree[i] = kind; MarkTrees(x, y);
            return new TerraResult { Ok = true, Gold = PlantGold };
        }

        public BrushPreview PreviewBrush(int tx, int ty)
        {
            if (Tool == Tool.Plant)
            {
                var q = new BrushPreview();
                ForBrush(tx, ty, (x, y) => { var r = PlantTree(x, y, PlantKind, true); if (r.Noop) return; if (r.Ok) { q.Gold += r.Gold; q.N++; } else q.Bad[(y - ty + 5) * 11 + (x - tx + 5)] = true; });
                return q;
            }
            if (!IsTerra(Tool)) return null; var p = new BrushPreview();
            foreach (var (x, y, target) in BrushJobs(tx, ty))
            {
                var r = Terraform(x, y, Tool, target, true); if (r.Noop) continue;
                if (r.Ok) { p.Gold += r.Gold; p.Stone += r.Stone; p.N++; } else p.Bad[(y - ty + 5) * 11 + (x - tx + 5)] = true;
            }
            return p;
        }
    }
}
