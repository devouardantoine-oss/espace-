using Espace.Core;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Diplomacy
{
    /// <summary>
    /// Compose et demarre la diplomatie dans la scene <c>GalaxyMap</c>.
    /// <para>
    /// <b>Initialisation differee a <c>Update</c>, pas <c>Start</c> :</b> meme raison que
    /// <c>MilitaryController</c> — ce composant a besoin d'<see cref="EmpireRegistry"/>
    /// (enregistre par <c>EmpireController.Start</c>) et d'<see cref="IEconomyService"/>
    /// (par <c>EconomyController.Start</c>), deux <c>Start</c> sans ordre garanti l'un envers
    /// l'autre ni envers celui-ci. <see cref="IDiplomacyService"/> doit exister avant que
    /// d'autres composants ne le cherchent des leur propre <c>Start</c> ou <c>Update</c> — la
    /// verification est donc repetee chaque frame jusqu'a ce que les dependances soient
    /// pretes.
    /// </para>
    /// <para>
    /// <b>Sans dependance a <see cref="Espace.Gameplay.Military.IMilitaryService"/> :</b>
    /// <see cref="DiplomacyService"/> a besoin d'estimer la puissance militaire des empires
    /// (paix, ultimatums), mais le resout paresseusement lui-meme via
    /// <see cref="ServiceLocator"/> plutot que de le recevoir au constructeur — sinon
    /// <c>MilitaryController</c> (qui a lui-meme besoin de <see cref="IDiplomacyService"/>,
    /// voir son commentaire) et ce composant s'attendraient mutuellement indefiniment. Ce
    /// composant peut donc s'initialiser des la premiere frame, avant l'armee.
    /// </para>
    /// </summary>
    public sealed class DiplomacyController : MonoBehaviour
    {
        private DiplomacyService _diplomacyService;
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
                !ServiceLocator.TryGet(out GalaxyMap map) ||
                !ServiceLocator.TryGet(out IEconomyService economy) ||
                !ServiceLocator.TryGet(out EmpireRegistry empireRegistry))
            {
                return;
            }

            _diplomacyService = new DiplomacyService(empireRegistry, economy, map, eventBus);
            _diplomacyService.Initialize();

            if (!ServiceLocator.IsRegistered<IDiplomacyService>())
            {
                ServiceLocator.Register<IDiplomacyService>(_diplomacyService);
            }

            _initialized = true;
            GameLog.Info("[Diplomacy] Demarree.");
        }

        private void OnDestroy()
        {
            if (_diplomacyService == null)
            {
                return;
            }

            _diplomacyService.Shutdown();
            ServiceLocator.Unregister<IDiplomacyService>();
            _diplomacyService = null;
        }
    }
}
