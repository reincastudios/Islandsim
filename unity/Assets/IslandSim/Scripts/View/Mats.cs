using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace IslandSim.View
{
    /// <summary>
    /// Textures and materials, matching the web game's material table: colour, roughness, metalness and texture maps per
    /// part, the foliage cards with their wind, and the ground's seven texture layers stacked into one texture array.
    /// </summary>
    public static class Mats
    {
        const string Tex = "IslandSim/Textures/";
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        public static Texture2D T(string name)
        {
            if (!textures.TryGetValue(name, out var t))
            {
                t = Resources.Load<Texture2D>(Tex + name);
                if (t == null) Debug.LogError("[Island Sim] Missing texture " + name + ". Run Island Sim > Set Up Project.");
                textures[name] = t;
            }
            return t;
        }

        static Shader Find(string name)
        {
            var s = Shader.Find(name);
            if (s == null) Debug.LogError("[Island Sim] Shader " + name + " is missing or failed to compile.");
            return s;
        }
        static Shader lit;
        public static Shader Lit => lit != null ? lit : lit = Find("IslandSim/Lit");

        // ------------------------------------------------------------ the material table
        struct Spec
        {
            public uint Col; public float Rough, Metal; public string Map, Nor; public bool Double; public float Wind; public bool Alpha;
        }
        static Spec S(uint c, float r = .85f, string map = null, string nor = null, float metal = 0, bool dbl = false) => new Spec { Col = c, Rough = r, Metal = metal, Map = map, Nor = nor, Double = dbl };
        static Spec Leaf(string map, float wind) => new Spec { Col = 0xffffff, Rough = .72f, Map = map, Double = true, Wind = wind, Alpha = true };

        static readonly Dictionary<string, Spec> table = new Dictionary<string, Spec>
        {
            ["plank"] = S(0xc9a27a, .85f, "wood_albedo", "wood_normal"), ["wood"] = S(0x9a7454, .9f, "wood_albedo", "wood_normal"),
            ["roof"] = S(0xffffff, .75f, "roof_albedo", "roof_normal"), ["roofGreen"] = S(0x8fb8a8, .7f, "roof_albedo", "roof_normal"),
            ["thatch"] = S(0xffffff, .95f, "thatch_albedo", "thatch_normal"), ["stone"] = S(0xb8b0a6, .9f, "rock_albedo", "rock_normal"), ["stoneD"] = S(0x8a847c, .92f, "rock_albedo", "rock_normal"),
            ["dark"] = S(0x2e2620, .9f), ["glass"] = S(0x1d2a30, .15f, metal: .2f), ["copper"] = S(0xd8915a, .32f, metal: 1),
            ["capFlag"] = S(0xff7a4a, .8f, dbl: true), ["flag"] = S(0x2fb8a8, .8f, dbl: true), ["cloth"] = S(0xf2ead8, .9f, dbl: true),
            ["rope"] = S(0xb89a6a, .95f), ["fish"] = S(0x9aa6ac, .4f, metal: .3f), ["soil"] = S(0x8a6a48, 1, "soil_albedo", "soil_normal"),
            ["coral"] = S(0xf4efe4, .96f, "rock_albedo", "rock_normal"), ["lime"] = S(0xf3eee2, .94f, "plaster_albedo", "plaster_normal"),
            ["clay"] = S(0xa8583a, .8f), ["husk"] = S(0x8a5a32, .95f), ["shell"] = S(0xf6efe0, .35f),
            ["wall0"] = S(0xf6ead2, .92f, "plaster_albedo", "plaster_normal"), ["wall1"] = S(0xf7cfae, .92f, "plaster_albedo", "plaster_normal"),
            ["wall2"] = S(0xc4e4da, .92f, "plaster_albedo", "plaster_normal"), ["wall3"] = S(0xf8e8a6, .92f, "plaster_albedo", "plaster_normal"),
            ["wall4"] = S(0xeebdb4, .92f, "plaster_albedo", "plaster_normal"), ["wall5"] = S(0xcfdcf2, .92f, "plaster_albedo", "plaster_normal"),
            ["wall6"] = S(0xfaf4ea, .92f, "plaster_albedo", "plaster_normal"),
            ["shut0"] = S(0x2f7f7a, .8f, "wood_albedo", "wood_normal"), ["shut1"] = S(0x3a5f9a, .8f, "wood_albedo", "wood_normal"), ["shut2"] = S(0x9a3a2f, .8f, "wood_albedo", "wood_normal"),
            ["shut3"] = S(0x5a8a3a, .8f, "wood_albedo", "wood_normal"), ["shut4"] = S(0x2f6f9a, .8f, "wood_albedo", "wood_normal"),
            ["bark"] = S(0x8a7a66, .95f, "bark_albedo", "bark_normal"), ["palmBark"] = S(0xbfae94, .95f, "bark_albedo", "bark_normal"), ["nut"] = S(0x5a4a2a, .7f),
            ["rock"] = S(0xffffff, .9f, "rock_albedo", "rock_normal"), ["root"] = S(0x6a5a48, .95f, "bark_albedo", "bark_normal"),
            ["sailOchre"] = S(0xd9a441, .9f, dbl: true),
            ["leaf"] = Leaf("leaf_cluster", .04f), ["frond"] = Leaf("palm_frond", .03f), ["tuft"] = Leaf("grass_tuft", .6f), ["cane"] = Leaf("grass_tuft", .22f),
        };

        public static float WindOf(string key) => table.TryGetValue(key, out var s) ? s.Wind : 0;
        public static bool IsFoliage(string key) => table.TryGetValue(key, out var s) && s.Wind > 0;

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        /// <summary>A part's material. baked = foliage merged into a batch, which sways by the height kept in its second UV set.</summary>
        public static Material Get(string key, bool baked = false)
        {
            string k = baked ? key + "@b" : key;
            if (mats.TryGetValue(k, out var m)) return m;
            if (!table.TryGetValue(key, out var s)) { Debug.LogError("[Island Sim] Unknown material " + key); return null; }
            m = new Material(Lit) { name = k, enableInstancing = true };
            m.SetColor("_BaseColor", World.Srgb(s.Col));
            m.SetFloat("_Smoothness", 1 - s.Rough); m.SetFloat("_Metallic", s.Metal);
            if (s.Map != null) m.SetTexture("_BaseMap", T(s.Map));
            if (s.Nor != null) { m.SetTexture("_BumpMap", T(s.Nor)); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Cull", s.Double ? (float)CullMode.Off : (float)CullMode.Back);
            if (s.Alpha) { m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", .45f); m.renderQueue = (int)RenderQueue.AlphaTest; }
            if (s.Wind > 0) { m.EnableKeyword("_WIND"); m.SetFloat("_Wind", s.Wind); if (baked) m.EnableKeyword("_BAKEDHEIGHT"); }
            return mats[k] = m;
        }

        /// <summary>
        /// A custom model's material (see CustomModels), turned into the game's own lit material so it gets the same sun,
        /// haze, shadows and post effects: colour, albedo and normal maps, smoothness and metalness carry over. Returns its key.
        /// </summary>
        public static string Custom(Material src, string model, float wind = 0)
        {
            string k = "custom/" + model + "/" + (src != null ? src.name : "default");
            if (mats.ContainsKey(k)) return k;
            var m = new Material(Lit) { name = k, enableInstancing = true };
            if (src != null)
            {
                var c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
                m.SetColor("_BaseColor", c);
                string mp = src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null ? "_BaseMap" : src.HasProperty("_MainTex") ? "_MainTex" : null;
                if (mp != null && src.GetTexture(mp) != null)
                {
                    m.SetTexture("_BaseMap", src.GetTexture(mp));
                    m.SetTextureScale("_BaseMap", src.GetTextureScale(mp)); m.SetTextureOffset("_BaseMap", src.GetTextureOffset(mp));
                }
                if (src.HasProperty("_BumpMap") && src.GetTexture("_BumpMap") != null) { m.SetTexture("_BumpMap", src.GetTexture("_BumpMap")); m.EnableKeyword("_NORMALMAP"); }
                if (src.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", src.GetFloat("_Smoothness")); else if (src.HasProperty("_Glossiness")) m.SetFloat("_Smoothness", src.GetFloat("_Glossiness"));
                if (src.HasProperty("_Metallic")) m.SetFloat("_Metallic", src.GetFloat("_Metallic"));
                // leaves and other cut-outs: transparent in Blender becomes alpha-tested here, both sides drawn
                if (Cutout(src, c, m.GetTexture("_BaseMap")))
                {
                    m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", src.HasProperty("_Cutoff") ? Mathf.Clamp(src.GetFloat("_Cutoff"), .2f, .6f) : .45f);
                    m.renderQueue = (int)RenderQueue.AlphaTest; m.SetFloat("_Cull", (float)CullMode.Off);
                    var cc = c; cc.a = 1; m.SetColor("_BaseColor", cc);
                    if (wind > 0) { m.EnableKeyword("_WIND"); m.SetFloat("_Wind", wind); }
                }
            }
            mats[k] = m; return k;
        }

        /// <summary>Leaves, fronds and grass cards: a transparent or cut-out material, or a leafy name over a texture with alpha.</summary>
        static bool Cutout(Material src, Color c, Texture map)
        {
            if (c.a < .99f || src.renderQueue >= (int)RenderQueue.AlphaTest || src.IsKeywordEnabled("_ALPHATEST_ON")) return true;
            string n = (src.name + " " + (src.shader != null ? src.shader.name : "")).ToLowerInvariant();
            bool leafy = n.Contains("leaf") || n.Contains("leaves") || n.Contains("frond") || n.Contains("foliage") || n.Contains("grass") || n.Contains("fern") || n.Contains("cutout");
            return leafy && map is Texture2D t && GraphicsFormatUtility.HasAlphaChannel(t.graphicsFormat);
        }

        /// <summary>A copy of a part's material, for boat sails whose colour changes.</summary>
        public static Material Own(string key) { var m = new Material(Get(key)); return m; }

        // ------------------------------------------------------------ other shaders
        static Material Make(string shader) { var s = Find(shader); return s != null ? new Material(s) : null; }

        public static Material Overlay(Color c, bool depthTest, int queueOffset = 0)
        {
            var m = Make("IslandSim/Overlay"); if (m == null) return null;
            m.SetColor("_Color", c); m.SetFloat("_ZTest", depthTest ? (float)CompareFunction.LessEqual : (float)CompareFunction.Always);
            m.renderQueue = (int)RenderQueue.Transparent + 20 + queueOffset; m.enableInstancing = true;
            return m;
        }
        public static Material Terrain() => Make("IslandSim/Terrain");
        public static Material Water() => Make("IslandSim/Water");
        public static Material Sky() => Make("IslandSim/Sky");
        public static Material Cursor() => Make("IslandSim/Cursor");

        /// <summary>The seabed beyond the map: the same sand, seen through deep water.</summary>
        public static Material Seabed()
        {
            var m = new Material(Lit) { name = "seabed" };
            m.SetColor("_BaseColor", World.Srgb(0x9a9278)); m.SetFloat("_Smoothness", 0);
            m.SetTexture("_BaseMap", T("sand_albedo")); m.SetTextureScale("_BaseMap", new Vector2(180, 180));
            m.SetTexture("_BumpMap", T("sand_normal")); m.SetTextureScale("_BumpMap", new Vector2(180, 180)); m.EnableKeyword("_NORMALMAP");
            return m;
        }

        // ------------------------------------------------------------ the ground's texture array
        public static readonly string[] Layers = { "sand", "grass", "forest", "rock", "reef", "soil", "path" };   // splat order

        /// <summary>Stacks same-size images into one texture array (one sampler for every ground layer), mipmaps included.</summary>
        public static Texture2DArray Array(string suffix)
        {
            var src = new Texture2D[Layers.Length];
            for (int k = 0; k < src.Length; k++) src[k] = T(Layers[k] + suffix);
            var a0 = src[0]; if (a0 == null) return null;
            var arr = new Texture2DArray(a0.width, a0.height, src.Length, a0.graphicsFormat, TextureCreationFlags.MipChain, a0.mipmapCount)
            { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "ground" + suffix };
            for (int k = 0; k < src.Length; k++)
            {
                var t = src[k];
                if (t == null || t.width != a0.width || t.graphicsFormat != a0.graphicsFormat || t.mipmapCount != a0.mipmapCount)
                { Debug.LogError($"[Island Sim] Ground texture {Layers[k]}{suffix} does not match the others; run Island Sim > Set Up Project."); continue; }
                if (SystemInfo.copyTextureSupport != CopyTextureSupport.None) Graphics.CopyTexture(t, 0, arr, k);
                else if (t.isReadable) { for (int mip = 0; mip < t.mipmapCount; mip++) arr.SetPixels32(t.GetPixels32(mip), k, mip); }
            }
            if (SystemInfo.copyTextureSupport == CopyTextureSupport.None) arr.Apply(false, true);
            return arr;
        }
    }
}
