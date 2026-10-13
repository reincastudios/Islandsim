using UnityEngine;
using UnityEngine.Rendering;
using static IslandSim.Core.Defs;
using static IslandSim.View.World;

namespace IslandSim.View
{
    /// <summary>The sea: one finely divided plane far past the map's edge, drawn with the water shader.</summary>
    public sealed class WaterView
    {
        public readonly Material Mat;
        public readonly MeshRenderer Renderer;

        public WaterView(Transform parent)
        {
            Mat = Mats.Water();
            var go = new GameObject("water"); go.transform.SetParent(parent, false);
            var mesh = Geo.Plane(2400, 2400, 240, 240).RotateX(-Mathf.PI / 2).Translate(W / 2f, WATER_Y, H / 2f).ToMesh("water");
            mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + new Vector3(0, 1, 0));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = go.AddComponent<MeshRenderer>(); Renderer.sharedMaterial = Mat;
            Renderer.shadowCastingMode = ShadowCastingMode.Off; Renderer.receiveShadows = false;
            { var n = Mats.T("water_normal"); if (Mat != null) Mat.SetTexture("_WaterNormal", n); Shader.SetGlobalTexture("_IsWaterNormal", n); }   // global: the beach draws the same ripples on the sheet of each wave
        }

        static readonly int IdSeaY = Shader.PropertyToID("_IsSeaY");
        /// <summary>Lifts the sea by the tide (world units above WATER_Y). The water and the beach find their edge from the
        /// ground's height against this level, so nothing else needs to move.</summary>
        public void SetTide(float h)
        {
            Renderer.transform.localPosition = new Vector3(0, h, 0);
            SeaY = WATER_Y + h; Shader.SetGlobalFloat(IdSeaY, SeaY);
        }

        /// <summary>Refraction reads the scene behind the water (High and Ultra); without it the sea is blended over the floor.</summary>
        public void SetRefraction(bool on)
        {
            if (Mat == null) return;
            Mat.SetFloat("_UseRefr", on ? 1 : 0);
            Mat.SetFloat("_SrcBlend", on ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
            Mat.SetFloat("_DstBlend", on ? (float)BlendMode.Zero : (float)BlendMode.OneMinusSrcAlpha);
            Mat.SetFloat("_ZWrite", on ? 1 : 0);
        }
    }
}
