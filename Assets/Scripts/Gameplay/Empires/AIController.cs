using Espace.Core;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Espionage;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.Research;
using UnityEngine;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Fait agir chaque empire IA une fois par mois de jeu : economie, puis recherche, puis
    /// espionnage, puis diplomatie, puis armee.
    /// <para>
    /// <b>Resolution paresseuse dans le gestionnaire d'evenement, pas dans <c>Start</c> :</b>
    /// ce composant a besoin d'<see cref="EmpireRegistry"/> (enregistre par
    /// <c>EmpireController.Start</c>), d'<see cref="IEconomyService"/>, d'<see cref="IResearchService"/>
    /// et d'<see cref="IEspionageService"/> (chacun par un <c>Start</c> propre, voir
    /// <c>EconomyController</c>/<c>ResearchController</c>/<c>EspionageController</c>),
    /// d'<see cref="IDiplomacyService"/> (au premier <c>Update</c> de <c>DiplomacyController</c>)
    /// et d'<see cref="IMilitaryService"/> (enregistre au premier <c>Update</c> de
    /// <c>MilitaryController</c>, voir son commentaire) — aucun ordre garanti entre ces
    /// enregistrements et celui-ci. S'abonner a <see cref="IEventBus"/> reste sur dans
    /// <c>Start</c> (enregistre par <c>GameBootstrap</c> bien avant, en <c>Awake</c>) ; mais
    /// resoudre le reste seulement au premier <see cref="MonthAdvancedEvent"/> elimine la
    /// course, puisqu'un mois de jeu ecoule largement apres que toutes les initialisations de
    /// la scene sont terminees.
    /// </para>
    /// <para>
    /// <b>Ordre Economie -> Recherche -> Espionnage -> Diplomatie -> Armee, deliberement</b> :
    /// une guerre declaree ce mois-ci par <see cref="Espace.Gameplay.Diplomacy.DiplomacyDecisionMaker"/>
    /// peut ainsi etre exploitee par <see cref="MilitaryDecisionMaker"/> ce meme mois, sans
    /// attendre le mois suivant. Recherche et espionnage n'ont pas cette contrainte de
    /// sequencement, mais restent groupes avant la diplomatie et l'armee par coherence
    /// thematique (les decisions les plus « discretes » d'abord).
    /// </para>
    /// <para>
    /// Les decisions elles-memes vivent dans <see cref="AIDecisionMaker"/> (economie),
    /// <see cref="Espace.Gameplay.Research.ResearchDecisionMaker"/> (recherche),
    /// <see cref="Espace.Gameplay.Espionage.EspionageDecisionMaker"/> (espionnage),
    /// <see cref="Espace.Gameplay.Diplomacy.DiplomacyDecisionMaker"/> (diplomatie) et
    /// <see cref="MilitaryDecisionMaker"/> (armee), des fonctions statiques testables sans
    /// scene : ce composant ne fait que les brancher sur l'horloge du jeu.
    /// </para>
    /// </summary>
    public sealed class AIController : MonoBehaviour
    {
        private IEventBus _eventBus;
        private EmpireRegistry _empireRegistry;
        private IEconomyService _economy;
        private IResearchService _research;
        private IEspionageService _espionage;
        private IDiplomacyService _diplomacy;
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

                AIDecisionMaker.DecideAndAct(empire, _map, _economy, _military);
                ResearchDecisionMaker.DecideAndAct(empire, _research);
                EspionageDecisionMaker.DecideAndAct(empire, _empireRegistry, _map, _espionage);
                DiplomacyDecisionMaker.DecideAndAct(empire, _map, _military, _diplomacy);
                MilitaryDecisionMaker.DecideAndAct(empire, _map, _economy, _military, _diplomacy);
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

            if (_research == null && !ServiceLocator.TryGet(out _research))
            {
                return false;
            }

            if (_espionage == null && !ServiceLocator.TryGet(out _espionage))
            {
                return false;
            }

            if (_diplomacy == null && !ServiceLocator.TryGet(out _diplomacy))
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
