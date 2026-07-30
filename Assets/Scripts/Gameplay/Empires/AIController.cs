using Espace.Core;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using UnityEngine;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Fait agir chaque empire IA une fois par mois de jeu : economie puis armee.
    /// <para>
    /// <b>Resolution paresseuse dans le gestionnaire d'evenement, pas dans <c>Start</c> :</b>
    /// ce composant a besoin d'<see cref="EmpireRegistry"/> (enregistre par
    /// <c>EmpireController.Start</c>), d'<see cref="IEconomyService"/> (par
    /// <c>EconomyController.Start</c>) et d'<see cref="IMilitaryService"/> (enregistre au
    /// premier <c>Update</c> de <c>MilitaryController</c>, voir son commentaire) — aucun ordre
    /// garanti entre ces enregistrements et celui-ci. S'abonner a <see cref="IEventBus"/>
    /// reste sur dans <c>Start</c> (enregistre par <c>GameBootstrap</c> bien avant, en
    /// <c>Awake</c>) ; mais resoudre le reste seulement au premier
    /// <see cref="MonthAdvancedEvent"/> elimine la course, puisqu'un mois de jeu ecoule
    /// largement apres que toutes les initialisations de la scene sont terminees.
    /// </para>
    /// <para>
    /// Les decisions elles-memes vivent dans <see cref="AIDecisionMaker"/> (economie) et
    /// <see cref="MilitaryDecisionMaker"/> (armee), deux fonctions statiques testables sans
    /// scene : ce composant ne fait que les brancher sur l'horloge du jeu.
    /// </para>
    /// </summary>
    public sealed class AIController : MonoBehaviour
    {
        private IEventBus _eventBus;
        private EmpireRegistry _empireRegistry;
        private IEconomyService _economy;
        private IMilitaryService _military;
        private GalaxyMap _map;

        private void Start()
        {
            if (ServiceLocator.TryGet(out _eventBus))
            {
                _eventBus.Subscribe<MonthAdvancedEvent>(OnMonthAdvanced);
            }
            else
            {
                GameLog.Error("[AIController] IEventBus indisponible : l'IA ne pourra jamais agir.");
            }
        }

        private void OnDestroy()
        {
            _eventBus?.Unsubscribe<MonthAdvancedEvent>(OnMonthAdvanced);
        }

        private void OnMonthAdvanced(MonthAdvancedEvent monthAdvancedEvent)
        {
            if (!ResolveDependencies())
            {
                return;
            }

            foreach (Empire empire in _empireRegistry.Empires)
            {
                if (empire.IsPlayerControlled)
                {
                    continue;
                }

                AIDecisionMaker.DecideAndAct(empire, _map, _economy);
                MilitaryDecisionMaker.DecideAndAct(empire, _map, _economy, _military);
            }
        }

        /// <summary>Resout et met en cache les dependances non disponibles au premier appel.</summary>
        private bool ResolveDependencies()
        {
            if (_empireRegistry == null && !ServiceLocator.TryGet(out _empireRegistry))
            {
                return false;
            }

            if (_economy == null && !ServiceLocator.TryGet(out _economy))
            {
                return false;
            }

            if (_military == null && !ServiceLocator.TryGet(out _military))
            {
                return false;
            }

            if (_map == null && !ServiceLocator.TryGet(out _map))
            {
                return false;
            }

            return true;
        }
    }
}
