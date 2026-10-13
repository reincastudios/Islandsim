using System.Collections.Generic;
using IslandSim.Core;
using UnityEngine;
using UnityEngine.Rendering;
using static IslandSim.Core.Defs;
using static IslandSim.View.World;

namespace IslandSim.View
{
    /// <summary>
    /// Buildings, boats and the trading ship. Static building parts are baked into world space and merged per material in
    /// 64-tile chunks, so a grown town draws in a few dozen calls instead of one per part per building (as in the web game).
    /// Jetties turn toward their berth, so they stay separate, as do the warning pips, the dhonis and the ship.
    /// </summary>
    public sealed class BuildingView
    {
        readonly Sim S; readonly Transform root;
        sealed class Baked { public string Key; public Dictionary<string, Geo> Parts; public BType Type; public int X, Y; public float Scale; public Vector3 Pip; }
        readonly Dictionary<int, Baked> live = new Dictionary<int, Baked>();
        readonly Dictionary<string, List<GameObject>> batches = new Dictionary<string, List<GameObject>>();
        readonly HashSet<string> dirty = new HashSet<string>();
        int seenVer = -1;

        readonly Dictionary<string, Dictionary<string, Mesh>> meshCache = new Dictionary<string, Dictionary<string, Mesh>>();
        readonly Mesh pipMesh; readonly Material pipMat;
        readonly Dictionary<string, Mesh> boatMeshes, shipMeshes;
        readonly Material sailMat, sailBlocked;

        public BuildingView(Sim s, Transform parent)
        {
            S = s; root = new GameObject("buildings").transform; root.SetParent(parent, false);
            pipMesh = Geo.Sphere(.07f, 10, 8).ToMesh("pip"); pipMat = Mats.Overlay(Lin(0xff5a4f), true, 1); if (pipMat != null) { pipMat.SetFloat("_ZWrite", 1); pipMat.renderQueue = (int)RenderQueue.Geometry + 10; }
            boatMeshes = ToMeshes(Models.Boat(), "boat"); shipMeshes = ToMeshes(Models.Ship(), "ship");
            sailMat = Mats.Own("cloth"); sailBlocked = Mats.Own("cloth"); sailBlocked.SetColor("_BaseColor", Srgb("#ff8a7a"));
        }

        static Dictionary<string, Mesh> ToMeshes(Dictionary<string, Geo> parts, string name)
        {
            var o = new Dictionary<string, Mesh>();
            foreach (var kv in parts) { var m = kv.Value.ToMesh(name + " " + kv.Key); m.RecalculateTangents(); o[kv.Key] = m; }
            return o;
        }

        static string ChunkKey(Building b) { var c = Sim.PlotCentre(b); return ((int)(c.x / CH)) + "," + ((int)(c.y / CH)); }
        static float Rot(Building b) => b.Type == BType.House ? (float)Sim.PlotRot(b) : ((int)(Hash(b.X, b.Y, 1) * 4)) * Mathf.PI / 2;
        static int Seed(Building b) => (int)(Hash(b.X, b.Y, 2) * 997);
        Dictionary<string, Geo> Model(Building b) => Models.Building(b.Type, b.Id == S.Capital, Seed(b), Mathf.Max(b.W, b.D), Mathf.Min(b.W, b.D));

        public void Clear()
        {
            foreach (var l in batches.Values) foreach (var go in l) Destroy(go);
            batches.Clear(); live.Clear(); dirty.Clear(); seenVer = -1;
        }
        static void Destroy(GameObject go) { var mf = go.GetComponent<MeshFilter>(); if (mf != null) Object.Destroy(mf.sharedMesh); Object.Destroy(go); }

        void Sync()
        {
            if (seenVer == S.BuildingsVer) return; seenVer = S.BuildingsVer;
            var ids = new HashSet<int>();
            foreach (var b in S.Buildings)
            {
                ids.Add(b.Id); if (live.ContainsKey(b.Id)) continue;
                var c = Sim.PlotCentre(b); float scale = b.Type == BType.House || b.Def.Unit ? 1 : 1.25f;
                var at = new Vector3((float)c.x, StandY((float)c.x, (float)c.y, TopY(S.Hgt[Sim.Idx(b.X, b.Y)])), (float)c.y);
                var bk = new Baked { Key = ChunkKey(b), Type = b.Type, X = b.X, Y = b.Y, Scale = scale, Pip = at + new Vector3(0, 1.05f * scale, 0) };
                if (b.Type != BType.Harbor)
                {
                    var m = Matrix4x4.Translate(at) * RotY(Rot(b)); bk.Parts = new Dictionary<string, Geo>();
                    foreach (var kv in Model(b)) bk.Parts[kv.Key] = kv.Value.Clone().Apply(m);
                    dirty.Add(bk.Key);
                }
                live[b.Id] = bk;
            }
            var gone = new List<int>(); foreach (var id in live.Keys) if (!ids.Contains(id)) gone.Add(id);
            foreach (var id in gone) { var bk = live[id]; if (bk.Parts != null) dirty.Add(bk.Key); live.Remove(id); }
            foreach (var key in dirty) RebuildBatch(key);
            dirty.Clear();
        }

        void RebuildBatch(string key)
        {
            if (batches.TryGetValue(key, out var old)) foreach (var go in old) Destroy(go);
            var groups = new Dictionary<string, List<Geo>>();
            foreach (var bk in live.Values)
            {
                if (bk.Key != key || bk.Parts == null) continue;
                foreach (var kv in bk.Parts) { if (!groups.TryGetValue(kv.Key, out var l)) groups[kv.Key] = l = new List<Geo>(); l.Add(kv.Value); }
            }
            var list = new List<GameObject>();
            foreach (var kv in groups)
            {
                bool fol = Mats.IsFoliage(kv.Key);
                var mesh = Geo.Merge(kv.Value).ToMesh($"town {key} {kv.Key}"); mesh.RecalculateTangents();
                var go = new GameObject(mesh.name); go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Mats.Get(kv.Key, fol); mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;
                list.Add(go);
            }
            batches[key] = list;
        }

        Dictionary<string, Mesh> HarborMeshes(Building b)
        {
            string k = $"h{b.Id == S.Capital}{Seed(b) % 7}{(Seed(b) >> 2) % 5}";
            if (!meshCache.TryGetValue(k, out var m)) meshCache[k] = m = ToMeshes(Model(b), "jetty");
            return m;
        }

        static void Draw(Mesh mesh, Material mat, Matrix4x4 m, bool shadow = true)
        {
            if (mat == null) return;
            var rp = new RenderParams(mat) { shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows = true };
            Graphics.RenderMesh(rp, mesh, 0, m);
        }

        /// <summary>Keeps buildings in step with the game and draws the moving parts: jetties, pips, boats and the ship.</summary>
        public void Update(float t)
        {
            Sync();
            foreach (var b in S.Buildings)
            {
                if (!live.TryGetValue(b.Id, out var bk)) continue;
                if (b.Type == BType.Harbor)
                {
                    var h = S.BerthOf(b.X, b.Y); float fx = h.HasValue ? h.Value.x - b.X : 0, fz = h.HasValue ? h.Value.y - b.Y : 1;
                    var c = Sim.PlotCentre(b);
                    var m = U(Matrix4x4.Translate(new Vector3((float)c.x, TopY(S.Hgt[Sim.Idx(b.X, b.Y)]), (float)c.y)) * RotY(Mathf.Atan2(fx, fz)));
                    foreach (var kv in HarborMeshes(b)) Draw(kv.Value, Mats.Get(kv.Key), m);
                }
                bool bad = b.Type == BType.Harbor ? (!b.Merged && !S.BerthOf(b.X, b.Y).HasValue) : !b.Off && (b.Eff < 0.5 || b.Status.StartsWith("Hungry") || b.Status.Contains("No "));
                if (bad) Draw(pipMesh, pipMat, U(Matrix4x4.Translate(bk.Pip + new Vector3(0, Mathf.Sin(t * 3) * .05f * bk.Scale, 0)) * Sc(bk.Scale)), false);
            }
            // trading ship
            var tr = S.Trader;
            if (tr != null)
            {
                var m = U(Matrix4x4.Translate(new Vector3((float)tr.X + .5f, SeaY + .05f + Mathf.Sin(t * 1.4f) * .04f, (float)tr.Y + .5f)) * RotX(Mathf.Sin(t * 1.1f) * .04f) * RotY(-(float)tr.Ang));
                foreach (var kv in shipMeshes) Draw(kv.Value, Mats.Get(kv.Key), m);
            }
            foreach (var bt in S.Boats)
            {
                var m = U(Matrix4x4.Translate(new Vector3((float)bt.X + .5f, SeaY + .03f + Mathf.Sin(t * 2 + bt.Home) * .03f, (float)bt.Y + .5f)) * RotX(Mathf.Sin(t * 1.7f + bt.Home) * .05f) * RotY(-(float)bt.Ang));
                foreach (var kv in boatMeshes) Draw(kv.Value, kv.Key == "sail" ? (bt.State == "blocked" ? sailBlocked : sailMat) : Mats.Get(kv.Key), m);
            }
        }
    }
}
