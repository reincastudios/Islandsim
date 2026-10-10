using System.Collections.Generic;
using IslandSim.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandSim.View
{
    /// <summary>
    /// Builds the low-poly building and boat models from boxes and cylinders, merged into one mesh per material.
    /// Dimensions follow the web prototype; replace these with authored prefabs as the art pipeline matures.
    /// </summary>
    public static class Models
    {
        static readonly string[] Walls = { "#f2e3c6", "#f4c9a8", "#bfe0d6", "#f6e7a1", "#e8b8b0", "#c9d8ef", "#f7f1e6" };
        static readonly string[] Roofs = { "#b9553a", "#c46a3e", "#3f7f7a" };
        const string Wood = "#8a5d36", Plank = "#b07c4c", Roof = "#b9553a", Stone = "#9c958d", StoneD = "#6e6862", Field = "#a99a52",
                     Crop = "#5c9a33", Copper = "#d39a4a", Dark = "#3a2a1e", CapFlag = "#ff8a5c", Flag = "#4fd1c5", Pip = "#ff5a4f";

        sealed class Builder
        {
            public readonly Dictionary<string, MeshKit> Parts = new Dictionary<string, MeshKit>();
            MeshKit K(string mat) { if (!Parts.TryGetValue(mat, out var k)) Parts[mat] = k = new MeshKit(); return k; }

            // box/cyl sit on y (bottom face at y), as in the prototype
            public void Box(float w, float h, float d, string mat, float x = 0, float y = 0, float z = 0) => K(mat).Box(w, h, d, MeshKit.TRS(x, y + h / 2, z));
            public void Cyl(float rTop, float rBot, float h, string mat, float x = 0, float y = 0, float z = 0, int seg = 8) => K(mat).Cylinder(rTop, rBot, h, seg, MeshKit.TRS(x, y + h / 2, z));
            public void Raw(string mat, System.Action<MeshKit> add) => add(K(mat));
        }

        static GameObject Assemble(string name, Builder b, float scale, System.Func<string, Material> material)
        {
            var root = new GameObject(name);
            var body = new GameObject("body"); body.transform.SetParent(root.transform, false); body.transform.localScale = Vector3.one * scale;
            foreach (var kv in b.Parts)
            {
                var go = new GameObject(kv.Key); go.transform.SetParent(body.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = kv.Value.ToMesh(name + " " + kv.Key);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material(kv.Key);
                mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;
            }
            return root;
        }

        static Material Solid(string key)
        {
            if (key == Copper) return Mats.Get(key, .6f, .6f);
            if (key == Pip) return Mats.Get(key, .4f);
            return Mats.Get(key, .1f);
        }

        public static GameObject Building(BType type, bool capital, int seed, Material overrideMat = null)
        {
            var b = new Builder();
            switch (type)
            {
                case BType.Harbor:
                    b.Box(.96f, .1f, .96f, Plank); b.Box(.5f, .42f, .46f, Wood, -.12f, .1f, -.1f); b.Box(.56f, .06f, .52f, Roof, -.12f, .52f, -.1f);
                    b.Cyl(.02f, .02f, .9f, Dark, .3f, .1f, .3f, 5); b.Box(.26f, .14f, .02f, capital ? CapFlag : Flag, .43f, .82f, .3f);
                    foreach (var p in new[] { new Vector2(-.45f, .45f), new Vector2(.45f, -.45f), new Vector2(.45f, .45f) }) b.Cyl(.04f, .04f, .35f, Wood, p.x, -.25f, p.y, 5);
                    break;
                case BType.House:
                    b.Box(.52f, .36f, .46f, Walls[seed % Walls.Length]); b.Box(.56f, .04f, .5f, Plank);
                    b.Raw(Roofs[(seed >> 3) % Roofs.Length], k => k.Cone(.5f, .32f, 4, MeshKit.TRS(0, .52f, 0, 0, 45)));
                    b.Box(.1f, .18f, .02f, Dark, 0, 0, .24f);
                    break;
                case BType.Fishery:
                    b.Box(.5f, .32f, .42f, Wood, -.1f); b.Box(.58f, .05f, .5f, Plank, -.1f, .32f); b.Box(.36f, .04f, .16f, Wood, .25f, 0, .25f);
                    for (int k = 0; k < 3; k++) b.Cyl(.015f, .015f, .4f, Dark, .2f + k * .1f, 0, -.3f, 4);
                    break;
                case BType.Lumber:
                    b.Box(.46f, .3f, .4f, Wood, -.15f, 0, -.1f); b.Box(.52f, .05f, .46f, Plank, -.15f, .3f, -.1f);
                    for (int k = 0; k < 3; k++)
                    {
                        float ly = .07f + (k == 2 ? .12f : 0), lz = .2f + (k == 1 ? .14f : k == 2 ? .07f : 0);
                        b.Raw(Plank, m => m.Cylinder(.07f, .07f, .5f, 6, MeshKit.TRS(.1f, ly, lz, 0, 0, 90)));
                    }
                    break;
                case BType.Quarry:
                    b.Box(.8f, .12f, .8f, StoneD); b.Box(.3f, .26f, .3f, Stone, -.2f, .12f, -.15f); b.Box(.24f, .18f, .28f, Stone, .2f, .12f, .15f); b.Box(.18f, .14f, .18f, Stone, .22f, .12f, -.22f);
                    break;
                case BType.Plantation:
                    b.Box(.94f, .04f, .94f, Field); for (int k = 0; k < 4; k++) b.Box(.12f, .16f, .84f, Crop, -.33f + k * .22f, .04f);
                    break;
                case BType.Distillery:
                    b.Box(.6f, .4f, .5f, Wood, -.05f); b.Box(.66f, .05f, .56f, Roof, -.05f, .4f); b.Cyl(.07f, .08f, .75f, StoneD, .18f, 0, -.15f, 6); b.Cyl(.12f, .12f, .22f, Copper, -.25f, 0, .3f, 10);
                    break;
            }
            var go = Assemble(type.ToString(), b, 1.25f, overrideMat != null ? (System.Func<string, Material>)(_ => overrideMat) : Solid);
            if (overrideMat != null) foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A small red marker shown above buildings that need attention.</summary>
        public static GameObject AlertPip(Transform parent)
        {
            var go = new GameObject("alert");
            go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0, 1.45f, 0);
            go.AddComponent<MeshFilter>().sharedMesh = new MeshKit().Icosphere(.11f, 1, Matrix4x4.identity).ToMesh("pip");
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Solid(Pip); mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Boat(out MeshRenderer sail)
        {
            var b = new Builder();
            b.Box(.75f, .16f, .3f, Wood, 0, -.06f); b.Box(.5f, .04f, .24f, Plank, -.04f, .1f); b.Cyl(.02f, .02f, .75f, Dark, 0, .1f, 0, 5);
            var go = Assemble("Boat", b, 1.3f, Solid);
            var s = new GameObject("sail"); s.transform.SetParent(go.transform.GetChild(0), false);
            var k = new MeshKit(); k.TriBoth(new Vector3(0, .25f, 0), new Vector3(0, .85f, 0), new Vector3(.34f, .25f, 0));
            s.AddComponent<MeshFilter>().sharedMesh = k.ToMesh("sail");
            sail = s.AddComponent<MeshRenderer>(); sail.sharedMaterial = Mats.Lit(World.Hex("#f6efe0"), .1f);
            return go;
        }
    }
}
