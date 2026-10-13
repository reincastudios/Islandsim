using System.Collections.Generic;
using IslandSim.Core;
using UnityEngine;

namespace IslandSim.View
{
    /// <summary>
    /// Models made in Blender (or any 3D tool) that replace the built-in ones. Drop an FBX into
    /// Assets/IslandSim/Resources/IslandSim/Models/ named after the building ("house", "fishery", "palace"...) and the game
    /// draws it instead, with the same placement, rotation, batching and shadows. 1 unit is 1 tile; the origin sits at the
    /// centre of the plot, on the ground.
    ///
    /// Lookup order: house_4x3 / house_3x2 for the two compound sizes, then house (stretched to the plot); harbor_capital
    /// for the capital's jetty, then harbor.
    /// </summary>
    public static class CustomModels
    {
        public const string Folder = "IslandSim/Models/";
        static readonly Dictionary<string, Dictionary<string, Geo>> cache = new Dictionary<string, Dictionary<string, Geo>>();

        /// <summary>The custom model for a building, in tile space per material, or null to use the built-in one.</summary>
        public static Dictionary<string, Geo> For(BType type, bool capital, int pw, int pd)
        {
            string id = Defs.Def(type).Id;
            if (type == BType.House) return Find($"{id}_{pw}x{pd}") ?? Stretched(id, pw / 3f, pd / 2f);
            if (capital) return Find(id + "_capital") ?? Find(id);
            return Find(id);
        }

        static Dictionary<string, Geo> Stretched(string name, float sx, float sz)
        {
            string key = $"{name}@{sx}x{sz}";
            if (cache.TryGetValue(key, out var m)) return m;
            var src = Find(name); if (src == null) return cache[key] = null;
            if (sx == 1 && sz == 1) return cache[key] = src;
            m = new Dictionary<string, Geo>(); foreach (var kv in src) m[kv.Key] = kv.Value.Clone().Scale(sx, 1, sz);
            return cache[key] = m;
        }

        public static Dictionary<string, Geo> Find(string name)
        {
            if (cache.TryGetValue(name, out var m)) return m;
            var go = Resources.Load<GameObject>(Folder + name);
            m = go != null ? FromPrefab(go, name) : null;
            if (m != null) Debug.Log($"[Island Sim] Using the custom model {Folder}{name}.");
            return cache[name] = m;
        }

        static Dictionary<string, Geo> FromPrefab(GameObject go, string name)
        {
            var parts = new Dictionary<string, List<Geo>>();
            var mirror = Matrix4x4.Scale(new Vector3(1, 1, -1));   // Unity space -> tile space (see World.U)
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh; var mr = mf.GetComponent<MeshRenderer>();
                if (mesh == null || mr == null) continue;
                if (!mesh.isReadable) { Debug.LogWarning($"[Island Sim] {name}: turn on Read/Write in the model's import settings."); continue; }
                var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv; var mats = mr.sharedMaterials;
                var m = mirror * mf.transform.localToWorldMatrix;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var tri = mesh.GetTriangles(s); if (tri.Length == 0) continue;
                    // keep only this sub-mesh's vertices
                    var remap = new Dictionary<int, int>(); var g = new Geo { I = new List<int>(tri.Length) };
                    foreach (int i in tri)
                    {
                        if (!remap.TryGetValue(i, out int j))
                        {
                            j = remap[i] = g.P.Count; g.P.Add(v[i]);
                            g.N.Add(n.Length == v.Length ? n[i] : Vector3.up); g.UV.Add(uv.Length == v.Length ? uv[i] : Vector2.zero);
                        }
                        g.I.Add(j);
                    }
                    g.Apply(m);
                    if (n.Length != v.Length) g.ComputeNormals();
                    string key = Mats.Custom(mats.Length > s ? mats[s] : mats.Length > 0 ? mats[mats.Length - 1] : null, name);
                    if (!parts.TryGetValue(key, out var l)) parts[key] = l = new List<Geo>();
                    l.Add(g);
                }
            }
            if (parts.Count == 0) { Debug.LogWarning($"[Island Sim] {name} has no meshes the game can use; keeping the built-in model."); return null; }
            var o = new Dictionary<string, Geo>(); foreach (var kv in parts) o[kv.Key] = Geo.Merge(kv.Value);
            return o;
        }
    }
}
