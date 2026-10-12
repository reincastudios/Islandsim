using System.IO;
using IslandSim.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
#if ISIM_URP
using System.Reflection;
using UnityEngine.Rendering.Universal;
#endif

namespace IslandSim.EditorTools
{
    /// <summary>
    /// One-time project setup, run automatically when the project first opens (and from the Island Sim menu):
    /// linear colour space, a URP pipeline asset tuned for the atoll, an instancing-ready lit material,
    /// and the Atoll scene added to the build.
    /// </summary>
    [InitializeOnLoad]
    static class IslandSimSetup
    {
        const string Root = "Assets/IslandSim";
        const string SettingsDir = Root + "/Settings";
        const string ScenePath = Root + "/Scenes/Atoll.unity";
        const string LitPath = Root + "/Resources/IslandSim/IslandLit.mat";
        const string DoneKey = "IslandSim.SetupDone.v1";

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

        static void Run(bool verbose)
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                Debug.Log("[Island Sim] Switched the project to linear colour space.");
            }
#if ISIM_URP
            EnsureUrp(verbose);
#else
            if (verbose) Debug.LogWarning("[Island Sim] URP package not found; the game will run on the built-in pipeline without post-processing.");
#endif
            EnsureLitMaterial();
            EnsureScene();
            AssetDatabase.SaveAssets();
            if (verbose) Debug.Log("[Island Sim] Project set up. Open Assets/IslandSim/Scenes/Atoll.unity and press Play.");
        }

#if ISIM_URP
        static void EnsureUrp(bool force)
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset && !force) return;
            Directory.CreateDirectory(SettingsDir);
            var assetPath = SettingsDir + "/IslandSim_URP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                // post-processing needs the default PostProcessData; it is internal in some URP versions, so look it up softly
                try
                {
                    var get = typeof(PostProcessData).GetMethod("GetDefaultPostProcessData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (get != null && rendererData.postProcessData == null) rendererData.postProcessData = (PostProcessData)get.Invoke(null, null);
                }
                catch (System.Exception e) { Debug.LogWarning("[Island Sim] Could not attach default post-process data: " + e.Message); }
                AssetDatabase.CreateAsset(rendererData, SettingsDir + "/IslandSim_Renderer.asset");

                asset = UniversalRenderPipelineAsset.Create(rendererData);
                asset.shadowDistance = 220;
                asset.shadowCascadeCount = 4;
                asset.msaaSampleCount = 4;
                asset.supportsHDR = true;
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
            Debug.Log("[Island Sim] Using the Universal Render Pipeline asset at " + assetPath + ".");
        }
#endif

        static void EnsureLitMaterial()
        {
            Shader shader = null;
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null) shader = rp.defaultMaterial.shader;
            if (shader == null) shader = Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(LitPath);
            if (mat != null && mat.shader == shader) return;
            Directory.CreateDirectory(Path.GetDirectoryName(LitPath));
            if (mat == null) { mat = new Material(shader) { name = "IslandLit", enableInstancing = true }; AssetDatabase.CreateAsset(mat, LitPath); }
            else { mat.shader = shader; mat.enableInstancing = true; EditorUtility.SetDirty(mat); }
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
}
