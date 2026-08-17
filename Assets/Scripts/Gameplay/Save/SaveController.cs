using Espace.Core;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.Research;
using UnityEngine;

namespace Espace.Gameplay.Save
{
    /// <summary>
    /// Compose et demarre la sauvegarde dans la scene <c>GalaxyMap</c>.
    /// <para>
    /// <b>Initialisation differee a <c>Update</c>, comme <c>MilitaryController</c> :</b> ce
    /// composant a besoin de <b>tous</b> les autres services de gameplay (voir
    /// <see cref="SaveService"/>) — le plus de dependances de tous les controleurs de la
    /// scene. Contrairement aux services diplomatie/recherche/espionnage (qui evitent les
    /// dependances de construction pour ne jamais imposer d'ordre a un autre controleur), rien
    /// ne depend de <see cref="SaveService"/> en retour : ce composant peut donc se permettre
    /// d'attendre patiemment que tout le reste soit pret, sans risque de blocage mutuel.
    /// </para>
    /// <para>
    /// <b>Chargement au demarrage, sauvegarde automatique ensuite :</b> des que toutes les
    /// dependances sont resolues (au plus tard a la deuxieme frame), une sauvegarde existante
    /// est chargee et appliquee avant qu'aucun <c>DayAdvancedEvent</c> n'ait pu s'ecouler.
    /// Ensuite, une sauvegarde automatique a lieu chaque mois de jeu
    /// (<see cref="MonthAdvancedEvent"/>) et a chaque mise en arriere-plan ou fermeture de
    /// l'application (<see cref="OnApplicationPause"/>/<see cref="OnApplicationQuit"/>) —
    /// pertinent sur mobile, ou l'application est bien plus souvent suspendue que fermee
    /// proprement.
    /// </para>
    /// </summary>
    public sealed class SaveController : MonoBehaviour
    {
        private SaveService _saveService;
        private IEventBus _eventBus;
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
                !ServiceLocator.TryGet(out IGameClock clock) ||
                !ServiceLocator.TryGet(out IEconomyService economy) ||
                !ServiceLocator.TryGet(out IMilitaryService military) ||
                !ServiceLocator.TryGet(out IDiplomacyService diplomacy) ||
                !ServiceLocator.TryGet(out IResearchService research) ||
                !ServiceLocator.TryGet(out EmpireRegistry empireRegistry))
            {
                return;
            }

            string filePath = SaveFileLocator.FilePath;

            // Le codex n'est pas exige : il n'existe que dans la scene de la carte, et une
            // sauvegarde sans lui reste valide (voir SaveService).
            ServiceLocator.TryGet(out Espace.Gameplay.Chronicle.ICodexService codex);
            ServiceLocator.TryGet(out Espace.Gameplay.Decisions.IDecisionService decisions);
            ServiceLocator.TryGet(out Espace.Gameplay.People.IGovernorService governors);
            ServiceLocator.TryGet(out Espace.Gameplay.Voies.IVoieService voies);

            _saveService = new SaveService(
                map, clock, economy, military, diplomacy, research, empireRegistry, filePath, codex, decisions, governors, voies);

            if (!ServiceLocator.IsRegistered<ISaveService>())
            {
                ServiceLocator.Register<ISaveService>(_saveService);
            }

            if (_saveService.SaveFileExists)
            {
                if (_saveService.TryLoadAndApply(out string error))
                {
                    GameLog.Info("[Save] Sauvegarde existante chargee au demarrage.");
                }
                else
                {
                    GameLog.Warning($"[Save] Sauvegarde existante ignoree : {error}");
                }
            }

            _eventBus = eventBus;
            _eventBus.Subscribe<MonthAdvancedEvent>(OnMonthAdvanced);

            _initialized = true;
            GameLog.Info($"[Save] Demarree ({filePath}).");
        }

        private void OnMonthAdvanced(MonthAdvancedEvent monthAdvancedEvent)
        {
            _saveService.SaveNow();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _initialized)
            {
                _saveService.SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            if (_initialized)
            {
                _saveService.SaveNow();
            }
        }

        private void OnDestroy()
        {
            _eventBus?.Unsubscribe<MonthAdvancedEvent>(OnMonthAdvanced);

            if (_saveService != null)
            {
                ServiceLocator.Unregister<ISaveService>();
                _saveService = null;
            }
        }
    }
}
