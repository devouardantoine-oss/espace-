using System.Collections.Generic;
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
        private const string GalaxyMapScenePath = "Assets/Scenes/GalaxyMap.unity";

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
            LogAppliedAndroidSettings();

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

        /// <summary>
        /// Declare les scenes du build : <c>Bootstrap</c> en scene 0, puis <c>GalaxyMap</c>.
        /// <para>
        /// <b>GalaxyMap doit imperativement y figurer</b>, meme si elle n'est jamais la scene de
        /// demarrage : <c>MainMenuController</c> et <c>FactionPickerController</c> la chargent
        /// par son nom via <c>ISceneLoader</c>, et <c>PauseMenuController</c> revient de meme
        /// sur <c>Bootstrap</c>. Une scene absente de cette liste n'existe pas dans une
        /// application compilee — le defaut ne se voit donc jamais dans l'editeur, ou toutes les
        /// scenes du projet sont chargeables, et se manifeste seulement sur l'appareil, ou
        /// « Nouvelle partie » ne fait plus rien.
        /// </para>
        /// </summary>
        private static void ConfigureBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();

            foreach (string path in new[] { BootstrapScenePath, GalaxyMapScenePath })
            {
                if (File.Exists(path))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
                else
                {
                    Debug.LogWarning($"[Setup] Scene introuvable : {path}");
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
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
            ConfigureLandscapeOrientation();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationIdentifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        /// <summary>
        /// Verrouille l'application en paysage.
        /// <para>
        /// <b>Impose par la largeur de l'interface :</b> la fenetre de gestion mesure 660
        /// unites de large. En portrait, <c>UITheme.Scale</c> doit se brider pour la faire
        /// tenir, et les boutons redeviennent trop petits pour un doigt ; en paysage, la meme
        /// fenetre tient largement et l'echelle peut suivre la densite reelle de l'ecran. Une
        /// carte galactique et un tableau a sept onglets sont de toute facon des formes larges.
        /// </para>
        /// <para>
        /// Reglage independant du reste : repasser <c>defaultInterfaceOrientation</c> sur
        /// <c>AutoRotation</c> suffit a revenir en arriere, sans toucher au code de l'interface.
        /// </para>
        /// </summary>
        private static void ConfigureLandscapeOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        }

        /// <summary>iOS : IL2CPP est le seul backend disponible ; on fixe la cible minimale.</summary>
        private static void ConfigureIos()
        {
            ConfigureLandscapeOrientation();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationIdentifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        }

        /// <summary>
        /// Relit et journalise les reglages Android qui font echouer un build quand ils sont
        /// faux, plutot que de supposer qu'ils ont ete appliques.
        /// <para>
        /// Ces quatre valeurs sont exactement celles qui coutent le plus cher a decouvrir trop
        /// tard : trois d'entre elles ne se manifestent qu'apres une centaine de secondes de
        /// compilation, et la quatrieme (l'orientation) qu'une fois l'application installee sur
        /// le telephone, ou l'interface se retrouve a l'etroit en portrait.
        /// </para>
        /// </summary>
        private static void LogAppliedAndroidSettings()
        {
            bool landscapeOnly = !PlayerSettings.allowedAutorotateToPortrait
                && !PlayerSettings.allowedAutorotateToPortraitUpsideDown
                && (PlayerSettings.allowedAutorotateToLandscapeLeft || PlayerSettings.allowedAutorotateToLandscapeRight);

            Debug.Log(
                "[Setup] Reglages Android relus : "
                + $"backend {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}, "
                + $"architectures {PlayerSettings.Android.targetArchitectures}, "
                + $"SDK minimal {PlayerSettings.Android.minSdkVersion}, "
                + $"orientation {(landscapeOnly ? "paysage uniquement" : "PORTRAIT AUTORISE — l'interface sera a l'etroit")}.");
        }

        /// <summary>Libelle lisible d'une valeur d'<c>activeInputHandler</c>, pour les messages de diagnostic.</summary>
        private static string DescribeInputHandler(int value)
        {
            switch (value)
            {
                case 0: return "Input Manager (Old)";
                case InputHandlerNewInputSystem: return "Input System Package (New)";
                case 2: return "Both";
                default: return $"inconnu ({value})";
            }
        }

        /// <summary>
        /// Bascule Active Input Handling sur le nouvel Input System.
        /// <para>
        /// Ce reglage n'expose aucune API publique : on edite directement la propriete
        /// serialisee <c>activeInputHandler</c> de <c>ProjectSettings.asset</c>. C'est la
        /// methode utilisee par les outils d'installation officiels du package.
        /// </para>
        /// <para>
        /// <b><c>AssetDatabase.SaveAssets</c> ne suffit pas :</b> il ne couvre que les assets du
        /// dossier <c>Assets/</c>. <c>ProjectSettings.asset</c> n'est reecrit sur le disque qu'a
        /// la fermeture de l'editeur ou sur <c>File → Save Project</c>. Sans cet appel explicite,
        /// la valeur restait en memoire, semblait appliquee, puis disparaissait — et la
        /// compilation Android echouait sur « Active Input Handling is set to Both » alors que le
        /// script venait d'annoncer avoir reussi.
        /// </para>
        /// <para>
        /// <b>Le resultat est relu et verifie</b> plutot que suppose : un echec silencieux sur ce
        /// reglage precis coute un build Android complet (une centaine de secondes) avant de se
        /// manifester.
        /// </para>
        /// </summary>
        /// <returns><c>true</c> si la valeur a change (un redemarrage de l'editeur est alors requis).</returns>
        private static bool ConfigureInputSystem()
        {
            Object[] settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets == null || settingsAssets.Length == 0)
            {
                Debug.LogError(
                    "[Setup] ProjectSettings.asset illisible. Reglez Active Input Handling sur "
                    + "« Input System Package (New) » dans Edit > Project Settings > Player > Other Settings.");
                return false;
            }

            var serializedSettings = new SerializedObject(settingsAssets[0]);
            serializedSettings.Update();

            SerializedProperty inputHandler = serializedSettings.FindProperty("activeInputHandler");
            if (inputHandler == null)
            {
                Debug.LogError(
                    "[Setup] Propriete 'activeInputHandler' introuvable. Reglez Active Input Handling sur "
                    + "« Input System Package (New) » dans Edit > Project Settings > Player > Other Settings.");
                return false;
            }

            int previous = inputHandler.intValue;
            if (previous == InputHandlerNewInputSystem)
            {
                Debug.Log("[Setup] Active Input Handling : deja sur Input System Package (New).");
                return false;
            }

            inputHandler.intValue = InputHandlerNewInputSystem;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();

            // Ecrit reellement ProjectSettings.asset sur le disque (voir la remarque ci-dessus).
            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("File/Save Project");

            serializedSettings.Update();
            int applied = serializedSettings.FindProperty("activeInputHandler").intValue;

            if (applied != InputHandlerNewInputSystem)
            {
                Debug.LogError(
                    $"[Setup] Active Input Handling est reste sur « {DescribeInputHandler(applied)} ». "
                    + "Reglez-le sur « Input System Package (New) » dans "
                    + "Edit > Project Settings > Player > Other Settings, puis redemarrez l'editeur. "
                    + "Laisse sur « Both », la compilation Android echoue.");
                return false;
            }

            Debug.Log(
                $"[Setup] Active Input Handling : {DescribeInputHandler(previous)} -> Input System Package (New). "
                + "Redemarrage de l'editeur requis.");
            return true;
        }
    }
}
