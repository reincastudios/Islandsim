using System.Collections.Generic;
using IslandSim.Core;
using UnityEngine;

namespace IslandSim.View
{
    /// <summary>Keeps building and boat GameObjects in step with the simulation, and draws the cursor and placement ghost.</summary>
    public sealed class BuildingView
    {
        sealed class BObj { public GameObject Go; public GameObject Pip; public BType Type; }
        sealed class BoatObj { public GameObject Go; public MeshRenderer Sail; public Vector3 Pos; public float Yaw; }

        readonly Sim sim;
        readonly Transform root;
        readonly Dictionary<int, BObj> bObjs = new Dictionary<int, BObj>();
        readonly Dictionary<int, BoatObj> boatObjs = new Dictionary<int, BoatObj>();
        readonly Dictionary<BType, GameObject> ghosts = new Dictionary<BType, GameObject>();
        readonly Material ghostOk, ghostBad, sailOk, sailBad;
        readonly GameObject cursor, range;
        readonly Material cursorMat;

        public BuildingView(Sim sim, Transform parent)
        {
            this.sim = sim;
            root = new GameObject("Buildings").transform; root.SetParent(parent, false);
            ghostOk = Mats.Overlay(World.Hex("#7ef0d8") * new Color(1, 1, 1, .6f));
            ghostBad = Mats.Overlay(World.Hex("#ff6b6b") * new Color(1, 1, 1, .6f));
            sailOk = Mats.Lit(World.Hex("#f6efe0"), .1f); sailBad = Mats.Lit(World.Hex("#ff7a6b"), .1f);

            cursorMat = Mats.Overlay(new Color(.95f, .9f, .77f, .9f), .06f, true);
            cursor = Overlay("Cursor", cursorMat);
            range = Overlay("Range", Mats.Overlay(new Color(1f, .96f, .85f, .16f), 0, true));
            range.transform.localScale = new Vector3(5, 1, 5);
        }

        GameObject Overlay(string name, Material m)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Quad(1);
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            go.SetActive(false);
            return go;
        }

        public void Sync()
        {
            var live = new HashSet<int>();
            foreach (var b in sim.Buildings)
            {
                live.Add(b.Id);
                if (!bObjs.TryGetValue(b.Id, out var o) || o.Type != b.Type)
                {
                    if (o != null) Object.Destroy(o.Go);
                    var go = Models.Building(b.Type, b.Id == sim.Capital, (int)(Noise.Hash2(b.X, b.Y, 2) * 997));
                    go.transform.SetParent(root, false);
                    go.transform.position = World.TileCentre(sim, b.X, b.Y);
                    go.transform.rotation = Quaternion.Euler(0, (int)(Noise.Hash2(b.X, b.Y, 1) * 4) * 90, 0);
                    o = new BObj { Go = go, Type = b.Type, Pip = Models.AlertPip(go.transform) };
                    bObjs[b.Id] = o;
                }
                if (b.Type == BType.Harbor)
                {
                    float fx = 0, fz = -1;
                    if (sim.BerthOf(b.X, b.Y, out var bx, out var by)) { fx = bx - b.X; fz = -(by - b.Y); }
                    o.Go.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(fx, fz) * Mathf.Rad2Deg - 45, 0);
                }
                bool bad = b.Type == BType.Harbor ? (!b.Merged && !sim.BerthOf(b.X, b.Y, out _, out _))
                                                  : (b.Eff < 0.5f || b.Status.StartsWith("Hungry") || b.Status.StartsWith("No "));
                if (o.Pip.activeSelf != bad) o.Pip.SetActive(bad);
                if (b.Type == BType.House) o.Go.transform.localScale = new Vector3(1, b.Res > 4.2f ? 1.3f : 1, 1);
            }
            var dead = new List<int>();
            foreach (var kv in bObjs) if (!live.Contains(kv.Key)) { Object.Destroy(kv.Value.Go); dead.Add(kv.Key); }
            foreach (var id in dead) bObjs.Remove(id);
        }

        /// <summary>Drop every building and boat object (after a new game or a load, when ids are reused).</summary>
        public void Clear()
        {
            foreach (var o in bObjs.Values) Object.Destroy(o.Go);
            foreach (var o in boatObjs.Values) Object.Destroy(o.Go);
            bObjs.Clear(); boatObjs.Clear();
            sim.DirtyBuildings = true;
        }

        /// <summary>Re-seat buildings after terraforming changed tile heights next to them.</summary>
        public void Reseat()
        {
            foreach (var b in sim.Buildings) if (bObjs.TryGetValue(b.Id, out var o)) o.Go.transform.position = World.TileCentre(sim, b.X, b.Y);
        }

        public void UpdateBoats(float dt, float t)
        {
            var live = new HashSet<int>();
            foreach (var bt in sim.Boats)
            {
                live.Add(bt.Home);
                var target = World.Pos(bt.X + .5f, bt.Y + .5f, World.WaterY + .05f);
                if (!boatObjs.TryGetValue(bt.Home, out var o))
                {
                    var go = Models.Boat(out var sail); go.transform.SetParent(root, false);
                    o = new BoatObj { Go = go, Sail = sail, Pos = target, Yaw = bt.Ang * Mathf.Rad2Deg };
                    boatObjs[bt.Home] = o;
                }
                // the sim ticks at 10 Hz; ease toward its position so motion stays smooth
                if ((o.Pos - target).sqrMagnitude > 9) o.Pos = target; else o.Pos = Vector3.Lerp(o.Pos, target, Mathf.Clamp01(dt * 12));
                o.Yaw = Mathf.LerpAngle(o.Yaw, bt.Ang * Mathf.Rad2Deg, Mathf.Clamp01(dt * 8));
                o.Go.transform.position = o.Pos + Vector3.up * Mathf.Sin(t * 2 + bt.Home) * .03f;
                o.Go.transform.rotation = Quaternion.Euler(0, o.Yaw, Mathf.Sin(t * 1.7f + bt.Home) * 3f);
                var m = bt.State == BoatState.Blocked ? sailBad : sailOk;
                if (o.Sail.sharedMaterial != m) o.Sail.sharedMaterial = m;
            }
            var dead = new List<int>();
            foreach (var kv in boatObjs) if (!live.Contains(kv.Key)) { Object.Destroy(kv.Value.Go); dead.Add(kv.Key); }
            foreach (var id in dead) boatObjs.Remove(id);
        }

        GameObject Ghost(BType t)
        {
            if (!ghosts.TryGetValue(t, out var g))
            {
                g = Models.Building(t, false, 0, ghostOk); g.transform.SetParent(root, false);
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) r.receiveShadows = false;
                ghosts[t] = g;
            }
            return g;
        }

        public void HideHover()
        {
            foreach (var g in ghosts.Values) if (g.activeSelf) g.SetActive(false);
            cursor.SetActive(false); range.SetActive(false);
        }

        public void ShowGhost(BType t, int x, int y, bool ok)
        {
            float gy = Mathf.Max(World.WaterY, World.TopY(sim.Hgt[Sim.Idx(x, y)]));
            var g = Ghost(t); g.SetActive(true);
            g.transform.position = World.Pos(x + .5f, y + .5f, gy);
            var m = ok ? ghostOk : ghostBad;
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) if (r.sharedMaterial != m) r.sharedMaterial = m;
            if (t == BType.Fishery || t == BType.Lumber || t == BType.Plantation)
            {
                range.SetActive(true); range.transform.position = World.Pos(x + .5f, y + .5f, gy + .05f);
            }
        }

        public void ShowCursor(int x, int y, int radius, Color c)
        {
            float gy = World.WaterY;
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (Sim.Inb(x + dx, y + dy)) gy = Mathf.Max(gy, World.TopY(sim.Hgt[Sim.Idx(x + dx, y + dy)]));
            cursor.SetActive(true);
            cursor.transform.position = World.Pos(x + .5f, y + .5f, gy + .04f);
            cursor.transform.localScale = new Vector3(2 * radius + 1, 1, 2 * radius + 1);
            cursorMat.SetColor("_Color", c);
            cursorMat.SetFloat("_Frame", .06f / (2 * radius + 1));
        }
    }
}
