using System;
using System.IO;
using System.Reflection;
using IslandSim.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IslandSim.EditorTools
{
    /// <summary>
    /// One-time project setup, run automatically when the project opens (and from the Island Sim menu): linear colour,
    /// a URP pipeline asset set up for the atoll (HDR, four shadow cascades, the scene colour and depth the water refracts,
    /// an ambient-occlusion feature that Ultra switches on), the game's settings asset, and the Atoll scene in the build.
    /// </summary>
    [InitializeOnLoad]
    static class IslandSimSetup
    {
        const string Root = "Assets/IslandSim";
        const string SettingsDir = Root + "/Settings";
        const string ScenePath = Root + "/Scenes/Atoll.unity";
        const string GameSettingsPath = Root + "/Resources/IslandSim/Settings.asset";
        const string DoneKey = "IslandSim.SetupDone.v2";

        static IslandSimSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(DoneKey, false)) return;
                SessionState.SetBool(DoneKey, true);
                Run(false);
            };
        }

        [MenuItem("Island Sim/Set Up Project")]
        static void Menu() => Run(true);

        [MenuItem("Island Sim/Open Atoll Scene")]
        static void OpenScene()
        {
            if (!File.Exists(ScenePath)) Run(false);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Island Sim/Reimport Textures")]
        static void Reimport() => AssetDatabase.ImportAsset(Root + "/Resources/IslandSim/Textures", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

        // a drone fly-through of Palm Isle with the HUD hidden (Esc stops it); Record also saves every frame as a JPEG under
        // unity/Recordings/Palm_Isle/ at 30 fps, ready to join into a video
        [MenuItem("Island Sim/Cinematic/Record Palm Isle")]
        static void RecordCinematic() => Cine(true);
        [MenuItem("Island Sim/Cinematic/Preview Palm Isle")]
        static void PreviewCinematic() => Cine(false);
        static void Cine(bool rec)
        {
            var g = Application.isPlaying ? UnityEngine.Object.FindAnyObjectByType<IslandGame>() : null;
            if (g == null) { Debug.LogWarning("[Island Sim] Press Play first, then start the cinematic."); return; }
            g.StartCinematic(rec);
        }

        static void Run(bool verbose)
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear) { PlayerSettings.colorSpace = ColorSpace.Linear; Debug.Log("[Island Sim] Switched the project to linear colour space."); }
            var ssao = EnsureUrp(verbose);
            EnsureGameSettings(ssao);
            EnsureScene();
            AssetDatabase.SaveAssets();
            if (verbose) Debug.Log("[Island Sim] Project set up. Open Assets/IslandSim/Scenes/Atoll.unity and press Play.");
        }

        static ScriptableRendererFeature EnsureUrp(bool force)
        {
            Directory.CreateDirectory(SettingsDir);
            var assetPath = SettingsDir + "/IslandSim_URP.asset"; var rendererPath = SettingsDir + "/IslandSim_Renderer.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (asset == null || rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                // post-processing needs the default PostProcessData; it is internal in some URP versions, so look it up softly
                try
                {
                    var get = typeof(PostProcessData).GetMethod("GetDefaultPostProcessData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (get != null && rendererData.postProcessData == null) rendererData.postProcessData = (PostProcessData)get.Invoke(null, null);
                }
                catch (Exception e) { Debug.LogWarning("[Island Sim] Could not attach default post-process data: " + e.Message); }
                AssetDatabase.CreateAsset(rendererData, rendererPath);
                asset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.supportsHDR = true; asset.shadowDistance = 220; asset.shadowCascadeCount = 4; asset.msaaSampleCount = 4;
            asset.supportsCameraOpaqueTexture = true; asset.supportsCameraDepthTexture = true;
            SetField(asset, "m_MainLightShadowmapResolution", 4096); SetField(asset, "m_SoftShadowsSupported", true);
            EditorUtility.SetDirty(asset);

            // ambient occlusion for Ultra: URP's own SSAO feature (its class is internal, so it is found by name)
            ScriptableRendererFeature ssao = null;
            foreach (var f in rendererData.rendererFeatures) if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") ssao = f;
            if (ssao == null)
            {
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (type != null)
                {
                    ssao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type); ssao.name = "SSAO";
                    AssetDatabase.AddObjectToAsset(ssao, rendererData);
                    rendererData.rendererFeatures.Add(ssao);
                    // keep URP's feature map in step with the list
                    var map = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (map != null && map.GetValue(rendererData) is System.Collections.Generic.List<long> ids && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long lid)) ids.Add(lid);
                    ssao.SetActive(false);
                    EditorUtility.SetDirty(rendererData);
                }
                else Debug.LogWarning("[Island Sim] URP's ambient occlusion feature was not found; Ultra will run without it.");
            }
            if (force || GraphicsSettings.defaultRenderPipeline != asset)
            {
                GraphicsSettings.defaultRenderPipeline = asset;
                int keep = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = asset; }
                QualitySettings.SetQualityLevel(keep, false);
                Debug.Log("[Island Sim] Using the Universal Render Pipeline asset at " + assetPath + ".");
            }
            return ssao;
        }

        static void SetField(object o, string name, object value)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) return;
            try { f.SetValue(o, f.FieldType.IsEnum ? Enum.ToObject(f.FieldType, value) : value); } catch (Exception) { }
        }

        static void EnsureGameSettings(ScriptableRendererFeature ssao)
        {
            var s = AssetDatabase.LoadAssetAtPath<IslandSimSettings>(GameSettingsPath);
            if (s == null) { s = ScriptableObject.CreateInstance<IslandSimSettings>(); AssetDatabase.CreateAsset(s, GameSettingsPath); }
            if (s.Ssao != ssao) { s.Ssao = ssao; EditorUtility.SetDirty(s); }
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var active = EditorSceneManager.GetActiveScene();
                bool replaceUntitled = string.IsNullOrEmpty(active.path) && !active.isDirty;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, replaceUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
                new GameObject("IslandGame").AddComponent<IslandGame>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (!replaceUntitled) EditorSceneManager.CloseScene(scene, true);
            }
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes) if (s.path == ScenePath) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes) { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }

    /// <summary>
    /// Import settings for the game's textures, matching the web game: colour maps in sRGB, normal maps as normal maps,
    /// tiling and 8× anisotropic filtering, the foliage cut-outs clamped. The seven ground layers must share size and
    /// format, since they are stacked into one texture array at run time.
    /// </summary>
    sealed class IslandSimTextures : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/'); if (!path.Contains("/Resources/IslandSim/Textures/") && !path.Contains("/Resources/IslandSim/PlantTextures/")) return;
            var ti = (TextureImporter)assetImporter; string name = Path.GetFileNameWithoutExtension(assetPath);
            bool normal = name.EndsWith("_normal"), cutout = assetPath.EndsWith(".png");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal;
            ti.mipmapEnabled = true; ti.isReadable = false; ti.anisoLevel = 8; ti.filterMode = FilterMode.Trilinear;
            ti.wrapMode = cutout ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.alphaSource = cutout ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = cutout; ti.mipMapsPreserveCoverage = cutout; if (cutout) ti.alphaTestReferenceValue = .45f;
            ti.maxTextureSize = 2048; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.crunchedCompression = false;
        }
    }

    /// <summary>
    /// Import settings for custom models and plants (see CustomModels, PlantModels): building meshes readable, so the game
    /// can merge them into its town batches; Blender's axes baked in, so nothing arrives lying on its side; tangents for
    /// normal maps; no cameras, lights or animation.
    /// </summary>
    sealed class IslandSimModels : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            var path = assetPath.Replace('\\', '/'); if (!path.Contains("/Resources/IslandSim/Models/") && !path.Contains("/Resources/IslandSim/Plants/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.isReadable = path.Contains("/Resources/IslandSim/Models/");   // plants are drawn from their own meshes, buildings are merged
            mi.bakeAxisConversion = true;
            mi.importCameras = false; mi.importLights = false; mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
            mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.CalculateMikk;   // for normal maps
        }
    }
}
