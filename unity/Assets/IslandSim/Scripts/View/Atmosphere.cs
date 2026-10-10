using UnityEngine;
using UnityEngine.Rendering;
#if ISIM_URP
using UnityEngine.Rendering.Universal;
#endif

namespace IslandSim.View
{
    /// <summary>Sun, sky, ambient light, fog and (under URP) post-processing for a bright tropical afternoon.</summary>
    public static class Atmosphere
    {
        // direction toward the sun (web prototype's SUN_DIR with Z flipped)
        public static readonly Vector3 SunDir = new Vector3(-0.55f, 0.62f, 0.42f).normalized;
        public static readonly Color Horizon = new Color(.81f, .90f, .93f);
        public const float FogStart = 260, FogEnd = 620;

        public static Light Setup(Transform parent, Camera cam)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.LookRotation(-SunDir);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .93f, .8f);
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .8f;
            sun.shadowBias = .04f; sun.shadowNormalBias = .3f;
            RenderSettings.sun = sun;

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                sky.SetFloat("_SunSize", .035f);
                sky.SetFloat("_SunSizeConvergence", 6f);
                sky.SetFloat("_AtmosphereThickness", .85f);
                sky.SetColor("_SkyTint", new Color(.48f, .62f, .78f));
                sky.SetColor("_GroundColor", new Color(.36f, .48f, .52f));
                sky.SetFloat("_Exposure", 1.2f);
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.62f, .74f, .86f);
            RenderSettings.ambientEquatorColor = new Color(.62f, .68f, .66f);
            RenderSettings.ambientGroundColor = new Color(.36f, .34f, .28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Horizon;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;

            Shader.SetGlobalVector("_IsimSunDir", SunDir);
            Shader.SetGlobalColor("_IsimHorizon", Horizon.linear);
            Shader.SetGlobalVector("_IsimFog", new Vector4(FogStart, FogEnd, 0, 0));

            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            cam.allowMSAA = true;

            if (GraphicsSettings.currentRenderPipeline == null)
            {
                QualitySettings.shadowDistance = 220;
                QualitySettings.shadowCascades = 4;
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            }
#if ISIM_URP
            else SetupUrp(parent, cam);
#endif
            return sun;
        }

#if ISIM_URP
        static void SetupUrp(Transform parent, Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;

            var go = new GameObject("Post FX");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = profile;

            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.35f); bloom.threshold.Override(1.05f); bloom.scatter.Override(.6f);
            var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(.35f); ca.contrast.Override(8f); ca.saturation.Override(14f);
            var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(6f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(.22f); vig.smoothness.Override(.45f);
        }
#endif
    }
}
