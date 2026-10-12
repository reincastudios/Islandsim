using System;
using System.Collections.Generic;
using System.Linq;
using IslandSim.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static IslandSim.Core.Defs;
using static IslandSim.View.UiKit;

namespace IslandSim.View
{
    /// <summary>
    /// The web game's HUD in UI Toolkit, same layout, wording and rules ("Island Sim UI Design"): the lacquer top bar with
    /// the warehouse of the island in view, the charter card, the katheeb's advisor feed and log, the minimap and compass,
    /// the build bar with its cards, brush and level pickers, the selection and island panels, the cursor tag, tooltips,
    /// the F1 help overlay, saves, settings and the game-over box. It only reads the game, and changes it through the same
    /// calls the web buttons make.
    /// </summary>
    public sealed class Hud
    {
        readonly IslandGame G; Sim S => G.S; RtsCamera C => G.Rig;
        public readonly UIDocument Doc; readonly PanelSettings panelSettings;
        VisualElement root, hud;

        // ------------------------------------------------------------ the tool vocabulary (web ids)
        static readonly string[] TerraIds = { "inspect", "raise", "lower", "flatten", "smooth", "road", "demolish" };
        static readonly Dictionary<string, (string name, string key)> TERRA = new Dictionary<string, (string, string)>
        {
            ["inspect"] = ("Survey", "V"), ["raise"] = ("Raise", "R"), ["lower"] = ("Lower", "F"), ["flatten"] = ("Level", "T"),
            ["smooth"] = ("Smooth", "Y"), ["road"] = ("Sand road", "G"), ["demolish"] = ("Demolish", "X"),
        };
        static readonly Dictionary<string, string> TOOL_ICO = new Dictionary<string, string>
        {
            ["inspect"] = "survey", ["raise"] = "raise", ["lower"] = "lower", ["flatten"] = "level", ["smooth"] = "smooth", ["road"] = "road", ["demolish"] = "demolish",
            ["harbor"] = "harbour", ["house"] = "homes", ["fishery"] = "fish", ["drying"] = "racks", ["grove"] = "palm", ["toddy"] = "toddy", ["lumber"] = "wood",
            ["quarry"] = "stone", ["coir"] = "rope", ["cowrie"] = "cowrie", ["mosque"] = "mosque", ["palace"] = "palace",
        };
        static readonly string[] GOOD_ICO = { "wood", "stone", "fish", "coconut", "toddy", "hikimas", "rope", "cowrie", "rice" };
        sealed class Cat { public string Id, Name, Ico; public string[] Tools; public bool Brush, Levels, Plots; }
        static readonly Cat[] CATS =
        {
            new Cat { Id = "terra", Name = "Terraform", Ico = "terra", Tools = new[] { "raise", "lower", "flatten", "smooth" }, Brush = true, Levels = true },
            new Cat { Id = "homes", Name = "Homes", Ico = "homes", Tools = new[] { "house" }, Plots = true },
            new Cat { Id = "food", Name = "Food", Ico = "fish", Tools = new[] { "fishery", "grove", "toddy", "drying" } },
            new Cat { Id = "industry", Name = "Industry", Ico = "axe", Tools = new[] { "lumber", "quarry", "coir", "cowrie" } },
            new Cat { Id = "civic", Name = "Civic", Ico = "mosque", Tools = new[] { "mosque", "palace" } },
            new Cat { Id = "harbour", Name = "Harbour", Ico = "harbour", Tools = new[] { "harbor" } },
            new Cat { Id = "roads", Name = "Roads", Ico = "road", Tools = new[] { "road" }, Brush = true },
        };
        static readonly Dictionary<string, (int gold, int stone)> TOOL_COST = new Dictionary<string, (int, int)>
        { ["raise"] = (4, 1), ["lower"] = (4, 0), ["flatten"] = (4, 0), ["smooth"] = (4, 0), ["road"] = (2, 0) };
        static readonly Dictionary<string, string> TOOL_TIP = new Dictionary<string, string>
        {
            ["inspect"] = "Click a building or an island's name to see its details. Left-drag pans the view.",
            ["raise"] = "Raises land one step; hold still to keep raising. Each step costs 4 gold and 1 coral stone from the nearest jetty.",
            ["lower"] = "Lowers land one step for 4 gold and returns half a coral stone; hold still to keep lowering. Lower shallows to let boats through. The open ocean beyond the reef cannot be dredged.",
            ["flatten"] = "Every tile you paint takes the height picked under Level to: deep lagoon, boat channel, reef, beach, land or ridge. 4 gold per step; raising uses coral stone.",
            ["smooth"] = "Evens out bumps: each tile steps toward the average of its neighbours. Hold still to keep smoothing. 4 gold per step.",
            ["road"] = "A sand road between buildings. 2 gold per tile; clears trees, and nothing grows on it.",
            ["demolish"] = "Removes a building for half its gold back, or a road tile for 1 gold.",
        };
        static readonly Dictionary<string, string> MAKES = new Dictionary<string, string>
        {
            ["house"] = "Tax gold and workers", ["fishery"] = "Fish", ["drying"] = "Dried fish (hiki mas), an export", ["grove"] = "Coconuts", ["toddy"] = "Sweet toddy",
            ["lumber"] = "Timber", ["quarry"] = "Coral stone", ["coir"] = "Coir rope, the main export", ["cowrie"] = "Cowries, an export",
            ["harbor"] = "A warehouse and a dhoni to the capital; at the capital, the trade post", ["mosque"] = "The faith need for compounds within 10 tiles", ["palace"] = "The advance to the next age",
        };
        static readonly Dictionary<string, string> NEEDS = new Dictionary<string, string>
        {
            ["house"] = "Fish or rice; then coconuts and a mosque nearby; sweet toddy raises tax", ["drying"] = "Fish, and timber for firewood",
            ["coir"] = "Coconuts, and water within 2 tiles", ["toddy"] = "Coconut palms within 3 tiles", ["palace"] = "The capital island; one per game",
        };
        static string CatOf(string id) => CATS.FirstOrDefault(c => c.Tools.Contains(id))?.Id;
        static bool IsBuilding(string id, out BType t) => TryType(id, out t);
        static string ToolName(string id) => IsBuilding(id, out var t) ? Def(t).Name : TERRA[id].name;
        static string ToolKey(string id) => IsBuilding(id, out var t) ? Def(t).Key : TERRA.TryGetValue(id, out var v) ? v.key : "";

        public string Tool => S.Tool == Core.Tool.Build ? Def(S.BuildType).Id : S.Tool.ToString().ToLowerInvariant();
        static string Fmt(double n) { double a = Math.Abs(n); return a >= 10000 ? (n / 1000).ToString("0") + "k" : a >= 1000 ? (n / 1000).ToString("0.0") + "k" : Math.Floor(n).ToString("0"); }
        static string Rate1(double v) => v >= 10 ? Noise.Round(v).ToString("0") : v.ToString("0.0");
        static string Lower1(string label) { int k = label.IndexOf(" ("); return (k >= 0 ? label.Substring(0, k) : label).ToLowerInvariant(); }

        // ------------------------------------------------------------ state
        string openCat; public Island UiIsl;
        sealed class Sel { public string Kind; public int Id; public bool Armed; public string Key, Name; public int Hid = -1; public bool Renaming; }
        Sel sel;
        public Building Selected => sel != null && sel.Kind == "b" ? S.ById(sel.Id) : null;
        sealed class LogItem { public string Msg; public ToastKind Kind; public int Day; public (int x, int y)? At; }
        readonly List<LogItem> LOG = new List<LogItem>();
        readonly Dictionary<string, float> toastMem = new Dictionary<string, float>();
        int unread; bool newArmed;
        string tagErr; float tagUntil;

        public Hud(IslandGame g)
        {
            G = g;
            var go = new GameObject("hud"); go.SetActive(false); go.transform.SetParent(g.transform, false);
            Doc = go.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>(); panelSettings.name = "Island Sim HUD";
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("IslandSim/UI/Theme");
            panelSettings.scaleMode = PanelScaleMode.ConstantPhysicalSize; panelSettings.referenceDpi = 96; panelSettings.fallbackDpi = 96;
            panelSettings.clearColor = false; panelSettings.sortingOrder = 10;
            Doc.panelSettings = panelSettings; go.SetActive(true);
            Build();
        }

        public void SetUiScale(float v) { panelSettings.scale = v; foreach (var (b, x) in uiBtns) b.EnableInClassList("on", Mathf.Abs(x - v) < .01f); }

        // ------------------------------------------------------------ layout
        readonly UiKit K = new UiKit { Record = false };
        Label dayLbl, islName, newLbl, cAge, cGoal, cWhy, cProg, cCount, badge;
        readonly List<(Button b, int n)> speedBtns = new List<(Button, int)>();
        readonly List<(Button b, Quality q)> qBtns = new List<(Button, Quality)>();
        readonly List<(Button b, float v)> uiBtns = new List<(Button, float)>();
        sealed class Ctr { public VisualElement El; public Label V, Tr; }
        readonly Dictionary<string, Ctr> ctrs = new Dictionary<string, Ctr>();
        VisualElement menu, settings, logPop, logList, saves, charter, cBarFill, goalsBox, chevron, advisor, mapbox, needle, cards, bar, selPanel, tag, tip, help, over, labels;
        Button menuBtn, setBtn, logBtn;
        readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();
        Image mini; Texture2D miniTex;
        Label overT, overWhy, overStats;

        void Build()
        {
            root = Doc.rootVisualElement; root.pickingMode = PickingMode.Ignore;
            var ss = Resources.Load<StyleSheet>("IslandSim/UI/Hud");
            if (ss != null) root.styleSheets.Add(ss); else Debug.LogError("[Island Sim] Missing HUD stylesheet IslandSim/UI/Hud.uss");
            var font = Resources.Load<Font>("IslandSim/Fonts/FamiljenGrotesk-Regular"); if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);

            labels = K.E(); labels.name = "labels"; labels.pickingMode = PickingMode.Ignore; labels.style.position = Position.Absolute; labels.style.left = labels.style.top = labels.style.right = labels.style.bottom = 0; root.Add(labels);
            hud = K.E(); hud.name = "hud"; hud.pickingMode = PickingMode.Ignore; hud.style.position = Position.Absolute; hud.style.left = hud.style.top = hud.style.right = hud.style.bottom = 0; root.Add(hud);

            BuildTop(); BuildPops(); BuildCharter();
            advisor = K.E(); advisor.name = "advisor"; advisor.pickingMode = PickingMode.Ignore; hud.Add(advisor);
            BuildMap(); BuildBuildBar();
            selPanel = K.E("pnl hidden"); selPanel.name = "sel"; hud.Add(selPanel);
            tag = K.E("hidden"); tag.name = "tag"; tag.pickingMode = PickingMode.Ignore; root.Add(tag);
            tip = K.E("hidden"); tip.name = "tip"; tip.pickingMode = PickingMode.Ignore; root.Add(tip);
            help = K.E("hidden"); help.name = "help"; help.RegisterCallback<PointerDownEvent>(_ => ToggleHelp(false)); root.Add(help);
            BuildOver(); BuildLabels();
            root.RegisterCallback<PointerDownEvent>(e => { var t = e.target as VisualElement; if (!InPop(t)) ClosePops(); }, TrickleDown.TrickleDown);
            RenderCards();
        }

        bool InPop(VisualElement t)
        {
            for (var v = t; v != null; v = v.parent) if (v == menu || v == settings || v == logPop || v.ClassListContains("ib")) return true;
            return false;
        }

        void Tip(VisualElement el, string key)
        {
            el.RegisterCallback<PointerEnterEvent>(_ => ShowTip(el, key));
            el.RegisterCallback<PointerLeaveEvent>(_ => { if (tipFor == el) HideTip(); });
        }

        void BuildTop()
        {
            var top = K.E("row"); top.name = "top"; hud.Add(top);
            var band = K.Band(); band.name = "topband"; hud.Add(band);
            menuBtn = K.B("ib", () => TogglePop(menu), K.I("menu"));
            var day = K.E("row day", K.I("sun"), K.L("Day"), dayLbl = K.L("1", "b")); Gap(day, 6); Tip(day, "day");
            var speed = K.E("speed");
            foreach (var (lbl, n) in new[] { ("II", 0), ("1×", 1), ("3×", 3), ("6×", 6) }) { var b = K.T(lbl, null, () => G.SetSpeed(n)); speedBtns.Add((b, n)); speed.Add(b); }
            var g1 = Gap(K.E("row", menuBtn, day, speed), 10);
            var res = K.E(); res.name = "res"; RenderRes(res);
            logBtn = K.B("ib", () => TogglePop(logPop), K.I("bell")); badge = K.L("", "badge hidden"); logBtn.Add(badge);
            setBtn = K.B("ib", () => TogglePop(settings), K.I("gear"));
            var g2 = Gap(K.E("row", logBtn, setBtn), 10);
            top.Add(g1); top.Add(res); top.Add(g2); Gap(top, 14);
        }

        void RenderRes(VisualElement res)
        {
            var chip = K.B("isl-chip", () => { if (UiIsl != null) OpenIsland(UiIsl); }, K.I("harbour"), islName = K.L("")); Tip(chip, "isl"); res.Add(chip);
            Ctr Add(string k, string icon, string tipKey, bool trend)
            {
                var c = new Ctr { V = K.L("", "v") }; c.El = K.B("ctr", () => { if (UiIsl != null) OpenIsland(UiIsl); }, K.I(icon), c.V);
                if (trend) { c.Tr = K.L("", "tr"); c.El.Add(c.Tr); }
                Tip(c.El, tipKey); res.Add(c.El); ctrs[k] = c; return c;
            }
            Add("gold", "gold", "gold", true); res.Add(K.E("sep"));
            for (int g = 0; g < GoodCount; g++) Add(GoodIds[g], GOOD_ICO[g], "good:" + GoodIds[g], true);
            res.Add(K.E("sep")); Add("pop", "people", "pop", false); Add("unrest", "warn", "unrest", false);
        }

        void BuildPops()
        {
            menu = K.E("pnl pop hidden"); menu.name = "menu"; hud.Add(menu);
            menu.Add(K.E("hd", K.L("Island Sim", "h2"))); menu.Add(K.Band());
            var items = K.E(); items.style.paddingTop = 6; menu.Add(items);
            Button Mi(string icon, string text, Action a, string key = null, Label lbl = null)
            {
                var b = K.B("mi", a, K.I(icon), lbl ?? K.L(text)); if (key != null) b.Add(K.L(key, "kbd")); items.Add(b); return b;
            }
            Mi("survey", "How to play", () => { ClosePops(); ToggleHelp(true); }, "F1");
            Mi("harbour", "Back to the capital", () => { ClosePops(); C.Home(S); });
            Mi("gear", "Settings", () => TogglePop(settings));
            newLbl = K.L("New game");
            Mi("terra", null, () => { if (!newArmed) { newArmed = true; newLbl.text = "Click again to start over"; return; } ClosePops(); G.Restart(); }, null, newLbl);
            saves = K.E("saves"); menu.Add(saves);

            settings = K.E("pnl pop hidden"); settings.name = "settings"; hud.Add(settings);
            settings.Add(K.E("hd", K.L("Settings", "h2"))); settings.Add(K.Band());
            var qs = K.E("segc");
            foreach (var (n, q) in new[] { ("Low", Quality.Low), ("High", Quality.High), ("Ultra", Quality.Ultra) }) { var b = K.T(n, null, () => G.SetQuality(q)); qBtns.Add((b, q)); qs.Add(b); }
            settings.Add(K.E("prow", K.L("Graphics"), qs));
            var us = K.E("segc");
            foreach (var v in new[] { .9f, 1f, 1.15f, 1.3f, 1.5f }) { var b = K.T(Mathf.RoundToInt(v * 100) + "%", null, () => G.SetUiScale(v)); uiBtns.Add((b, v)); us.Add(b); }
            settings.Add(K.E("prow", K.L("UI size"), us));

            logPop = K.E("pnl pop hidden"); logPop.name = "log"; hud.Add(logPop);
            logPop.Add(K.E("hd", K.L("Katheeb's log", "h2"))); logPop.Add(K.Band());
            var sv = new ScrollView(ScrollViewMode.Vertical); logList = sv.contentContainer; logPop.Add(sv);
        }

        void BuildCharter()
        {
            charter = K.E("pnl"); charter.name = "charter"; hud.Add(charter);
            chevron = K.I("chevd");
            var toggle = K.B("x", () => { bool o = !Shown(goalsBox); Show(goalsBox, o); chevron.style.backgroundImage = new StyleBackground(Icons.Get(o ? "chevu" : "chevd")); }, chevron);
            charter.Add(K.E("hd", cAge = K.L("Age of Cowries", "h2"), toggle)); charter.Add(K.Band());
            var bar0 = K.Bar(0); cBarFill = bar0[0];
            goalsBox = K.E("goals hidden");
            charter.Add(K.E("body", cGoal = K.L("", "goal b"), cWhy = K.L("", "why"), bar0, K.E("barlbl", cProg = K.L(""), cCount = K.L("")), goalsBox));
        }

        void BuildMap()
        {
            mapbox = K.E("pnl"); mapbox.name = "mapbox"; hud.Add(mapbox);
            miniTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "minimap" };
            mini = new Image { image = miniTex, scaleMode = ScaleMode.StretchToFill }; mini.name = "mini"; mapbox.Add(mini);
            bool md = false;
            void Jump(Vector2 lp) { var r = mini.contentRect; C.Goal = null; C.Tx = lp.x / Mathf.Max(1, r.width) * W; C.Tz = lp.y / Mathf.Max(1, r.height) * H; }
            mini.RegisterCallback<PointerDownEvent>(e => { md = true; mini.CapturePointer(e.pointerId); Jump(e.localPosition); });
            mini.RegisterCallback<PointerMoveEvent>(e => { if (md) Jump(e.localPosition); });
            mini.RegisterCallback<PointerUpEvent>(e => { md = false; mini.ReleasePointer(e.pointerId); });

            VisualElement Rot(string icon, int dir)
            {
                var b = K.E("cb"); b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center; b.Add(K.I(icon)); float t0 = 0;
                b.RegisterCallback<PointerDownEvent>(e => { b.CapturePointer(e.pointerId); t0 = Time.unscaledTime; C.Spin = dir; C.Goal = null; });
                b.RegisterCallback<PointerUpEvent>(e =>
                {
                    b.ReleasePointer(e.pointerId); if (C.Spin == 0) return; C.Spin = 0;
                    if (Time.unscaledTime - t0 < .28f) C.Goal = Mathf.Round((C.Yaw + dir * Mathf.PI / 4) / (Mathf.PI / 4)) * (Mathf.PI / 4);   // a tap turns 45°
                });
                return b;
            }
            needle = K.I("needle");
            var north = K.B(null, () => C.Goal = Mathf.Round(C.Yaw / (Mathf.PI * 2)) * Mathf.PI * 2, needle); north.name = "north";
            var home = K.B("cb", () => C.Home(S), K.I("home")); home.style.alignItems = Align.Center; home.style.justifyContent = Justify.Center;
            Tip(north, "north");
            mapbox.Add(K.E("compass", Rot("rotl", 1), north, Rot("rotr", -1), home));
        }

        void BuildBuildBar()
        {
            var build = K.E(); build.name = "build"; build.pickingMode = PickingMode.Ignore; hud.Add(build);
            cards = K.E("pnl hidden"); cards.name = "cards"; build.Add(cards);
            bar = K.E("pnl"); bar.name = "bar"; build.Add(bar);
            Button Tab(string id, string icon, string name, string key, Action a, string cls = "tab")
            {
                var b = K.B(cls, a, K.I(icon), K.L(name), K.L(key, "k")); tabs[id] = b; bar.Add(b); return b;
            }
            Tip(Tab("t:inspect", "survey", "Survey", "V", () => SetTool("inspect")), "tool:inspect");
            bar.Add(K.E("bsep"));
            for (int i = 0; i < CATS.Length; i++) { var c = CATS[i]; Tab("c:" + c.Id, c.Ico, c.Name, (i + 1).ToString(), () => SetCat(openCat == c.Id ? null : c.Id)); }
            bar.Add(K.E("bsep"));
            Tip(Tab("t:demolish", "demolish", "Demolish", "X", () => SetTool("demolish"), "tab danger"), "tool:demolish");
        }

        void BuildOver()
        {
            over = K.E("dim-bg hidden"); over.name = "over"; root.Add(over);
            var box = K.E("pnl box"); over.Add(box);
            box.Add(K.E("hd", overT = K.L("", "h2"))); box.Add(K.Band());
            overWhy = K.L("", "p"); overStats = K.L("", "p sub");
            box.Add(K.E("body", overWhy, overStats, Gap(K.E("acts", K.T("Start a new atoll", "btn go", () => G.Restart()), K.T("Load a save", "btn", () => { Show(over, false); TogglePop(menu); })), 8)));
        }

        readonly List<Button> islLabels = new List<Button>(), atollLabels = new List<Button>();
        void BuildLabels()
        {
            for (int k = 0; k < Islands.Length; k++)
            {
                int kk = k; var b = K.T(Islands[k].Name, "lbl", () => { var a = S.IslAnchor[kk]; if (a.HasValue) OpenIsland(S.IslandOf(a.Value.x, a.Value.y)); });
                labels.Add(b); islLabels.Add(b);
            }
            foreach (var A in Atolls)
            {
                var a = A; var b = K.T(A.Name.ToUpperInvariant(), "lbl atl", () => { C.Goal = null; C.Tx = (float)a.X; C.Tz = (float)a.Y; C.Dist = Mathf.Min(C.Dist, a.Lone ? 90 : 200); });
                labels.Add(b); atollLabels.Add(b);
            }
        }

        // ------------------------------------------------------------ is the pointer over the HUD?
        public bool Over(Vector2 screenTopLeft)
        {
            if (root?.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, screenTopLeft);
            var e = root.panel.Pick(p);
            return e != null && e != root;
        }
        public bool Typing => root?.panel?.focusController?.focusedElement is VisualElement f && (f is TextField || f.parent is TextField);
        public bool Blocking => Shown(help) || Shown(over);

        // ------------------------------------------------------------ build bar and cards
        public void SetCat(string id) { if (openCat == id) return; openCat = id; RenderCards(); }
        public void SetTool(string id)
        {
            if (IsBuilding(id, out var t)) { S.Tool = Core.Tool.Build; S.BuildType = t; }
            else S.Tool = (Core.Tool)Enum.Parse(typeof(Core.Tool), id, true);
            var c = CatOf(id);
            if (c != null && c != openCat) { openCat = c; RenderCards(); }
            else if (id == "inspect" && openCat != null) { openCat = null; RenderCards(); }
            else SyncPressed();
        }
        public void SetBrush(int n) { S.SetBrush(n); SyncPressed(); }
        public void SetLevel(int h) { S.LevelTo = h; if (Tool != "flatten") SetTool("flatten"); else SyncPressed(); }
        public void SetPlot(int n) { S.Plot = n; if (Tool != "house") SetTool("house"); else SyncPressed(); UpdateChips(); }

        readonly List<(Button b, string id)> cardBtns = new List<(Button, string)>();
        readonly List<(VisualElement el, string id)> chipBoxes = new List<(VisualElement, string)>();
        readonly List<(Button b, int h)> lvBtns = new List<(Button, int)>();
        readonly List<(Button b, int n)> plotBtns = new List<(Button, int)>();
        VisualElement lvBox; Label bsV; SliderInt bsR;

        void RenderCards()
        {
            var c = CATS.FirstOrDefault(x => x.Id == openCat); Show(cards, c != null);
            cards.Clear(); cardBtns.Clear(); chipBoxes.Clear(); lvBtns.Clear(); plotBtns.Clear(); lvBox = null; bsV = null; bsR = null;
            if (c != null)
            {
                foreach (var id in c.Tools)
                {
                    var chips = K.E("chips"); chipBoxes.Add((chips, id));
                    var b = K.B("card", () => SetTool(id), K.I(TOOL_ICO[id]), K.E("col", K.L(ToolName(id), "nm"), chips));
                    var key = ToolKey(id); if (!string.IsNullOrEmpty(key)) b.Add(K.L(key, "kbd"));
                    Tip(b, "tool:" + id); cards.Add(b); cardBtns.Add((b, id));
                }
                if (c.Brush)
                {
                    bsR = new SliderInt(1, 9) { value = S.BrushSize }; bsR.RegisterValueChangedCallback(e => SetBrush(e.newValue));
                    var box = K.E("brushes", Gap(K.E("lbl2", K.L("Brush size"), K.L("[", "kbd"), K.L("]", "kbd")), 4),
                        K.E("bsz", K.T("−", null, () => SetBrush(S.BrushSize - 1)), bsR, bsV = K.L(S.BrushSize.ToString(), "v b"), K.T("+", null, () => SetBrush(S.BrushSize + 1))));
                    if (c.Id == "terra") box.Add(K.E("lbl2", K.L("Ctrl+Z undoes the last 10 s")));
                    cards.Add(box);
                }
                if (c.Levels)
                {
                    var seg = K.E("segc lv");
                    foreach (var (h, n) in Levels) { var b = K.B(null, () => SetLevel(h), K.L(n), K.L(h.ToString(), "small")); lvBtns.Add((b, h)); seg.Add(b); }
                    lvBox = K.E("brushes", K.E("lbl2", K.L("Level to")), seg); cards.Add(lvBox);
                }
                if (c.Plots)
                {
                    var seg = K.E("segc"); var sz = Def(BType.House).Sizes;
                    for (int n = 0; n < sz.Length; n++) { int nn = n; var b = K.T($"{sz[n][0]}×{sz[n][1]}", null, () => SetPlot(nn)); plotBtns.Add((b, n)); seg.Add(b); }
                    var box = K.E("brushes", Gap(K.E("lbl2", K.L("Plot size"), K.L("P", "kbd")), 4), seg); box.Children().Last().style.marginTop = 4; cards.Add(box);
                }
                UpdateChips();
            }
            SyncPressed();
        }

        void SyncPressed()
        {
            string tool = Tool;
            foreach (var (b, id) in cardBtns) b.EnableInClassList("on", id == tool);
            tabs["t:inspect"].EnableInClassList("on", tool == "inspect"); tabs["t:demolish"].EnableInClassList("on", tool == "demolish");
            foreach (var c in CATS) tabs["c:" + c.Id].EnableInClassList("on", c.Id == openCat || (openCat == null && CatOf(tool) == c.Id));
            foreach (var (b, h) in lvBtns) b.EnableInClassList("on", h == S.LevelTo);
            if (lvBox != null) Show(lvBox, tool == "flatten");
            cards.EnableInClassList("compact", openCat == "terra");
            foreach (var (b, n) in plotBtns) b.EnableInClassList("on", n == S.Plot);
            if (bsR != null) { bsR.SetValueWithoutNotify(S.BrushSize); bsV.text = S.BrushSize.ToString(); }
        }

        double[] StockFor(string id) => id == "harbor" ? S.CapitalIsland?.Harbor?.Stock : S.StockOf(UiIsl);
        void Chips(VisualElement box, string id, double[] st)
        {
            box.Clear();
            void Chip(Good? g, int v) { double have = g == null ? S.Gold : st != null ? st[(int)g.Value] : 0; box.Add(K.E("chip" + (have < v ? " no" : ""), K.I(g == null ? "gold" : GOOD_ICO[(int)g.Value]), K.L(v.ToString()))); }
            if (IsBuilding(id, out var t)) { var c = S.CostOf(t); if (c.Gold > 0) Chip(null, c.Gold); if (c.Wood > 0) Chip(Good.Wood, c.Wood); if (c.Stone > 0) Chip(Good.Stone, c.Stone); }
            else if (TOOL_COST.TryGetValue(id, out var tc)) { Chip(null, tc.gold); if (tc.stone > 0) Chip(Good.Stone, tc.stone); }
        }
        void UpdateChips() { foreach (var (el, id) in chipBoxes) Chips(el, id, StockFor(id)); }

        // ------------------------------------------------------------ tooltips
        VisualElement tipFor; string tipKey;
        void ShowTip(VisualElement el, string key)
        {
            var kit = new UiKit { Record = false }; var body = TipBody(kit, key); if (body == null) return;
            tipFor = el; tipKey = key; tip.Clear(); tip.Add(body); PassThrough(tip); Show(tip, true);
            tip.schedule.Execute(() => PlaceTip(el));
            PlaceTip(el);
        }
        void PlaceTip(VisualElement el)
        {
            if (tipFor != el) return;
            var r = el.worldBound; var rr = root.worldBound; float tw = tip.resolvedStyle.width, th = float.IsNaN(tip.resolvedStyle.height) ? 120 : tip.resolvedStyle.height;
            float x = Mathf.Max(8, Mathf.Min(rr.width - tw - 8, r.center.x - tw / 2)), y = r.yMin > rr.height / 2 ? r.yMin - th - 10 : r.yMax + 10;
            tip.style.left = x; tip.style.top = Mathf.Max(8, y);
        }
        void HideTip() { tipFor = null; Show(tip, false); }

        VisualElement TipBody(UiKit k, string key)
        {
            var parts = key.Split(':'); string kind = parts[0], id = parts.Length > 1 ? parts[1] : null; var isl = UiIsl;
            var v = k.E("col");
            Label P(string s, string cls = "p") { var l = k.L(s, cls); v.Add(l); return l; }
            void Hr() => v.Add(k.E("hr"));
            if (kind == "tool")
            {
                if (IsBuilding(id, out var t))
                {
                    var d = Def(t); string sz = d.Size != null ? $"{d.Size[0]}×{d.Size[1]}" : "1×1";
                    P(d.Name, "t"); P(d.Info); Hr(); P("Makes: " + MAKES[id]); if (NEEDS.TryGetValue(id, out var nd)) P("Needs: " + nd);
                    P($"Size {sz} · height {d.MinH}–{d.MaxH} · {(d.Jobs > 0 ? d.Jobs + " workers" : "no workers")} · upkeep {d.Upkeep} gold/s", "k");
                }
                else
                {
                    P(ToolName(id), "t"); P(TOOL_TIP[id]);
                    var key2 = ToolKey(id); if (key2 != "") P($"Key {key2}{(new[] { "raise", "lower", "flatten", "smooth", "road" }.Contains(id) ? " · [ and ] change the brush" : "")}", "k");
                }
                return v;
            }
            if (kind == "good")
            {
                int g = Array.IndexOf(GoodIds, id); var st = S.StockOf(isl); var f = S.Flows(isl)[g];
                P($"{GoodLabel[g]} on {(isl?.Name ?? "no island")}", "t"); P($"{(st != null ? Math.Floor(st[g]) : 0)} in the warehouse"); Hr();
                P($"Made: {Rate1(f.Make * 60)} a minute"); P($"Used: {Rate1(f.Use * 60)} a minute"); P("Boats also carry goods to and from the capital.", "k");
                return v;
            }
            if (kind == "gold")
            {
                double inc = 0, up = 0; foreach (var b in S.Buildings) { up += b.Def.Upkeep; if (b.Type == BType.House) inc += b.Tax; }
                P("Gold", "t"); P($"Tax: +{Rate1(inc * 60)} a minute"); P($"Upkeep: −{Rate1(up * 60)} a minute"); P($"Trade: +{Fmt(S.Trade.Earned)} so far · next ship in {S.TradeIn} s");
                P($"Fed compounds pay tax; coconuts, a mosque and sweet toddy each raise it. Below {DebtLimit} gold the sultan takes the atoll.", "k");
                return v;
            }
            if (kind == "pop") { P("Residents", "t"); foreach (var i in S.Islands.Where(i => i.Harbor != null)) P($"{i.Name}: {Math.Floor(i.Res)} residents, {i.Jobs} jobs"); P("Each worked building needs 1 to 3 residents within walking distance.", "k"); return v; }
            if (kind == "unrest") { P($"Unrest {Noise.Round(S.Unrest)}%", "t"); P("Unrest rises while more than 30% of residents are hungry and falls when they are fed. At 100% they depose you."); return v; }
            if (kind == "isl") { P(isl?.Name ?? "", "t"); P("The goods shown are in this island's warehouse. Pan to another island to see its stock, or press Tab."); return v; }
            if (kind == "day") { P($"Day {S.Day}", "t"); P("A day lasts 30 seconds at normal speed."); return v; }
            if (kind == "north") { P("Compass", "t"); P("The needle points north: click it to face north. The arrows beside it rotate the view (Q / E), and the house returns to the capital."); return v; }
            return null;
        }

        // ------------------------------------------------------------ the top bar
        void SetCtr(string k, string val, double trend = 0, bool outv = false)
        {
            var c = ctrs[k]; c.V.text = val;
            if (c.Tr != null) { double a = Math.Abs(trend); bool show = a >= .5; c.Tr.text = show ? (trend > 0 ? "▲" : "▼") + Fmt(Noise.Round(a)) : ""; c.Tr.EnableInClassList("up", show && trend > 0); c.Tr.EnableInClassList("dn", show && trend < 0); }
            c.El.EnableInClassList("out", outv);
        }

        /// <summary>The island whose warehouse the top bar shows: the one under the camera, else the nearest settled one.</summary>
        Island ViewIsland()
        {
            int tx = Mathf.FloorToInt(C.Tx), tz = Mathf.FloorToInt(C.Tz); var isl = Sim.Inb(tx, tz) ? S.IslandOf(tx, tz) : null;
            return isl != null && isl.Harbor != null ? isl : (S.NearestHarborIsland(C.Tx, C.Tz) ?? S.CapitalIsland);
        }

        /// <summary>Four times a second, as in the web game.</summary>
        public void UpdateUI()
        {
            var vi = ViewIsland(); UiIsl = vi;
            islName.text = vi != null ? vi.Name : "No jetty";
            SetCtr("gold", Fmt(S.Gold), S.Rate * 60, S.Gold < 0);
            var st = S.StockOf(vi); var fl = S.Flows(vi);
            for (int g = 0; g < GoodCount; g++)
            {
                SetCtr(GoodIds[g], st != null ? Fmt(st[g]) : "–", (fl[g].Make - fl[g].Use) * 60, st != null && st[g] < 1 && fl[g].Use > 0);
                bool basic = g == (int)Good.Wood || g == (int)Good.Stone || g == (int)Good.Fish;
                ctrs[GoodIds[g]].El.EnableInClassList("idle", !basic && !(st != null && st[g] >= 1) && fl[g].Make == 0 && fl[g].Use == 0);
            }
            SetCtr("pop", Math.Floor(S.TotalPop).ToString("0"));
            Show(ctrs["unrest"].El, S.Unrest >= 5); SetCtr("unrest", Noise.Round(S.Unrest) + "%", 0, S.Unrest >= 50);
            dayLbl.text = S.Day.ToString();
            foreach (var (b, n) in speedBtns) b.EnableInClassList("on", n == G.Speed);
            foreach (var (b, q) in qBtns) b.EnableInClassList("on", q == G.Atmos.Q);
            RenderCharter(); UpdateChips(); RenderSel(false);
            if (tipFor != null) { if (tipFor.panel == null) HideTip(); else { var body = TipBody(new UiKit { Record = false }, tipKey); if (body != null) { tip.Clear(); tip.Add(body); PassThrough(tip); } } }
        }

        // ------------------------------------------------------------ charter
        (double have, double need)? GoalProg(string id)
        {
            switch (id)
            {
                case "grow": return (S.CapitalIsland?.Res ?? 0, 30);
                case "rope": return (S.StatRope, 40);
                case "fishers": return (S.ContentFishers(), Age1Fishers);
                case "advance": { var r = S.AgeReady(out _); return (r.Count(i => i.Have >= i.Need), r.Count); }
            }
            return null;
        }
        string goalsSig;
        void RenderCharter()
        {
            var open = Sim.GoalList.FirstOrDefault(g => !S.Goals.Contains(g.Id)); int done = Sim.GoalList.Count(g => S.Goals.Contains(g.Id));
            double f = 1; string prog = "Done";
            if (open != null) { var p = GoalProg(open.Id); f = p.HasValue ? Math.Min(1, p.Value.have / p.Value.need) : 0; prog = p.HasValue ? $"{Math.Floor(p.Value.have)} / {p.Value.need}" : "Not yet"; }
            cAge.text = S.AgeDone ? "Age of Cowries ✓" : "Age 1 · Age of Cowries";
            cGoal.text = open != null ? open.Title : "The Age of Cowries is complete";
            cWhy.text = open != null ? open.Hint : "The Age of Forts and Sail comes in the next update. Keep building.";
            cBarFill.style.width = Length.Percent((float)f * 100); cProg.text = prog; cCount.text = $"{done} of {Sim.GoalList.Length} met";
            string sig = string.Join(",", Sim.GoalList.Select(g => S.Goals.Contains(g.Id) ? "d" : g == open ? "n" : "o"));
            if (sig != goalsSig)
            {
                goalsSig = sig; goalsBox.Clear();
                foreach (var g in Sim.GoalList) goalsBox.Add(K.E("li" + (S.Goals.Contains(g.Id) ? " done" : g == open ? " now" : ""), K.E("dot"), K.L(g.Title, "t")));
            }
        }
        /// <summary>The charter card flashes when a goal is met.</summary>
        public void GoalMet() { charter.AddToClassList("won"); charter.schedule.Execute(() => charter.RemoveFromClassList("won")).StartingIn(900); }

        // ------------------------------------------------------------ advisor feed and log
        public void Toast(string msg, ToastKind kind, (int x, int y)? at)
        {
            float now = Time.unscaledTime; if (toastMem.TryGetValue(msg, out var t0) && now - t0 < 2.5f) return; toastMem[msg] = now;
            LOG.Insert(0, new LogItem { Msg = msg, Kind = kind, Day = S != null ? S.Day : 1, At = at }); if (LOG.Count > 20) LOG.RemoveAt(LOG.Count - 1);
            if (!Shown(logPop)) { unread++; RenderBadge(); } else RenderLog();
            string kc = kind == ToastKind.Good ? " good" : kind == ToastKind.Bad ? " bad" : "";
            var portrait = K.E("portrait"); portrait.style.backgroundImage = new StyleBackground(Icons.Get("portrait"));
            var text = K.E("col grow", K.L("The katheeb" + (at.HasValue ? " · click to go there" : ""), "who"), K.L(msg, "txt"));
            text.style.flexShrink = 1;
            VisualElement el = at.HasValue ? (VisualElement)K.B("msg pnl" + kc + " go", () => C.JumpTo(at.Value.x, at.Value.y), portrait, text) : K.E("msg pnl" + kc, portrait, text);
            el.style.whiteSpace = WhiteSpace.Normal;
            advisor.Add(el); while (advisor.childCount > 3) advisor.RemoveAt(0);
            el.schedule.Execute(() => el.AddToClassList("fade")).StartingIn(6000);
            el.schedule.Execute(() => el.RemoveFromHierarchy()).StartingIn(6700);
        }
        void RenderBadge() { badge.text = unread > 9 ? "9+" : unread.ToString(); Show(badge, unread > 0); }
        void RenderLog()
        {
            logList.Clear();
            if (LOG.Count == 0) { logList.Add(K.E("logitem", K.L("Nothing to report yet."))); return; }
            foreach (var m in LOG)
            {
                var cls = "logitem" + (m.Kind == ToastKind.Bad ? " bad" : m.Kind == ToastKind.Good ? " good" : "");
                var txt = K.L(m.Msg, "wrap"); txt.style.flexShrink = 1;
                var mm = m;
                logList.Add(m.At.HasValue ? (VisualElement)K.B(cls, () => C.JumpTo(mm.At.Value.x, mm.At.Value.y), K.L("Day " + m.Day, "d"), txt) : K.E(cls, K.L("Day " + m.Day, "d"), txt));
            }
        }

        // ------------------------------------------------------------ popovers, help, escape
        void ClosePops() { foreach (var (p, b) in new[] { (menu, menuBtn), (settings, setBtn), (logPop, logBtn) }) { Show(p, false); b.RemoveFromClassList("on"); } }
        void TogglePop(VisualElement p)
        {
            bool was = Shown(p); ClosePops(); if (was) return;
            Show(p, true); (p == menu ? menuBtn : p == settings ? setBtn : logBtn).AddToClassList("on");
            if (p == logPop) { unread = 0; RenderBadge(); RenderLog(); }
            if (p == menu) { newArmed = false; newLbl.text = "New game"; RenderSaves(); }
        }
        public void Escape()
        {
            if (Shown(help)) { ToggleHelp(false); return; }
            if (Shown(menu) || Shown(settings) || Shown(logPop)) { ClosePops(); return; }
            if (Tool != "inspect") { SetTool("inspect"); return; }
            if (openCat != null) { SetCat(null); return; }
            if (sel != null) { CloseSel(); return; }
            TogglePop(menu);
        }

        public void ToggleHelp(bool? force = null)
        {
            bool show = force ?? !Shown(help); if (!show) { Show(help, false); return; }
            ClosePops(); help.Clear(); var k = K; var rr = root.worldBound;
            void Note(VisualElement target, string title, string text, Action<VisualElement, Rect> place)
            {
                var n = k.E("note", k.L(title, "t"), k.L(text, "p")); help.Add(n); place(n, target.worldBound);
            }
            Note(hud.Q("res"), "Top bar", "Gold, then the goods in the warehouse of the island you are looking at, then residents. Arrows show the change per minute. A red counter has run out. Hover any counter for detail.", (n, r) => { n.style.left = Mathf.Max(8, r.center.x - 330); n.style.top = r.yMax + 14; });
            Note(charter, "Charter", "Your current goal and how close you are. The arrow lists every goal.", (n, r) => { n.style.right = rr.width - r.xMax; n.style.top = r.yMax + 12; });
            Note(advisor, "The katheeb", "Your island chief reports here. Click a message to go to the place; the bell keeps the last 20.", (n, r) => { n.style.left = r.xMin; n.style.top = Mathf.Max(r.yMin, 130); });
            Note(mapbox, "Minimap and compass", "Click to jump, drag to pan. The arrows rotate, the house returns to the capital, and the needle points north: click it to face north.", (n, r) => { n.style.left = r.xMax + 12; n.style.bottom = rr.height - r.yMax; });
            Note(bar, "Build bar", "Pick a category (1–7), then a building card. Hover a card for what it needs and makes. Right-click or Esc drops the tool.", (n, r) => { n.style.left = Mathf.Max(8, r.center.x - 120); n.style.bottom = rr.height - r.yMin + (openCat != null ? 90 : 14); });
            var keys = k.E("keys pnl", k.E("hd", k.L("How to play", "h2")), k.Band());
            foreach (var (a, b) in new[]
            {
                ("Click", "A building or an island's name for details"), ("Right or middle drag", "Rotate and tilt (also Shift + drag)"), ("Wheel · WASD", "Zoom · pan"),
                ("1–7", "Open a build category"), ("R / F / T / Y · G", "Raise / lower / level / smooth · sand road"), ("[ ] · Ctrl+Z", "Brush size 1–9 · undo reshaping (10 s)"),
                ("P · X · V", "Compound plot size · demolish · survey"), ("Q / E", "Rotate the camera"), ("Tab", "Next settled island"), ("Space", "Pause"),
                ("Esc", "Drop the tool, close a panel, then open the menu"),
            }) keys.Add(k.E("krow", k.L(a, "a"), k.L(b, "b2")));
            keys.Add(k.L("Press F1 or Esc, or click anywhere, to close", "close"));
            help.Add(keys); Show(help, true);
        }

        // ------------------------------------------------------------ saves
        void RenderSaves()
        {
            saves.Clear();
            void Row(string lbl, string key, int i)
            {
                var m = G.SlotMeta(key); var row = K.E("slot", K.L(lbl, "b"), K.L(m != null ? $" · day {m.Value.day}, {m.Value.pop} residents" : " · empty", "meta"));
                if (i >= 0) row.Add(K.T("Save", null, () => { if (G.SaveTo(key)) Toast($"Saved to slot {i + 1}.", ToastKind.Good, null); RenderSaves(); }));
                var load = K.T("Load", null, () => { ClosePops(); G.LoadFrom(key); }); load.SetEnabled(m != null); row.Add(load);
                saves.Add(row);
            }
            for (int i = 0; i < IslandGame.Slots.Length; i++) Row($"Slot {i + 1}", IslandGame.Slots[i], i);
            Row("Autosave", IslandGame.Auto, -1);
        }

        public void ShowGameOver(string kind)
        {
            overT.text = kind == "bankrupt" ? "The treasury is broken" : "The islanders have deposed you";
            overWhy.text = kind == "bankrupt" ? $"Debts passed {-DebtLimit} gold, so the sultan has handed the atoll to another governor." : "Too many families went hungry for too long. The island chiefs have chosen a new governor.";
            int days = (int)Math.Floor(S.T / 30);
            overStats.text = $"You lasted {1 + days} day{(days > 0 ? "s" : "")} and housed {Math.Floor(S.TotalPop)} residents. Charter goals met: {Sim.GoalList.Count(g => S.Goals.Contains(g.Id))} of {Sim.GoalList.Length}.";
            Show(over, true);
        }
        public void Reset() { sel = null; RenderSel(true); Show(over, false); }

        // ------------------------------------------------------------ selection and island panels
        public void SelectBuilding(Building b) { sel = b != null ? new Sel { Kind = "b", Id = b.Id } : null; RenderSel(true); }
        public void OpenIsland(Island isl) { sel = isl != null ? new Sel { Kind = "isl", Key = isl.Key, Name = isl.Name, Hid = isl.Harbor?.Id ?? -1 } : null; RenderSel(true); }
        public void CloseSel() { sel = null; RenderSel(false); }
        public bool HasSel => sel != null;
        Island SelIsland() => S.Islands.FirstOrDefault(i => sel.Hid >= 0 && i.Harbor != null && i.Harbor.Id == sel.Hid) ?? S.Islands.FirstOrDefault(i => i.Key == sel.Key) ?? S.Islands.FirstOrDefault(i => i.Name == sel.Name);

        string selSig = ""; TextField renameIn;
        void RenderSel(bool fresh)
        {
            if (sel != null && sel.Renaming && renameIn != null && renameIn.panel?.focusController?.focusedElement == renameIn) return;   // don't wipe what the player is typing
            var k = new UiKit(); VisualElement content = null;
            if (sel?.Kind == "b") { var b = S.ById(sel.Id); if (b != null) content = BuildingPanel(k, b); else sel = null; }
            else if (sel?.Kind == "isl") { var isl = SelIsland(); if (isl != null) content = IslandPanel(k, isl); else sel = null; }
            if (content == null) { Show(selPanel, false); selSig = ""; return; }
            string sig = k.Sig.ToString(); if (sig == selSig && !fresh) return; selSig = sig;
            selPanel.Clear(); selPanel.Add(content); Show(selPanel, true);
            if (renameIn != null && sel.Renaming) { var f = renameIn; f.schedule.Execute(() => { f.Focus(); f.SelectAll(); }); }
        }

        static VisualElement Node(UiKit k, string icon, string n, string lbl, bool me = false)
        {
            var v = k.E("node" + (me ? " me" : ""), k.I(icon)); if (n != null) v.Add(k.L(n, "n")); v.Add(k.L(lbl)); return v;
        }

        VisualElement BuildingPanel(UiKit k, Building b)
        {
            var d = b.Def; var isl = S.IslandOfB(b); string Per(double v) => Rate1(v * 60);
            string status = b.Off ? "Paused" : b.Status ?? "";
            string cls = b.Off ? "warn" : (status.Contains("Hungry") || status.Contains("No ") || status.Contains("Waiting") || status.Contains("stuck") || b.Eff < .5) ? "bad" : b.Eff < 1 && b.Type != BType.House ? "warn" : "";
            VisualElement flow = null; var rows = new List<VisualElement>(); var extra = new List<VisualElement>();
            VisualElement Me() => Node(k, TOOL_ICO[d.Id], null, d.Name, true);
            VisualElement Arr() => k.L("→", "arr");
            VisualElement Flow(params VisualElement[] n) => k.E("flow", n);
            VisualElement Out(Good g) => Node(k, GOOD_ICO[(int)g], Per(b.Rate), Lower1(GoodLabel[(int)g]) + "/min");
            VisualElement Yes(bool ok, string ic, string t) => k.E("pill " + (ok ? "ok" : "no"), k.I(ic), k.L(t + " " + (ok ? "✓" : "")), ok ? null : k.I("cross", "i"));
            VisualElement KvRow(string label, float f, string n, string barCls = null) => k.E("kvrow", k.L(label, "k"), k.Bar(f, barCls), k.L(n, "n"));
            switch (b.Type)
            {
                case BType.House:
                    {
                        var n = b.Need ?? new Needs(); double cap = b.Cap > 0 ? b.Cap : Sim.PlotFactor(b) * 4;
                        var nodes = new List<VisualElement> { Node(k, n.Rice ? "rice" : "fish", Per(.012 * b.Res), (n.Rice ? "rice" : "fish") + "/min") };
                        if (n.Coco) nodes.Add(Node(k, "coconut", Per(.006 * b.Res), "coconuts/min"));
                        if (n.Toddy) nodes.Add(Node(k, "toddy", Per(.004 * b.Res), "toddy/min"));
                        nodes.Add(Arr()); nodes.Add(Me()); nodes.Add(Arr()); nodes.Add(Node(k, "gold", Per(b.Tax), "tax/min"));
                        flow = Flow(nodes.ToArray());
                        rows.Add(KvRow("Residents", (float)(b.Res / cap), $"{b.Res:0.0} / {cap}"));
                        extra.Add(k.E("need", Yes(n.Food, n.Rice ? "rice" : "fish", n.Rice ? "Rice" : "Fish"), Yes(n.Coco, "coconut", "Coconuts"), Yes(n.Faith, "mosque", "Mosque nearby"), Yes(n.Toddy, "toddy", "Sweet toddy")));
                        extra.Add(k.L($"{b.W}×{b.D} plot. Each need met makes room for {2 * Sim.PlotFactor(b)} more residents and raises the tax.", "sub"));
                        break;
                    }
                case BType.Harbor: if (isl?.Harbor != null) extra.Add(Stock(k, isl)); if (b.Id == S.Capital) extra.Add(TradePanel(k)); break;
                case BType.Drying: flow = Flow(Node(k, "fish", Per(b.Rate * 2), "fish/min"), Node(k, "wood", Per(b.Rate * .4), "timber/min"), Arr(), Me(), Arr(), Out(Good.Hikimas)); break;
                case BType.Coir: flow = Flow(Node(k, "coconut", Per(b.Rate / .4), "coconuts/min"), Arr(), Me(), Arr(), Out(Good.Rope)); break;
                case BType.Mosque:
                    {
                        int n = 0; double r = 0; var q = Sim.PlotCentre(b);
                        foreach (var h in S.Buildings) if (h.Type == BType.House && h.Isl == b.Isl) { var c = Sim.PlotCentre(h); if (Noise.Hypot(c.x - q.x, c.y - q.y) <= MosqueWalk) { n++; r += h.Res; } }
                        extra.Add(k.L($"Serves {n} compound{(n == 1 ? "" : "s")} ({Math.Floor(r)} residents) within {MosqueWalk} tiles.", "sub")); break;
                    }
                case BType.Palace: extra.Add(AgePanel(k)); break;
                default:
                    {
                        Good? g = b.Type == BType.Fishery ? Good.Fish : b.Type == BType.Lumber ? Good.Wood : b.Type == BType.Quarry ? Good.Stone : b.Type == BType.Grove ? Good.Coconut : b.Type == BType.Toddy ? Good.Toddy : b.Type == BType.Cowrie ? Good.Cowrie : (Good?)null;
                        if (g.HasValue) flow = Flow(Me(), Arr(), Out(g.Value)); break;
                    }
            }
            if (d.Jobs > 0)
            {
                int filled = b.Off ? 0 : (int)Noise.Round(b.Staff * d.Jobs);
                rows.Add(KvRow("Workers", filled / (float)d.Jobs, $"{filled} / {d.Jobs}", filled < d.Jobs ? "warn" : null));
                rows.Add(KvRow("Output", (float)b.Eff, $"{Noise.Round(b.Eff * 100)}%", b.Eff < .5 ? "bad" : b.Eff < 1 ? "warn" : null));
                extra.Add(k.E("need", k.E("pill " + (b.Road ? "ok" : "no"), k.I("road"), k.L(b.Road ? "Beside a road: +25% output" : "No road beside it: build one for +25%"))));
            }
            int refund = (int)Math.Floor(Sim.CostOfB(b).Gold / 2.0);
            var acts = new List<VisualElement>();
            if (b.Type == BType.Palace && !S.AgeDone) { S.AgeReady(out bool ok); var adv = k.B("btn go", () => { S.AdvanceAge(); RenderSel(true); }, k.I("palace"), k.L("Advance the age")); adv.SetEnabled(ok); k.Note("adv" + ok); acts.Add(adv); }
            if (d.Jobs > 0) acts.Add(k.B("btn", () => { b.Off = !b.Off; RenderSel(false); }, k.I(b.Off ? "play" : "pause"), k.L(b.Off ? "Resume" : "Pause")));
            if (b.Type == BType.Harbor) acts.Add(k.B("btn", () => OpenIsland(S.IslandOfB(b)), k.I("harbour"), k.L("Island")));
            if (b.Id != S.Capital) acts.Add(k.B("btn red", () => { if (!sel.Armed) { sel.Armed = true; RenderSel(false); return; } S.Demolish(b.X, b.Y); CloseSel(); }, k.I("demolish"), k.L(sel.Armed ? "Click again to demolish" : $"Demolish (+{refund})")));

            var title = k.E("row", k.L(b.Id == S.Capital ? "Jetty and trade post" : d.Name, "h2")); if (b.Id == S.Capital) title.Add(StarIcon(k));
            var hd = k.E("hd", k.I(TOOL_ICO[d.Id]), k.E("col", title, k.L((isl?.Name ?? "") + (b.Id == S.Capital ? " · capital" : ""), "small")), k.T("×", "x", CloseSel));
            var body = k.E("body", k.E("status " + cls, k.E("dot"), k.L(status, "t")));
            if (flow != null) body.Add(flow); foreach (var e in extra) body.Add(e);
            if (rows.Count > 0) body.Add(k.E("kv", rows.ToArray()));
            if (acts.Count > 0) body.Add(Gap(k.E("acts", acts.ToArray()), 8));
            Gap(body, 10, false);
            return k.E("col", hd, k.Band(), body);
        }
        static VisualElement StarIcon(UiKit k) { var s = k.I("star"); s.style.width = s.style.height = 16; s.style.marginLeft = 6; s.style.unityBackgroundImageTintColor = World.Srgb(0xd9a441); return s; }

        // the capital jetty's trade post: what ships may buy, and the rice they bring
        VisualElement TradePanel(UiKit k)
        {
            var T = S.Trade; var v = k.E("trade", k.E("t2", k.I("trade"), k.L($"Trade post · next ship in {S.TradeIn} s")));
            foreach (var (g, price) in Price)
            {
                var gg = g; bool on = T.Sell[(int)g];
                v.Add(k.B("tg " + (on ? "on" : "off"), () => { T.Sell[(int)gg] = !T.Sell[(int)gg]; RenderSel(false); }, k.I(GOOD_ICO[(int)g]), k.L("Sell " + Lower1(GoodLabel[(int)g]), "lab"), k.L(price + "g", "p")));
            }
            v.Add(k.B("tg " + (T.Rice ? "on" : "off"), () => { T.Rice = !T.Rice; RenderSel(false); }, k.I("rice"), k.L($"Buy rice up to {RiceKeep}", "lab"), k.L(RicePrice + "g", "p")));
            string last = T.Last != null ? $"Day {T.Last.Day}: sold {T.Last.Sold}{(T.Last.Rice > 0 ? $", bought {T.Last.Rice} rice" : "")}." : "No ship has called yet.";
            var note = k.L((!S.AgeDone ? $"Keeps {Age1Rope} rope for the charter, and 20 fish and coconuts for the families. " : "") + last, "sub"); note.style.marginTop = 5; v.Add(note);
            return v;
        }
        VisualElement AgePanel(UiKit k)
        {
            if (S.AgeDone) return k.L("The Age of Cowries is complete. The Age of Forts and Sail comes in the next update.", "sub");
            var items = S.AgeReady(out _); var v = k.E("col", k.E("t2", k.L("To end the Age of Cowries")));
            foreach (var i in items) { var li = k.E("li" + (i.Have >= i.Need ? " ok" : ""), k.E("dot"), k.L(i.Title, "t")); if (i.Need > 1) li.Add(k.L($"{Fmt(Math.Min(i.Have, i.Need))} / {Fmt(i.Need)}", "n")); v.Add(li); }
            v[1].style.marginTop = 5;
            return v;
        }
        VisualElement Stock(UiKit k, Island isl)
        {
            var st = isl.Harbor.Stock; var f = S.Flows(isl); var v = k.E("stock");
            for (int g = 0; g < GoodCount; g++)
            {
                bool basic = g == (int)Good.Wood || g == (int)Good.Stone || g == (int)Good.Fish;
                if (!(st[g] >= 1 || f[g].Make != 0 || f[g].Use != 0 || basic)) continue;
                double net = (f[g].Make - f[g].Use) * 60, a = Math.Abs(net); bool outv = st[g] < 1 && f[g].Use > 0;
                var cell = k.E("g" + (outv ? " out" : ""), k.I(GOOD_ICO[g]), k.L(Fmt(st[g]), "n b"), k.L(a >= .5 ? (net > 0 ? "▲" : "▼") + Rate1(a) : "steady", "tr" + (a >= .5 ? net > 0 ? " up" : " dn" : "")));
                Tip(cell, "good:" + GoodIds[g]); v.Add(cell);
            }
            return v;
        }
        VisualElement IslandPanel(UiKit k, Island isl)
        {
            var hb = isl.Harbor; bool cap = hb != null && hb.Id == S.Capital;
            var kv = k.E("kv", k.E("kvrow", k.L("Atoll", "k"), k.L(Atolls[isl.Atoll].Name, "n")), k.E("kvrow", k.L("Residents", "k"), k.L(Math.Floor(isl.Res).ToString("0"), "n")));
            if (isl.Jobs > 0) kv.Add(k.E("kvrow", k.L("Jobs filled", "k"), k.Bar((float)isl.Eff, isl.Eff < 1 ? "warn" : null), k.L($"{Noise.Round(isl.Eff * 100)}%", "n")));
            var body = k.E("body", kv);
            if (hb != null)
            {
                body.Add(Stock(k, isl));
                var bt = S.Boats.FirstOrDefault(x => x.Home == hb.Id);
                string line = cap ? (S.Boats.Count > 0 ? $"{S.Boats.Count} boat{(S.Boats.Count > 1 ? "s" : "")} trade with the capital" : "No other jetties yet, so no dhonis.")
                    : bt != null ? (bt.State == "blocked" ? bt.Why : $"Boat {(bt.Out ? "sailing to the capital" : "sailing home")} · {bt.Trips} trips") : "Goods move overland to the capital.";
                var l = k.L(line, "wrap"); if (bt != null && bt.State == "blocked" && !cap) l.style.color = World.Srgb(0x8c2a1f);
                body.Add(k.E("boat", k.I("harbour"), l));
            }
            else body.Add(k.L($"No jetty yet. Build one on its coast to settle {isl.Name}.", "sub"));
            int ax = hb != null ? hb.X : (int)Noise.Round(isl.Cx), ay = hb != null ? hb.Y : (int)Noise.Round(isl.Cy);
            body.Add(Gap(k.E("acts", k.B("btn go", () => C.JumpTo(ax, ay), k.I("pin"), k.L("Go there")), k.B("btn", () => { sel.Renaming = true; RenderSel(true); }, k.I("edit"), k.L("Rename"))), 8));
            Gap(body, 10, false);

            VisualElement title;
            if (sel.Renaming)
            {
                renameIn = new TextField { value = isl.Name, maxLength = 28 }; renameIn.AddToClassList("rename"); k.Note("rename");
                renameIn.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { e.StopPropagation(); FinishRename(true); }
                    else if (e.keyCode == KeyCode.Escape) { e.StopPropagation(); FinishRename(false); }
                }, TrickleDown.TrickleDown);
                renameIn.RegisterCallback<FocusOutEvent>(_ => FinishRename(true));
                title = renameIn;
            }
            else { title = k.E("row", k.L(isl.Name, "h2")); if (cap) title.Add(StarIcon(k)); renameIn = null; }
            string small = sel.Renaming ? "Enter to save, Esc to cancel, empty for the old name" : cap ? "Capital island" : hb != null ? "Settled island" : "Unsettled";
            if (!sel.Renaming && isl.Name != isl.AutoName) small += " · once " + isl.AutoName;
            var hd = k.E("hd", k.I("harbour"), k.E("col grow", title, k.L(small, "small")), k.T("×", "x", CloseSel));
            return k.E("col", hd, k.Band(), body);
        }
        // renaming an island: Enter or leaving the box saves, Esc cancels, an empty box restores the map's name
        void FinishRename(bool save)
        {
            if (sel == null || !sel.Renaming) return; sel.Renaming = false;
            var isl = SelIsland(); var inp = renameIn;
            if (save && inp != null && isl != null)
            {
                var v = System.Text.RegularExpressions.Regex.Replace(inp.value ?? "", @"\s+", " ").Trim(); if (v.Length > 28) v = v.Substring(0, 28);
                if (v == "" || v == isl.AutoName) S.Names.Remove(isl.Key); else S.Names[isl.Key] = v;
                isl.Name = S.Names.TryGetValue(isl.Key, out var nm) ? nm : isl.AutoName; sel.Name = isl.Name; selSig = ""; UpdateUI();
            }
            renameIn = null; RenderSel(true);
        }

        public void NextIsland(int dir)
        {
            var list = S.Islands.Where(i => i.Harbor != null).OrderBy(i => i.Harbor.Id).ToList(); if (list.Count == 0) return;
            var cur = sel?.Kind == "isl" ? SelIsland() : UiIsl; int k = list.IndexOf(cur); k = k < 0 ? 0 : (k + dir + list.Count) % list.Count;
            var isl = list[k]; C.JumpTo(isl.Harbor.X, isl.Harbor.Y); OpenIsland(isl);
        }

        // ------------------------------------------------------------ minimap
        Color32[] miniBase, miniFrame; bool miniBaseOk;
        static Color32 RGB(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);
        static readonly Color32[] MCOL = { RGB(19, 74, 94), RGB(31, 125, 140), RGB(79, 196, 192), RGB(241, 227, 191), RGB(156, 196, 106), RGB(201, 180, 138), RGB(179, 154, 112) };
        static readonly Color32 MC_POND = RGB(92, 120, 96), MC_KULHI = RGB(84, 118, 72), MC_TARO = RGB(176, 206, 110), MC_GRASS = RGB(70, 160, 150), MC_ROAD = RGB(216, 195, 154),
            MC_TREE = RGB(95, 148, 69), MC_HOUSE = RGB(250, 246, 236), MC_HARBOR = RGB(163, 54, 42), MC_WORK = RGB(217, 164, 65);
        // the ground layer is painted only when the map changes; each tick copies it and adds boats and the view outline
        void PaintMiniBase()
        {
            miniBase = miniBase ?? new Color32[W * H]; var types = new Dictionary<int, BType>(); foreach (var b in S.Buildings) types[b.Id] = b.Type;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = Sim.Idx(x, y), h = S.Hgt[i]; var c = MCOL[Mathf.Clamp(h + 3, 0, 6)]; int g = S.Gt[i];
                    if (g == GT.Pond) c = MC_POND; else if (g == GT.Kulhi) c = MC_KULHI; else if (g == GT.Taro) c = MC_TARO; else if (g == GT.Seagrass) c = MC_GRASS;
                    if (S.Road[i] != 0) c = MC_ROAD; else if (h >= 1 && S.Tree[i] == 1) c = MC_TREE;
                    int id = S.Bld[i]; if (id >= 0 && types.TryGetValue(id, out var t)) c = t == BType.House ? MC_HOUSE : t == BType.Harbor ? MC_HARBOR : MC_WORK;
                    miniBase[(H - 1 - y) * W + x] = c;
                }
            S.MiniDirty = false; miniBaseOk = true;
        }
        void Px(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < W && y < H) miniFrame[(H - 1 - y) * W + x] = c; }
        void Blend(int x, int y, Color c, float a) { if (x < 0 || y < 0 || x >= W || y >= H) return; int i = (H - 1 - y) * W + x; Color o = miniFrame[i]; miniFrame[i] = Color.Lerp(o, c, a); }
        public void DrawMini()
        {
            if (S.MiniDirty || !miniBaseOk) PaintMiniBase();
            miniFrame = miniFrame ?? new Color32[W * H]; Array.Copy(miniBase, miniFrame, miniBase.Length);
            foreach (var bt in S.Boats) { var c = bt.State == "blocked" ? RGB(255, 107, 90) : RGB(255, 255, 255); int bx = (int)Math.Floor(bt.X - 2), by = (int)Math.Floor(bt.Y - 2); for (int dy = 0; dy < 5; dy++) for (int dx = 0; dx < 5; dx++) Px(bx + dx, by + dy, c); }
            if (S.Trader != null) { int bx = (int)Math.Floor(S.Trader.X - 3), by = (int)Math.Floor(S.Trader.Y - 3); for (int dy = 0; dy < 7; dy++) for (int dx = 0; dx < 7; dx++) Px(bx + dx, by + dy, MC_WORK); }
            // the view: the ground under the screen's corners
            var q = new[] { C.GroundAt(-1, 1), C.GroundAt(1, 1), C.GroundAt(1, -1), C.GroundAt(-1, -1) };
            var coral = World.Srgb(0xf2ece0);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(q.Min(p => p.y))), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(q.Max(p => p.y)));
            var xs = new List<float>();
            for (int y = y0; y <= y1; y++)
            {
                float fy = y + .5f; xs.Clear();
                for (int e = 0; e < 4; e++) { var a = q[e]; var b = q[(e + 1) % 4]; if ((a.y <= fy) != (b.y <= fy)) xs.Add(a.x + (fy - a.y) / (b.y - a.y) * (b.x - a.x)); }
                xs.Sort();
                for (int e = 0; e + 1 < xs.Count; e += 2) for (int x = Mathf.Max(0, Mathf.CeilToInt(xs[e] - .5f)); x <= Mathf.Min(W - 1, Mathf.FloorToInt(xs[e + 1] - .5f)); x++) Blend(x, y, coral, .16f);
            }
            for (int e = 0; e < 4; e++)
            {
                var a = q[e]; var b = q[(e + 1) % 4]; int n = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
                for (int s = 0; s <= n; s++) { var p = Vector2.Lerp(a, b, s / (float)n); int px = Mathf.FloorToInt(p.x - .5f), py = Mathf.FloorToInt(p.y - .5f); Px(px, py, coral); Px(px + 1, py, coral); Px(px, py + 1, coral); Px(px + 1, py + 1, coral); }
            }
            miniTex.SetPixels32(miniFrame); miniTex.Apply(false);
        }

        // ------------------------------------------------------------ per frame: labels, cursor tag, compass
        public void Refuse(string msg) { tagErr = msg; tagUntil = Time.unscaledTime + 2.4f; }

        public void Frame(Vector2Int? hover, bool dragging, Vector2 mouse)
        {
            needle.style.rotate = new Rotate(new Angle(C.Yaw * Mathf.Rad2Deg, AngleUnit.Degree));
            UpdateLabels(); UpdateTag(hover, dragging, mouse);
        }

        void PlaceLabel(VisualElement el, float x, float y, float hgt)
        {
            var w = World.U(x, hgt, y); var vp = C.Cam.WorldToViewportPoint(w);
            if (vp.z < 0 || vp.x < -.05f || vp.x > 1.05f || vp.y < -.05f || vp.y > 1.05f) { el.style.display = DisplayStyle.None; return; }
            var p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, w, C.Cam);
            el.style.display = DisplayStyle.Flex; el.style.left = p.x; el.style.top = p.y;
        }
        void UpdateLabels()
        {
            if (root.panel == null) return;
            bool far = C.Dist > 280, close = C.Dist < 210;
            for (int k = 0; k < Islands.Length; k++)
            {
                var a = S.IslAnchor[k]; var el = islLabels[k]; if (far || !a.HasValue) { el.style.display = DisplayStyle.None; continue; }
                var nm = S.Names.TryGetValue("k" + k, out var n) ? n : Islands[k].Name; if (el.text != nm) el.text = nm;
                var isl = S.IslandOf(a.Value.x, a.Value.y);
                var mid = isl != null && isl.Parts.Count == 1 && Islands[k].Kind != "vilu" ? ((float)isl.Cx, (float)isl.Cy) : ((float)Sim.IslC[k].x, (float)Sim.IslC[k].y);
                PlaceLabel(el, mid.Item1 + .5f, mid.Item2 + .5f, World.TopY(2) + 2.2f);
            }
            for (int k = 0; k < Atolls.Length; k++)
            {
                var A = Atolls[k]; var el = atollLabels[k]; if (close) { el.style.display = DisplayStyle.None; continue; }
                PlaceLabel(el, (float)A.X, (float)(A.Y - (A.Lone ? A.R + 4 : 0)), A.Lone ? 6 : 2);
            }
        }

        string tagSig;
        void UpdateTag(Vector2Int? hover, bool dragging, Vector2 mouse)
        {
            string tool = Tool;
            if (!hover.HasValue || tool == "inspect" || dragging || Blocking) { Show(tag, false); tagSig = null; return; }
            var k = new UiKit(); var v = k.E("col"); bool bad = false; int hx = hover.Value.x, hy = hover.Value.y, i = Sim.Idx(hx, hy);
            VisualElement Chip(string icon, string text, bool no = false) => k.E("chip" + (no ? " no" : ""), k.I(icon), k.L(text));
            if (IsBuilding(tool, out var t))
            {
                var d = Def(t); var r = S.CanPlace(t, hx, hy); var isl = S.IslandOf(hx, hy);
                var st = t == BType.Harbor ? S.CapitalIsland?.Harbor?.Stock : S.StockOf(isl);
                v.Add(k.L(d.Name, "t")); var chips = k.E("chips"); Chips(chips, tool, st); v.Add(chips); k.Note(string.Join(",", chips.Children().Select(c => c.ClassListContains("no") ? "n" : "y")));
                if (d.Jobs > 0 && isl != null) v.Add(k.L($"{Math.Floor(S.WorkersNear(hx, hy, isl.Id))} residents within walking distance (needs {d.Jobs})", "sub"));
                if (!r.Ok && !string.IsNullOrEmpty(r.Why)) { bad = true; v.Add(k.L(r.Why, "why")); }
            }
            else if (tool == "demolish")
            {
                var b = S.BAt(hx, hy);
                if (b != null) { v.Add(k.L("Demolish " + b.Def.Name, "t")); if (b.Id == S.Capital) { bad = true; v.Add(k.L("The capital jetty cannot be demolished", "why")); } else v.Add(k.L($"Refund {Math.Floor(Sim.CostOfB(b).Gold / 2.0)} gold")); }
                else if (S.Road[i] != 0) { v.Add(k.L("Remove sand road", "t")); v.Add(k.L("Refund 1 gold")); }
                else { v.Add(k.L("Demolish", "t")); v.Add(k.L("Nothing to remove here", "sub")); }
            }
            else
            {
                int hv = S.Hgt[i]; string lvName = Levels.FirstOrDefault(l => l.h == S.LevelTo).name?.ToLowerInvariant();
                v.Add(k.L($"{TERRA[tool].name}{(tool == "flatten" ? $" to {lvName} ({S.LevelTo})" : "")} · brush {S.BrushSize}", "t"));
                v.Add(k.L($"{(S.Gt[i] != 0 ? GT.Name[S.Gt[i]] : TerrainName(hv))} · height {hv}", "sub"));
                var bc = Sim.IsTerra(S.Tool) ? S.PreviewBrush(hx, hy) : null;
                if (S.CurStroke != null && (S.SpendGold != 0 || S.SpendStone != 0))
                {
                    var row = k.E("chips", Chip("gold", Noise.Round(S.SpendGold).ToString("0")));
                    if (S.SpendStone != 0) row.Add(Chip("stone", (S.SpendStone > 0 ? "" : "+") + Fmt(Math.Abs(S.SpendStone))));
                    row.Add(k.L("spent", "sub")); v.Add(row);
                }
                else if (bc != null)
                {
                    if (bc.N > 0)
                    {
                        var row = k.E("chips", Chip("gold", Noise.Round(bc.Gold).ToString("0"), S.Gold < bc.Gold));
                        if (bc.Stone != 0) row.Add(Chip("stone", (bc.Stone > 0 ? "" : "+") + Fmt(Math.Abs(bc.Stone))));
                        row.Add(k.L($"{bc.N} tile{(bc.N > 1 ? "s" : "")}", "sub")); v.Add(row);
                    }
                    else v.Add(k.L("Nothing to change here", "sub"));
                }
                else { var row = k.E("chips"); Chips(row, tool, S.StockOf(UiIsl)); row.Add(k.L(tool == "road" ? "per tile" : "per step", "sub")); v.Add(row); }
            }
            if (tagErr != null && tagUntil > Time.unscaledTime) { bad = true; v.Add(k.L(tagErr, "why")); }
            string sig = k.Sig.ToString();
            if (sig != tagSig) { tag.Clear(); tag.Add(v); PassThrough(tag); tagSig = sig; }
            tag.EnableInClassList("bad", bad); Show(tag, true);
            var pm = RuntimePanelUtils.ScreenToPanel(root.panel, mouse); bool flip = pm.x > root.worldBound.width - 290;
            float tw = tag.resolvedStyle.width; if (float.IsNaN(tw)) tw = 200;
            tag.style.left = flip ? pm.x - tw - 16 : pm.x + 16; tag.style.top = pm.y + 14;
        }

        // ------------------------------------------------------------ keyboard (the web game's keydown handler)
        public void Keys()
        {
            if (InputBridge.Pressed(InputBridge.Sp.F1)) { ToggleHelp(); return; }
            if (Typing) return;
            if (InputBridge.Pressed(InputBridge.Sp.Esc)) { Escape(); return; }
            bool ctrl = InputBridge.Held(InputBridge.Sp.Ctrl);
            if (ctrl && InputBridge.Pressed('z') && !Shown(help)) { S.UndoTerraform(); return; }
            if (Shown(help) || ctrl || InputBridge.Held(InputBridge.Sp.Alt)) return;
            if (InputBridge.Pressed(InputBridge.Sp.Tab)) { NextIsland(InputBridge.Held(InputBridge.Sp.Shift) ? -1 : 1); return; }
            for (int n = 1; n <= CATS.Length; n++) if (InputBridge.Pressed((char)('0' + n))) { var c = CATS[n - 1].Id; SetCat(openCat == c ? null : c); return; }
            foreach (var id in TerraIds) if (InputBridge.Pressed(char.ToLowerInvariant(TERRA[id].key[0]))) SetTool(id);
            foreach (var d in Buildings) if (!string.IsNullOrEmpty(d.Key) && InputBridge.Pressed(char.ToLowerInvariant(d.Key[0]))) SetTool(d.Id);
            if (InputBridge.Pressed('b')) SetBrush(S.BrushSize >= 9 ? 1 : S.BrushSize >= 5 ? 9 : S.BrushSize + 2);
            if (InputBridge.Pressed('[')) SetBrush(S.BrushSize - 1);
            if (InputBridge.Pressed(']')) SetBrush(S.BrushSize + 1);
            if (InputBridge.Pressed('p')) SetPlot(1 - S.Plot);
            if (InputBridge.Pressed(' ') && S.Over == null) G.SetSpeed(G.Speed != 0 ? 0 : 1);
        }
    }
}
