using Espace.Core;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Espionage;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.Research;

namespace Espace.Gameplay.Chronicle
{
    /// <summary>Journal de la partie, tel que l'interface le consulte.</summary>
    public interface IChronicleService
    {
        /// <summary>Le journal lui-meme, du plus recent au plus ancien.</summary>
        NoticeLog Log { get; }
    }

    /// <summary>
    /// Traduit les evenements du jeu en lignes de journal (Phase 24, etape 1).
    /// <para>
    /// <b>Le defaut corrige.</b> Trente et un types d'evenements sont publies et aucun n'est
    /// affiche. Une bataille se resout, une construction s'acheve, une proposition arrive : rien
    /// ne le dit. Le joueur qui regardait ailleurs ne l'apprend jamais.
    /// </para>
    /// <para>
    /// <b>La regle qui gouverne ce service : le journal ne rapporte que ce que le joueur peut
    /// savoir.</b> Une bataille entre deux empires adverses a l'autre bout de la galaxie n'y
    /// figure pas — le joueur n'a aucun moyen d'en etre informe, et l'y faire apparaitre
    /// contredirait tout le reste du jeu, ou la garnison d'un systeme adverse reste invisible
    /// tant qu'aucun espionnage n'a abouti. <b>Seule exception : les changements de statut
    /// diplomatique</b>, qui sont publics par nature — une declaration de guerre s'annonce.
    /// </para>
    /// <para>
    /// <b>Il remplace les rapports d'operation de la Phase 20</b>, qui tenaient six lignes dans
    /// <c>ManagementWindowController</c> et n'ecoutaient que deux evenements. Une interface qui
    /// s'abonne elle-meme au bus pour tenir son propre historique etait un contournement ; c'est
    /// desormais un service, et l'interface se contente de le lire.
    /// </para>
    /// </summary>
    public sealed class ChronicleService : IChronicleService, IGameService
    {
        private readonly IEventBus _eventBus;
        private readonly GalaxyMap _map;

        private IGameClock _clock;
        private EmpireRegistry _empires;

        /// <inheritdoc />
        public NoticeLog Log { get; } = new NoticeLog();

        public ChronicleService(IEventBus eventBus, GalaxyMap map)
        {
            _eventBus = eventBus;
            _map = map;
        }

        /// <inheritdoc />
        public void Initialize()
        {
            Log.Clear();

            _eventBus.Subscribe<BattleResolvedEvent>(OnBattleResolved);
            _eventBus.Subscribe<EncounterStartedEvent>(OnEncounterStarted);
            _eventBus.Subscribe<SystemColonizedEvent>(OnSystemColonized);
            _eventBus.Subscribe<FleetDepartedEvent>(OnFleetDeparted);
            _eventBus.Subscribe<RecruitmentCompletedEvent>(OnRecruitmentCompleted);
            _eventBus.Subscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            _eventBus.Subscribe<TechnologyResearchedEvent>(OnTechnologyResearched);
            _eventBus.Subscribe<ActiveDomainChangedEvent>(OnActiveDomainChanged);
            _eventBus.Subscribe<ProposalReceivedEvent>(OnProposalReceived);
            _eventBus.Subscribe<DiplomaticStatusChangedEvent>(OnDiplomaticStatusChanged);
            _eventBus.Subscribe<SystemSabotagedEvent>(OnSystemSabotaged);
            _eventBus.Subscribe<RevoltIncitedEvent>(OnRevoltIncited);
            _eventBus.Subscribe<TechnologyStolenEvent>(OnTechnologyStolen);
            _eventBus.Subscribe<MissionFailedEvent>(OnMissionFailed);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _eventBus.Unsubscribe<BattleResolvedEvent>(OnBattleResolved);
            _eventBus.Unsubscribe<EncounterStartedEvent>(OnEncounterStarted);
            _eventBus.Unsubscribe<SystemColonizedEvent>(OnSystemColonized);
            _eventBus.Unsubscribe<FleetDepartedEvent>(OnFleetDeparted);
            _eventBus.Unsubscribe<RecruitmentCompletedEvent>(OnRecruitmentCompleted);
            _eventBus.Unsubscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            _eventBus.Unsubscribe<TechnologyResearchedEvent>(OnTechnologyResearched);
            _eventBus.Unsubscribe<ActiveDomainChangedEvent>(OnActiveDomainChanged);
            _eventBus.Unsubscribe<ProposalReceivedEvent>(OnProposalReceived);
            _eventBus.Unsubscribe<DiplomaticStatusChangedEvent>(OnDiplomaticStatusChanged);
            _eventBus.Unsubscribe<SystemSabotagedEvent>(OnSystemSabotaged);
            _eventBus.Unsubscribe<RevoltIncitedEvent>(OnRevoltIncited);
            _eventBus.Unsubscribe<TechnologyStolenEvent>(OnTechnologyStolen);
            _eventBus.Unsubscribe<MissionFailedEvent>(OnMissionFailed);

            Log.Clear();
        }

        // --- Militaire --------------------------------------------------------------------

        private void OnBattleResolved(BattleResolvedEvent e)
        {
            bool playerAttacked = e.AttackerEmpireId == EconomyService.PlayerOwnerId;
            bool playerDefended = e.DefenderEmpireId == EconomyService.PlayerOwnerId;

            if (!playerAttacked && !playerDefended)
            {
                return;
            }

            bool playerWon = playerAttacked == e.AttackerWon;
            string place = SystemName(e.SystemId);

            Record(
                playerWon ? NoticeKind.BattleWon : NoticeKind.BattleLost,
                playerWon ? $"Bataille gagnee a {place}." : $"Bataille perdue a {place}.",
                e.SystemId);
        }

        /// <summary>
        /// Une rencontre attend une decision du joueur : c'est l'un des trois seuls avis
        /// critiques du jeu.
        /// </summary>
        private void OnEncounterStarted(EncounterStartedEvent e)
        {
            if (e.InitiatorEmpireId != EconomyService.PlayerOwnerId && e.OtherEmpireId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            Record(NoticeKind.EncounterStarted, $"Rencontre en route vers {SystemName(e.LegTo)}.", e.LegTo);
        }

        private void OnSystemColonized(SystemColonizedEvent e)
        {
            if (e.EmpireId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            Record(NoticeKind.SystemColonized, $"{SystemName(e.SystemId)} est colonisee.", e.SystemId);
        }

        private void OnFleetDeparted(FleetDepartedEvent e)
        {
            if (e.OwnerId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            string text = e.IsRetreating
                ? $"Repli vers {SystemName(e.ToSystemId)}."
                : $"Flotte en route vers {SystemName(e.ToSystemId)}.";

            Record(NoticeKind.FleetDeparted, text, e.ToSystemId);
        }

        private void OnRecruitmentCompleted(RecruitmentCompletedEvent e)
        {
            if (e.OwnerId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            Record(NoticeKind.RecruitmentCompleted, $"{e.Count} unite(s) prete(s) a {SystemName(e.SystemId)}.", e.SystemId);
        }

        // --- Economie et recherche ---------------------------------------------------------

        private void OnBuildingCompleted(BuildingCompletedEvent e)
        {
            if (!OwnedByPlayer(e.SystemId))
            {
                return;
            }

            string what = e.BuildingType != null ? e.BuildingType.DisplayName : "Batiment";
            Record(NoticeKind.BuildingCompleted, $"{what} acheve a {SystemName(e.SystemId)}.", e.SystemId);
        }

        private void OnTechnologyResearched(TechnologyResearchedEvent e)
        {
            if (e.EmpireId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            string what = e.Technology != null ? e.Technology.DisplayName : "Palier";
            Record(NoticeKind.TechnologyResearched, $"{what} : recherche achevee.");
        }

        private void OnActiveDomainChanged(ActiveDomainChangedEvent e)
        {
            if (e.EmpireId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            Record(NoticeKind.ResearchDomainChanged, $"Recherche reorientee vers {e.Domain}.");
        }

        // --- Diplomatie --------------------------------------------------------------------

        private void OnProposalReceived(ProposalReceivedEvent e)
        {
            if (e.Proposal == null || e.Proposal.TargetId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            Record(NoticeKind.ProposalReceived, $"{EmpireName(e.Proposal.ProposerId)} propose : {e.Proposal.Type}.");
        }

        /// <summary>
        /// Seul evenement rapporte meme lorsqu'il ne concerne pas le joueur : une declaration de
        /// guerre ou un traite entre deux tiers est public par nature. Il descend alors au niveau
        /// information.
        /// </summary>
        private void OnDiplomaticStatusChanged(DiplomaticStatusChangedEvent e)
        {
            bool concernsPlayer = e.EmpireAId == EconomyService.PlayerOwnerId || e.EmpireBId == EconomyService.PlayerOwnerId;

            string text = concernsPlayer
                ? $"{EmpireName(Other(e))} : {e.NewStatus}."
                : $"{EmpireName(e.EmpireAId)} et {EmpireName(e.EmpireBId)} : {e.NewStatus}.";

            Record(concernsPlayer ? NoticeKind.DiplomaticStatusChanged : NoticeKind.ForeignDiplomacy, text);
        }

        private static int Other(DiplomaticStatusChangedEvent e)
        {
            return e.EmpireAId == EconomyService.PlayerOwnerId ? e.EmpireBId : e.EmpireAId;
        }

        // --- Espionnage --------------------------------------------------------------------

        private void OnSystemSabotaged(SystemSabotagedEvent e)
        {
            if (e.TargetEmpireId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            Record(NoticeKind.SystemSabotaged, $"Sabotage a {SystemName(e.SystemId)}.", e.SystemId);
        }

        private void OnRevoltIncited(RevoltIncitedEvent e)
        {
            if (e.TargetEmpireId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            Record(NoticeKind.RevoltIncited, $"Troubles fomentes a {SystemName(e.SystemId)}.", e.SystemId);
        }

        private void OnTechnologyStolen(TechnologyStolenEvent e)
        {
            if (e.ProposerId != EconomyService.PlayerOwnerId)
            {
                // La victime d'un vol reussi ne s'en apercoit pas : c'est tout l'interet d'une
                // operation discrete. Ne rien inscrire est ici la bonne reponse.
                return;
            }

            Record(NoticeKind.TechnologyStolen, $"Technologie {e.Domain} derobee a {EmpireName(e.TargetEmpireId)}.");
        }

        private void OnMissionFailed(MissionFailedEvent e)
        {
            if (e.ProposerId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            Record(NoticeKind.EspionageFailed, $"Operation {e.Type} dejouee chez {EmpireName(e.TargetEmpireId)}.");
        }

        // --- Utilitaires -------------------------------------------------------------------

        private void Record(NoticeKind kind, string text, StarSystemId? subject = null)
        {
            Log.Add(new GameNotice(kind, text, CurrentDate(), subject));
        }

        private GameDate CurrentDate()
        {
            if (_clock == null)
            {
                ServiceLocator.TryGet(out _clock);
            }

            return _clock?.CurrentDate ?? GameDate.StartOfGame;
        }

        private bool OwnedByPlayer(StarSystemId id)
        {
            return _map != null
                && _map.TryGetSystem(id, out StarSystemState system)
                && system.OwnerId == EconomyService.PlayerOwnerId;
        }

        private string SystemName(StarSystemId id)
        {
            return _map != null && _map.TryGetSystem(id, out StarSystemState system) ? system.Name : "un systeme";
        }

        private string EmpireName(int empireId)
        {
            if (_empires == null)
            {
                ServiceLocator.TryGet(out _empires);
            }

            return _empires != null && _empires.TryGetEmpire(empireId, out Empire empire) ? empire.Name : "un empire";
        }
    }
}
