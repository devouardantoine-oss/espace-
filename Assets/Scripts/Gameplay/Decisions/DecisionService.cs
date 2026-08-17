using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Chronicle;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using UnityEngine;

namespace Espace.Gameplay.Decisions
{
    /// <summary>Les decisions en attente, telles que l'interface et la sauvegarde les consultent.</summary>
    public interface IDecisionService
    {
        /// <summary>Decisions en attente de reponse, de la plus ancienne a la plus recente.</summary>
        IReadOnlyList<PendingDecision> Pending { get; }

        /// <summary>Ardoises contractees et pas encore echues.</summary>
        IReadOnlyList<ScheduledConsequence> Scheduled { get; }

        /// <summary>
        /// Repond a une decision. Applique l'effet immediat et prend date pour le differe.
        /// </summary>
        /// <returns>Faux si la decision ou l'option n'existe pas.</returns>
        bool Answer(int decisionId, int optionIndex);

        /// <summary>Restaure l'etat sauvegarde.</summary>
        void Restore(IEnumerable<PendingDecision> pending, IEnumerable<ScheduledConsequence> scheduled, int nextId);

        /// <summary>Prochain identifiant a attribuer, a sauvegarder pour que les identifiants restent uniques.</summary>
        int NextId { get; }
    }

    /// <summary>
    /// Pose les questions et tient l'echeancier (Phase 24, etape 5).
    /// <para>
    /// <b>Ce que ce systeme apporte, et qui manquait entierement.</b> Le jeu publiait trente et
    /// un evenements et n'en <i>demandait</i> aucun. Tout se decidait par des boutons toujours
    /// disponibles ; rien ne venait jamais chercher le joueur pour lui imposer un choix qu'il
    /// aurait prefere ne pas avoir a faire.
    /// </para>
    /// <para>
    /// <b>Un systeme ne repose pas la question tant que son ardoise n'est pas soldee.</b> C'est
    /// la regle qui remplace un minuteur : repondre contracte forcement une consequence differee
    /// — toute option en porte une, c'est la regle absolue du catalogue — donc l'echeance en
    /// cours <i>est</i> le delai de repos. Rien de plus a stocker, rien de plus a sauvegarder, et
    /// aucun risque qu'un minuteur et une echeance se desynchronisent.
    /// </para>
    /// <para>
    /// <b>Il lit l'etat, il n'ecoute pas les mutations</b>, comme <c>CodexService</c> et
    /// <c>SystemGlyphController</c> avant lui : la stabilite bouge chaque mois par le bilan
    /// economique, par l'espionnage adverse et par les decisions elles-memes. Relire la carte
    /// chaque jour reste vrai par construction.
    /// </para>
    /// </summary>
    public sealed class DecisionService : IDecisionService, IGameService
    {
        private readonly IEventBus _eventBus;
        private readonly GalaxyMap _map;

        private readonly List<PendingDecision> _pending = new List<PendingDecision>();
        private readonly List<ScheduledConsequence> _scheduled = new List<ScheduledConsequence>();

        private IEconomyService _economy;
        private IMilitaryService _military;
        private IChronicleService _chronicle;
        private IGameClock _clock;

        private int _nextId = 1;

        public DecisionService(IEventBus eventBus, GalaxyMap map)
        {
            _eventBus = eventBus;
            _map = map;
        }

        /// <inheritdoc />
        public IReadOnlyList<PendingDecision> Pending
        {
            get { return _pending; }
        }

        /// <inheritdoc />
        public IReadOnlyList<ScheduledConsequence> Scheduled
        {
            get { return _scheduled; }
        }

        /// <inheritdoc />
        public int NextId
        {
            get { return _nextId; }
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _pending.Clear();
            _scheduled.Clear();
            _nextId = 1;

            _eventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _eventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);

            _pending.Clear();
            _scheduled.Clear();
        }

        /// <inheritdoc />
        public void Restore(IEnumerable<PendingDecision> pending, IEnumerable<ScheduledConsequence> scheduled, int nextId)
        {
            _pending.Clear();
            _scheduled.Clear();
            _nextId = Mathf.Max(1, nextId);

            if (pending != null)
            {
                foreach (PendingDecision decision in pending)
                {
                    if (decision != null)
                    {
                        _pending.Add(decision);
                    }
                }
            }

            if (scheduled == null)
            {
                return;
            }

            foreach (ScheduledConsequence consequence in scheduled)
            {
                if (consequence != null)
                {
                    _scheduled.Add(consequence);
                }
            }
        }

        private void OnDayAdvanced(DayAdvancedEvent dayAdvanced)
        {
            SettleDueConsequences(dayAdvanced.Date);
            RaiseNewDecisions(dayAdvanced.Date);
        }

        /// <summary>
        /// Fait tomber les ardoises echues.
        /// <para>
        /// <b>Avant de lever de nouvelles questions</b>, deliberement : une ardoise qui tombe
        /// libere le systeme et peut, du meme coup, le replonger sous le seuil. L'ordre inverse
        /// aurait fait attendre un jour de plus pour rien.
        /// </para>
        /// </summary>
        private void SettleDueConsequences(GameDate today)
        {
            for (int i = _scheduled.Count - 1; i >= 0; i--)
            {
                ScheduledConsequence consequence = _scheduled[i];

                if (today.CompareTo(consequence.DueOn) < 0)
                {
                    continue;
                }

                Apply(consequence.SystemId, consequence.Credits, consequence.GarrisonFraction, consequence.Stability);
                _scheduled.RemoveAt(i);

                Record(NoticeKind.DecisionSettled, consequence.Text, consequence.SystemId);
            }
        }

        private void RaiseNewDecisions(GameDate today)
        {
            if (_map == null)
            {
                return;
            }

            foreach (StarSystemState system in _map.Systems)
            {
                if (system.OwnerId != EconomyService.PlayerOwnerId
                    || system.Stability >= DecisionCatalogue.UnrestThreshold
                    || HasOpenBusinessWith(system.Id))
                {
                    continue;
                }

                var decision = DecisionCatalogue.Unrest(_nextId++, system, today);
                _pending.Add(decision);

                Record(NoticeKind.DecisionRequired, decision.Title + " : une reponse est attendue.", system.Id);
            }
        }

        /// <summary>Vrai si ce systeme a deja une question posee ou une ardoise en cours.</summary>
        private bool HasOpenBusinessWith(StarSystemId systemId)
        {
            foreach (PendingDecision decision in _pending)
            {
                if (decision.SystemId.Equals(systemId))
                {
                    return true;
                }
            }

            foreach (ScheduledConsequence consequence in _scheduled)
            {
                if (consequence.SystemId.Equals(systemId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public bool Answer(int decisionId, int optionIndex)
        {
            int index = _pending.FindIndex(d => d.Id == decisionId);
            if (index < 0)
            {
                return false;
            }

            PendingDecision decision = _pending[index];
            if (optionIndex < 0 || optionIndex >= decision.Options.Count)
            {
                return false;
            }

            DecisionOption option = decision.Options[optionIndex];

            Apply(decision.SystemId, option.ImmediateCredits, option.ImmediateGarrisonFraction, option.ImmediateStability);
            _pending.RemoveAt(index);

            _scheduled.Add(new ScheduledConsequence(
                decision.SystemId,
                Today().AddDays(option.DeferredDelayDays),
                option.DeferredCredits,
                option.DeferredGarrisonFraction,
                option.DeferredStability,
                $"{decision.Title} — {option.DeferredText}."));

            Record(NoticeKind.DecisionAnswered, $"{decision.Title} : {option.Label}.", decision.SystemId);

            // Le gouverneur du monde apprend ce qu'on a decide pour lui.
            _eventBus.Publish(new DecisionAnsweredEvent(decision.SystemId, option.RememberedAs));

            return true;
        }

        /// <summary>
        /// Applique un jeu d'effets a un systeme.
        /// <para>
        /// Les Credits sont pris sur le tresor du joueur meme s'il ne les a pas : une dette
        /// contractee par une decision se paie, sinon temporiser deviendrait gratuit pour un
        /// empire ruine — exactement le joueur a qui la lecon s'adresse.
        /// </para>
        /// </summary>
        private void Apply(StarSystemId systemId, float credits, float garrisonFraction, float stability)
        {
            // Indispensable ici, et pas seulement dans Record : la toute premiere ardoise a
            // tomber le faisait avant que le moindre appel a Record n'ait resolu l'economie, et
            // se reglait donc gratuitement. Un test l'a montre, l'ecran ne l'aurait jamais fait —
            // rien ne plante quand une dette silencieuse n'est pas prelevee.
            ResolveServices();

            if (credits > 0f && _economy != null)
            {
                _economy.Grant(EconomyService.PlayerOwnerId, new ResourceBundle(credits: -credits));
            }

            if (garrisonFraction > 0f && _military != null)
            {
                _military.ReduceGarrison(systemId, EconomyService.PlayerOwnerId, garrisonFraction);
            }

            if (stability == 0f || _map == null || !_map.TryGetSystem(systemId, out StarSystemState system))
            {
                return;
            }

            system.Stability = Mathf.Clamp01(system.Stability + stability);
        }

        private void Record(NoticeKind kind, string text, StarSystemId subject)
        {
            ResolveServices();
            _chronicle?.Log.Add(new GameNotice(kind, text, Today(), subject));
        }

        private GameDate Today()
        {
            ResolveServices();
            return _clock != null ? _clock.CurrentDate : GameDate.StartOfGame;
        }

        private void ResolveServices()
        {
            if (_economy == null) ServiceLocator.TryGet(out _economy);
            if (_military == null) ServiceLocator.TryGet(out _military);
            if (_chronicle == null) ServiceLocator.TryGet(out _chronicle);
            if (_clock == null) ServiceLocator.TryGet(out _clock);
        }
    }
}
