using Espace.Core;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Compose et demarre l'economie dans la scene <c>GalaxyMap</c>.
    /// <para>
    /// <b>Resolution des dependances dans <c>Start</c>, pas <c>Awake</c> :</b> ce composant a
    /// besoin de la <see cref="GalaxyMap"/> deja generee par <c>GalaxyMapController</c>, qui
    /// vit sur un GameObject different. Unity ne garantit pas l'ordre des <c>Awake</c> entre
    /// composants de meme priorite, mais garantit que <b>tous</b> les <c>Awake</c> sont
    /// termines avant le premier <c>Start</c> : resoudre les dependances dans <c>Start</c>
    /// evite donc toute course avec l'<c>Awake</c> de <c>GalaxyMapController</c>, sans avoir
    /// a fixer un ordre d'execution explicite entre les deux composants.
    /// </para>
    /// <para>
    /// <b>Depuis la Phase 5 :</b> l'attribution des systemes d'origine ne se fait plus ici —
    /// c'est desormais le role d'<c>EmpireController</c>/<c>EmpirePlacement</c>, generalise a
    /// tous les empires (joueur et IA). Ce composant ne fait plus que demarrer le service
    /// economique lui-meme, qui reste totalement ignorant des empires en tant qu'objets : il
    /// ne connait que des identifiants entiers de proprietaire.
    /// </para>
    /// </summary>
    public sealed class EconomyController : MonoBehaviour
    {
        [Tooltip("Catalogue des types de batiments constructibles.")]
        [SerializeField]
        private BuildingType[] buildingCatalog = System.Array.Empty<BuildingType>();

        private EconomyService _economyService;

        private void Start()
        {
            if (!ServiceLocator.TryGet(out IEventBus eventBus) ||
                !ServiceLocator.TryGet(out IGameClock gameClock) ||
                !ServiceLocator.TryGet(out GalaxyMap map))
            {
                GameLog.Error("[EconomyController] Dependances manquantes (IEventBus / IGameClock / GalaxyMap) : l'economie ne demarre pas.");
                return;
            }

            _economyService = new EconomyService(map, gameClock, eventBus, buildingCatalog);
            _economyService.Initialize();

            if (!ServiceLocator.IsRegistered<IEconomyService>())
            {
                ServiceLocator.Register<IEconomyService>(_economyService);
            }

            GameLog.Info($"[Economy] Demarree avec {buildingCatalog.Length} types de batiments disponibles.");
        }

        private void OnDestroy()
        {
            if (_economyService == null)
            {
                return;
            }

            _economyService.Shutdown();
            ServiceLocator.Unregister<IEconomyService>();
            _economyService = null;
        }
    }
}
