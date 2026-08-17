using System;
using System.Collections.Generic;
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
    /// (voir <see cref="TryAttempt"/>) et peut couter une penalite d'opinion, alors qu'une
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

        /// <summary>Vigilance acquise par empire cible (voir <see cref="RaiseVigilance"/>).</summary>
        private readonly Dictionary<int, float> _vigilanceByEmpire = new Dictionary<int, float>();

        /// <summary>
        /// Tirage des issues. Propre au service plutot que partage : une operation clandestine
        /// ne doit pas consommer la meme suite que la generation de galaxie, qui doit rester
        /// reproductible a graine fixe.
        /// </summary>
        private readonly System.Random _random = new System.Random();

        /// <summary>
        /// Source des tirages, injectable.
        /// <para>
        /// <b>Pourquoi ce point d'entree existe.</b> <see cref="EspionageResolution"/> a ete
        /// ecrit en fonction pure, hasard injecte, precisement pour que les issues soient
        /// verifiables. Le service reintroduisait ensuite un generateur interne, ce qui annulait
        /// cette propriete un cran plus haut : les tests d'<c>EspionageService</c> ne pouvaient
        /// plus affirmer « cette mission reussit », et ceux qui semblaient passer le devaient au
        /// tirage du moment. Injecter la source rend le service aussi verifiable que le modele,
        /// <b>sans changer une seule regle de jeu</b> — en production le parametre reste absent.
        /// </para>
        /// </summary>
        private readonly Func<float> _roll;

        private const float StealTechnologyCost = 150f;
        private const float SabotageCost = 100f;
        private const float InciteRevoltCost = 100f;
        private const float InfluenceGovernmentCost = 80f;
        private const float DiscoverArmiesCost = 50f;

        private const float FailureOpinionPenalty = 10f;

        /// <summary>
        /// Influence engagee par credit depense. C'est ce ratio qui relie l'espionnage a
        /// l'administration : les deux puisent dans la meme reserve.
        /// </summary>
        private const float InfluenceStakeRatio = 0.6f;

        /// <summary>Vigilance gagnee par la cible a chaque tentative, reussie ou non.</summary>
        private const float VigilancePerAttempt = 0.5f;

        /// <summary>Vigilance perdue chaque mois sans nouvelle tentative.</summary>
        private const float VigilanceDecayPerMonth = 0.15f;

        /// <summary>Plafond de vigilance : une cible sur ses gardes ne devient jamais imprenable.</summary>
        private const float MaximumVigilance = 2f;
        private const float InfluenceOpinionGain = 15f;
        private const float RevoltStabilityLoss = 0.3f;

        private static readonly ResearchDomain[] AllDomains = (ResearchDomain[])Enum.GetValues(typeof(ResearchDomain));

        private readonly GalaxyMap _map;
        private readonly IEventBus _eventBus;

        /// <param name="rollSource">
        /// Source des tirages dans [0,1[. Laisser a <c>null</c> en jeu : le service utilise alors
        /// son propre generateur. Les tests l'injectent pour fixer l'issue d'une operation.
        /// </param>
        public EspionageService(GalaxyMap map, IEventBus eventBus, Func<float> rollSource = null)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _roll = rollSource ?? NextRandomRoll;
        }

        private float NextRandomRoll()
        {
            return (float)_random.NextDouble();
        }

        /// <inheritdoc />
        public void Initialize()
        {
            // Le service detient desormais un etat propre — la vigilance des cibles (Phase 22,
            // P6) — qu'il faut faire retomber avec le temps.
            _vigilanceByEmpire.Clear();
            _eventBus.Subscribe<MonthAdvancedEvent>(OnMonthAdvanced);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _eventBus.Unsubscribe<MonthAdvancedEvent>(OnMonthAdvanced);
            _vigilanceByEmpire.Clear();
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
        public float GetVigilance(int targetEmpireId)
        {
            return VigilanceOf(targetEmpireId);
        }

        /// <inheritdoc />
        float IEspionageService.MaximumVigilance
        {
            get { return MaximumVigilance; }
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
        /// Verifications communes a toute mission : pas d'auto-espionnage, cout paye d'avance
        /// (que la mission reussisse ou non — l'operation a un cout meme ratee), puis resolution
        /// par <see cref="EspionageResolution"/>.
        /// <para>
        /// <b>Ce qui a change en Phase 22 (P6).</b> La regle tenait en une comparaison :
        /// <c>puissance &gt; contre-puissance</c>, ou la contre-puissance valait
        /// <c>10 x stabilite</c>. La stabilite etant toujours inferieure a 1, l'attaquant
        /// <b>gagnait toujours</b> a recherche egale. Il n'y avait ni probabilite, ni risque, ni
        /// detection distincte de l'echec.
        /// </para>
        /// <para>
        /// <b>L'influence est desormais la mise.</b> Le point d'entree public n'a pas change —
        /// aucun appelant n'est a retoucher — mais l'operation engage l'influence disponible,
        /// plafonnee par <see cref="InfluenceStakeRatio"/>. La consequence est systemique :
        /// l'influence paie <em>aussi</em> l'administration de l'empire (P4), donc un empire
        /// etale n'a plus les moyens de comploter. Personne n'a eu a ecrire cette regle, elle
        /// tombe du partage d'une meme ressource.
        /// </para>
        /// <para>
        /// <b>Quatre issues.</b> Une reussite peut etre attribuee — l'effet a lieu mais la
        /// relation en paie le prix — et un echec peut passer inapercu. Auparavant tout echec
        /// coutait de l'opinion, ce qui rendait toute tentative diplomatiquement chere.
        /// </para>
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

            float influenceCommitted = CommitInfluence(economy, proposerId, cost);

            float attack = EspionageResolution.AttackPower(
                influenceCommitted,
                networkStrength: 1f,
                researchBonus: ResearchBonus(proposerId, ResearchDomain.Espionage));

            float defence = EspionageResolution.DefencePower(
                referenceSystem.Stability,
                ResearchBonus(targetEmpireId, ResearchDomain.Espionage),
                VigilanceOf(targetEmpireId));

            EspionageOutcome outcome = EspionageResolution.Resolve(attack, defence, _roll(), _roll());

            // La vigilance monte que l'operation reussisse ou non : la cible apprend qu'on
            // s'interesse a elle. C'est ce qui empeche de repeter indefiniment une operation
            // rentable, sans qu'aucun delai arbitraire soit impose.
            RaiseVigilance(targetEmpireId);

            if (EspionageResolution.WasAttributed(outcome))
            {
                ApplyAttributionPenalty(proposerId, targetEmpireId);
            }

            if (EspionageResolution.Succeeded(outcome))
            {
                error = null;
                return true;
            }

            _eventBus.Publish(new MissionFailedEvent(proposerId, targetEmpireId, type));

            error = outcome == EspionageOutcome.Exposed
                ? "Mission dejouee, et nos agents ont ete identifies."
                : "Mission dejouee, mais nos agents sont restes anonymes.";

            return false;
        }

        /// <summary>
        /// Preleve l'influence engagee dans l'operation et renvoie ce qui a pu l'etre.
        /// <para>
        /// Un empire a court d'influence monte quand meme l'operation, mais faiblement : mieux
        /// vaut une tentative desesperee qu'un refus sec, qui obligerait a expliquer au joueur
        /// une regle de plus.
        /// </para>
        /// </summary>
        private static float CommitInfluence(IEconomyService economy, int proposerId, float cost)
        {
            float desired = cost * InfluenceStakeRatio;
            float available = economy.GetTreasury(proposerId).Influence;
            float committed = Mathf.Min(desired, Mathf.Max(0f, available));

            if (committed > 0f)
            {
                economy.TrySpend(proposerId, new ResourceBundle(influence: committed), out _);
            }

            return committed;
        }

        private float VigilanceOf(int empireId)
        {
            return _vigilanceByEmpire.TryGetValue(empireId, out float vigilance) ? vigilance : 0f;
        }

        private void RaiseVigilance(int empireId)
        {
            _vigilanceByEmpire[empireId] = Mathf.Min(MaximumVigilance, VigilanceOf(empireId) + VigilancePerAttempt);
        }

        /// <summary>
        /// La vigilance retombe avec le temps : une cible harcelee puis laissee tranquille
        /// redevient penetrable, sinon un empire attaque tot deviendrait definitivement
        /// intouchable.
        /// </summary>
        private void OnMonthAdvanced(MonthAdvancedEvent monthAdvancedEvent)
        {
            if (_vigilanceByEmpire.Count == 0)
            {
                return;
            }

            var empireIds = new List<int>(_vigilanceByEmpire.Keys);
            foreach (int empireId in empireIds)
            {
                float decayed = _vigilanceByEmpire[empireId] - VigilanceDecayPerMonth;
                if (decayed <= 0f)
                {
                    _vigilanceByEmpire.Remove(empireId);
                }
                else
                {
                    _vigilanceByEmpire[empireId] = decayed;
                }
            }
        }

        /// <summary>Consequence diplomatique d'une operation attribuee, reussie ou non.</summary>
        private void ApplyAttributionPenalty(int proposerId, int targetEmpireId)
        {
            if (ServiceLocator.TryGet(out IDiplomacyService diplomacy))
            {
                diplomacy.ApplyOpinionShift(targetEmpireId, proposerId, -FailureOpinionPenalty);
            }
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
