using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace IslandSim.View
{
    /// <summary>
    /// Small helpers for building the HUD in code. A Kit also records a signature of everything it builds, so a panel can
    /// be rebuilt from scratch each refresh and swapped in only when something visible changed (as the web game compares
    /// its generated HTML).
    /// </summary>
    public sealed class UiKit
    {
        public readonly StringBuilder Sig = new StringBuilder();
        public bool Record = true;
        void S(string a, string b = null) { if (!Record) return; Sig.Append(a).Append('|'); if (b != null) Sig.Append(b).Append('|'); }

        public static void Cls(VisualElement v, string cls) { if (string.IsNullOrEmpty(cls)) return; foreach (var c in cls.Split(' ')) if (c.Length > 0) v.AddToClassList(c); }

        public VisualElement E(string cls = null, params VisualElement[] kids)
        {
            var v = new VisualElement(); Cls(v, cls); S("E", cls);
            foreach (var k in kids) if (k != null) v.Add(k);
            return v;
        }
        public Label L(string text, string cls = null)
        {
            var l = new Label(text ?? "") { enableRichText = false }; l.ClearClassList(); Cls(l, cls); S("L" + text, cls);
            return l;
        }
        public Button B(string cls, Action click, params VisualElement[] kids)
        {
            var b = new Button(click) { text = "" }; b.ClearClassList(); Cls(b, cls); S("B", cls);
            foreach (var k in kids) if (k != null) b.Add(k);
            return b;
        }
        /// <summary>A button with plain text.</summary>
        public Button T(string text, string cls, Action click) { var b = new Button(click) { text = text, enableRichText = false }; b.ClearClassList(); Cls(b, cls); S("T" + text, cls); return b; }
        public VisualElement I(string name, string cls = "i")
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore }; Cls(v, cls); S("I" + name, cls);
            v.style.backgroundImage = new StyleBackground(Icons.Get(name));
            return v;
        }
        /// <summary>A progress bar; f is 0..1.</summary>
        public VisualElement Bar(float f, string cls = null)
        {
            var b = E("bar " + (cls ?? "")); var i = new VisualElement(); i.AddToClassList("fill"); i.style.width = Length.Percent(Mathf.Clamp01(f) * 100); b.Add(i);
            S("bar" + Mathf.RoundToInt(f * 1000));
            return b;
        }
        /// <summary>The carved coral-stone frieze under a panel header (a tiled texture in Hud.uss).</summary>
        public VisualElement Band() { var b = E("band"); b.pickingMode = PickingMode.Ignore; return b; }
        public void Note(string s) => S(s);

        /// <summary>Spaces children evenly (USS has no gap property).</summary>
        public static T Gap<T>(T v, float px, bool row = true) where T : VisualElement
        {
            int n = 0;
            foreach (var c in v.Children()) { if (n++ == 0) continue; if (row) c.style.marginLeft = px; else c.style.marginTop = px; }
            return v;
        }
        /// <summary>Lets the pointer pass through an element and everything in it (the cursor tag and tooltips).</summary>
        public static void PassThrough(VisualElement v) => v.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
        public static void Show(VisualElement v, bool on) => v.EnableInClassList("hidden", !on);
        public static bool Shown(VisualElement v) => !v.ClassListContains("hidden");
    }
}
