using System.Collections.Generic;
using IslandSim.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandSim.View
{
    /// <summary>World-space conventions shared by every view: one tile is one unit, map row y runs toward -Z.</summary>
    public static class World
    {
        public const float Lvl = 0.62f;     // world units per height level
        public const float WaterY = 0.02f;

        public static float TopY(int h) => h >= 0 ? 0.28f + h * Lvl : h * Lvl - 0.12f;

        /// <summary>Tile-space point (continuous, 0..W / 0..H) to world XZ. Rows are flipped so the atoll reads as in the web prototype.</summary>
        public static Vector3 Pos(float tx, float ty, float y) => new Vector3(tx, y, Defs.H - ty);

        public static Vector3 TileCentre(Sim s, int x, int y) => Pos(x + .5f, y + .5f, TopY(s.Hgt[Sim.Idx(x, y)]));

        public static bool ToTile(Vector3 p, out int x, out int y)
        {
            x = Mathf.FloorToInt(p.x); y = Mathf.FloorToInt(Defs.H - p.z);
            return Sim.Inb(x, y);
        }

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    }

    /// <summary>Material factory that works under URP (preferred) and the built-in pipeline.</summary>
    public static class Mats
    {
        static Material template;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        public static bool Urp { get; private set; }

        public static void Init()
        {
            Urp = GraphicsSettings.currentRenderPipeline != null;
            cache.Clear();
            template = Resources.Load<Material>("IslandSim/IslandLit");
            if (template == null)
            {
                Shader sh = null;
                if (Urp) sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                template = new Material(sh);
            }
        }

        public static Material Lit(Color c, float smoothness = .12f, float metallic = 0, Texture tex = null)
        {
            var m = new Material(template) { enableInstancing = true };
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (tex != null)
            {
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
            return m;
        }

        /// <summary>Shared lit material by sRGB hex colour.</summary>
        public static Material Get(string hex, float smoothness = .12f, float metallic = 0)
        {
            string key = hex + "/" + smoothness + "/" + metallic;
            if (!cache.TryGetValue(key, out var m)) { m = Lit(World.Hex(hex), smoothness, metallic); m.name = "isim " + hex; cache[key] = m; }
            return m;
        }

        public static Material Overlay(Color c, float frame = 0, bool onTop = false)
        {
            var sh = Shader.Find("IslandSim/Overlay");
            var m = new Material(sh);
            m.SetColor("_Color", c);
            m.SetFloat("_Frame", frame);
            m.SetFloat("_ZTest", onTop ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual);
            return m;
        }
    }
}
