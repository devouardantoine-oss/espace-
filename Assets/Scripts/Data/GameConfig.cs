using UnityEngine;

namespace Espace.Data
{
    /// <summary>
    /// Reglages globaux du jeu, editables sans recompiler.
    /// <para>
    /// <b>Pourquoi un ScriptableObject ?</b> C'est le conteneur de donnees natif d'Unity :
    /// une seule instance en memoire partagee par tous les consommateurs (pas de copie par
    /// GameObject), editable dans l'inspecteur par un game designer, et versionnable en
    /// texte dans Git. Tout le contenu du MVP (factions, unites, batiments) suivra ce meme
    /// pattern : <b>les donnees vivent dans des assets, jamais en dur dans le code</b>.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Espace/Core/Game Config", order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Performance mobile")]
        [Tooltip("Images par seconde visees. 60 sur mobile recent, 30 pour economiser la batterie.")]
        [SerializeField]
        [Range(30, 120)]
        private int targetFrameRate = 60;

        [Tooltip("La synchro verticale ignore targetFrameRate : la laisser desactivee sur mobile.")]
        [SerializeField]
        private bool vSyncEnabled;

        [Tooltip("Empeche la mise en veille de l'ecran pendant une partie.")]
        [SerializeField]
        private bool preventScreenDimming = true;

        [Header("Simulation")]
        [Tooltip("Graine du generateur aleatoire. 0 = graine aleatoire a chaque lancement.")]
        [SerializeField]
        private int randomSeed;

        /// <summary>Images par seconde visees.</summary>
        public int TargetFrameRate => targetFrameRate;

        /// <summary>Etat souhaite de la synchronisation verticale.</summary>
        public bool VSyncEnabled => vSyncEnabled;

        /// <summary>Graine du generateur aleatoire (0 = aleatoire).</summary>
        public int RandomSeed => randomSeed;

        /// <summary>
        /// Applique la configuration au moteur. Appele une seule fois par <c>GameBootstrap</c>.
        /// </summary>
        public void Apply()
        {
            // Important : sur mobile, Application.targetFrameRate n'est pris en compte
            // que si vSyncCount vaut 0.
            QualitySettings.vSyncCount = vSyncEnabled ? 1 : 0;
            Application.targetFrameRate = vSyncEnabled ? -1 : targetFrameRate;

            Screen.sleepTimeout = preventScreenDimming
                ? SleepTimeout.NeverSleep
                : SleepTimeout.SystemSetting;

            if (randomSeed != 0)
            {
                Random.InitState(randomSeed);
            }
        }
    }
}
