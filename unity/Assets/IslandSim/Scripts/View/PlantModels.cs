using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace IslandSim.View
{
    /// <summary>
    /// A plant model from outside the game (a Poly Haven scan, an Asset Store pack, your own Blender model) that replaces
    /// one of the built-in plants. Put an FBX or prefab in Assets/IslandSim/Resources/IslandSim/Plants/ named after the
    /// slot (palm, tree, mangrove, bush, taro, rock, grass), plus palm_1 ... palm_9 for variety. It is sized to the
    /// built-in plant's height whatever units it was made in, keeps its LODs (an LODGroup, or meshes named _LOD0, _LOD1...),
    /// and its leaves get the game's wind. The source meshes are drawn as they are, so they need no Read/Write.
    /// </summary>
    public sealed class PlantModel
    {
        public sealed class Part { public Mesh Mesh; public int Sub; public Material Mat; public Matrix4x4 M; }
        public string Name;
        /// <summary>Triangles in the nearest level; a heavy model without LODs is only drawn up close (see VegetationView).</summary>
        public int Tris;
        public readonly List<Part[]> Lods = new List<Part[]>();
        public int PartCount { get { int n = 0; foreach (var l in Lods) n += l.Length; return n; } }
    }

    public static class PlantModels
    {
        public const string Folder = "IslandSim/Plants/";

        /// <summary>Every variant found for a slot, fitted to the given height, with the given wind on its leaves.</summary>
        public static List<PlantModel> Variants(string slot, float height, float wind)
        {
            var o = new List<PlantModel>();
            void Try(string n)
            {
                var go = Resources.Load<GameObject>(Folder + n); if (go == null) return;
                // further levels may come as their own files: palm_LOD1, palm_LOD2 ...
                var extra = new List<GameObject>();
                for (int l = 1; l <= 4; l++) { var e = Resources.Load<GameObject>($"{Folder}{n}_LOD{l}"); if (e == null) break; extra.Add(e); }
                var m = Build(go, extra, n, height, wind); if (m != null) o.Add(m);
            }
            Try(slot); for (int k = 1; k <= 9; k++) Try(slot + "_" + k);
            if (o.Count > 0) Debug.Log($"[Island Sim] Using {o.Count} custom {slot} model{(o.Count > 1 ? "s" : "")} from {Folder}.");
            return o;
        }

        static PlantModel Build(GameObject go, List<GameObject> extra, string name, float height, float wind)
        {
            // the LOD levels: from an LODGroup, or from renderer names (tree_LOD0, tree_LOD1...), else one level
            var sets = new List<List<MeshRenderer>>();
            var lg = go.GetComponentInChildren<LODGroup>(true);
            if (lg != null)
                foreach (var l in lg.GetLODs())
                {
                    var s = new List<MeshRenderer>(); foreach (var r in l.renderers) if (r is MeshRenderer mr && Usable(mr)) s.Add(mr);
                    if (s.Count > 0) sets.Add(s);
                }
            if (sets.Count == 0)
            {
                var byLod = new SortedDictionary<int, List<MeshRenderer>>();
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!Usable(mr)) continue;
                    var m = Regex.Match(mr.name, @"LOD(\d)", RegexOptions.IgnoreCase); int lod = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                    if (!byLod.TryGetValue(lod, out var l)) byLod[lod] = l = new List<MeshRenderer>(); l.Add(mr);
                }
                foreach (var kv in byLod) sets.Add(kv.Value);
            }
            foreach (var e in extra)
            {
                var s = new List<MeshRenderer>(); foreach (var mr in e.GetComponentsInChildren<MeshRenderer>(true)) if (Usable(mr)) s.Add(mr);
                if (s.Count > 0 && sets.Count > 0) sets.Add(s);
            }
            if (sets.Count == 0) { Debug.LogWarning($"[Island Sim] {Folder}{name} has no meshes the game can draw; keeping the built-in plant."); return null; }

            // fit: the nearest level's bounds, base on the ground, centred, as tall as the built-in plant
            var origin = Matrix4x4.Translate(-go.transform.position);
            bool any = false; var b = new Bounds(); int tris = 0;
            foreach (var mr in sets[0])
            {
                var mf = mr.GetComponent<MeshFilter>(); var mb = mf.sharedMesh.bounds; var m = origin * mr.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tris += (int)mf.sharedMesh.GetIndexCount(s) / 3;
            }
            float scale = height / Mathf.Max(1e-4f, b.size.y);
            var fit = Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Translate(new Vector3(-b.center.x, -b.min.y, -b.center.z)) * origin;
            if (sets.Count == 1 && tris > 20000)
                Debug.LogWarning($"[Island Sim] {Folder}{name} has {tris:N0} triangles and no LODs, so it is drawn only up close and the built-in plant stands in farther out. Add LODs (an LODGroup, or meshes named _LOD0, _LOD1...) to see it everywhere.");

            // the shader sways by height in the model's own units, so the wind is scaled to match the fit
            var pm = new PlantModel { Name = name, Tris = tris };
            foreach (var set in sets)
            {
                var parts = new List<PlantModel.Part>();
                foreach (var mr in set)
                {
                    var mesh = mr.GetComponent<MeshFilter>().sharedMesh; var mats = mr.sharedMaterials;
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        var src = mats.Length > s ? mats[s] : mats.Length > 0 ? mats[mats.Length - 1] : null;
                        var mat = Mats.Get(Mats.Custom(src, "plant/" + name, wind * scale));
                        if (mat != null) parts.Add(new PlantModel.Part { Mesh = mesh, Sub = s, Mat = mat, M = fit * mr.transform.localToWorldMatrix });
                    }
                }
                if (parts.Count > 0) pm.Lods.Add(parts.ToArray());
            }
            return pm.Lods.Count > 0 ? pm : null;
        }

        static bool Usable(MeshRenderer mr) { var mf = mr.GetComponent<MeshFilter>(); return mf != null && mf.sharedMesh != null; }
    }
}
