using Espace.Core;
using Espace.Data;
using UnityEngine;

namespace Espace.Managers
{
    /// <summary>
    /// Point d'entree unique du jeu : le seul MonoBehaviour de la scene <c>Bootstrap</c>.
    /// <para>
    /// <b>Role :</b> composer l'application. Il cree les services concrets, les enregistre
    /// dans le <see cref="ServiceLocator"/> sous leur interface, les initialise dans l'ordre,
    /// puis les arrete proprement. C'est la seule classe du projet qui connait des types
    /// concrets de services — partout ailleurs on ne manipule que des interfaces.
    /// </para>
    /// <para>
    /// <b><c>[DefaultExecutionOrder(-1000)]</c></b> garantit que son <c>Awake</c> s'execute
    /// avant tout autre script : aucun composant ne peut interroger un service avant son
    /// enregistrement.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Espace/Game Bootstrap")]
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>Tres tot dans la frame : avant tous les scripts a ordre par defaut (0).</summary>
        private const int ExecutionOrder = -1000;

        [Header("Configuration")]
        [Tooltip("Asset de reglages globaux. Si vide, les valeurs par defaut du moteur sont conservees.")]
        [SerializeField]
        private GameConfig gameConfig;

        /// <summary>
        /// Instance active. Statique uniquement pour detecter les doublons apres un
        /// rechargement de scene — ce n'est pas un point d'acces public aux services,
        /// ce role revient au <see cref="ServiceLocator"/>.
        /// </summary>
        private static GameBootstrap _instance;

        private GameManager _gameManager;
        private EventBus _eventBus;
        private SceneLoaderService _sceneLoader;
        private bool _servicesReady;

        private void Awake()
        {
            // La scene Bootstrap peut etre rechargee (retour au menu) : on ne veut
            // qu'un seul jeu de services vivant.
            if (_instance != null && _instance != this)
            {
                GameLog.Warning("[Bootstrap] Instance dupliquee detectee, celle-ci est detruite.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            ApplyConfiguration();
            RegisterServices();
            InitializeServices();
        }

        private void Update()
        {
            if (!_servicesReady)
            {
                return;
            }

            // Un unique point d'entree par frame : plus lisible et moins couteux
            // que N MonoBehaviours avec chacun leur Update.
            _gameManager.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            ShutdownServices();
            _instance = null;
        }

        /// <summary>Applique les reglages moteur issus du <see cref="GameConfig"/>.</summary>
        private void ApplyConfiguration()
        {
            if (gameConfig == null)
            {
                GameLog.Warning("[Bootstrap] Aucun GameConfig assigne : les reglages par defaut du moteur sont utilises.");
                return;
            }

            gameConfig.Apply();
            GameLog.Info($"[Bootstrap] Configuration appliquee (cible : {gameConfig.TargetFrameRate} FPS).");
        }

        /// <summary>
        /// Cree les services et les publie sous leur interface.
        /// Ordre libre : aucune dependance croisee n'est resolue a ce stade.
        /// </summary>
        private void RegisterServices()
        {
            // Le registre est statique : on repart d'une base propre a chaque demarrage
            // (indispensable en editeur, ou l'etat statique survit a l'arret du Play Mode).
            ServiceLocator.Clear();

            _eventBus = new EventBus();
            _sceneLoader = new SceneLoaderService();
            _gameManager = new GameManager();

            ServiceLocator.Register<IEventBus>(_eventBus);
            ServiceLocator.Register<ISceneLoader>(_sceneLoader);

            // GameManager est enregistre sous son type concret : c'est le chef d'orchestre
            // du flux, il n'a pas vocation a etre substitue.
            ServiceLocator.Register(_gameManager);

            GameLog.Info($"[Bootstrap] {ServiceLocator.Count} services enregistres.");
        }

        /// <summary>
        /// Initialise les services une fois qu'ils sont tous enregistres, afin qu'ils
        /// puissent resoudre leurs dependances mutuelles sans contrainte d'ordre.
        /// </summary>
        private void InitializeServices()
        {
            _eventBus.Initialize();
            _sceneLoader.Initialize();
            _gameManager.Initialize();

            _servicesReady = true;
        }

        /// <summary>Arrete les services dans l'ordre inverse de leur initialisation.</summary>
        private void ShutdownServices()
        {
            _servicesReady = false;

            _gameManager?.Shutdown();
            _sceneLoader?.Shutdown();
            _eventBus?.Shutdown();

            ServiceLocator.Clear();

            _gameManager = null;
            _sceneLoader = null;
            _eventBus = null;

            GameLog.Info("[Bootstrap] Services arretes.");
        }
    }
}
