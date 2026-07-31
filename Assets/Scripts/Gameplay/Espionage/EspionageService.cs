using System;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.Research;
using UnityEngine;

namespace Espace.Gameplay.Espionage
{
    /// <summary>
    /// Implementation par defaut de <see cref="IEspionageService"/>.
    /// <para>
    /// <b>Deterministe, sans hasard :</b> une mission reussit si et seulement si la puissance
    /// d'espionnage du proposeur depasse strictement le contre-espionnage de la cible (egalite
    /// stricte incluse → echec, meme convention que <see cref="CombatResolver"/>) — pas de jet
    /// de de. Le risque vient d'ailleurs : une mission echouee est toujours decouverte
    /// (<see cref="ApplyFailureConsequences"/>) et coute une penalite d'opinion, alors qu'une
    /// mission reussie est invisible pour la cible.
    /// </para>
    /// <para>
    /// <b>Aucune dependance de construction vers les autres services :</b> une mission peut
    /// toucher l'economie (cout), la recherche (vol), la diplomatie (penalite d'echec,
    /// propagande) ou l'armee (decouverte) — les injecter toutes au constructeur imposerait un
    /// ordre d'initialisation a quatre autres controleurs. Chacune est resolue paresseusement
    /// via <see cref="ServiceLocator"/> au moment ou une mission en a besoin (meme precedent
    /// que <see cref="Espace.Gameplay.Diplomacy.DiplomacyService"/> pour <see cref="IMilitaryService"/>),
    /// avec un echec explicite si le service correspondant n'est pas encore disponible plutot
    /// que de se degrader silencieusement — a la difference d'un bonus de recherche absent, une
    /// mission sans economie ou sans cible valide n'a pas de sens a moitie reussie.
    /// </para>
    /// </summary>
    public sealed class EspionageService : IEspionageService, IGameService
    {
        private const float BaseEspionagePower = 10f;

        private const float StealTechnologyCost = 150f;
        private const float SabotageCost = 100f;
        private const float InciteRevoltCost = 100f;
        private const float InfluenceGovernmentCost = 80f;
        private const float DiscoverArmiesCost = 50f;

        private const float FailureOpinionPenalty = 10f;
        private const float InfluenceOpinionGain = 15f;
        private const float RevoltStabilityLoss = 0.3f;

        private static readonly ResearchDomain[] AllDomains = (ResearchDomain[])Enum.GetValues(typeof(ResearchDomain));

        private readonly GalaxyMap _map;
        private readonly IEventBus _eventBus;

        public EspionageService(GalaxyMap map, IEventBus eventBus)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <inheritdoc />
        public void Initialize()
        {
            // Rien a initialiser : ce service ne detient aucun etat propre, uniquement des
            // effets immediats sur GalaxyMap et les autres services.
        }

        /// <inheritdoc />
        public void Shutdown()
        {
        }

        /// <inheritdoc />
        public float GetEspionagePower(int empireId)
        {
            return BaseEspionagePower * (1f + ResearchBonus(empireId, ResearchDomain.Espionage));
        }

        /// <inheritdoc />
        public float GetCounterEspionagePower(int empireId, StarSystemId referenceSystemId)
        {
            float stability = _map.TryGetSystem(referenceSystemId, out StarSystemState system) ? system.Stability : 1f;
            return BaseEspionagePower * stability * (1f + ResearchBonus(empireId, ResearchDomain.Espionage));
        }

        /// <inheritdoc />
        public bool TryStealTechnology(int proposerId, int targetEmpireId, out string error)
        {
            if (!ServiceLocator.TryGet(out IResearchService research))
            {
                error = "Recherche indisponible.";
                return false;
            }

            ResearchDomain? domain = FindMostValuableStealableDomain(proposerId, targetEmpireId, research);
            if (domain == null)
            {
                error = "Cet empire n'a aucune avance technologique a voler.";
                return false;
            }

            StarSystemState referenceSystem = FindPrimarySystem(targetEmpireId);
            if (referenceSystem == null)
            {
                error = "Cet empire ne possede aucun systeme.";
                return false;
            }

            if (!TryAttempt(proposerId, targetEmpireId, referenceSystem, StealTechnologyCost, EspionageMissionType.StealTechnology, out error))
            {
                return false;
            }

            research.GrantTier(proposerId, domain.Value);
            _eventBus.Publish(new TechnologyStolenEvent(proposerId, targetEmpireId, domain.Value));

            error = null;
            return true;
        }

        /// <inheritdoc />
        public bool TrySabotage(int proposerId, StarSystemId targetSystemId, out string error)
        {
            if (!_map.TryGetSystem(targetSystemId, out StarSystemState system))
            {
                error = "Systeme introuvable.";
                return false;
            }

            if (system.OwnerId == StarSystemState.UnownedOwnerId)
            {
                error = "Ce systeme n'a pas de proprietaire.";
                return false;
            }

            int targetEmpireId = system.OwnerId;
            if (!TryAttempt(proposerId, targetEmpireId, system, SabotageCost, EspionageMissionType.Sabotage, out error))
            {
                return false;
            }

            system.DevelopmentLevel = Mathf.Max(0, system.DevelopmentLevel - 1);
            _eventBus.Publish(new SystemSabotagedEvent(proposerId, targetEmpireId, targetSystemId));

            error = null;
            return true;
        }

        /// <inheritdoc />
        public bool TryInciteRevolt(int proposerId, StarSystemId targetSystemId, out string error)
        {
            if (!_map.TryGetSystem(targetSystemId, out StarSystemState system))
            {
                error = "Systeme introuvable.";
                return false;
            }

            if (system.OwnerId == StarSystemState.UnownedOwnerId)
            {
                error = "Ce systeme n'a pas de proprietaire.";
                return false;
            }

            int targetEmpireId = system.OwnerId;
            if (!TryAttempt(proposerId, targetEmpireId, system, InciteRevoltCost, EspionageMissionType.IncitesRevolt, out error))
            {
                return false;
            }

            system.Stability = Mathf.Max(0f, system.Stability - RevoltStabilityLoss);
            _eventBus.Publish(new RevoltIncitedEvent(proposerId, targetEmpireId, targetSystemId));

            error = null;
            return true;
        }

        /// <inheritdoc />
        public bool TryInfluenceGovernment(int proposerId, int targetEmpireId, out string error)
        {
            StarSystemState referenceSystem = FindPrimarySystem(targetEmpireId);
            if (referenceSystem == null)
            {
                error = "Cet empire ne possede aucun systeme.";
                return false;
            }

            if (!TryAttempt(proposerId, targetEmpireId, referenceSystem, InfluenceGovernmentCost, EspionageMissionType.InfluenceGovernment, out error))
            {
                return false;
            }

            if (ServiceLocator.TryGet(out IDiplomacyService diplomacy))
            {
                diplomacy.ApplyOpinionShift(targetEmpireId, proposerId, InfluenceOpinionGain);
            }

            _eventBus.Publish(new GovernmentInfluencedEvent(proposerId, targetEmpireId));

            error = null;
            return true;
        }

        /// <inheritdoc />
        public bool TryDiscoverArmies(int proposerId, int targetEmpireId, StarSystemId targetSystemId, out UnitBundle discoveredGarrison, out string error)
        {
            discoveredGarrison = UnitBundle.Zero;

            if (!_map.TryGetSystem(targetSystemId, out StarSystemState system) || system.OwnerId != targetEmpireId)
            {
                error = "Ce systeme n'appartient pas a cet empire.";
                return false;
            }

            if (!TryAttempt(proposerId, targetEmpireId, system, DiscoverArmiesCost, EspionageMissionType.DiscoverArmies, out error))
            {
                return false;
            }

            if (ServiceLocator.TryGet(out IMilitaryService military))
            {
                discoveredGarrison = military.GetGarrison(targetSystemId, targetEmpireId);
            }

            _eventBus.Publish(new ArmiesDiscoveredEvent(proposerId, targetEmpireId, targetSystemId, discoveredGarrison));

            error = null;
            return true;
        }

        /// <summary>
        /// Verifications communes a toute mission : pas d'auto-espionnage, cout paye
        /// d'avance (que la mission reussisse ou non — l'operation a un cout meme ratee), puis
        /// comparaison deterministe de puissance. En cas d'echec, applique les consequences
        /// (decouverte, penalite d'opinion) avant de retourner faux.
        /// </summary>
        private bool TryAttempt(
            int proposerId, int targetEmpireId, StarSystemState referenceSystem, float cost, EspionageMissionType type, out string error)
        {
            if (proposerId == targetEmpireId)
            {
                error = "Un empire ne peut pas s'espionner lui-meme.";
                return false;
            }

            if (!ServiceLocator.TryGet(out IEconomyService economy))
            {
                error = "Economie indisponible.";
                return false;
            }

            if (!economy.TrySpend(proposerId, new ResourceBundle(credits: cost), out error))
            {
                return false;
            }

            float ownPower = GetEspionagePower(proposerId);
            float counterPower = GetCounterEspionagePower(targetEmpireId, referenceSystem.Id);

            if (ownPower <= counterPower)
            {
                ApplyFailureConsequences(proposerId, targetEmpireId, type);
                error = "Mission dejouee par le contre-espionnage adverse.";
                return false;
            }

            error = null;
            return true;
        }

        private void ApplyFailureConsequences(int proposerId, int targetEmpireId, EspionageMissionType type)
        {
            if (ServiceLocator.TryGet(out IDiplomacyService diplomacy))
            {
                diplomacy.ApplyOpinionShift(targetEmpireId, proposerId, -FailureOpinionPenalty);
            }

            _eventBus.Publish(new MissionFailedEvent(proposerId, targetEmpireId, type));
        }

        /// <summary>Le domaine ou l'ecart de paliers completes (cible moins proposeur) est le plus grand, ou <c>null</c> si aucun domaine n'est en avance.</summary>
        private static ResearchDomain? FindMostValuableStealableDomain(int proposerId, int targetEmpireId, IResearchService research)
        {
            ResearchDomain? best = null;
            int bestGap = 0;

            foreach (ResearchDomain domain in AllDomains)
            {
                int gap = research.GetCompletedTierCount(targetEmpireId, domain) - research.GetCompletedTierCount(proposerId, domain);
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = domain;
                }
            }

            return best;
        }

        /// <summary>
        /// Systeme de reference d'un empire pour le calcul de contre-espionnage : sa capitale,
        /// c'est-a-dire son systeme le plus developpe (Phase 18).
        /// <para>
        /// Avant cette phase, c'etait le premier systeme rencontre dans l'ordre de la carte —
        /// arbitraire des que la colonisation fonctionne reellement (Phase 16), et souvent une
        /// colonie vide dont le contre-espionnage quasi nul rendait toutes les missions
        /// triviales. La capitale est aussi ce que vise desormais
        /// <see cref="EspionageDecisionMaker"/> : les deux doivent designer le meme systeme,
        /// sinon l'IA raisonne sur une difficulte qui n'est pas celle qu'elle rencontre.
        /// </para>
        /// </summary>
        private StarSystemState FindPrimarySystem(int empireId)
        {
            return EmpireHoldings.Capital(empireId, _map);
        }

        private static float ResearchBonus(int empireId, ResearchDomain domain)
        {
            return ServiceLocator.TryGet(out IResearchService research) ? research.GetBonus(empireId, domain) : 0f;
        }
    }
}
