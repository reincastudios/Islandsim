using System;
using System.Collections.Generic;
using System.IO;
using IslandSim.Core;
using UnityEngine;
using static IslandSim.Core.Defs;

namespace IslandSim.View
{
    /// <summary>
    /// The game, as the web version's main loop runs it: the rules tick ten times a second (times the game speed), the
    /// HUD refreshes four times a second, alerts every three, the autosave every minute; each frame rebuilds whatever
    /// the rules changed and draws the atoll. Pointer and keys work as in the browser.
    /// </summary>
    public sealed class IslandGame : MonoBehaviour
    {
        public Sim S { get; private set; }
        public RtsCamera Rig { get; private set; }
        public Atmosphere Atmos { get; private set; }
        public int Speed { get; private set; } = 1;
        Hud hud; TerrainView terrain; WaterView water; VegetationView veg; BuildingView buildings; CursorView cursor;
        Transform world;

        public static readonly string[] Slots = { "islandsim.save1", "islandsim.save2", "islandsim.save3" };
        public const string Auto = "islandsim.auto";

        void Awake()
        {
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 1;
            world = new GameObject("atoll").transform; world.SetParent(transform, false);
            var cam = Camera.main;
            if (cam == null) { var cg = new GameObject("camera") { tag = "MainCamera" }; cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); }
            Rig = new RtsCamera(cam);
            Atmos = new Atmosphere(world);

            S = new Sim { Now = () => Time.realtimeSinceStartupAsDouble };
            hud = new Hud(this);
            S.Toast += hud.Toast; S.Refused += hud.Refuse; S.Ended += kind => { SetSpeed(0); hud.ShowGameOver(kind); };

            bool auto = false;
            try { var a = Read(Auto); if (a != null) { S.LoadJson(a); Rig.Load(S); auto = true; } } catch (Exception e) { Debug.Log("[Island Sim] Autosave not loaded: " + e.Message); }
            if (!auto) { S.NewGame(); Rig.Home(S); }

            terrain = new TerrainView(S, world);
            Shader.SetGlobalTexture("_IsShore", terrain.ShoreTex); Shader.SetGlobalTexture("_IsDepth", terrain.DepthTex);
            water = new WaterView(world);
            veg = new VegetationView(S);
            buildings = new BuildingView(S, world);
            cursor = new CursorView(S, terrain);

            var q = (Quality)PlayerPrefs.GetInt("islandsim.quality", Application.isMobilePlatform ? (int)Quality.Low : (int)Quality.High);
            SetQuality(q);
            SetUiScale(PlayerPrefs.GetFloat("islandsim.ui", 1));
            hud.SetTool("inspect"); hud.UpdateUI(); hud.DrawMini();
            hud.Toast(auto ? $"Welcome back, governor. Your autosave from day {S.Day} is loaded; the menu starts a new game."
                : "Welcome, governor. It is the Age of Cowries, and Palm Isle is yours. Feed your fishing families, plant coconuts, and sell rope and dried fish to the ships that call at your jetty.", ToastKind.Good, null);
        }

        // ------------------------------------------------------------ settings
        public void SetSpeed(int n) => Speed = n;
        public void SetQuality(Quality q)
        {
            PlayerPrefs.SetInt("islandsim.quality", (int)q);
            Atmos.SetQuality(q, Rig.Cam); water.SetRefraction(Atmos.Refraction);
            veg.Grass = Atmos.Grass; veg.Sync(true);
            hud?.UpdateUI();
        }
        public void SetUiScale(float v) { PlayerPrefs.SetFloat("islandsim.ui", v); hud.SetUiScale(v); }

        // ------------------------------------------------------------ saves: three slots and an autosave each minute
        static string PathOf(string key) => Path.Combine(Application.persistentDataPath, key + ".json");
        static string Read(string key) { var p = PathOf(key); return File.Exists(p) ? File.ReadAllText(p) : null; }
        public (int day, int pop)? SlotMeta(string key)
        {
            try
            {
                var p = Path.Combine(Application.persistentDataPath, key + ".meta.json"); if (!File.Exists(p)) return null;
                var d = Json.Parse(File.ReadAllText(p)) as Dictionary<string, object>; if (d == null) return null;
                return (Convert.ToInt32(d["day"]), Convert.ToInt32(d["pop"]));
            }
            catch (Exception) { return null; }
        }
        public bool SaveTo(string key)
        {
            try
            {
                Rig.Store(S);
                File.WriteAllText(PathOf(key), S.SaveJson());
                var meta = new Dictionary<string, object> { ["day"] = (double)S.Day, ["pop"] = Math.Floor(S.TotalPop), ["at"] = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                File.WriteAllText(Path.Combine(Application.persistentDataPath, key + ".meta.json"), Json.Write(meta));
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[Island Sim] Save failed: " + e.Message); hud.Toast("Could not save: the game could not write its save file.", ToastKind.Bad, null); return false; }
        }
        public bool LoadFrom(string key)
        {
            string json = null; try { json = Read(key); } catch (Exception) { }
            if (json == null) { hud.Toast("That save slot is empty.", ToastKind.Bad, null); return false; }
            var keep = S.SaveJson();
            try { ResetWorld(); S.LoadJson(json); Rig.Load(S); }
            catch (Exception e)
            {
                Debug.LogWarning("[Island Sim] Load failed: " + e.Message); S.LoadJson(keep); AfterLoad();
                hud.Toast("That save is from an older version and cannot be loaded.", ToastKind.Bad, null); return false;
            }
            AfterLoad(); hud.Toast($"Loaded day {S.Day}.", ToastKind.Good, null); return true;
        }
        public void Restart() { ResetWorld(); S.NewGame(); Rig.Home(S); AfterLoad(); hud.Toast("A new atoll. Palm Isle is yours again, in the Age of Cowries.", ToastKind.Good, null); }
        void ResetWorld() { buildings.Clear(); hud.Reset(); S.ClearAlerts(); }
        void AfterLoad() { S.MarkAll(); veg.Sync(true); SetSpeed(1); hud.UpdateUI(); hud.DrawMini(); }

        // ------------------------------------------------------------ pointer: the web game's canvas handlers
        enum Drag { None, Pan, Rot }
        Drag drag; int dragBtn; float moved; Vector2 last; Vector2Int? hover;
        bool pinching; float pinchD, pinchA, pinchDist, pinchYaw; Vector2 pinchC;
        static float Px => Screen.dpi > 0 ? Screen.dpi / 96f : 1f;   // device pixels per CSS pixel, so drags feel as in the browser

        void Pointer()
        {
            var m = InputBridge.MousePos; bool overHud = hud.Over(m) || hud.Blocking;
            // two fingers: pinch to zoom, twist to turn, move to pan
            if (InputBridge.TouchCount >= 2)
            {
                var a = InputBridge.TouchPos(0); var b = InputBridge.TouchPos(1); float d = Vector2.Distance(a, b), an = Mathf.Atan2(b.y - a.y, b.x - a.x); var c = (a + b) / 2;
                if (!pinching) { pinching = true; S.EndStroke(); drag = Drag.None; pinchD = d; pinchA = an; pinchDist = Rig.Dist; pinchYaw = Rig.Yaw; pinchC = c; }
                else { Rig.Goal = null; Rig.Dist = pinchDist * pinchD / Mathf.Max(20, d); Rig.Yaw = pinchYaw + (an - pinchA); Rig.PanBy((c.x - pinchC.x) / Px, (c.y - pinchC.y) / Px); pinchC = c; }
                return;
            }
            pinching = false;

            for (int btn = 0; btn < 3; btn++)
            {
                if (!InputBridge.MouseDown(btn) || overHud || drag != Drag.None || S.CurStroke != null) continue;
                bool mod = InputBridge.Held(InputBridge.Sp.Shift) || InputBridge.Held(InputBridge.Sp.Alt) || InputBridge.Held(InputBridge.Sp.Ctrl);
                // rotate: right or middle drag, or Shift/Alt/Ctrl + left drag (trackpads, one-button mice)
                if (btn == 1 || btn == 2 || (btn == 0 && mod)) { Rig.Goal = null; drag = Drag.Rot; dragBtn = btn; moved = 0; last = m; }
                else if (hud.Tool == "inspect") { drag = Drag.Pan; dragBtn = 0; moved = 0; last = m; }
                else { var t = Rig.Pick(S, m); S.BeginStroke(t?.x ?? -1, t?.y ?? -1); }
            }

            if (drag != Drag.None)
            {
                float dx = (m.x - last.x) / Px, dy = (m.y - last.y) / Px; last = m; moved += Mathf.Abs(dx) + Mathf.Abs(dy);
                if (drag == Drag.Rot) { Rig.Yaw -= dx * .006f; Rig.Pitch += dy * .005f; } else Rig.PanBy(dx, dy);
                hover = null;
            }
            else
            {
                hover = overHud ? null : Rig.Pick(S, m);
                if (S.CurStroke != null && hover.HasValue) S.DragStroke(hover.Value.x, hover.Value.y);
            }

            for (int btn = 0; btn < 3; btn++)
            {
                if (!InputBridge.MouseUp(btn)) continue;
                // a click in Survey selects; a right-click without dragging drops the tool
                if (drag == Drag.Pan && btn == 0 && moved < 6 && hud.Tool == "inspect") { var t = Rig.Pick(S, m); var bd = t.HasValue ? S.BAt(t.Value.x, t.Value.y) : null; if (bd != null) hud.SelectBuilding(bd); else hud.CloseSel(); }
                if (drag == Drag.Rot && dragBtn == 1 && btn == 1 && moved < 6) { if (hud.Tool != "inspect") hud.SetTool("inspect"); else hud.CloseSel(); }
                if (drag != Drag.None && btn == dragBtn) drag = Drag.None;
                if (!InputBridge.MouseHeld(0) && !InputBridge.MouseHeld(1) && !InputBridge.MouseHeld(2)) { S.EndStroke(); drag = Drag.None; }
            }

            float sc = InputBridge.Scroll; if (sc != 0 && !overHud) Rig.Dist *= Mathf.Exp(sc * .0012f);
        }

        // ------------------------------------------------------------ the loop
        float acc, uiT, goalT, miniT, alertT, autoT;
        void Update()
        {
            float t = Time.realtimeSinceStartup, dt = Mathf.Min(.1f, Time.unscaledDeltaTime);
            hud.Keys();
            if (!hud.Typing && !hud.Blocking)
            {
                float kp = 600 * dt;
                if (InputBridge.Held('w') || InputBridge.Held(InputBridge.Sp.Up)) Rig.PanBy(0, kp);
                if (InputBridge.Held('s') || InputBridge.Held(InputBridge.Sp.Down)) Rig.PanBy(0, -kp);
                if (InputBridge.Held('a') || InputBridge.Held(InputBridge.Sp.Left)) Rig.PanBy(kp, 0);
                if (InputBridge.Held('d') || InputBridge.Held(InputBridge.Sp.Right)) Rig.PanBy(-kp, 0);
            }
            bool ctrl = InputBridge.Held(InputBridge.Sp.Ctrl);
            float turn = hud.Typing || ctrl ? 0 : (InputBridge.Held('q') ? 1 : 0) - (InputBridge.Held('e') ? 1 : 0);
            Rig.Turn(dt, turn);
            Pointer();

            if (S.Over != null) acc = 0; else acc += dt * Speed;
            int n = 0; while (acc >= .1f && n < 20) { S.Step(.1); acc -= .1f; n++; } if (n >= 20) acc = 0;
            uiT += dt; if (uiT > .25f) { uiT = 0; hud.UpdateUI(); }
            miniT += dt; if (miniT > .2f) { miniT = 0; hud.DrawMini(); }
            alertT += dt; if (alertT > 3) { alertT = 0; S.CheckAlerts(); }
            autoT += dt; if (autoT > 60) { autoT = 0; if (S.Over == null) SaveTo(Auto); }
            goalT += dt; if (goalT > .5f) { goalT = 0; if (S.CheckGoals().Count > 0) hud.GoalMet(); }
            S.RepeatStroke(dt, hover?.x ?? 0, hover?.y ?? 0, hover.HasValue);

            Rig.Apply();
            terrain.Sync(); veg.Sync();
            Atmos.Update(t, Rig.Tx, Rig.Tz, Rig.Dist);
            veg.Draw(Rig.Cam); buildings.Update(t);
            cursor.Draw(hover, hud.Selected);
            hud.Frame(hover, drag != Drag.None || pinching, InputBridge.MousePos);
        }

        void OnApplicationQuit() { if (S != null && S.Over == null) SaveTo(Auto); }
    }
}
