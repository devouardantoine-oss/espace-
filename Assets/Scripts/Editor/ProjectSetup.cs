using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Espace.Editor
{
    /// <summary>
    /// Configure le projet en un clic : URP, Input System, cible mobile, Build Settings.
    /// <para>
    /// <b>Pourquoi un script plutot que des <c>ProjectSettings/*.asset</c> versionnes ?</b>
    /// Ces fichiers YAML sont lies a la version de serialisation exacte de l'editeur ; un
    /// champ obsolete suffit a empecher l'ouverture du projet. Passer par les API officielles
    /// (<c>PlayerSettings</c>, <c>GraphicsSettings</c>, <c>QualitySettings</c>) donne un
    /// resultat valide quelle que soit la version d'Unity 6 installee, et le tout reste
    /// lisible et versionnable.
    /// </para>
    /// <para>
    /// L'operation est idempotente : la relancer ne cree pas de doublons.
    /// </para>
    /// </summary>
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string PipelineAssetPath = SettingsFolder + "/URP-Mobile.asset";
        private const string RendererAssetPath = SettingsFolder + "/URP-Mobile-Renderer.asset";
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

        private const string CompanyName = "Espace Studio";
        private const string ProductName = "Espace";
        private const string ApplicationIdentifier = "com.espacestudio.espace";

        /// <summary>Valeur de <c>activeInputHandler</c> correspondant a « Input System Package (New) ».</summary>
        private const int InputHandlerNewInputSystem = 1;

        [MenuItem("Tools/Espace/Setup Project", priority = 0)]
        public static void SetupProject()
        {
            EnsureSettingsFolder();

            UniversalRenderPipelineAsset pipelineAsset = CreateOrLoadPipelineAsset();
            AssignPipelineAsset(pipelineAsset);

            ConfigureRendering();
            ConfigureBuildScenes();
            ConfigureAndroid();
            ConfigureIos();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool inputHandlerChanged = ConfigureInputSystem();

            Debug.Log("[Setup] Configuration du projet terminee.");

            if (inputHandlerChanged)
            {
                EditorUtility.DisplayDialog(
                    "Redemarrage requis",
                    "L'Input System a ete active.\n\n" +
                    "Unity doit redemarrer pour que le changement prenne effet. " +
                    "Acceptez le redemarrage propose par l'editeur, ou fermez et rouvrez le projet.",
                    "Compris");
            }
        }

        /// <summary>Cree <c>Assets/Settings</c> s'il n'existe pas encore.</summary>
        private static void EnsureSettingsFolder()
        {
            if (!AssetDatabase.IsValidFolder(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Cree (ou recharge) l'asset URP et son renderer.
        /// <para>
        /// Ces assets sont generes par code plutot qu'ecrits a la main : leur format de
        /// serialisation depend de la version d'URP installee, et
        /// <c>UniversalRenderPipelineAsset.Create</c> garantit un asset coherent.
        /// </para>
        /// </summary>
        private static UniversalRenderPipelineAsset CreateOrLoadPipelineAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (existing != null)
            {
                ApplyMobileRenderingSettings(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = Path.GetFileNameWithoutExtension(RendererAssetPath);
            AssetDatabase.CreateAsset(rendererData, RendererAssetPath);

            UniversalRenderPipelineAsset pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            pipelineAsset.name = Path.GetFileNameWithoutExtension(PipelineAssetPath);
            ApplyMobileRenderingSettings(pipelineAsset);
            AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);

            Debug.Log($"[Setup] Asset URP cree : {PipelineAssetPath}");
            return pipelineAsset;
        }

        /// <summary>
        /// Reglages orientes mobile : chaque option desactivee ici retire une passe de rendu
        /// ou un buffer plein ecran, donc de la bande passante GPU — le facteur limitant
        /// principal sur telephone.
        /// </summary>
        private static void ApplyMobileRenderingSettings(UniversalRenderPipelineAsset pipelineAsset)
        {
            // HDR : inutile pour un rendu stylise, couteux en bande passante.
            pipelineAsset.supportsHDR = false;

            // MSAA 2x : bon compromis qualite / cout sur les silhouettes d'unites.
            pipelineAsset.msaaSampleCount = 2;

            // Ces deux textures declenchent chacune une copie plein ecran. On les activera
            // seulement si un effet le necessite reellement.
            pipelineAsset.supportsCameraDepthTexture = false;
            pipelineAsset.supportsCameraOpaqueTexture = false;

            // La camera est en vue 3/4 rapprochee : inutile de calculer des ombres au loin.
            pipelineAsset.shadowDistance = 60f;
        }

        /// <summary>Assigne le pipeline en global et sur chaque niveau de qualite.</summary>
        private static void AssignPipelineAsset(UniversalRenderPipelineAsset pipelineAsset)
        {
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;

            int previousLevel = QualitySettings.GetQualityLevel();
            string[] levels = QualitySettings.names;
            for (int i = 0; i < levels.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelineAsset;
            }

            QualitySettings.SetQualityLevel(previousLevel, false);
        }

        /// <summary>Espace colorimetrique lineaire : indispensable pour un eclairage URP correct.</summary>
        private static void ConfigureRendering()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
        }

        /// <summary>Declare la scene Bootstrap comme scene 0 du build.</summary>
        private static void ConfigureBuildScenes()
        {
            if (!File.Exists(BootstrapScenePath))
            {
                Debug.LogWarning($"[Setup] Scene introuvable : {BootstrapScenePath}");
                return;
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true)
            };
        }

        /// <summary>
        /// Android : IL2CPP + ARM64 sont exiges par Google Play, et IL2CPP est nettement
        /// plus rapide que Mono sur le code de simulation.
        /// <para>
        /// <b>API 26 minimum</b> : Unity 6 refuse desormais toute valeur inferieure et
        /// journalise une erreur sans appliquer le reglage. La valeur d'origine (24, choisie en
        /// Phase 1 sous une version anterieure de l'editeur) etait donc devenue inoperante.
        /// Android 8.0 date de 2017 et couvre la quasi-totalite du parc en service.
        /// </para>
        /// </summary>
        private static void ConfigureAndroid()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationIdentifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        /// <summary>iOS : IL2CPP est le seul backend disponible ; on fixe la cible minimale.</summary>
        private static void ConfigureIos()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationIdentifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        }

        /// <summary>
        /// Bascule Active Input Handling sur le nouvel Input System.
        /// <para>
        /// Ce reglage n'expose aucune API publique : on edite directement la propriete
        /// serialisee <c>activeInputHandler</c> de <c>ProjectSettings.asset</c>. C'est la
        /// methode utilisee par les outils d'installation officiels du package.
        /// </para>
        /// </summary>
        /// <returns><c>true</c> si la valeur a change (un redemarrage de l'editeur est alors requis).</returns>
        private static bool ConfigureInputSystem()
        {
            Object[] settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets == null || settingsAssets.Length == 0)
            {
                Debug.LogWarning("[Setup] ProjectSettings.asset illisible : activez l'Input System manuellement.");
                return false;
            }

            var serializedSettings = new SerializedObject(settingsAssets[0]);
            SerializedProperty inputHandler = serializedSettings.FindProperty("activeInputHandler");
            if (inputHandler == null)
            {
                Debug.LogWarning("[Setup] Propriete 'activeInputHandler' introuvable : activez l'Input System manuellement.");
                return false;
            }

            if (inputHandler.intValue == InputHandlerNewInputSystem)
            {
                return false;
            }

            inputHandler.intValue = InputHandlerNewInputSystem;
            serializedSettings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
