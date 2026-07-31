using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="DiplomacyService"/> : statut guerre/paix/alliance/pacte, opinion,
    /// traites commerciaux, embargos, resolution instantanee (IA) et en attente (joueur) des
    /// propositions, derive mensuelle de l'opinion.
    /// </summary>
    [TestFixture]
    public sealed class DiplomacyServiceTests
    {
        private const float FloatTolerance = 0.001f;
        private const int PlayerId = EconomyService.PlayerOwnerId;
        private const int PacifistId = 1; // MinOpinionToAcceptPact = -20 (accepte facilement)
        private const int MilitaristId = 2; // MinOpinionToAcceptPact = 40 (accepte difficilement)

        private sealed class FakeGameClock : IGameClock
        {
            public GameDate CurrentDate { get; set; } = GameDate.StartOfGame;
            public GameSpeed CurrentSpeed => GameSpeed.Normal;
            public float CurrentMultiplier => 1f;
            public bool IsPaused => false;
            public void Pause() { }
            public void Resume() { }
            public void TogglePause() { }
            public void SetSpeed(GameSpeed speed) { }
            public void SetDate(GameDate date) => CurrentDate = date;
            public void ResetToStart() { }
        }

        /// <summary>Puissance controlee : chaque empire a une garnison fixe sur son unique systeme, puissance = nombre d'unites.</summary>
        private sealed class FakePowerMilitaryService : IMilitaryService
        {
            private readonly Dictionary<int, UnitBundle> _garrisonsByEmpire;

            public FakePowerMilitaryService(Dictionary<int, UnitBundle> garrisonsByEmpire)
            {
                _garrisonsByEmpire = garrisonsByEmpire;
            }

            public IReadOnlyList<UnitTypeDefinition> UnitCatalog => Array.Empty<UnitTypeDefinition>();
            public bool TryGetStationedFleet(StarSystemId systemId, int empireId, out Fleet fleet) { fleet = null; return false; }
            public IReadOnlyList<Fleet> GetFleetsAt(StarSystemId systemId) => Array.Empty<Fleet>();
            public IReadOnlyList<Fleet> GetFleetsForEmpire(int empireId) => Array.Empty<Fleet>();
            public UnitBundle GetGarrison(StarSystemId systemId, int empireId) => _garrisonsByEmpire.TryGetValue(empireId, out UnitBundle bundle) ? bundle : UnitBundle.Zero;
            public float EstimatePower(UnitBundle composition) => composition.TotalCount;
            public bool TryRecruitUnits(StarSystemId systemId, UnitTypeDefinition unitType, int count, out string error) { error = "n/a"; return false; }
            public bool TryMoveFleet(Fleet fleet, StarSystemId destinationSystemId, out string error) { error = "n/a"; return false; }
            public bool TryDetachFleet(StarSystemId systemId, int empireId, UnitBundle unitsToDetach, out Fleet detachedFleet, out string error) { detachedFleet = null; error = "n/a"; return false; }
            public void RestoreGarrison(StarSystemId systemId, int empireId, UnitBundle composition, string fleetName = null, Admiral? admiral = null) { }
            public bool CanDeployAnotherFleet(int empireId) => true;
            public IReadOnlyList<Fleet> GetFleetsInTransit() => Array.Empty<Fleet>();
            public void ClearFleetsInTransit() { }
            public void RestoreFleetInTransit(
                int empireId, UnitBundle composition, string fleetName, Admiral? admiral,
                IReadOnlyList<StarSystemId> route, int routeIndex, StarSystemId originSystemId,
                GameDate journeyStartDate, GameDate departureDate, GameDate legArrivalDate, bool isRetreating) { }
        }

        private EventBus _eventBus;
        private FakeGameClock _clock;
        private EmpireRegistry _empireRegistry;
        private GalaxyMap _map;
        private StarSystemState _playerSystem;
        private StarSystemState _pacifistSystem;
        private StarSystemState _militaristSystem;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _eventBus.Initialize();
            _clock = new FakeGameClock();
            _empireRegistry = new EmpireRegistry(new[]
            {
                new Empire(PlayerId, "Joueur", Color.blue, EmpirePersonality.Expansionist, isPlayerControlled: true),
                new Empire(PacifistId, "Pacifiste", Color.green, EmpirePersonality.Pacifist, isPlayerControlled: false),
                new Empire(MilitaristId, "Militariste", Color.red, EmpirePersonality.Militarist, isPlayerControlled: false),
            });

            _playerSystem = MakeSystem(0, new Vector2(0f, 0f), PlayerId);
            _pacifistSystem = MakeSystem(1, new Vector2(1f, 0f), PacifistId);
            _militaristSystem = MakeSystem(2, new Vector2(2f, 0f), MilitaristId);
            _map = new GalaxyMap(new[] { _playerSystem, _pacifistSystem, _militaristSystem }, Array.Empty<HyperlaneLink>());
        }

        [TearDown]
        public void TearDown()
        {
            _eventBus.Shutdown();
            if (ServiceLocator.IsRegistered<IMilitaryService>())
            {
                ServiceLocator.Unregister<IMilitaryService>();
            }
        }

        private static StarSystemState MakeSystem(int id, Vector2 position, int ownerId)
        {
            var system = new StarSystemState(new StarSystemId(id), $"System{id}", position, 1000, 500, developmentLevel: 3, stability: 1f, resourceDeposits: Array.Empty<ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        private EconomyService MakeEconomy()
        {
            var economy = new EconomyService(_map, _clock, _eventBus, Array.Empty<BuildingType>());
            economy.Initialize();
            return economy;
        }

        private DiplomacyService MakeDiplomacy(EconomyService economy)
        {
            var diplomacy = new DiplomacyService(_empireRegistry, economy, _map, _eventBus);
            diplomacy.Initialize();
            return diplomacy;
        }

        // --- Construction ------------------------------------------------------------

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            EconomyService economy = MakeEconomy();

            Assert.Throws<ArgumentNullException>(() => new DiplomacyService(null, economy, _map, _eventBus));
            Assert.Throws<ArgumentNullException>(() => new DiplomacyService(_empireRegistry, null, _map, _eventBus));
            Assert.Throws<ArgumentNullException>(() => new DiplomacyService(_empireRegistry, economy, null, _eventBus));
            Assert.Throws<ArgumentNullException>(() => new DiplomacyService(_empireRegistry, economy, _map, null));
        }

        // --- Statut de base ------------------------------------------------------------

        [Test]
        public void GetStatus_NeverInteracted_ReturnsPeace()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());

            Assert.AreEqual(DiplomaticStatus.Peace, diplomacy.GetStatus(PlayerId, PacifistId));
        }

        [Test]
        public void TryDeclareWar_Success_SetsWarSymmetric()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());

            bool success = diplomacy.TryDeclareWar(PlayerId, PacifistId, out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);
            Assert.AreEqual(DiplomaticStatus.War, diplomacy.GetStatus(PlayerId, PacifistId));
            Assert.AreEqual(DiplomaticStatus.War, diplomacy.GetStatus(PacifistId, PlayerId), "Le statut est symetrique.");
        }

        [Test]
        public void TryDeclareWar_AlreadyAtWar_Fails()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TryDeclareWar(PlayerId, PacifistId, out _);

            bool success = diplomacy.TryDeclareWar(PlayerId, PacifistId, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryDeclareWar_WhileAllied_FailsUntilPactBroken()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TrySubmitProposal(PlayerId, PacifistId, ProposalType.NonAggressionPact, default, default, null, null, out _);
            Assert.AreEqual(DiplomaticStatus.NonAggressionPact, diplomacy.GetStatus(PlayerId, PacifistId), "Precondition : le Pacifiste doit avoir accepte.");

            bool blocked = diplomacy.TryDeclareWar(PlayerId, PacifistId, out string blockedError);
            Assert.IsFalse(blocked);
            Assert.IsNotNull(blockedError);

            diplomacy.TryBreakPact(PlayerId, PacifistId, out _);
            bool allowed = diplomacy.TryDeclareWar(PlayerId, PacifistId, out string allowedError);

            Assert.IsTrue(allowed);
            Assert.IsNull(allowedError);
        }

        [Test]
        public void TryDeclareWar_EndsActiveTradeTreaty()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TrySubmitProposal(PlayerId, PacifistId, ProposalType.TradeTreaty, default, default, null, null, out _);
            Assert.IsTrue(diplomacy.HasTradeTreaty(PlayerId, PacifistId), "Precondition.");

            diplomacy.TryDeclareWar(PlayerId, PacifistId, out _);

            Assert.IsFalse(diplomacy.HasTradeTreaty(PlayerId, PacifistId), "La guerre met fin au commerce.");
        }

        [Test]
        public void TrySetEmbargo_ToggleOnOff()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());

            Assert.IsTrue(diplomacy.TrySetEmbargo(PlayerId, PacifistId, true, out _));
            Assert.IsTrue(diplomacy.IsEmbargoing(PlayerId, PacifistId));
            Assert.IsFalse(diplomacy.IsEmbargoing(PacifistId, PlayerId), "Dirige : pas symetrique.");

            Assert.IsTrue(diplomacy.TrySetEmbargo(PlayerId, PacifistId, false, out _));
            Assert.IsFalse(diplomacy.IsEmbargoing(PlayerId, PacifistId));
        }

        [Test]
        public void TrySetEmbargo_RedundantCall_Fails()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TrySetEmbargo(PlayerId, PacifistId, true, out _);

            bool success = diplomacy.TrySetEmbargo(PlayerId, PacifistId, true, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryBreakPact_NoActivePact_Fails()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());

            bool success = diplomacy.TryBreakPact(PlayerId, PacifistId, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryBreakPact_Success_ReturnsToPeaceWithOpinionPenalty()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TrySubmitProposal(PlayerId, PacifistId, ProposalType.NonAggressionPact, default, default, null, null, out _);

            bool success = diplomacy.TryBreakPact(PlayerId, PacifistId, out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);
            Assert.AreEqual(DiplomaticStatus.Peace, diplomacy.GetStatus(PlayerId, PacifistId));
            Assert.Less(diplomacy.GetOpinion(PacifistId, PlayerId), 0f, "Rompre un pacte deteriore l'opinion de la victime.");
        }

        // --- Validation des propositions -----------------------------------------------

        [Test]
        public void TrySubmitProposal_NonAggressionPact_RequiresPeaceStatus()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TryDeclareWar(PlayerId, PacifistId, out _);

            bool success = diplomacy.TrySubmitProposal(PlayerId, PacifistId, ProposalType.NonAggressionPact, default, default, null, null, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TrySubmitProposal_PeaceTreaty_RequiresWarStatus()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());

            bool success = diplomacy.TrySubmitProposal(PlayerId, PacifistId, ProposalType.PeaceTreaty, default, default, null, null, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TrySubmitProposal_Alliance_BlockedDuringWar()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TryDeclareWar(PlayerId, PacifistId, out _);

            bool success = diplomacy.TrySubmitProposal(PlayerId, PacifistId, ProposalType.Alliance, default, default, null, null, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TrySubmitProposal_TradeTreaty_BlockedIfAlreadyActive()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TrySubmitProposal(PlayerId, PacifistId, ProposalType.TradeTreaty, default, default, null, null, out _);

            bool success = diplomacy.TrySubmitProposal(PlayerId, PacifistId, ProposalType.TradeTreaty, default, default, null, null, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        // --- File d'attente du joueur ---------------------------------------------------

        [Test]
        public void TrySubmitProposal_ToPlayer_QueuesAndPublishesEvent()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            bool eventPublished = false;
            _eventBus.Subscribe<ProposalReceivedEvent>(e =>
            {
                eventPublished = true;
                Assert.AreEqual(PacifistId, e.Proposal.ProposerId);
                Assert.AreEqual(PlayerId, e.Proposal.TargetId);
            });

            bool submitted = diplomacy.TrySubmitProposal(PacifistId, PlayerId, ProposalType.NonAggressionPact, default, default, null, null, out string error);

            Assert.IsTrue(submitted);
            Assert.IsNull(error);
            Assert.IsTrue(eventPublished);
            Assert.AreEqual(1, diplomacy.GetPendingProposalsFor(PlayerId).Count);
            Assert.AreEqual(DiplomaticStatus.Peace, diplomacy.GetStatus(PlayerId, PacifistId), "Pas encore resolue : statut inchange.");
        }

        [Test]
        public void TryRespondToProposal_Accept_AppliesEffectAndRemovesFromQueue()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TrySubmitProposal(PacifistId, PlayerId, ProposalType.NonAggressionPact, default, default, null, null, out _);
            int proposalId = diplomacy.GetPendingProposalsFor(PlayerId)[0].Id;

            bool success = diplomacy.TryRespondToProposal(proposalId, true, out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);
            Assert.AreEqual(DiplomaticStatus.NonAggressionPact, diplomacy.GetStatus(PlayerId, PacifistId));
            Assert.AreEqual(0, diplomacy.GetPendingProposalsFor(PlayerId).Count);
        }

        [Test]
        public void TryRespondToProposal_Reject_Ultimatum_TriggersAutomaticWar()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TrySubmitProposal(PacifistId, PlayerId, ProposalType.Ultimatum, new ResourceBundle(credits: 100f), default, null, null, out _);
            int proposalId = diplomacy.GetPendingProposalsFor(PlayerId)[0].Id;

            diplomacy.TryRespondToProposal(proposalId, false, out _);

            Assert.AreEqual(DiplomaticStatus.War, diplomacy.GetStatus(PlayerId, PacifistId), "Refuser un ultimatum declare automatiquement la guerre.");
        }

        [Test]
        public void TryRespondToProposal_UnknownId_Fails()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());

            bool success = diplomacy.TryRespondToProposal(9999, true, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        // --- Resolution instantanee entre IA ---------------------------------------------

        [Test]
        public void TrySubmitProposal_ToLenientAi_AutoAccepted()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());

            diplomacy.TrySubmitProposal(PlayerId, PacifistId, ProposalType.NonAggressionPact, default, default, null, null, out _);

            Assert.AreEqual(
                DiplomaticStatus.NonAggressionPact, diplomacy.GetStatus(PlayerId, PacifistId),
                "Le Pacifiste (MinOpinionToAcceptPact = -20) accepte des l'opinion neutre (0).");
        }

        [Test]
        public void TrySubmitProposal_ToDistrustfulAi_AutoRejected()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());

            diplomacy.TrySubmitProposal(PlayerId, MilitaristId, ProposalType.NonAggressionPact, default, default, null, null, out _);

            Assert.AreEqual(
                DiplomaticStatus.Peace, diplomacy.GetStatus(PlayerId, MilitaristId),
                "Le Militariste (MinOpinionToAcceptPact = 40) refuse a l'opinion neutre (0).");
        }

        // --- Effets appliques a l'acceptation ---------------------------------------------

        [Test]
        public void ApplyAcceptedProposal_ResourceExchange_TransfersBothWays()
        {
            EconomyService economy = MakeEconomy();
            DiplomacyService diplomacy = MakeDiplomacy(economy);
            economy.Grant(PlayerId, new ResourceBundle(credits: 500f));
            economy.Grant(PacifistId, new ResourceBundle(minerals: 200f));

            diplomacy.TrySubmitProposal(
                PacifistId, PlayerId, ProposalType.ResourceExchange,
                offeredResources: new ResourceBundle(minerals: 100f), requestedResources: new ResourceBundle(credits: 50f),
                offeredSystemId: null, requestedSystemId: null, out _);
            int proposalId = diplomacy.GetPendingProposalsFor(PlayerId)[0].Id;

            diplomacy.TryRespondToProposal(proposalId, true, out _);

            Assert.AreEqual(450f, economy.GetTreasury(PlayerId).Credits, FloatTolerance);
            Assert.AreEqual(100f, economy.GetTreasury(PlayerId).Minerals, FloatTolerance);
            Assert.AreEqual(50f, economy.GetTreasury(PacifistId).Credits, FloatTolerance);
            Assert.AreEqual(100f, economy.GetTreasury(PacifistId).Minerals, FloatTolerance);
        }

        [Test]
        public void ApplyAcceptedProposal_TerritoryExchange_SwapsOwnership()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());

            diplomacy.TrySubmitProposal(
                PacifistId, PlayerId, ProposalType.TerritoryExchange,
                default, default, offeredSystemId: _pacifistSystem.Id, requestedSystemId: _playerSystem.Id, out _);
            int proposalId = diplomacy.GetPendingProposalsFor(PlayerId)[0].Id;

            diplomacy.TryRespondToProposal(proposalId, true, out _);

            Assert.AreEqual(PlayerId, _pacifistSystem.OwnerId, "Le systeme offert par le Pacifiste devient celui du joueur.");
            Assert.AreEqual(PacifistId, _playerSystem.OwnerId, "Le systeme demande au joueur devient celui du Pacifiste.");
        }

        [Test]
        public void ApplyAcceptedProposal_Ultimatum_Accepted_TransfersTributeAndPenalizesOpinion()
        {
            EconomyService economy = MakeEconomy();
            DiplomacyService diplomacy = MakeDiplomacy(economy);
            economy.Grant(PlayerId, new ResourceBundle(credits: 500f));

            diplomacy.TrySubmitProposal(PacifistId, PlayerId, ProposalType.Ultimatum, new ResourceBundle(credits: 200f), default, null, null, out _);
            int proposalId = diplomacy.GetPendingProposalsFor(PlayerId)[0].Id;

            diplomacy.TryRespondToProposal(proposalId, true, out _);

            Assert.AreEqual(300f, economy.GetTreasury(PlayerId).Credits, FloatTolerance);
            Assert.AreEqual(200f, economy.GetTreasury(PacifistId).Credits, FloatTolerance);
            Assert.Less(diplomacy.GetOpinion(PlayerId, PacifistId), 0f, "Se soumettre a un ultimatum deteriore l'opinion envers l'agresseur.");
            Assert.AreEqual(DiplomaticStatus.Peace, diplomacy.GetStatus(PlayerId, PacifistId), "Accepte : pas de guerre.");
        }

        // --- Ultimatum et paix : evaluation par rapport de puissance ---------------------

        [Test]
        public void Ultimatum_WeakAiTarget_AutoAcceptsRatherThanRiskWar()
        {
            var garrisons = new Dictionary<int, UnitBundle>
            {
                [PlayerId] = new UnitBundle(infantry: 100),
                [PacifistId] = new UnitBundle(infantry: 1),
            };
            ServiceLocator.Register<IMilitaryService>(new FakePowerMilitaryService(garrisons));
            EconomyService economy = MakeEconomy();
            DiplomacyService diplomacy = MakeDiplomacy(economy);
            economy.Grant(PacifistId, new ResourceBundle(credits: 500f));

            diplomacy.TrySubmitProposal(PlayerId, PacifistId, ProposalType.Ultimatum, new ResourceBundle(credits: 50f), default, null, null, out _);

            Assert.AreEqual(DiplomaticStatus.Peace, diplomacy.GetStatus(PlayerId, PacifistId), "Le Pacifiste ecrase paie plutot que de risquer la guerre.");
            Assert.AreEqual(450f, economy.GetTreasury(PacifistId).Credits, FloatTolerance);
        }

        [Test]
        public void Ultimatum_StrongAiTarget_RejectsAndTriggersWar()
        {
            var garrisons = new Dictionary<int, UnitBundle>
            {
                [PlayerId] = new UnitBundle(infantry: 1),
                [MilitaristId] = new UnitBundle(infantry: 100),
            };
            ServiceLocator.Register<IMilitaryService>(new FakePowerMilitaryService(garrisons));
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());

            diplomacy.TrySubmitProposal(PlayerId, MilitaristId, ProposalType.Ultimatum, new ResourceBundle(credits: 50f), default, null, null, out _);

            Assert.AreEqual(DiplomaticStatus.War, diplomacy.GetStatus(PlayerId, MilitaristId), "Le Militariste dominant refuse et declare la guerre en retour.");
        }

        // --- Derive de l'opinion et revenu commercial ------------------------------------

        [Test]
        public void OpinionDrift_OnMonthAdvanced_MovesTowardStatusTarget()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TryDeclareWar(PlayerId, PacifistId, out _);
            float afterWarDeclared = diplomacy.GetOpinion(PacifistId, PlayerId);

            _eventBus.Publish(new MonthAdvancedEvent(_clock.CurrentDate));

            Assert.Less(diplomacy.GetOpinion(PacifistId, PlayerId), afterWarDeclared, "L'opinion doit continuer a se degrader vers -100 tant que la guerre dure.");
        }

        [Test]
        public void TradeTreatyIncome_AccruedMonthlyToBothPartners()
        {
            EconomyService economy = MakeEconomy();
            DiplomacyService diplomacy = MakeDiplomacy(economy);
            diplomacy.TrySubmitProposal(PlayerId, PacifistId, ProposalType.TradeTreaty, default, default, null, null, out _);
            float playerBefore = economy.GetTreasury(PlayerId).Credits;
            float pacifistBefore = economy.GetTreasury(PacifistId).Credits;

            _eventBus.Publish(new MonthAdvancedEvent(_clock.CurrentDate));

            Assert.Greater(economy.GetTreasury(PlayerId).Credits, playerBefore);
            Assert.Greater(economy.GetTreasury(PacifistId).Credits, pacifistBefore);
        }

        [Test]
        public void GetPendingProposalsFor_FiltersByTarget()
        {
            DiplomacyService diplomacy = MakeDiplomacy(MakeEconomy());
            diplomacy.TrySubmitProposal(PacifistId, PlayerId, ProposalType.NonAggressionPact, default, default, null, null, out _);

            Assert.AreEqual(1, diplomacy.GetPendingProposalsFor(PlayerId).Count);
            Assert.AreEqual(0, diplomacy.GetPendingProposalsFor(PacifistId).Count);
        }
    }
}
