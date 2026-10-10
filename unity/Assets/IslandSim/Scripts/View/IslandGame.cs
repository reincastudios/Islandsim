using System.Collections.Generic;
using System.IO;
using System.Linq;
using IslandSim.Core;
using UnityEngine;

namespace IslandSim.View
{
    public enum Tool { Survey, Raise, Lower, Level, Demolish, Build }

    /// <summary>
    /// Game root: owns the simulation, the views, the camera and input, and draws the HUD with IMGUI.
    /// Drop it on an empty GameObject, or let <see cref="Bootstrap"/> create it in any scene.
    /// </summary>
    public sealed class IslandGame : MonoBehaviour
    {
        public const float Tick = 0.1f;

        public Sim Sim { get; private set; }
        RtsCamera rig;
        TerrainView terrain;
        WaterView water;
        VegetationView veg;
        BuildingView buildings;

        public Tool Tool { get; private set; } = Tool.Survey;
        public BType BuildType { get; private set; } = BType.House;
        public int Brush { get; private set; }
        public int Speed { get; private set; } = 1;

        float acc, goalT, syncT;
        bool hasHover; int hoverX, hoverY;
        bool hasSel; int selX, selY;

        // pointer state
        bool rotating, panning, stroking; Vector2 lastMouse; Vector3 grab; float panMoved;
        HashSet<int> strokeDone = new HashSet<int>(); int strokeTarget, strokeLastX = -1, strokeLastY = -1;

        // HUD
        sealed class ToastMsg { public string Text; public ToastKind Kind; public float Time; }
        readonly List<ToastMsg> toasts = new List<ToastMsg>();
        readonly Dictionary<string, float> toastSeen = new Dictionary<string, float>();
        readonly List<Rect> panels = new List<Rect>();
        float ui = 1;
        GUIStyle panel, title, label, small, good, bad, button, buttonOn, tableCell, tableHead, toastStyle, worldLabel;
        Texture2D panelTex, buttonTex, buttonOnTex, toastTex;
        string inspectText = "";

        static string SavePath => Path.Combine(Application.persistentDataPath, "islandsim_save.json");

        void Awake()
        {
            Application.targetFrameRate = 120;
            Mats.Init();

            // the game builds its own camera and sun; drop the defaults of an empty scene
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c.gameObject != gameObject) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) Destroy(l.gameObject);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40; cam.nearClipPlane = .5f; cam.farClipPlane = 1500;
            camGo.AddComponent<AudioListener>();
            rig = new RtsCamera(cam);
            Atmosphere.Setup(transform, cam);

            Sim = new Sim();
            Sim.Toast += Toast;
            Sim.NewGame();

            terrain = new TerrainView(Sim, transform);
            water = new WaterView(Sim, transform);
            veg = new VegetationView(Sim);
            buildings = new BuildingView(Sim, transform);
            Refresh();
            Toast("Welcome to the atoll. Raise and lower the reef to link islands and open channels for boats.", ToastKind.Info);
        }

        void Refresh()
        {
            if (Sim.TerrainDirty)
            {
                terrain.Rebuild(Sim.DirtyX0, Sim.DirtyY0, Sim.DirtyX1, Sim.DirtyY1);
                water.Rebuild();
                buildings.Reseat();
                Sim.ClearDirty();
            }
            if (Sim.DirtyTrees) { veg.Rebuild(); Sim.DirtyTrees = false; }
            if (Sim.DirtyBuildings) { buildings.Sync(); Sim.DirtyBuildings = false; }
        }

        void Update()
        {
            float dt = Mathf.Min(.1f, Time.unscaledDeltaTime);
            HandleKeys(dt);
            HandlePointer();

            acc += dt * Speed; int n = 0;
            while (acc >= Tick && n < 20) { Sim.Step(Tick); acc -= Tick; n++; }
            if (n >= 20) acc = 0;
            goalT += dt; if (goalT > .5f) { goalT = 0; Sim.CheckGoals(); }
            syncT += dt; if (syncT > .25f) { syncT = 0; Sim.DirtyBuildings = true; }

            Refresh();
            buildings.UpdateBoats(dt, Time.time);
            UpdateHover();
            rig.Apply();
            veg.Draw();
        }

        // ------------------------------------------------------------------ input
        void SetTool(Tool t, BType b = BType.House) { Tool = t; BuildType = b; }

        void HandleKeys(float dt)
        {
            if (InputBridge.Pressed(K.V) || InputBridge.Pressed(K.Escape)) SetTool(Tool.Survey);
            if (InputBridge.Pressed(K.R)) SetTool(Tool.Raise);
            if (InputBridge.Pressed(K.F)) SetTool(Tool.Lower);
            if (InputBridge.Pressed(K.T)) SetTool(Tool.Level);
            if (InputBridge.Pressed(K.X)) SetTool(Tool.Demolish);
            if (InputBridge.Pressed(K.H)) SetTool(Tool.Build, BType.Harbor);
            K[] nums = { K.N1, K.N2, K.N3, K.N4, K.N5, K.N6 };
            for (int i = 0; i < nums.Length; i++) if (InputBridge.Pressed(nums[i])) SetTool(Tool.Build, (BType)(i + 1));
            if (InputBridge.Pressed(K.B)) Brush = (Brush + 1) % 3;
            if (InputBridge.Pressed(K.Space)) Speed = Speed == 0 ? 1 : 0;
            if (InputBridge.Pressed(K.F5)) Save();
            if (InputBridge.Pressed(K.F9)) Load();

            float kp = rig.Dist * 1.1f * dt * (InputBridge.Held(K.Shift) ? 2.5f : 1);
            var mv = Vector3.zero;
            if (InputBridge.Held(K.W) || InputBridge.Held(K.Up)) mv += rig.Forward;
            if (InputBridge.Held(K.S) || InputBridge.Held(K.Down)) mv -= rig.Forward;
            if (InputBridge.Held(K.D) || InputBridge.Held(K.Right)) mv += rig.Right;
            if (InputBridge.Held(K.A) || InputBridge.Held(K.Left)) mv -= rig.Right;
            rig.Pan(mv * kp);
            if (InputBridge.Held(K.Q)) rig.Yaw += 90 * dt;
            if (InputBridge.Held(K.E)) rig.Yaw -= 90 * dt;
        }

        bool OverUi(Vector2 mouse)
        {
            var g = new Vector2(mouse.x, Screen.height - mouse.y) / ui;
            foreach (var r in panels) if (r.Contains(g)) return true;
            return false;
        }

        void HandlePointer()
        {
            var mp = InputBridge.MousePos;
            bool over = OverUi(mp);

            float sc = InputBridge.Scroll;
            if (!over && Mathf.Abs(sc) > 0.001f) rig.Dist *= Mathf.Exp(-sc * 0.12f);

            if (InputBridge.MouseDown(1) && !over) { rotating = true; lastMouse = mp; }
            if (rotating)
            {
                var d = mp - lastMouse; lastMouse = mp;
                rig.Yaw += d.x * .35f; rig.Pitch -= d.y * .3f;
                if (!InputBridge.MouseHeld(1)) rotating = false;
            }

            bool panStart = (InputBridge.MouseDown(2) || (InputBridge.MouseDown(0) && Tool == Tool.Survey)) && !over;
            if (panStart && rig.GroundPoint(mp, 0, out grab)) { panning = true; panMoved = 0; lastMouse = mp; }
            if (panning)
            {
                if (rig.GroundPoint(mp, 0, out var now)) rig.Pan(grab - now);
                panMoved += (mp - lastMouse).magnitude; lastMouse = mp;
                if (!InputBridge.MouseHeld(2) && !InputBridge.MouseHeld(0))
                {
                    panning = false;
                    if (panMoved < 6 && Tool == Tool.Survey && Pick(mp, out var sx, out var sy)) { hasSel = true; selX = sx; selY = sy; }
                }
                rig.Apply();
            }

            if (Tool != Tool.Survey && InputBridge.MouseDown(0) && !over)
            {
                stroking = true; strokeDone.Clear(); strokeLastX = strokeLastY = -1;
                if (Pick(mp, out var x, out var y)) { strokeLastX = x; strokeLastY = y; ApplyAt(x, y, true); }
            }
            if (stroking)
            {
                if (!InputBridge.MouseHeld(0)) stroking = false;
                else if (Pick(mp, out var x, out var y) && (x != strokeLastX || y != strokeLastY)) { strokeLastX = x; strokeLastY = y; ApplyAt(x, y, false); }
            }
        }

        void ApplyAt(int x, int y, bool first)
        {
            switch (Tool)
            {
                case Tool.Build: if (first) Sim.Place(BuildType, x, y); return;
                case Tool.Demolish: if (first) Sim.Demolish(x, y); return;
                case Tool.Survey: return;
            }
            if (first && Tool == Tool.Level) strokeTarget = Sim.Hgt[Sim.Idx(x, y)];
            var mode = Tool == Tool.Raise ? TerraMode.Raise : Tool == Tool.Lower ? TerraMode.Lower : TerraMode.Level;
            Sim.TerraformBrush(x, y, Brush, mode, strokeTarget, strokeDone);
        }

        /// <summary>Tile under the pointer: the first of the terrain surface or the water surface along the ray.</summary>
        bool Pick(Vector2 screen, out int x, out int y)
        {
            var ray = rig.Cam.ScreenPointToRay(screen);
            float best = float.MaxValue; Vector3 p = default;
            if (Physics.Raycast(ray, out var hit, 3000f)) { best = hit.distance; p = hit.point; }
            if (new Plane(Vector3.up, new Vector3(0, World.WaterY, 0)).Raycast(ray, out var d) && d < best) { best = d; p = ray.GetPoint(d); }
            x = y = -1;
            return best < float.MaxValue && World.ToTile(p, out x, out y);
        }

        void UpdateHover()
        {
            buildings.HideHover();
            hasHover = !OverUi(InputBridge.MousePos) && !panning && !rotating && Pick(InputBridge.MousePos, out hoverX, out hoverY);
            if (hasHover)
            {
                if (Tool == Tool.Build) buildings.ShowGhost(BuildType, hoverX, hoverY, Sim.CanPlace(BuildType, hoverX, hoverY).Ok);
                else
                {
                    int r = Tool == Tool.Survey || Tool == Tool.Demolish ? 0 : Brush;
                    var c = Tool == Tool.Raise ? World.Hex("#ffcf5a") : Tool == Tool.Lower ? World.Hex("#4fd1c5") : Tool == Tool.Demolish ? World.Hex("#ff6b6b") : World.Hex("#f3e6c4");
                    c.a = .9f;
                    buildings.ShowCursor(hoverX, hoverY, r, c);
                }
            }
            if (hasHover) inspectText = Inspect(hoverX, hoverY);
            else if (hasSel && Tool == Tool.Survey) inspectText = Inspect(selX, selY);
            else inspectText = "";
        }

        string Inspect(int x, int y)
        {
            if (Tool == Tool.Build)
            {
                var d = Defs.Def(BuildType); var r = Sim.CanPlace(BuildType, x, y);
                return $"<b>{d.Name}</b>\n{(r.Ok ? "<color=#7ef0a0>Can build here</color>" : $"<color=#ff8a7a>{r.Why}</color>")}\n<color=#c9c2b0>{d.Info}</color>";
            }
            int i = Sim.Idx(x, y), hv = Sim.Hgt[i]; var isl = Sim.IslandAt(x, y); var b = Sim.At(x, y);
            var s = $"<b>{Defs.TerrainName(hv)}</b>\n<color=#c9c2b0>Height {hv}{(isl != null ? " · " + isl.Name : "")}{(Sim.Tree[i] != 0 ? " · trees" : "")}</color>";
            if (b != null)
            {
                s += $"\n<b>{Defs.Def(b.Type).Name}</b>";
                if (b.Type == BType.House) s += $" · {b.Res:F1} residents";
                else if (b.HasRate) s += $" · {b.Rate * 60:F0}/min";
                s += $"\n<color=#c9c2b0>{b.Status}</color>";
            }
            return s;
        }

        // ------------------------------------------------------------------ save / load
        void Save()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(Sim.ToSave())); Toast("Game saved", ToastKind.Good); }
            catch (System.Exception e) { Toast("Save failed: " + e.Message, ToastKind.Bad); }
        }

        void Load()
        {
            try
            {
                if (!File.Exists(SavePath)) { Toast("No saved game yet (F5 saves)", ToastKind.Bad); return; }
                var d = JsonUtility.FromJson<Sim.SaveData>(File.ReadAllText(SavePath));
                if (!Sim.FromSave(d)) { Toast("Save file is not readable", ToastKind.Bad); return; }
                buildings.Clear(); hasSel = false; Refresh(); Toast("Game loaded", ToastKind.Good);
            }
            catch (System.Exception e) { Toast("Load failed: " + e.Message, ToastKind.Bad); }
        }

        void NewGame()
        {
            Sim.NewGame(); buildings.Clear(); hasSel = false; rig.Home(); Refresh();
            Toast("A fresh charter. Good luck, governor.", ToastKind.Info);
        }

        // ------------------------------------------------------------------ HUD
        void Toast(string msg, ToastKind kind)
        {
            if (toastSeen.TryGetValue(msg, out var t) && Time.unscaledTime - t < 2.5f) return;
            toastSeen[msg] = Time.unscaledTime;
            toasts.Add(new ToastMsg { Text = msg, Kind = kind, Time = Time.unscaledTime });
            if (toasts.Count > 4) toasts.RemoveAt(0);
        }

        static Texture2D Solid(Color c) { var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; t.SetPixel(0, 0, c); t.Apply(); return t; }

        void Styles()
        {
            if (panel != null) return;
            panelTex = Solid(new Color(.07f, .11f, .13f, .82f));
            buttonTex = Solid(new Color(.16f, .22f, .24f, .95f));
            buttonOnTex = Solid(new Color(.25f, .62f, .58f, 1f));
            toastTex = Solid(new Color(.06f, .09f, .1f, .9f));
            var txt = new Color(.95f, .92f, .85f);
            panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8), normal = { background = panelTex } };
            title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.98f, .86f, .6f) } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true, normal = { textColor = txt } };
            small = new GUIStyle(label) { fontSize = 11, normal = { textColor = new Color(.78f, .76f, .7f) } };
            good = new GUIStyle(label) { normal = { textColor = new Color(.5f, .95f, .65f) } };
            bad = new GUIStyle(label) { normal = { textColor = new Color(1f, .55f, .48f) } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 6, 4, 4), richText = true,
                normal = { background = buttonTex, textColor = txt }, hover = { background = buttonTex, textColor = Color.white }, active = { background = buttonOnTex, textColor = Color.white } };
            buttonOn = new GUIStyle(button) { normal = { background = buttonOnTex, textColor = Color.white }, hover = { background = buttonOnTex, textColor = Color.white } };
            tableCell = new GUIStyle(label) { fontSize = 12, wordWrap = false, alignment = TextAnchor.MiddleRight };
            tableHead = new GUIStyle(tableCell) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(.98f, .86f, .6f) } };
            toastStyle = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(12, 12, 6, 6), normal = { background = toastTex, textColor = txt } };
            worldLabel = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, normal = { textColor = new Color(1, .97f, .9f) } };
        }

        static string Fmt(float n) => Mathf.Abs(n) >= 1000 ? (n / 1000).ToString("0.0") + "k" : Mathf.FloorToInt(n).ToString();

        bool Btn(string text, bool on, params GUILayoutOption[] o) => GUILayout.Button(text, on ? buttonOn : button, o);

        void OnGUI()
        {
            Styles();
            ui = Mathf.Clamp(Screen.height / 900f, .8f, 2.2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(ui, ui, 1));
            float sw = Screen.width / ui, sh = Screen.height / ui;
            if (Event.current.type == EventType.Layout) panels.Clear();

            DrawWorldLabels();
            DrawTopBar(sw);
            DrawTools();
            DrawSide(sw, sh);
            DrawInspector(sh);
            DrawToasts(sw, sh);
        }

        void Panel(Rect r) { if (Event.current.type == EventType.Layout) panels.Add(r); }

        void DrawTopBar(float sw)
        {
            var r = new Rect(0, 0, sw, 36); Panel(r);
            GUILayout.BeginArea(r, panel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("ISLAND SIM", title, GUILayout.Width(100));
            GUILayout.Label($"Gold <b>{Fmt(Sim.Gold)}</b>", Sim.Gold < 0 ? bad : label, GUILayout.Width(110));
            GUILayout.Label($"{(Sim.Rate >= 0 ? "+" : "")}{Sim.Rate:F1}/s", Sim.Rate >= 0 ? good : bad, GUILayout.Width(70));
            GUILayout.Label($"Residents <b>{Mathf.FloorToInt(Sim.TotalPop)}</b>", label, GUILayout.Width(120));
            GUILayout.Label($"Day <b>{1 + Mathf.FloorToInt(Sim.T / 30)}</b>", label, GUILayout.Width(70));
            GUILayout.FlexibleSpace();
            foreach (var s in new[] { 0, 1, 2, 4 }) if (Btn(s == 0 ? "  II" : $"  {s}x", Speed == s, GUILayout.Width(40))) Speed = s;
            GUILayout.Space(12);
            if (Btn("Save", false, GUILayout.Width(52))) Save();
            if (Btn("Load", false, GUILayout.Width(52))) Load();
            if (Btn("New", false, GUILayout.Width(48))) NewGame();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawTools()
        {
            var r = new Rect(10, 46, 214, 440); Panel(r);
            GUILayout.BeginArea(r, panel);
            GUILayout.Label("TERRAFORM", title);
            ToolButton(Tool.Survey, "Survey", "V", "look & pan");
            ToolButton(Tool.Raise, "Raise", "R", "4g + 1 stone");
            ToolButton(Tool.Lower, "Lower", "F", "4g, +½ stone");
            ToolButton(Tool.Level, "Level", "T", "match first tile");
            ToolButton(Tool.Demolish, "Demolish", "X", "50% refund");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Brush (B)", small, GUILayout.Width(62));
            string[] bn = { "1×1", "3×3", "5×5" };
            for (int i = 0; i < 3; i++) if (Btn(bn[i], Brush == i)) Brush = i;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.Label("BUILD", title);
            for (int i = 0; i < Defs.Buildings.Length; i++)
            {
                var d = Defs.Buildings[i]; bool on = Tool == Tool.Build && (int)BuildType == i;
                if (Btn($"<b>{d.Key}</b>  {d.Name}  <size=10><color=#bdb6a4>{d.CostText()}</color></size>", on)) SetTool(Tool.Build, (BType)i);
            }
            GUILayout.EndArea();
        }

        void ToolButton(Tool t, string name, string key, string cost)
        {
            if (Btn($"<b>{key}</b>  {name}  <size=10><color=#bdb6a4>{cost}</color></size>", Tool == t)) SetTool(t);
        }

        void DrawSide(float sw, float sh)
        {
            var r = new Rect(sw - 350, 46, 340, Mathf.Min(sh - 56, 560)); Panel(r);
            GUILayout.BeginArea(r, panel);
            GUILayout.Label("ISLANDS", title);
            GUILayout.BeginHorizontal();
            GUILayout.Label("", tableHead, GUILayout.Width(104)); GUILayout.Label("Pop", tableHead, GUILayout.Width(34));
            foreach (var g in Defs.GoodNames) GUILayout.Label(g.Substring(0, 4), tableHead, GUILayout.Width(34));
            GUILayout.EndHorizontal();
            foreach (var isl in Sim.Islands.Where(i => i.N >= 60 || i.Harbor != null).OrderByDescending(i => i.Harbor != null ? 1 : 0).ThenByDescending(i => i.N))
            {
                GUILayout.BeginHorizontal();
                bool cap = isl.Harbor != null && isl.Harbor.Id == Sim.Capital;
                GUILayout.Label(isl.Name + (cap ? " ★" : ""), new GUIStyle(tableCell) { alignment = TextAnchor.MiddleLeft }, GUILayout.Width(104));
                GUILayout.Label(Mathf.FloorToInt(isl.Res).ToString(), tableCell, GUILayout.Width(34));
                for (int g = 0; g < Defs.GoodCount; g++) GUILayout.Label(isl.Harbor != null ? Mathf.FloorToInt(isl.Harbor.Stock[g]).ToString() : "–", tableCell, GUILayout.Width(34));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
            GUILayout.Label("BOATS", title);
            if (Sim.Boats.Count == 0) GUILayout.Label("No boats yet. Build a harbor on another island and one will sail for the capital.", small);
            foreach (var bt in Sim.Boats)
            {
                var hb = Sim.ById(bt.Home);
                string cargo = string.Join(", ", Enumerable.Range(0, Defs.GoodCount).Where(g => bt.Cargo[g] > 0).Select(g => $"{bt.Cargo[g]:0} {Defs.GoodNames[g].ToLower()}"));
                if (bt.State == BoatState.Blocked) GUILayout.Label($"<b>{Sim.IslandName(hb)}</b> boat: <color=#ff8a7a>{bt.Why}</color>", label);
                else GUILayout.Label($"<b>{Sim.IslandName(hb)}</b> boat: sailing {(bt.Out ? "to capital" : "home")}{(cargo.Length > 0 ? " with " + cargo : "")} · {bt.Trips} trips", label);
            }
            GUILayout.Space(8);
            GUILayout.Label("CHARTER GOALS", title);
            bool firstOpen = true;
            foreach (var g in Sim.Goals)
            {
                bool done = Sim.GoalsDone.Contains(g.Id), now = !done && firstOpen; if (now) firstOpen = false;
                GUILayout.Label((done ? "<color=#7ef0a0>✓</color> " : now ? "<color=#ffcf5a>►</color> " : "<color=#8a8578>○</color> ") + (done ? $"<color=#9a9688>{g.Title}</color>" : g.Title), label);
                if (now && g.Hint.Length > 0) GUILayout.Label(g.Hint, small);
            }
            GUILayout.EndArea();
        }

        void DrawInspector(float sh)
        {
            var r = new Rect(10, sh - 128, 430, 118); Panel(r);
            GUILayout.BeginArea(r, panel);
            GUILayout.Label(inspectText.Length > 0 ? inspectText : "<color=#c9c2b0>Hover a tile to survey it.</color>", label);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Right-drag rotate · wheel zoom · WASD pan · Q/E turn · Space pause · F5 save · F9 load", small);
            GUILayout.EndArea();
        }

        void DrawToasts(float sw, float sh)
        {
            float y = sh - 48; float now = Time.unscaledTime;
            toasts.RemoveAll(t => now - t.Time > 5.5f);
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                var c = t.Kind == ToastKind.Good ? "#8ff0b0" : t.Kind == ToastKind.Bad ? "#ff9a8a" : "#f3ead8";
                var content = new GUIContent($"<color={c}>{t.Text}</color>");
                var size = toastStyle.CalcSize(content); float w = Mathf.Min(560, size.x + 4);
                float h = toastStyle.CalcHeight(content, w);
                var r = new Rect((sw - w) / 2, y - h, w, h);
                var prev = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01((5.5f - (now - t.Time)) / .6f));
                GUI.Label(r, content, toastStyle);
                GUI.color = prev;
                y -= h + 6;
            }
        }

        void DrawWorldLabels()
        {
            var cam = rig.Cam;
            foreach (var isl in Sim.Islands)
            {
                if (isl.N < 60) continue;
                int cx = Mathf.Clamp(Mathf.RoundToInt(isl.Cx), 0, Defs.W - 1), cy = Mathf.Clamp(Mathf.RoundToInt(isl.Cy), 0, Defs.H - 1);
                var wp = World.Pos(isl.Cx + .5f, isl.Cy + .5f, Mathf.Max(0, World.TopY(Sim.Hgt[Sim.Idx(cx, cy)])) + 3f);
                var sp = cam.WorldToScreenPoint(wp);
                if (sp.z <= 0) continue;
                var g = new Vector2(sp.x, Screen.height - sp.y) / ui;
                var r = new Rect(g.x - 90, g.y - 12, 180, 24);
                var prev = GUI.color; GUI.color = new Color(0, 0, 0, .55f);
                GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), isl.Name, worldLabel);
                GUI.color = prev;
                GUI.Label(r, isl.Name, worldLabel);
            }
        }
    }
}
