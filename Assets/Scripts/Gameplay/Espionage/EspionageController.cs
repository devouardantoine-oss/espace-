using Espace.Core;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Espionage
{
    /// <summary>
    /// Compose et demarre l'espionnage dans la scene <c>GalaxyMap</c>.
    /// <para>
    /// <b>Resolution des dependances dans <c>Start</c>, pas <c>Update</c> :</b> meme raison que
    /// <c>ResearchController</c> — ce composant n'a besoin que de la <see cref="GalaxyMap"/>
    /// (generee dans l'<c>Awake</c> de <c>GalaxyMapController</c>) et d'<see cref="IEventBus"/>
    /// (enregistre avant meme la scene <c>GalaxyMap</c>), toutes deux disponibles des la
    /// premiere frame. <see cref="EspionageService"/> resout lui-meme, paresseusement,
    /// <see cref="Espace.Gameplay.Economy.IEconomyService"/>, <see cref="Espace.Gameplay.Military.IMilitaryService"/>,
    /// <see cref="Espace.Gameplay.Diplomacy.IDiplomacyService"/> et <see cref="Espace.Gameplay.Research.IResearchService"/>
    /// au moment ou une mission en a besoin (voir son commentaire) — ce composant n'a donc
    /// aucune contrainte d'ordre avec les autres controleurs de la scene.
    /// </para>
    /// </summary>
    public sealed class EspionageController : MonoBehaviour
    {
        private EspionageService _espionageService;

        private void Start()
        {
            if (!ServiceLocator.TryGet(out IEventBus eventBus) ||
                !ServiceLocator.TryGet(out GalaxyMap map))
            {
                GameLog.Error("[EspionageController] Dependances manquantes (IEventBus / GalaxyMap) : l'espionnage ne demarre pas.");
                return;
            }

            _espionageService = new EspionageService(map, eventBus);
            _espionageService.Initialize();

            if (!ServiceLocator.IsRegistered<IEspionageService>())
            {
                ServiceLocator.Register<IEspionageService>(_espionageService);
            }

            GameLog.Info("[Espionage] Demarree.");
        }

        private void OnDestroy()
        {
            if (_espionageService == null)
            {
                return;
            }

            _espionageService.Shutdown();
            ServiceLocator.Unregister<IEspionageService>();
            _espionageService = null;
        }
    }
}
