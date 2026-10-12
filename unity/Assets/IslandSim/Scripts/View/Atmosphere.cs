using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static IslandSim.Core.Defs;
using static IslandSim.View.World;

namespace IslandSim.View
{
    public enum Quality { Low, High, Ultra }

    /// <summary>
    /// Sun, sky, haze and the camera's post effects, matching the web game: a warm low sun whose shadow area follows the
    /// camera, a physical sky that also lights and reflects in the scene, haze that recedes as you zoom out, ACES tone
    /// mapping with a soft bloom and vignette, and on Ultra ambient occlusion and a tilt-shift blur.
    /// </summary>
    public sealed class Atmosphere
    {
        public readonly Light Sun;
        readonly Volume volume; readonly Bloom bloom; readonly DepthOfField dof;
        readonly ReflectionProbe probe; int probeId = -1; bool probeDone;
        readonly IslandSimSettings settings;
        public Quality Q { get; private set; } = Quality.High;
        public bool Grass => Q != Quality.Low;
        public bool Refraction => Q != Quality.Low;

        static readonly Color FOG = Srgb(0xc4d6df);

        public Atmosphere(Transform parent)
        {
            settings = IslandSimSettings.Load();

            var sg = new GameObject("sun"); sg.transform.SetParent(parent, false);
            Sun = sg.AddComponent<Light>(); Sun.type = LightType.Directional;
            Sun.color = Srgb(0xfff1dc); Sun.intensity = 3.1f / Mathf.PI;     // three.js lights carry π that URP leaves out
            Sun.shadows = LightShadows.Soft; Sun.shadowStrength = 1;
            sg.transform.rotation = Quaternion.LookRotation(-SunDir);
            RenderSettings.sun = Sun;

            var sky = Mats.Sky();
            if (sky != null) { sky.SetVector("_SunDir", SunDir); RenderSettings.skybox = sky; }
            RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = .75f;
            RenderSettings.reflectionIntensity = .75f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = FOG;
            DynamicGI.UpdateEnvironment();

            // the reflection cube: the sky alone, captured once
            var pg = new GameObject("sky reflection"); pg.transform.SetParent(parent, false); pg.transform.position = new Vector3(W / 2f, 40, -H / 2f);
            probe = pg.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing; probe.cullingMask = 0; probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.resolution = 256; probe.hdr = true; probe.size = new Vector3(4000, 4000, 4000); probe.importance = 0;
            probe.nearClipPlane = 1; probe.farClipPlane = 3000;

            // post effects
            var vg = new GameObject("post effects"); vg.transform.SetParent(parent, false);
            volume = vg.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); volume.sharedProfile = profile;
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(Mathf.Log(.82f / .6f, 2));   // URP's ACES runs about 0.6× brighter than three's at the same exposure
            bloom = profile.Add<Bloom>(true); bloom.threshold.Override(.92f); bloom.intensity.Override(.3f); bloom.scatter.Override(.55f);
            var vi = profile.Add<Vignette>(true); vi.color.Override(new Color(4 / 255f, 22 / 255f, 34 / 255f)); vi.intensity.Override(.25f); vi.smoothness.Override(.45f);
            dof = profile.Add<DepthOfField>(true); dof.mode.Override(DepthOfFieldMode.Bokeh); dof.focalLength.Override(50); dof.aperture.Override(5.6f);

            Shader.SetGlobalVector("_IsMapSize", new Vector4(W, H, 0, 0));
            Shader.SetGlobalVector("_IsSunDir", SunDir);
            var sc = new Color(1, .93f, .8f).linear * 5; Shader.SetGlobalVector("_IsSunCol", new Vector4(sc.r, sc.g, sc.b, 0));
            var fc = FOG.linear; Shader.SetGlobalVector("_IsFogColor", new Vector4(fc.r, fc.g, fc.b, 0));
            var blank = new Cubemap(1, TextureFormat.RGBA32, false); var bc = Srgb(0x9fc4d8);
            foreach (CubemapFace f in Enum.GetValues(typeof(CubemapFace))) if (f != CubemapFace.Unknown) blank.SetPixel(f, 0, 0, bc);
            blank.Apply(); Shader.SetGlobalTexture("_IsEnvCube", blank);       // until the sky capture is ready
        }

        static UniversalRenderPipelineAsset Urp => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        /// <summary>Low for weak GPUs, High by default, Ultra adds ambient occlusion and tilt-shift (as in the web game).</summary>
        public void SetQuality(Quality q, Camera cam)
        {
            Q = q;
            var urp = Urp;
            if (urp != null)
            {
                SetShadowRes(urp, q == Quality.Low ? 2048 : 4096);
                urp.shadowCascadeCount = 4;
                urp.msaaSampleCount = q == Quality.Low ? 2 : 4;
                urp.supportsCameraOpaqueTexture = q != Quality.Low; urp.supportsCameraDepthTexture = true;
            }
            bloom.active = q != Quality.Low;
            dof.active = q == Quality.Ultra;
            if (settings != null && settings.Ssao != null) settings.Ssao.SetActive(q == Quality.Ultra);
            if (cam != null)
            {
                var cd = cam.GetUniversalAdditionalCameraData();
                cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.None;
                cd.requiresColorOption = q != Quality.Low ? CameraOverrideOption.On : CameraOverrideOption.Off;
                cd.requiresDepthOption = CameraOverrideOption.On;
            }
        }

        // the shadow-map size has no public setter in some URP versions
        static void SetShadowRes(UniversalRenderPipelineAsset urp, int res)
        {
            try
            {
                var p = typeof(UniversalRenderPipelineAsset).GetProperty("mainLightShadowmapResolution", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanWrite) { p.SetValue(urp, res); return; }
                var f = typeof(UniversalRenderPipelineAsset).GetField("m_MainLightShadowmapResolution", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) f.SetValue(urp, Enum.ToObject(f.FieldType, res));
            }
            catch (Exception e) { Debug.LogWarning("[Island Sim] Could not set the shadow map size: " + e.Message); }
        }

        /// <summary>Per frame: the shadow area and haze follow the camera, the tilt-shift strength follows zoom.</summary>
        public void Update(float t, float tx, float tz, float dist)
        {
            Shader.SetGlobalFloat(IdTime, t);
            float span = Mathf.Round(Mathf.Clamp(dist * .75f, 22, 120));
            var urp = Urp; if (urp != null) urp.shadowDistance = dist + span * 1.5f;
            float near = 200 + dist * .8f, far = 700 + dist * 1.4f;     // the haze recedes as you zoom out, so the whole archipelago stays readable
            RenderSettings.fogStartDistance = near; RenderSettings.fogEndDistance = far;
            Shader.SetGlobalVector("_IsFogRange", new Vector4(near - 40, far - 80, 0, 0));
            if (Q == Quality.Ultra)
            {
                // focus on the camera's target; the blur grows when far out (miniature look) and is gone in close-ups
                float k = Mathf.Clamp01((dist - 30) / 150);
                dof.active = k > .01f; dof.focusDistance.Override(dist); dof.aperture.Override(Mathf.Lerp(22, 2.2f, k)); dof.focalLength.Override(Mathf.Lerp(20, 90, k));
            }

            if (!probeDone)
            {
                if (probeId < 0) probeId = probe.RenderProbe();
                else if (probe.IsFinishedRendering(probeId) && probe.realtimeTexture != null)
                {
                    probeDone = true; Shader.SetGlobalTexture("_IsEnvCube", probe.realtimeTexture);
                    try
                    {
                        // the scene's default reflection too (customReflectionTexture in Unity 2022+, customReflection before)
                        var p = typeof(RenderSettings).GetProperty("customReflectionTexture") ?? typeof(RenderSettings).GetProperty("customReflection");
                        if (p != null && p.PropertyType.IsInstanceOfType(probe.realtimeTexture)) { RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; p.SetValue(null, probe.realtimeTexture); }
                    }
                    catch (Exception) { }
                }
            }
        }
    }
}
