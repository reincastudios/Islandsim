using System;
using System.Collections.Generic;
using System.Linq;
using static IslandSim.Core.Defs;

namespace IslandSim.Core
{
    /// <summary>
    /// Saves in the browser's own format (version 7, version 6 still loads), so a save copied out of the web game loads here
    /// and the other way round. Tile layers are run-length packed and base64'd, as in the web game.
    /// </summary>
    public sealed partial class Sim
    {
        /// <summary>The camera, saved alongside the game; the view fills it before a save and reads it after a load.</summary>
        public Dictionary<string, object> Cam = new Dictionary<string, object>();

        public static string Pack(byte[] a, int off = 0)
        {
            var o = new List<byte>();
            for (int i = 0; i < a.Length;)
            {
                int v = (a[i] + off) & 255, n = 1;
                while (i + n < a.Length && n < 255 && ((a[i + n] + off) & 255) == v) n++;
                o.Add((byte)v); o.Add((byte)n); i += n;
            }
            return Convert.ToBase64String(o.ToArray());
        }
        public static string Pack(sbyte[] a, int off) { var b = new byte[a.Length]; for (int i = 0; i < a.Length; i++) b[i] = unchecked((byte)a[i]); return Pack(b, off); }
        public static byte[] Unpack(string str, int len, int off = 0)
        {
            var b = Convert.FromBase64String(str); var a = new byte[len]; int k = 0;
            for (int i = 0; i + 1 < b.Length; i += 2) { int n = b[i + 1]; if (k + n > len) throw new FormatException("bad save"); byte v = unchecked((byte)(b[i] - off)); for (int j = 0; j < n; j++) a[k + j] = v; k += n; }
            if (k != len) throw new FormatException("bad save");
            return a;
        }

        static Dictionary<string, object> GoodsObj(double[] st) { var d = new Dictionary<string, object>(); for (int g = 0; g < GoodCount; g++) d[GoodIds[g]] = st[g]; return d; }

        public Dictionary<string, object> Snapshot()
        {
            var sell = new Dictionary<string, object>(); foreach (var (g, _) in Price) sell[GoodIds[(int)g]] = Trade.Sell[(int)g];
            var trade = new Dictionary<string, object> { ["sell"] = sell, ["rice"] = Trade.Rice, ["next"] = Trade.Next, ["visits"] = (double)Trade.Visits, ["earned"] = Trade.Earned,
                ["last"] = Trade.Last == null ? null : new Dictionary<string, object> { ["day"] = (double)Trade.Last.Day, ["sold"] = Trade.Last.Sold, ["rice"] = (double)Trade.Last.Rice, ["gold"] = Trade.Last.Gold } };
            var blds = Buildings.Select(b =>
            {
                var o = new Dictionary<string, object> { ["id"] = (double)b.Id, ["type"] = b.Def.Id, ["x"] = (double)b.X, ["y"] = (double)b.Y, ["w"] = (double)b.W, ["d"] = (double)b.D, ["res"] = b.Res, ["stock"] = b.Stock != null ? GoodsObj(b.Stock) : null };
                if (b.Off) o["off"] = true;
                return (object)o;
            }).ToList();
            var goals = new Dictionary<string, object>(); foreach (var g in Goals) goals[g] = true;
            var names = new Dictionary<string, object>(); foreach (var kv in Names) names[kv.Key] = kv.Value;
            return new Dictionary<string, object>
            {
                ["v"] = 7.0, ["dims"] = new List<object> { (double)W, (double)H }, ["names"] = names, ["age"] = (double)Age, ["ageDone"] = AgeDone, ["trade"] = trade,
                ["h"] = Pack(Hgt, 128), ["tree"] = Pack(Tree), ["road"] = Pack(Road), ["gt"] = Pack(Gt), ["buildings"] = blds,
                ["gold"] = Gold, ["t"] = T, ["nextId"] = (double)NextId, ["stats"] = new Dictionary<string, object> { ["rope"] = StatRope, ["linkTrip"] = (double)StatLinkTrip },
                ["goals"] = goals, ["capital"] = (double)Capital, ["unrest"] = Unrest, ["origin"] = Pack(Origin), ["dug"] = Pack(Dug), ["cam"] = new Dictionary<string, object>(Cam),
            };
        }
        public string SaveJson() => Json.Write(Snapshot());

        /// <summary>Loads a save; throws if it is from an older version or another map size, leaving the game as it was.</summary>
        public void Restore(Dictionary<string, object> d)
        {
            double v = d.Get("v", 0.0); var dims = d.Arr("dims");
            if ((v != 6 && v != 7) || d.Get("h", (string)null) == null || dims == null || dims.Count < 2 || Json.Num(dims[0]) != W || Json.Num(dims[1]) != H) throw new FormatException("old save");
            var h = Unpack(d.Get("h", ""), W * H, 128); var hs = new sbyte[W * H]; for (int i = 0; i < hs.Length; i++) hs[i] = unchecked((sbyte)h[i]);
            var tree = Unpack(d.Get("tree", ""), W * H); var road = Unpack(d.Get("road", ""), W * H); var gt = Unpack(d.Get("gt", ""), W * H); var origin = Unpack(d.Get("origin", ""), W * H);
            var dugS = d.Get("dug", ""); var dug = dugS != "" ? Unpack(dugS, W * H) : new byte[W * H];   // saves from before dug tiles were recorded: none
            var blds = new List<Building>(); var bld = new int[W * H]; for (int i = 0; i < bld.Length; i++) bld[i] = -1;
            // saves from before Age 1: sugar fields become coconut groves, distilleries toddy tappers, and rum and sugar are gone
            foreach (var o in d.Arr("buildings") ?? new List<object>())
            {
                var b0 = o as Dictionary<string, object>; if (b0 == null) continue;
                var id = b0.Get("type", ""); if (id == "plantation") id = "grove"; else if (id == "distillery") id = "toddy";
                if (!TryType(id, out var type)) continue;
                var b = new Building { Id = (int)b0.Get("id", 0.0), Type = type, X = (int)b0.Get("x", 0.0), Y = (int)b0.Get("y", 0.0), W = (int)b0.Get("w", 1.0), D = (int)b0.Get("d", 1.0), Res = b0.Get("res", 0.0), Off = b0.Get("off", false) };
                var so = b0.Obj("stock"); if (so != null) { b.Stock = NoStock(); for (int g = 0; g < GoodCount; g++) b.Stock[g] = so.Get(GoodIds[g], 0.0); }
                else if (type == BType.Harbor) b.Stock = NoStock();
                blds.Add(b); for (int ty = b.Y; ty < b.Y + b.D; ty++) for (int tx = b.X; tx < b.X + b.W; tx++) bld[Idx(tx, ty)] = b.Id;
            }
            // everything parsed: now replace the game
            Hgt = hs; Tree = tree; Road = road; Gt = gt; Origin = origin; Dug = dug; Bld = bld; Buildings = blds;
            Gold = d.Get("gold", 0.0); T = d.Get("t", 0.0); NextId = (int)d.Get("nextId", 1.0); Capital = (int)d.Get("capital", 0.0); Unrest = d.Get("unrest", 0.0);
            var stats = d.Obj("stats"); StatRope = stats.Get("rope", 0.0); StatLinkTrip = (int)stats.Get("linkTrip", 0.0);
            Goals = new HashSet<string>(); var go = d.Obj("goals"); if (go != null) foreach (var kv in go) if (kv.Value is bool x && x) Goals.Add(kv.Key);
            Names = new Dictionary<string, string>(); var nm = d.Obj("names"); if (nm != null) foreach (var kv in nm) if (kv.Value is string s) Names[kv.Key] = s;
            Age = (int)d.Get("age", 1.0); AgeDone = d.Get("ageDone", false); Over = null;
            Trade = new TradeState(); var tr = d.Obj("trade");
            if (tr != null)
            {
                var sell = tr.Obj("sell"); if (sell != null) foreach (var (g, _) in Price) Trade.Sell[(int)g] = sell.Get(GoodIds[(int)g], Trade.Sell[(int)g]);
                Trade.Rice = tr.Get("rice", true); Trade.Next = tr.Get("next", 45.0); Trade.Visits = (int)tr.Get("visits", 0.0); Trade.Earned = tr.Get("earned", 0.0);
                var last = tr.Obj("last"); if (last != null) Trade.Last = new TradeVisit { Day = (int)last.Get("day", 1.0), Sold = last.Get("sold", "nothing"), Rice = (int)last.Get("rice", 0.0), Gold = last.Get("gold", 0.0) };
            }
            Cam = d.Obj("cam") ?? new Dictionary<string, object>();
            Boats.Clear(); Trader = null; tradeCache = (-1, null); undoStack.Clear(); alerted.Clear(); CurStroke = null;
            ComputeIslands(); TerrainVer++; MarkAll();
        }
        public void LoadJson(string json)
        {
            if (!(Json.Parse(json) is Dictionary<string, object> d)) throw new FormatException("not a save");
            Restore(d);
        }
    }
}
