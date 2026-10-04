using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Configure le projet en un clic (menu PuffyBird > Configurer le projet), et
    /// automatiquement à la première ouverture : pipeline URP réglé pour le mobile, réglages
    /// iOS / Android, scène principale et liste des scènes du build.
    /// Idempotent : peut être relancé sans risque.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/PuffyBird/Scenes/Main.unity";
        const string SettingsFolder = "Assets/PuffyBird/Settings";
        const string PipelinePath = SettingsFolder + "/PuffyBird_URP.asset";
        const string RendererPath = SettingsFolder + "/PuffyBird_Renderer.asset";
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
        const string BundleId = "fr.puffybird.app";
        /// <summary>Icône générée par tools/icon/make_icon.py (1024 px, sans alpha).</summary>
        public const string IconPath = "Assets/PuffyBird/Icons/AppIcon.png";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !Application.isBatchMode)
                {
                    Debug.Log("PuffyBird : première ouverture, configuration automatique du projet.");
                    Configure(openScene: true);
                }
            };
        }

        [MenuItem("PuffyBird/Configurer le projet", priority = 0)]
        public static void ConfigureFromMenu() => Configure(openScene: true);

        public static void Configure(bool openScene)
        {
            var pipeline = EnsurePipeline();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            ConfigurePlayer();
            UnityCloudProject.Apply();
            EnsureScene(openScene);
            AssetDatabase.SaveAssets();
            Debug.Log("PuffyBird : projet configuré (URP mobile, iOS, Android, scène principale).");
        }

        static UniversalRenderPipelineAsset EnsurePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                Directory.CreateDirectory(SettingsFolder);
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            var rendererAsset = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererAsset != null)
            {
                var so = new SerializedObject(rendererAsset);
                // Forward : peu de lumières, le plus économique sur mobile.
                Set(so, "m_RenderingMode", 0);
                var postProcess = AssetDatabase.LoadAssetAtPath<Object>(PostProcessDataPath);
                if (postProcess != null) SetObject(so, "postProcessData", postProcess);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rendererAsset);
            }

            // Réglages par leur nom sérialisé : robuste d'une version d'URP à l'autre.
            var p = new SerializedObject(pipeline);
            Set(p, "m_SupportsHDR", true);
            Set(p, "m_MSAA", 4);
            Set(p, "m_RenderScale", 1f);
            Set(p, "m_MainLightRenderingMode", 1);
            Set(p, "m_MainLightShadowsSupported", true);
            Set(p, "m_MainLightShadowmapResolution", 2048);
            Set(p, "m_AdditionalLightsRenderingMode", 1);
            Set(p, "m_AdditionalLightsPerObjectLimit", 4);
            Set(p, "m_AdditionalLightShadowsSupported", false);
            Set(p, "m_ShadowDistance", 30f);
            Set(p, "m_ShadowCascadeCount", 1);
            Set(p, "m_SoftShadowsSupported", true);
            Set(p, "m_SupportsCameraDepthTexture", true);
            Set(p, "m_SupportsCameraOpaqueTexture", false);
            Set(p, "m_ReflectionProbeBlending", false);
            p.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Sylar78";
            PlayerSettings.productName = "PuffyBird";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.statusBarHidden = true;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            // Portrait seul sur iPad : plein écran obligatoire (sinon l'App Store exige toutes les orientations).
            PlayerSettings.iOS.requiresFullScreen = true;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            else Debug.LogWarning("PuffyBird : icône introuvable (" + IconPath + ").");
        }

        static void EnsureScene(bool openScene)
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var cameraGo = new GameObject("Caméra") { tag = "MainCamera" };
                cameraGo.AddComponent<Camera>();
                cameraGo.AddComponent<AudioListener>();
                cameraGo.AddComponent<UniversalAdditionalCameraData>();

                new GameObject("PuffyBird").AddComponent<PuffyBirdGame>();

                // Le brouillard enregistré dans la scène garde ses variantes de shader dans le build.
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 18f;
                RenderSettings.fogEndDistance = 95f;
                RenderSettings.skybox = null;

                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else if (openScene && SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void Set(SerializedObject so, string name, bool value)
        {
            var prop = so.FindProperty(name);
            if (prop != null) prop.boolValue = value;
            else Debug.LogWarning("PuffyBird : réglage URP introuvable " + name);
        }

        static void Set(SerializedObject so, string name, int value)
        {
            var prop = so.FindProperty(name);
            if (prop == null)
            {
                Debug.LogWarning("PuffyBird : réglage URP introuvable " + name);
                return;
            }
            // intValue écrit la valeur sous-jacente, y compris pour une énumération à valeurs
            // explicites (MSAA 4, résolution 2048, PerPixel = 1…).
            prop.intValue = value;
        }

        static void Set(SerializedObject so, string name, float value)
        {
            var prop = so.FindProperty(name);
            if (prop != null) prop.floatValue = value;
            else Debug.LogWarning("PuffyBird : réglage URP introuvable " + name);
        }

        static void SetObject(SerializedObject so, string name, Object value)
        {
            var prop = so.FindProperty(name);
            if (prop != null) prop.objectReferenceValue = value;
        }
    }
}
