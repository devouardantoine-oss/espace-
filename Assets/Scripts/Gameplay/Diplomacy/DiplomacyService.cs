using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.Research;
using UnityEngine;

namespace Espace.Gameplay.Diplomacy
{
    /// <summary>
    /// Implementation par defaut de <see cref="IDiplomacyService"/>.
    /// <para>
    /// <b>Pas de dependance directe a <see cref="IMilitaryService"/> :</b> l'evaluation des
    /// propositions (paix, ultimatum) a besoin d'estimer la puissance militaire des empires,
    /// mais l'injecter au constructeur creerait un cycle — <see cref="MilitaryService"/> a
    /// lui-meme besoin de ce service pour bloquer les mouvements de flotte hors etat de
    /// guerre (voir son commentaire). Ce service resout <see cref="IMilitaryService"/>
    /// paresseusement via <see cref="ServiceLocator"/> au moment ou il en a besoin, et se
    /// degrade proprement (puissance nulle) si indisponible, au lieu d'echouer.
    /// </para>
    /// <para>
    /// <b>Statut symetrique, opinion dirigee :</b> <see cref="DiplomaticStatus"/> est stocke
    /// par paire non ordonnee (identique pour A→B et B→A) ; l'opinion est stockee par paire
    /// ordonnee (A→B peut differer de B→A) — deux structures de donnees differentes pour deux
    /// semantiques differentes.
    /// </para>
    /// </summary>
    public sealed class DiplomacyService : IDiplomacyService, IGameService
    {
        private const float WarDeclaredOpinionPenalty = 30f;
        private const float PactBrokenOpinionPenalty = 25f;
        private const float AllianceOpinionBonus = 20f;
        private const float PactOpinionBonus = 15f;
        private const float PeaceOpinionBonus = 10f;
        private const float TradeGoodwillOpinionBonus = 5f;
        private const float UltimatumComplianceOpinionPenalty = 15f;
        private const float OpinionDriftPerMonth = 1f;
        private const float TradeTreatyMonthlyIncome = 10f;
        private const float MinOpinion = -100f;
        private const float MaxOpinion = 100f;

        private readonly EmpireRegistry _empireRegistry;
        private readonly IEconomyService _economy;
        private readonly GalaxyMap _map;
        private readonly IEventBus _eventBus;

        private readonly Dictionary<(int, int), DiplomaticStatus> _statuses = new Dictionary<(int, int), DiplomaticStatus>();
        private readonly Dictionary<(int, int), float> _opinions = new Dictionary<(int, int), float>();
        private readonly HashSet<(int, int)> _tradeTreaties = new HashSet<(int, int)>();
        private readonly HashSet<(int, int)> _embargoes = new HashSet<(int, int)>();
        private readonly List<DiplomaticProposal> _pendingProposals = new List<DiplomaticProposal>();
        private int _nextProposalId;

        public DiplomacyService(EmpireRegistry empireRegistry, IEconomyService economy, GalaxyMap map, IEventBus eventBus)
        {
            _empireRegistry = empireRegistry ?? throw new ArgumentNullException(nameof(empireRegistry));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _statuses.Clear();
            _opinions.Clear();
            _tradeTreaties.Clear();
            _embargoes.Clear();
            _pendingProposals.Clear();
            _nextProposalId = 1;
            _eventBus.Subscribe<MonthAdvancedEvent>(OnMonthAdvanced);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _eventBus.Unsubscribe<MonthAdvancedEvent>(OnMonthAdvanced);
            _pendingProposals.Clear();
        }

        /// <inheritdoc />
        public DiplomaticStatus GetStatus(int empireAId, int empireBId)
        {
            return _statuses.TryGetValue(NormalizeKey(empireAId, empireBId), out DiplomaticStatus status) ? status : DiplomaticStatus.Peace;
        }

        /// <inheritdoc />
        public float GetOpinion(int observerId, int targetId)
        {
            return _opinions.TryGetValue((observerId, targetId), out float opinion) ? opinion : 0f;
        }

        /// <inheritdoc />
        public bool HasTradeTreaty(int empireAId, int empireBId)
        {
            return _tradeTreaties.Contains(NormalizeKey(empireAId, empireBId));
        }

        /// <inheritdoc />
        public bool IsEmbargoing(int fromEmpireId, int toEmpireId)
        {
            return _embargoes.Contains((fromEmpireId, toEmpireId));
        }

        /// <inheritdoc />
        public IReadOnlyList<DiplomaticProposal> GetPendingProposalsFor(int empireId)
        {
            return _pendingProposals.FindAll(p => p.TargetId == empireId);
        }

        /// <inheritdoc />
        public bool TryDeclareWar(int declarerId, int targetId, out string error)
        {
            if (declarerId == targetId)
            {
                error = "Un empire ne peut pas se declarer la guerre a lui-meme.";
                return false;
            }

            DiplomaticStatus current = GetStatus(declarerId, targetId);
            if (current == DiplomaticStatus.War)
            {
                error = "Ces empires sont deja en guerre.";
                return false;
            }

            if (current == DiplomaticStatus.Alliance || current == DiplomaticStatus.NonAggressionPact)
            {
                error = "Rompez d'abord le pacte en vigueur (TryBreakPact).";
                return false;
            }

            if (HasTradeTreaty(declarerId, targetId))
            {
                SetTradeTreaty(declarerId, targetId, false);
            }

            SetStatus(declarerId, targetId, DiplomaticStatus.War);
            AdjustOpinion(targetId, declarerId, -WarDeclaredOpinionPenalty);

            GameLog.Info($"[Diplomacy] L'empire {declarerId} declare la guerre a l'empire {targetId}.");

            error = null;
            return true;
        }

        /// <inheritdoc />
        public bool TrySetEmbargo(int fromEmpireId, int toEmpireId, bool active, out string error)
        {
            if (fromEmpireId == toEmpireId)
            {
                error = "Un empire ne peut pas s'embarguer lui-meme.";
                return false;
            }

            var key = (fromEmpireId, toEmpireId);
            bool currentlyActive = _embargoes.Contains(key);
            if (currentlyActive == active)
            {
                error = active ? "Cet embargo est deja en vigueur." : "Aucun embargo a lever.";
                return false;
            }

            if (active)
            {
                _embargoes.Add(key);
            }
            else
            {
                _embargoes.Remove(key);
            }

            _eventBus.Publish(new EmbargoChangedEvent(fromEmpireId, toEmpireId, active));

            error = null;
            return true;
        }

        /// <inheritdoc />
        public bool TryBreakPact(int fromEmpireId, int toEmpireId, out string error)
        {
            DiplomaticStatus current = GetStatus(fromEmpireId, toEmpireId);
            if (current != DiplomaticStatus.Alliance && current != DiplomaticStatus.NonAggressionPact)
            {
                error = "Aucune Alliance ni Pacte de non-agression en vigueur avec cet empire.";
                return false;
            }

            SetStatus(fromEmpireId, toEmpireId, DiplomaticStatus.Peace);
            AdjustOpinion(toEmpireId, fromEmpireId, -PactBrokenOpinionPenalty);

            GameLog.Info($"[Diplomacy] L'empire {fromEmpireId} rompt son pacte avec l'empire {toEmpireId}.");

            error = null;
            return true;
        }

        /// <inheritdoc />
        public bool TrySubmitProposal(
            int proposerId, int targetId, ProposalType type,
            ResourceBundle offeredResources, ResourceBundle requestedResources,
            StarSystemId? offeredSystemId, StarSystemId? requestedSystemId,
            out string error)
        {
            if (proposerId == targetId)
            {
                error = "Un empire ne peut pas se faire une proposition a lui-meme.";
                return false;
            }

            if (!ValidateProposalPreconditions(proposerId, targetId, type, out error))
            {
                return false;
            }

            var proposal = new DiplomaticProposal(
                _nextProposalId++, proposerId, targetId, type,
                offeredResources, requestedResources, offeredSystemId, requestedSystemId);

            if (_empireRegistry.PlayerEmpire.Id == targetId)
            {
                _pendingProposals.Add(proposal);
                _eventBus.Publish(new ProposalReceivedEvent(proposal));
                error = null;
                return true;
            }

            bool accepted = EvaluateAsAi(proposal);
            ResolveProposal(proposal, accepted);

            error = null;
            return true;
        }

        /// <inheritdoc />
        public bool TryRespondToProposal(int proposalId, bool accept, out string error)
        {
            int index = _pendingProposals.FindIndex(p => p.Id == proposalId);
            if (index < 0)
            {
                error = "Proposition introuvable ou deja resolue.";
                return false;
            }

            DiplomaticProposal proposal = _pendingProposals[index];
            _pendingProposals.RemoveAt(index);
            ResolveProposal(proposal, accept);

            error = null;
            return true;
        }

        /// <inheritdoc />
        public void ApplyOpinionShift(int observerId, int targetId, float delta)
        {
            AdjustOpinion(observerId, targetId, delta);
        }

        /// <inheritdoc />
        public void RestoreRelations(int empireAId, int empireBId, DiplomaticStatus status, bool hasTradeTreaty)
        {
            _statuses[NormalizeKey(empireAId, empireBId)] = status;

            if (hasTradeTreaty)
            {
                _tradeTreaties.Add(NormalizeKey(empireAId, empireBId));
            }
        }

        /// <inheritdoc />
        public void RestoreOpinion(int observerId, int targetId, float value)
        {
            _opinions[(observerId, targetId)] = Mathf.Clamp(value, MinOpinion, MaxOpinion);
        }

        /// <inheritdoc />
        public void RestoreEmbargo(int fromEmpireId, int toEmpireId)
        {
            _embargoes.Add((fromEmpireId, toEmpireId));
        }

        private bool ValidateProposalPreconditions(int proposerId, int targetId, ProposalType type, out string error)
        {
            if (!_empireRegistry.TryGetEmpire(proposerId, out _) || !_empireRegistry.TryGetEmpire(targetId, out _))
            {
                error = "Empire inconnu.";
                return false;
            }

            DiplomaticStatus status = GetStatus(proposerId, targetId);

            switch (type)
            {
                case ProposalType.Alliance:
                    if (status == DiplomaticStatus.War)
                    {
                        error = "Faites d'abord la paix avant de proposer une alliance.";
                        return false;
                    }

                    if (status == DiplomaticStatus.Alliance)
                    {
                        error = "Ces empires sont deja allies.";
                        return false;
                    }

                    break;

                case ProposalType.NonAggressionPact:
                    if (status != DiplomaticStatus.Peace)
                    {
                        error = "Seule la Paix simple permet de proposer un pacte de non-agression.";
                        return false;
                    }

                    break;

                case ProposalType.TradeTreaty:
                    if (status == DiplomaticStatus.War)
                    {
                        error = "Impossible de commercer en temps de guerre.";
                        return false;
                    }

                    if (HasTradeTreaty(proposerId, targetId))
                    {
                        error = "Un traite commercial est deja en vigueur.";
                        return false;
                    }

                    break;

                case ProposalType.PeaceTreaty:
                    if (status != DiplomaticStatus.War)
                    {
                        error = "Ces empires ne sont pas en guerre.";
                        return false;
                    }

                    break;

                case ProposalType.ResourceExchange:
                case ProposalType.TerritoryExchange:
                    if (status == DiplomaticStatus.War)
                    {
                        error = "Impossible d'echanger en temps de guerre.";
                        return false;
                    }

                    break;

                case ProposalType.Ultimatum:
                    if (status == DiplomaticStatus.Alliance || status == DiplomaticStatus.NonAggressionPact)
                    {
                        error = "Rompez d'abord le pacte en vigueur (TryBreakPact).";
                        return false;
                    }

                    if (status == DiplomaticStatus.War)
                    {
                        error = "Ces empires sont deja en guerre.";
                        return false;
                    }

                    break;

                default:
                    error = "Type de proposition inconnu.";
                    return false;
            }

            error = null;
            return true;
        }

        private bool EvaluateAsAi(DiplomaticProposal proposal)
        {
            Empire target = _empireRegistry.GetEmpire(proposal.TargetId);
            EmpirePersonalityProfileData targetProfile = EmpirePersonalityProfile.Get(target.Personality);
            float opinionOfProposer = GetOpinion(proposal.TargetId, proposal.ProposerId);
            float targetPower = EstimatePower(proposal.TargetId);
            float proposerPower = EstimatePower(proposal.ProposerId);

            return ProposalEvaluator.Evaluate(proposal, targetProfile, opinionOfProposer, targetPower, proposerPower);
        }

        private void ResolveProposal(DiplomaticProposal proposal, bool accepted)
        {
            if (accepted)
            {
                ApplyAcceptedProposal(proposal);
            }
            else if (proposal.Type == ProposalType.Ultimatum)
            {
                // Un ultimatum refuse se traduit par une declaration de guerre automatique du proposeur.
                TryDeclareWar(proposal.ProposerId, proposal.TargetId, out _);
            }

            GameLog.Info(
                $"[Diplomacy] Proposition #{proposal.Id} ({proposal.Type}) de l'empire {proposal.ProposerId} "
                + $"vers l'empire {proposal.TargetId} : {(accepted ? "acceptee" : "refusee")}.");

            _eventBus.Publish(new ProposalResolvedEvent(proposal.Id, proposal.ProposerId, proposal.TargetId, proposal.Type, accepted));
        }

        private void ApplyAcceptedProposal(DiplomaticProposal proposal)
        {
            switch (proposal.Type)
            {
                case ProposalType.Alliance:
                    SetStatus(proposal.ProposerId, proposal.TargetId, DiplomaticStatus.Alliance);
                    AdjustOpinionGain(proposal.ProposerId, proposal.TargetId, AllianceOpinionBonus);
                    AdjustOpinionGain(proposal.TargetId, proposal.ProposerId, AllianceOpinionBonus);
                    break;

                case ProposalType.NonAggressionPact:
                    SetStatus(proposal.ProposerId, proposal.TargetId, DiplomaticStatus.NonAggressionPact);
                    AdjustOpinionGain(proposal.ProposerId, proposal.TargetId, PactOpinionBonus);
                    AdjustOpinionGain(proposal.TargetId, proposal.ProposerId, PactOpinionBonus);
                    break;

                case ProposalType.TradeTreaty:
                    SetTradeTreaty(proposal.ProposerId, proposal.TargetId, true);
                    break;

                case ProposalType.PeaceTreaty:
                    SetStatus(proposal.ProposerId, proposal.TargetId, DiplomaticStatus.Peace);
                    AdjustOpinionGain(proposal.ProposerId, proposal.TargetId, PeaceOpinionBonus);
                    AdjustOpinionGain(proposal.TargetId, proposal.ProposerId, PeaceOpinionBonus);
                    break;

                case ProposalType.ResourceExchange:
                    TransferResources(proposal.ProposerId, proposal.TargetId, proposal.OfferedResources);
                    TransferResources(proposal.TargetId, proposal.ProposerId, proposal.RequestedResources);
                    AdjustOpinionGain(proposal.TargetId, proposal.ProposerId, TradeGoodwillOpinionBonus);
                    break;

                case ProposalType.TerritoryExchange:
                    TransferSystemOwnership(proposal.OfferedSystemId, proposal.TargetId);
                    TransferSystemOwnership(proposal.RequestedSystemId, proposal.ProposerId);
                    break;

                case ProposalType.Ultimatum:
                    TransferResources(proposal.TargetId, proposal.ProposerId, proposal.OfferedResources);
                    AdjustOpinion(proposal.TargetId, proposal.ProposerId, -UltimatumComplianceOpinionPenalty);
                    break;
            }
        }

        private void TransferResources(int fromEmpireId, int toEmpireId, ResourceBundle amount)
        {
            if (amount == ResourceBundle.Zero)
            {
                return;
            }

            _economy.TrySpend(fromEmpireId, amount, out _);
            _economy.Grant(toEmpireId, amount);
        }

        private void TransferSystemOwnership(StarSystemId? systemId, int newOwnerId)
        {
            if (systemId == null)
            {
                return;
            }

            if (_map.TryGetSystem(systemId.Value, out StarSystemState system))
            {
                system.OwnerId = newOwnerId;
            }
        }

        /// <summary>
        /// Puissance militaire totale d'un empire : somme de la puissance de ses garnisons sur
        /// tous ses systemes (pas seulement son systeme principal, a la difference des
        /// decisions au tour par tour de <see cref="MilitaryDecisionMaker"/>) — l'evaluation
        /// d'une proposition de paix ou d'un ultimatum doit refleter la force reelle de
        /// l'empire, pas seulement celle d'un front. Degrade a 0 si <see cref="IMilitaryService"/>
        /// n'est pas encore disponible (voir la remarque du constructeur).
        /// </summary>
        private float EstimatePower(int empireId)
        {
            if (!ServiceLocator.TryGet(out IMilitaryService military))
            {
                return 0f;
            }

            float total = 0f;
            foreach (StarSystemState system in _map.Systems)
            {
                if (system.OwnerId != empireId)
                {
                    continue;
                }

                total += military.EstimatePower(military.GetGarrison(system.Id, empireId));
            }

            return total;
        }

        private void OnMonthAdvanced(MonthAdvancedEvent monthAdvancedEvent)
        {
            DriftOpinions();
            AccrueTradeTreatyIncome();
        }

        /// <summary>Chaque opinion derive doucement vers la valeur cible de son statut courant (guerre : hostile, alliance : tres favorable, pacte : favorable, sinon neutre).</summary>
        private void DriftOpinions()
        {
            var keys = new List<(int, int)>(_opinions.Keys);
            foreach ((int observerId, int targetId) in keys)
            {
                float target = OpinionDriftTarget(GetStatus(observerId, targetId));
                _opinions[(observerId, targetId)] = Mathf.MoveTowards(_opinions[(observerId, targetId)], target, OpinionDriftPerMonth);
            }
        }

        private static float OpinionDriftTarget(DiplomaticStatus status)
        {
            switch (status)
            {
                case DiplomaticStatus.War: return -100f;
                case DiplomaticStatus.Alliance: return 100f;
                case DiplomaticStatus.NonAggressionPact: return 30f;
                default: return 0f;
            }
        }

        private void AccrueTradeTreatyIncome()
        {
            foreach ((int empireAId, int empireBId) in _tradeTreaties)
            {
                var income = new ResourceBundle(credits: TradeTreatyMonthlyIncome);
                _economy.Grant(empireAId, income);
                _economy.Grant(empireBId, income);
            }
        }

        private void AdjustOpinion(int observerId, int targetId, float delta)
        {
            var key = (observerId, targetId);
            float current = _opinions.TryGetValue(key, out float value) ? value : 0f;
            _opinions[key] = Mathf.Clamp(current + delta, MinOpinion, MaxOpinion);
        }

        /// <summary>
        /// Variante d'<see cref="AdjustOpinion"/> pour un gain d'opinion positif issu d'une
        /// proposition acceptee (alliance, pacte, paix, bonne volonte commerciale) : amplifie
        /// par la recherche en Diplomatie (Phase 8) de <paramref name="observerId"/> — de
        /// meilleurs diplomates suscitent une meilleure opinion. Volontairement pas utilisee
        /// pour les penalites (guerre declaree, pacte rompu, soumission a un ultimatum) : la
        /// recherche rend plus convaincant, elle n'attenue pas la colere qu'on suscite.
        /// </summary>
        private void AdjustOpinionGain(int observerId, int targetId, float baseBonus)
        {
            AdjustOpinion(observerId, targetId, baseBonus * ResearchMultiplier(observerId, ResearchDomain.Diplomacy));
        }

        private static float ResearchMultiplier(int empireId, ResearchDomain domain)
        {
            return ServiceLocator.TryGet(out IResearchService research) ? 1f + research.GetBonus(empireId, domain) : 1f;
        }

        private void SetStatus(int empireAId, int empireBId, DiplomaticStatus newStatus)
        {
            var key = NormalizeKey(empireAId, empireBId);
            DiplomaticStatus oldStatus = _statuses.TryGetValue(key, out DiplomaticStatus existing) ? existing : DiplomaticStatus.Peace;
            if (oldStatus == newStatus)
            {
                return;
            }

            _statuses[key] = newStatus;
            _eventBus.Publish(new DiplomaticStatusChangedEvent(empireAId, empireBId, oldStatus, newStatus));
        }

        private void SetTradeTreaty(int empireAId, int empireBId, bool active)
        {
            var key = NormalizeKey(empireAId, empireBId);
            bool currentlyActive = _tradeTreaties.Contains(key);
            if (currentlyActive == active)
            {
                return;
            }

            if (active)
            {
                _tradeTreaties.Add(key);
            }
            else
            {
                _tradeTreaties.Remove(key);
            }

            _eventBus.Publish(new TradeTreatyChangedEvent(empireAId, empireBId, active));
        }

        private static (int, int) NormalizeKey(int empireAId, int empireBId)
        {
            return empireAId <= empireBId ? (empireAId, empireBId) : (empireBId, empireAId);
        }
    }
}
