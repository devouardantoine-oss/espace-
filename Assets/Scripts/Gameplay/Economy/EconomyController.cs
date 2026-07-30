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

            AssignHomeSystem(map);

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

        /// <summary>
        /// Attribue au joueur le systeme le plus proche du centre de la galaxie, s'il n'en
        /// possede pas deja un.
        /// <para>
        /// Solution minimale en attendant les empires (Phase 5) : l'economie a besoin d'un
        /// proprietaire concret pour produire quelque chose de testable des maintenant, sans
        /// construire par avance tout le cadre empires/IA/colonisation. Le choix du systeme
        /// le plus proche du centre (plutot qu'aleatoire) garde la partie reproductible pour
        /// une graine de galaxie donnee.
        /// </para>
        /// </summary>
        private static void AssignHomeSystem(GalaxyMap map)
        {
            StarSystemState closest = null;
            float closestSqrDistance = float.MaxValue;

            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId == EconomyService.PlayerOwnerId)
                {
                    // Un systeme d'origine a deja ete attribue (rechargement de la meme
                    // GalaxyMap, par exemple) : ne pas en assigner un second.
                    return;
                }

                float sqrDistance = system.Position.sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = system;
                }
            }

            if (closest != null)
            {
                closest.OwnerId = EconomyService.PlayerOwnerId;
                GameLog.Info($"[Economy] Systeme d'origine attribue au joueur : {closest.Name}.");
            }
        }
    }
}
