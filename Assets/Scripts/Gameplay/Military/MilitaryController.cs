using System;
using Espace.Core;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Compose et demarre l'armee dans la scene <c>GalaxyMap</c>.
    /// <para>
    /// <b>Initialisation differee a <c>Update</c>, pas <c>Start</c> :</b> ce composant a besoin
    /// de <see cref="EmpireRegistry"/> (enregistre par <c>EmpireController.Start</c>) et
    /// <see cref="IEconomyService"/> (par <c>EconomyController.Start</c>) — deux <c>Start</c>
    /// sans ordre garanti l'un envers l'autre, ni envers celui-ci. Contrairement aux panneaux
    /// de diagnostic (qui peuvent se permettre d'attendre <c>OnGUI</c> ou un evenement de jeu),
    /// <see cref="MilitaryService"/> doit exister et etre enregistre <b>avant</b> que d'autres
    /// composants (IA, panneau de recrutement) ne le cherchent des leur propre <c>Start</c>.
    /// La verification est donc repetee a chaque <c>Update</c> jusqu'a ce que toutes les
    /// dependances soient pretes — garanti d'arriver au plus tard a la deuxieme frame, puisque
    /// tous les <c>Start</c> d'une meme frame sont termines avant le premier <c>Update</c>.
    /// </para>
    /// </summary>
    public sealed class MilitaryController : MonoBehaviour
    {
        [Tooltip("Catalogue des types d'unites recrutables.")]
        [SerializeField]
        private UnitTypeDefinition[] unitCatalog = Array.Empty<UnitTypeDefinition>();

        private MilitaryService _militaryService;
        private bool _initialized;

        private void Update()
        {
            if (_initialized)
            {
                return;
            }

            TryInitialize();
        }

        private void TryInitialize()
        {
            if (!ServiceLocator.TryGet(out IEventBus eventBus) ||
                !ServiceLocator.TryGet(out IGameClock gameClock) ||
                !ServiceLocator.TryGet(out GalaxyMap map) ||
                !ServiceLocator.TryGet(out IEconomyService economy) ||
                !ServiceLocator.TryGet(out EmpireRegistry empireRegistry))
            {
                return;
            }

            _militaryService = new MilitaryService(map, gameClock, eventBus, economy, empireRegistry, unitCatalog);
            _militaryService.Initialize();

            if (!ServiceLocator.IsRegistered<IMilitaryService>())
            {
                ServiceLocator.Register<IMilitaryService>(_militaryService);
            }

            _initialized = true;
            GameLog.Info($"[Military] Demarree avec {unitCatalog.Length} types d'unites disponibles.");
        }

        private void OnDestroy()
        {
            if (_militaryService == null)
            {
                return;
            }

            _militaryService.Shutdown();
            ServiceLocator.Unregister<IMilitaryService>();
            _militaryService = null;
        }
    }
}
