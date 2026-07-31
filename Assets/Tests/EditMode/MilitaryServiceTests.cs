using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// Verifie <see cref="MilitaryService"/> : recrutement, deplacement, colonisation, combat
    /// (victoire/defaite/retraite), fusion de garnisons, entretien.
    /// </summary>
    [TestFixture]
    public sealed class MilitaryServiceTests
    {
        private const float FloatTolerance = 0.001f;
        private const int PlayerId = EconomyService.PlayerOwnerId;
        private const int OtherEmpireId = 1;

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

        /// <summary>
        /// Remplace <see cref="DiplomacyService"/> pour ces tests : seul le statut
        /// guerre/paix importe ici (voir le blocage introduit en Phase 7 dans
        /// <see cref="MilitaryService.TryMoveFleet"/>), directement pilotable via
        /// <see cref="SetStatus"/> sans reconstruire tout le contexte diplomatique
        /// (registre d'empires, service economique, carte).
        /// </summary>
        private sealed class FakeDiplomacyService : IDiplomacyService
        {
            private readonly Dictionary<(int, int), DiplomaticStatus> _statuses = new Dictionary<(int, int), DiplomaticStatus>();

            private static (int, int) Key(int a, int b) => a <= b ? (a, b) : (b, a);

            public void SetStatus(int empireAId, int empireBId, DiplomaticStatus status) => _statuses[Key(empireAId, empireBId)] = status;

            public DiplomaticStatus GetStatus(int empireAId, int empireBId) =>
                _statuses.TryGetValue(Key(empireAId, empireBId), out DiplomaticStatus status) ? status : DiplomaticStatus.Peace;

            public float GetOpinion(int observerId, int targetId) => 0f;
            public bool HasTradeTreaty(int empireAId, int empireBId) => false;
            public bool IsEmbargoing(int fromEmpireId, int toEmpireId) => false;
            public IReadOnlyList<DiplomaticProposal> GetPendingProposalsFor(int empireId) => Array.Empty<DiplomaticProposal>();

            public bool TryDeclareWar(int declarerId, int targetId, out string error)
            {
                SetStatus(declarerId, targetId, DiplomaticStatus.War);
                error = null;
                return true;
            }

            public bool TrySetEmbargo(int fromEmpireId, int toEmpireId, bool active, out string error)
            {
                error = "Non supporte par ce faux service.";
                return false;
            }

            public bool TryBreakPact(int fromEmpireId, int toEmpireId, out string error)
            {
                SetStatus(fromEmpireId, toEmpireId, DiplomaticStatus.Peace);
                error = null;
                return true;
            }

            public bool TrySubmitProposal(
                int proposerId, int targetId, ProposalType type, ResourceBundle offeredResources, ResourceBundle requestedResources,
                StarSystemId? offeredSystemId, StarSystemId? requestedSystemId, out string error)
            {
                error = "Non supporte par ce faux service.";
                return false;
            }

            public bool TryRespondToProposal(int proposalId, bool accept, out string error)
            {
                error = "Non supporte par ce faux service.";
                return false;
            }

            public void ApplyOpinionShift(int observerId, int targetId, float delta) { }
            public void RestoreRelations(int empireAId, int empireBId, DiplomaticStatus status, bool hasTradeTreaty) { }
            public void RestoreOpinion(int observerId, int targetId, float value) { }
            public void RestoreEmbargo(int fromEmpireId, int toEmpireId) { }
        }

        private EventBus _eventBus;
        private FakeGameClock _clock;
        private EmpireRegistry _empireRegistry;
        private FakeDiplomacyService _diplomacy;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _eventBus.Initialize();
            _clock = new FakeGameClock();
            _diplomacy = new FakeDiplomacyService();
            _empireRegistry = new EmpireRegistry(new[]
            {
                new Empire(PlayerId, "Joueur", Color.blue, EmpirePersonality.Expansionist, isPlayerControlled: true),
                new Empire(OtherEmpireId, "Voisin", Color.red, EmpirePersonality.Militarist, isPlayerControlled: false),
            });
        }

        [TearDown]
        public void TearDown()
        {
            _eventBus.Shutdown();
        }

        private static StarSystemState MakeSystem(int id, Vector2 position, int ownerId = StarSystemState.UnownedOwnerId, int developmentLevel = 3, float stability = 1f)
        {
            var system = new StarSystemState(new StarSystemId(id), $"System{id}", position, 1000, 500, developmentLevel, stability, Array.Empty<ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        private static GalaxyMap MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, int neighborOwnerId = StarSystemState.UnownedOwnerId)
        {
            home = MakeSystem(0, Vector2.zero, PlayerId);
            neighbor = MakeSystem(1, new Vector2(5f, 0f), neighborOwnerId);
            var links = new[] { new HyperlaneLink(home.Id, neighbor.Id) };
            return new GalaxyMap(new[] { home, neighbor }, links);
        }

        private static UnitTypeDefinition MakeUnitType(
            UnitType type, float power = 10f, float speed = 5f, float creditsCost = 50f, float mineralsCost = 20f,
            int recruitmentDays = 3, float upkeepPerDay = 0.5f, int minDevelopment = 0)
        {
            var unitType = ScriptableObject.CreateInstance<UnitTypeDefinition>();
            SetPrivateField(unitType, "displayName", type.ToString());
            SetPrivateField(unitType, "unitType", type);
            SetPrivateField(unitType, "power", power);
            SetPrivateField(unitType, "speed", speed);
            SetPrivateField(unitType, "creditsCost", creditsCost);
            SetPrivateField(unitType, "mineralsCost", mineralsCost);
            SetPrivateField(unitType, "recruitmentDays", recruitmentDays);
            SetPrivateField(unitType, "upkeepPerDay", upkeepPerDay);
            SetPrivateField(unitType, "minimumDevelopmentLevel", minDevelopment);
            return unitType;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Champ introuvable : {fieldName}");
            field.SetValue(target, value);
        }

        private EconomyService MakeEconomy(GalaxyMap map)
        {
            var economy = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            economy.Initialize();
            return economy;
        }

        private MilitaryService MakeMilitary(GalaxyMap map, IEconomyService economy, params UnitTypeDefinition[] catalog)
        {
            var military = new MilitaryService(map, _clock, _eventBus, economy, _diplomacy, _empireRegistry, catalog);
            military.Initialize();
            return military;
        }

        /// <summary>Donne au tresor de <paramref name="empireId"/> au moins <paramref name="minimumAmount"/> de Credits.</summary>
        private void GiveCredits(EconomyService economy, StarSystemState system, int empireId, float minimumAmount)
        {
            int originalWealth = system.Wealth;
            float originalTax = economy.GetTaxRate(empireId);

            system.Wealth = Mathf.CeilToInt(minimumAmount / 0.05f) + 1;
            economy.SetTaxRate(empireId, 1f);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(1)));

            system.Wealth = originalWealth;
            economy.SetTaxRate(empireId, originalTax);
        }

        // --- Construction / validation --------------------------------------------

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            GalaxyMap map = MakeAdjacentPair(out _, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition[] catalog = Array.Empty<UnitTypeDefinition>();

            Assert.Throws<ArgumentNullException>(() => new MilitaryService(null, _clock, _eventBus, economy, _diplomacy, _empireRegistry, catalog));
            Assert.Throws<ArgumentNullException>(() => new MilitaryService(map, null, _eventBus, economy, _diplomacy, _empireRegistry, catalog));
            Assert.Throws<ArgumentNullException>(() => new MilitaryService(map, _clock, null, economy, _diplomacy, _empireRegistry, catalog));
            Assert.Throws<ArgumentNullException>(() => new MilitaryService(map, _clock, _eventBus, null, _diplomacy, _empireRegistry, catalog));
            Assert.Throws<ArgumentNullException>(() => new MilitaryService(map, _clock, _eventBus, economy, null, _empireRegistry, catalog));
            Assert.Throws<ArgumentNullException>(() => new MilitaryService(map, _clock, _eventBus, economy, _diplomacy, null, catalog));
        }

        // --- Recrutement -----------------------------------------------------------

        [Test]
        public void TryRecruitUnits_UnownedSystem_Fails()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            home.OwnerId = StarSystemState.UnownedOwnerId;
            EconomyService economy = MakeEconomy(map);
            MilitaryService military = MakeMilitary(map, economy, MakeUnitType(UnitType.Infantry));

            bool success = military.TryRecruitUnits(home.Id, military.UnitCatalog[0], 1, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryRecruitUnits_BelowMinimumDevelopment_Fails()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            home.DevelopmentLevel = 0;
            EconomyService economy = MakeEconomy(map);
            GiveCredits(economy, home, PlayerId, 1000f);
            MilitaryService military = MakeMilitary(map, economy, MakeUnitType(UnitType.SpecialForces, minDevelopment: 2));

            bool success = military.TryRecruitUnits(home.Id, military.UnitCatalog[0], 1, out string error);

            Assert.IsFalse(success);
            StringAssert.Contains("developpement", error);
        }

        [Test]
        public void TryRecruitUnits_InsufficientResources_Fails()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            MilitaryService military = MakeMilitary(map, economy, MakeUnitType(UnitType.Infantry, creditsCost: 99999f));

            bool success = military.TryRecruitUnits(home.Id, military.UnitCatalog[0], 1, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryRecruitUnits_ZeroOrNegativeCount_Fails()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            MilitaryService military = MakeMilitary(map, economy, MakeUnitType(UnitType.Infantry));

            Assert.IsFalse(military.TryRecruitUnits(home.Id, military.UnitCatalog[0], 0, out _));
            Assert.IsFalse(military.TryRecruitUnits(home.Id, military.UnitCatalog[0], -1, out _));
        }

        [Test]
        public void TryRecruitUnits_NullUnitType_Fails()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            MilitaryService military = MakeMilitary(map, economy);

            Assert.IsFalse(military.TryRecruitUnits(home.Id, null, 1, out string error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryRecruitUnits_Success_DeductsCostAndQueues()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            GiveCredits(economy, home, PlayerId, 1000f);
            float before = economy.GetTreasury(PlayerId).Credits;
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, creditsCost: 50f, mineralsCost: 20f);
            MilitaryService military = MakeMilitary(map, economy, infantry);

            bool success = military.TryRecruitUnits(home.Id, infantry, 2, out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);
            Assert.AreEqual(before - 100f, economy.GetTreasury(PlayerId).Credits, FloatTolerance);
            // Pas encore dans la garnison : le recrutement met du temps.
            Assert.AreEqual(UnitBundle.Zero, military.GetGarrison(home.Id, PlayerId));
        }

        [Test]
        public void Recruitment_CompletesOnScheduleAndJoinsGarrison()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            GiveCredits(economy, home, PlayerId, 1000f);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, recruitmentDays: 3);
            MilitaryService military = MakeMilitary(map, economy, infantry);

            military.TryRecruitUnits(home.Id, infantry, 4, out _);

            bool completedEventPublished = false;
            _eventBus.Subscribe<RecruitmentCompletedEvent>(e =>
            {
                completedEventPublished = true;
                Assert.AreEqual(4, e.Count);
            });

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(2)));
            Assert.AreEqual(UnitBundle.Zero, military.GetGarrison(home.Id, PlayerId), "Pas encore termine.");

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(3)));

            Assert.IsTrue(completedEventPublished);
            Assert.AreEqual(new UnitBundle(infantry: 4), military.GetGarrison(home.Id, PlayerId));
        }

        // --- Deplacement -------------------------------------------------------------

        [Test]
        public void TryMoveFleet_NoRouteAvailable_Fails()
        {
            // Depuis la Phase 17 l'adjacence n'est plus requise : ce qui reste refuse, c'est une
            // destination qu'aucune route hyperspatiale ne relie a l'origine.
            StarSystemState home = MakeSystem(0, Vector2.zero, PlayerId);
            StarSystemState farAway = MakeSystem(1, new Vector2(100f, 0f));
            var map = new GalaxyMap(new[] { home, farAway }, Array.Empty<HyperlaneLink>()); // aucun lien
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 1);
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet fleet);

            bool success = military.TryMoveFleet(fleet, farAway.Id, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        // --- Deplacement longue distance (Phase 17) ---------------------------------------

        /// <summary>Chaine de quatre systemes alignes, relies de proche en proche : 0 - 1 - 2 - 3.</summary>
        private static GalaxyMap MakeChain(out StarSystemState[] systems, int ownerOfFirst = PlayerId)
        {
            systems = new[]
            {
                MakeSystem(0, Vector2.zero, ownerOfFirst),
                MakeSystem(1, new Vector2(10f, 0f)),
                MakeSystem(2, new Vector2(20f, 0f)),
                MakeSystem(3, new Vector2(30f, 0f))
            };

            var links = new[]
            {
                new HyperlaneLink(systems[0].Id, systems[1].Id),
                new HyperlaneLink(systems[1].Id, systems[2].Id),
                new HyperlaneLink(systems[2].Id, systems[3].Id)
            };

            return new GalaxyMap(systems, links);
        }

        [Test]
        public void TryMoveFleet_MultiHopDestination_Succeeds()
        {
            GalaxyMap map = MakeChain(out StarSystemState[] systems);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 10f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            military.RestoreGarrison(systems[0].Id, PlayerId, new UnitBundle(infantry: 6));
            military.TryGetStationedFleet(systems[0].Id, PlayerId, out Fleet fleet);

            bool success = military.TryMoveFleet(fleet, systems[3].Id, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(FleetStatus.Moving, fleet.Status);
            Assert.AreEqual(4, fleet.Route.Count, "L'itineraire doit passer par les deux systemes intermediaires.");
            Assert.AreEqual(systems[3].Id, fleet.DestinationSystemId.Value);
        }

        [Test]
        public void Journey_IntermediateUnownedWaypoint_IsNotColonized()
        {
            // Le piege central de la phase : ResolveArrival colonise tout systeme libre ou une
            // flotte arrive. Un point de passage doit etre traverse, jamais occupe.
            GalaxyMap map = MakeChain(out StarSystemState[] systems);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 10f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            military.RestoreGarrison(systems[0].Id, PlayerId, new UnitBundle(infantry: 6));
            military.TryGetStationedFleet(systems[0].Id, PlayerId, out Fleet fleet);
            military.TryMoveFleet(fleet, systems[3].Id, out _);

            // On avance jour par jour jusqu'a l'arrivee finale.
            for (int day = 1; day <= 10; day++)
            {
                _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(day)));
            }

            Assert.AreEqual(StarSystemState.UnownedOwnerId, systems[1].OwnerId, "Le premier point de passage ne doit pas avoir ete colonise.");
            Assert.AreEqual(StarSystemState.UnownedOwnerId, systems[2].OwnerId, "Le second point de passage non plus.");
            Assert.AreEqual(PlayerId, systems[3].OwnerId, "Seule la destination finale est colonisee.");
        }

        [Test]
        public void TryMoveFleet_RouteThroughForeignTerritory_Fails()
        {
            GalaxyMap map = MakeChain(out StarSystemState[] systems);
            systems[1].OwnerId = OtherEmpireId; // le seul passage est occupe par un tiers
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 10f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            military.RestoreGarrison(systems[0].Id, PlayerId, new UnitBundle(infantry: 6));
            military.TryGetStationedFleet(systems[0].Id, PlayerId, out Fleet fleet);

            bool success = military.TryMoveFleet(fleet, systems[3].Id, out string error);

            Assert.IsFalse(success, "On ne traverse pas le territoire d'un tiers.");
            Assert.IsNotNull(error);
        }

        [Test]
        public void Journey_DestinationTakenDuringFlight_RetreatsInsteadOfAttacking()
        {
            // Trou cree par les trajets longs : la destination peut changer de mains en cours de
            // route. Resoudre aveuglement contournerait le verrou de guerre de la Phase 7.
            GalaxyMap map = MakeChain(out StarSystemState[] systems);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 10f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            military.RestoreGarrison(systems[0].Id, PlayerId, new UnitBundle(infantry: 6));
            military.TryGetStationedFleet(systems[0].Id, PlayerId, out Fleet fleet);
            military.TryMoveFleet(fleet, systems[3].Id, out _);

            // Un tiers s'installe sur la destination pendant le trajet, sans guerre declaree.
            systems[3].OwnerId = OtherEmpireId;

            for (int day = 1; day <= 10; day++)
            {
                _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(day)));
            }

            Assert.AreEqual(OtherEmpireId, systems[3].OwnerId, "Le systeme ne doit pas avoir ete pris.");
            Assert.IsFalse(
                military.TryGetStationedFleet(systems[3].Id, PlayerId, out _),
                "La flotte doit faire demi-tour plutot que d'attaquer un empire avec qui on est en paix.");
        }

        [Test]
        public void TryMoveFleet_EmptyFleet_Fails()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor);
            EconomyService economy = MakeEconomy(map);
            MilitaryService military = MakeMilitary(map, economy, MakeUnitType(UnitType.Infantry));
            var emptyFleet = new Fleet(1, PlayerId, home.Id, UnitBundle.Zero);

            bool success = military.TryMoveFleet(emptyFleet, neighbor.Id, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryMoveFleet_Success_SetsMovingStatus()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 5f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            // 3 Infanterie : l'exigence de colonisation du systeme voisin (Pop 1000, Dev 3) depuis la Phase 16.
            RecruitAndComplete(military, home, infantry, 3);
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet fleet);

            bool departedPublished = false;
            _eventBus.Subscribe<FleetDepartedEvent>(_ => departedPublished = true);

            bool success = military.TryMoveFleet(fleet, neighbor.Id, out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);
            Assert.AreEqual(FleetStatus.Moving, fleet.Status);
            Assert.IsTrue(departedPublished);
        }

        // --- Colonisation --------------------------------------------------------------

        [Test]
        public void Arrival_AtUnownedSystem_Colonizes()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f); // arrivee en 1 jour
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            // Voisin Pop 1000 / Dev 3 / Stab 1 -> 3 Infanterie requises, 1 perdue a l'installation (Phase 16).
            RecruitAndComplete(military, home, infantry, 3);
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet fleet);
            military.TryMoveFleet(fleet, neighbor.Id, out _);

            bool colonizedPublished = false;
            _eventBus.Subscribe<SystemColonizedEvent>(e =>
            {
                colonizedPublished = true;
                Assert.AreEqual(neighbor.Id, e.SystemId);
                Assert.AreEqual(PlayerId, e.EmpireId);
                Assert.AreEqual(1, e.InfantryLost);
            });

            _eventBus.Publish(new DayAdvancedEvent(fleet.ArrivalDate.Value));

            Assert.IsTrue(colonizedPublished);
            Assert.AreEqual(PlayerId, neighbor.OwnerId);
            Assert.AreEqual(
                new UnitBundle(infantry: 2), military.GetGarrison(neighbor.Id, PlayerId),
                "Le reste de la flotte (3 engagees - 1 perdue) devient la garnison de la nouvelle colonie.");
        }

        [Test]
        public void TryMoveFleet_NotEnoughInfantryToColonize_Fails()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 2); // 2 < 3 requises
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet fleet);

            bool success = military.TryMoveFleet(fleet, neighbor.Id, out string error);

            Assert.IsFalse(success, "Le depart doit etre refuse en amont plutot que d'echouer a l'arrivee.");
            StringAssert.Contains("Infanterie", error);
            Assert.AreEqual(FleetStatus.Stationed, fleet.Status);
        }

        [Test]
        public void Arrival_AtUnownedSystem_ExactlyRequiredInfantry_FleetDissolves()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor);
            neighbor.Population = 0;
            neighbor.DevelopmentLevel = 0;
            neighbor.Stability = 0.2f; // exigence 1, pertes 1 : toute la flotte y passe
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 1);
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet fleet);
            military.TryMoveFleet(fleet, neighbor.Id, out _);

            _eventBus.Publish(new DayAdvancedEvent(fleet.ArrivalDate.Value));

            Assert.AreEqual(PlayerId, neighbor.OwnerId);
            Assert.IsFalse(
                military.TryGetStationedFleet(neighbor.Id, PlayerId, out _),
                "Une flotte qui n'embarquait que le strict necessaire se dissout entierement dans la colonie.");
        }

        // --- Renfort ---------------------------------------------------------------

        [Test]
        public void Arrival_AtOwnSystem_MergesIntoExistingGarrison()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor);
            neighbor.OwnerId = PlayerId;
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);

            RecruitAndComplete(military, neighbor, infantry, 3); // garnison existante sur la destination
            RecruitAndComplete(military, home, infantry, 2);

            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet reinforcements);
            military.TryMoveFleet(reinforcements, neighbor.Id, out _);
            _eventBus.Publish(new DayAdvancedEvent(reinforcements.ArrivalDate.Value));

            Assert.AreEqual(new UnitBundle(infantry: 5), military.GetGarrison(neighbor.Id, PlayerId));
            Assert.AreEqual(1, military.GetFleetsAt(neighbor.Id).Count, "Une seule flotte stationnee par (systeme, proprietaire).");
        }

        // --- Combat ------------------------------------------------------------------

        [Test]
        public void Arrival_AtUndefendedEnemySystem_AttackerWinsWithoutLosses()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, OtherEmpireId);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 3);
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet attackers);
            _diplomacy.SetStatus(PlayerId, OtherEmpireId, DiplomaticStatus.War);

            bool battlePublished = false;
            _eventBus.Subscribe<BattleResolvedEvent>(e =>
            {
                battlePublished = true;
                Assert.IsTrue(e.AttackerWon);
                Assert.AreEqual(PlayerId, e.AttackerEmpireId);
                Assert.AreEqual(OtherEmpireId, e.DefenderEmpireId);
            });

            military.TryMoveFleet(attackers, neighbor.Id, out _);
            _eventBus.Publish(new DayAdvancedEvent(attackers.ArrivalDate.Value));

            Assert.IsTrue(battlePublished);
            Assert.AreEqual(PlayerId, neighbor.OwnerId);
            Assert.AreEqual(new UnitBundle(infantry: 3), military.GetGarrison(neighbor.Id, PlayerId));
        }

        // --- Invasion : Infanterie indispensable pour occuper (Phase 16) --------------------

        [Test]
        public void ResolveBattle_VictoryWithoutInfantry_DoesNotCaptureSystem()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, OtherEmpireId);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, power: 10f, speed: 100f);
            UnitTypeDefinition fighter = MakeUnitType(UnitType.Fighter, power: 20f, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry, fighter);
            _diplomacy.SetStatus(PlayerId, OtherEmpireId, DiplomaticStatus.War);

            // Chasseurs seuls, ecrasants : ils gagnent la bataille mais n'ont personne pour occuper.
            military.RestoreGarrison(home.Id, PlayerId, new UnitBundle(fighter: 10));
            military.RestoreGarrison(neighbor.Id, OtherEmpireId, new UnitBundle(infantry: 1));
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet attackers);

            military.TryMoveFleet(attackers, neighbor.Id, out string moveError);
            Assert.IsNull(moveError, moveError);
            _eventBus.Publish(new DayAdvancedEvent(attackers.ArrivalDate.Value));

            Assert.AreEqual(OtherEmpireId, neighbor.OwnerId, "Sans Infanterie survivante, le systeme ne change pas de main.");
            Assert.AreEqual(UnitBundle.Zero, military.GetGarrison(neighbor.Id, OtherEmpireId), "La garnison defenderesse est bien detruite.");
            Assert.AreEqual(FleetStatus.Moving, attackers.Status, "Les vainqueurs sans Infanterie repartent.");
            Assert.IsTrue(attackers.IsRetreating);
        }

        [Test]
        public void ResolveBattle_VictoryWithInfantry_CapturesSystem()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, OtherEmpireId);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, power: 10f, speed: 100f);
            UnitTypeDefinition fighter = MakeUnitType(UnitType.Fighter, power: 20f, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry, fighter);
            _diplomacy.SetStatus(PlayerId, OtherEmpireId, DiplomaticStatus.War);

            // Meme rapport de force ecrasant, mais avec de l'Infanterie a bord : le systeme tombe.
            military.RestoreGarrison(home.Id, PlayerId, new UnitBundle(infantry: 4, fighter: 8));
            military.RestoreGarrison(neighbor.Id, OtherEmpireId, new UnitBundle(infantry: 1));
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet attackers);

            military.TryMoveFleet(attackers, neighbor.Id, out _);
            _eventBus.Publish(new DayAdvancedEvent(attackers.ArrivalDate.Value));

            Assert.AreEqual(PlayerId, neighbor.OwnerId, "Avec de l'Infanterie survivante, la conquete aboutit (non-regression).");
            Assert.Greater(military.GetGarrison(neighbor.Id, PlayerId).Infantry, 0);
        }

        [Test]
        public void Arrival_AtStrongerDefendedSystem_AttackerLosesAndSurvivorsRetreat()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, OtherEmpireId);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            GiveCredits(economy, neighbor, OtherEmpireId, 1000f);

            RecruitAndComplete(military, home, infantry, 2); // attaquant faible
            // Plafond de 10 unites par flotte (Phase 14) : la garnison ecrasante du defenseur
            // est restauree directement plutot que recrutee, comme le ferait un chargement de sauvegarde.
            military.RestoreGarrison(neighbor.Id, OtherEmpireId, new UnitBundle(infantry: 20));

            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet attackers);
            _diplomacy.SetStatus(PlayerId, OtherEmpireId, DiplomaticStatus.War);
            military.TryMoveFleet(attackers, neighbor.Id, out _);
            GameDate arrival = attackers.ArrivalDate.Value;
            _eventBus.Publish(new DayAdvancedEvent(arrival));

            Assert.AreEqual(OtherEmpireId, neighbor.OwnerId, "Le systeme reste au defenseur.");
            Assert.AreEqual(FleetStatus.Moving, attackers.Status, "Les survivants repartent (retraite).");
            Assert.IsTrue(attackers.IsRetreating);
            Assert.AreEqual(home.Id, attackers.DestinationSystemId);

            _eventBus.Publish(new DayAdvancedEvent(attackers.ArrivalDate.Value));
            Assert.AreEqual(FleetStatus.Stationed, attackers.Status);
            Assert.AreEqual(home.Id, attackers.CurrentSystemId);
        }

        [Test]
        public void Arrival_TotalDefeat_AttackerFleetDestroyed()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, OtherEmpireId);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            GiveCredits(economy, neighbor, OtherEmpireId, 1000f);

            RecruitAndComplete(military, home, infantry, 1);
            // Plafond de 10 unites par flotte (Phase 14) : garnison ecrasante restauree directement.
            military.RestoreGarrison(neighbor.Id, OtherEmpireId, new UnitBundle(infantry: 500));

            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet attackers);
            _diplomacy.SetStatus(PlayerId, OtherEmpireId, DiplomaticStatus.War);
            military.TryMoveFleet(attackers, neighbor.Id, out _);
            _eventBus.Publish(new DayAdvancedEvent(attackers.ArrivalDate.Value));

            Assert.IsFalse(military.TryGetStationedFleet(home.Id, PlayerId, out _));
            Assert.AreEqual(OtherEmpireId, neighbor.OwnerId);
        }

        [Test]
        public void Battle_DefenderTerrainBonus_CanFavorDefenderAtEqualRawPower()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, OtherEmpireId);
            neighbor.DevelopmentLevel = 5; // bonus de terrain maximal
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            GiveCredits(economy, neighbor, OtherEmpireId, 1000f);

            RecruitAndComplete(military, home, infantry, 10);
            RecruitAndComplete(military, neighbor, infantry, 10); // meme puissance brute

            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet attackers);
            _diplomacy.SetStatus(PlayerId, OtherEmpireId, DiplomaticStatus.War);
            military.TryMoveFleet(attackers, neighbor.Id, out _);
            _eventBus.Publish(new DayAdvancedEvent(attackers.ArrivalDate.Value));

            Assert.AreEqual(OtherEmpireId, neighbor.OwnerId, "A puissance brute egale, le bonus de terrain doit favoriser le defenseur.");
        }

        [Test]
        public void TryMoveFleet_ForeignSystemWithoutWar_Fails()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, OtherEmpireId);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 3);
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet fleet);

            bool success = military.TryMoveFleet(fleet, neighbor.Id, out string error);

            Assert.IsFalse(success, "Sans guerre declaree, un deplacement vers un systeme etranger doit etre refuse (Phase 7).");
            Assert.IsNotNull(error);
            Assert.AreEqual(FleetStatus.Stationed, fleet.Status);
        }

        [Test]
        public void TryMoveFleet_AlliedSystem_Fails()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, OtherEmpireId);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 3);
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet fleet);
            _diplomacy.SetStatus(PlayerId, OtherEmpireId, DiplomaticStatus.Alliance);

            bool success = military.TryMoveFleet(fleet, neighbor.Id, out string error);

            Assert.IsFalse(success, "Une Alliance (ou un Pacte de non-agression) interdit tout autant l'entree sur le territoire.");
            Assert.IsNotNull(error);
        }

        // --- Detachement -------------------------------------------------------------

        [Test]
        public void TryDetachFleet_InsufficientUnits_Fails()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 2);

            bool success = military.TryDetachFleet(home.Id, PlayerId, new UnitBundle(infantry: 5), out Fleet detached, out string error);

            Assert.IsFalse(success);
            Assert.IsNull(detached);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryDetachFleet_Success_ReducesGarrisonAndCreatesNewFleet()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 5);

            bool success = military.TryDetachFleet(home.Id, PlayerId, new UnitBundle(infantry: 2), out Fleet detached, out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);
            Assert.AreEqual(new UnitBundle(infantry: 2), detached.Composition);
            Assert.AreEqual(new UnitBundle(infantry: 3), military.GetGarrison(home.Id, PlayerId));
        }

        [Test]
        public void TryDetachFleet_DrainsGarrisonEntirely_RemovesOriginalFleet()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 2);

            military.TryDetachFleet(home.Id, PlayerId, new UnitBundle(infantry: 2), out _, out _);

            Assert.IsFalse(military.TryGetStationedFleet(home.Id, PlayerId, out _));
        }

        // --- Entretien -----------------------------------------------------------------

        [Test]
        public void Upkeep_DeductsFromOwnerTreasuryDaily()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, upkeepPerDay: 2f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            home.Wealth = 0; // isole l'entretien de toute production ce jour-la
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 3);

            float before = economy.GetTreasury(PlayerId).Credits;
            home.Wealth = 0;
            economy.SetTaxRate(PlayerId, 0f);

            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(1)));

            Assert.AreEqual(before - 6f, economy.GetTreasury(PlayerId).Credits, FloatTolerance, "3 unites x 2 Cr/jour = 6 Cr.");
        }

        [Test]
        public void Upkeep_InsufficientTreasury_FailsSilentlyWithoutException()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, upkeepPerDay: 999f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 1);
            home.Wealth = 0;
            economy.SetTaxRate(PlayerId, 0f);

            Assert.DoesNotThrow(() => _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(1))));
            Assert.AreEqual(new UnitBundle(infantry: 1), military.GetGarrison(home.Id, PlayerId), "Aucune desertion en Phase 6.");
        }

        // --- Divers --------------------------------------------------------------------

        [Test]
        public void EstimatePower_MatchesCombatResolverComputePower()
        {
            GalaxyMap map = MakeAdjacentPair(out _, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, power: 10f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            var composition = new UnitBundle(infantry: 4);

            Assert.AreEqual(CombatResolver.ComputePower(composition, new[] { infantry }), military.EstimatePower(composition), FloatTolerance);
        }

        [Test]
        public void GetGarrison_NoFleet_ReturnsZero()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            MilitaryService military = MakeMilitary(map, economy);

            Assert.AreEqual(UnitBundle.Zero, military.GetGarrison(home.Id, PlayerId));
        }

        // --- Plafond de 10 unites par flotte (Phase 14) ---------------------------------

        [Test]
        public void TryRecruitUnits_WouldExceedFleetCap_Fails()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 9); // garnison a 9

            bool success = military.TryRecruitUnits(home.Id, infantry, 2, out string error);

            Assert.IsFalse(success, "9 + 2 depasse le plafond de 10.");
            StringAssert.Contains("Plafond", error);
        }

        [Test]
        public void TryRecruitUnits_ExactlyAtFleetCap_Succeeds()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 9);

            bool success = military.TryRecruitUnits(home.Id, infantry, 1, out string error);

            Assert.IsTrue(success, error);
        }

        [Test]
        public void TryRecruitUnits_PendingOrdersCountTowardFleetCap()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);

            // Deux ordres de 5 (encore en attente, pas encore dans la garnison) : le troisieme doit etre refuse.
            Assert.IsTrue(military.TryRecruitUnits(home.Id, infantry, 5, out _));
            Assert.IsTrue(military.TryRecruitUnits(home.Id, infantry, 5, out _));

            bool success = military.TryRecruitUnits(home.Id, infantry, 1, out string error);

            Assert.IsFalse(success, "Les commandes en attente comptent deja pour 10 unites.");
            Assert.IsNotNull(error);
        }

        // --- Plafond de flottes en deplacement simultane, lie a la Logistique (Phase 14) ---

        [Test]
        public void TryMoveFleet_BeyondSimultaneousCap_Fails()
        {
            // Le plafond de base est passe de 1 a 2 en Phase 17 : avec des trajets de plusieurs
            // semaines, un plafond de 1 privait un empire sans recherche de tout mouvement.
            StarSystemState home = MakeSystem(0, Vector2.zero, PlayerId);
            StarSystemState neighborA = MakeSystem(1, new Vector2(5f, 0f));
            StarSystemState neighborB = MakeSystem(2, new Vector2(-5f, 0f));
            StarSystemState neighborC = MakeSystem(3, new Vector2(0f, 5f));
            var links = new[]
            {
                new HyperlaneLink(home.Id, neighborA.Id),
                new HyperlaneLink(home.Id, neighborB.Id),
                new HyperlaneLink(home.Id, neighborC.Id)
            };
            var map = new GalaxyMap(new[] { home, neighborA, neighborB, neighborC }, links);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 5f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            // 9 Infanterie : trois groupes de 3, l'exigence de colonisation de chaque voisin libre.
            military.RestoreGarrison(home.Id, PlayerId, new UnitBundle(infantry: 9));
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet garrison);
            military.TryDetachFleet(home.Id, PlayerId, new UnitBundle(infantry: 3), out Fleet second, out _);
            military.TryDetachFleet(home.Id, PlayerId, new UnitBundle(infantry: 3), out Fleet third, out _);

            bool firstMoveSucceeds = military.TryMoveFleet(garrison, neighborA.Id, out _);
            bool secondMoveSucceeds = military.TryMoveFleet(second, neighborB.Id, out _);
            bool thirdMoveSucceeds = military.TryMoveFleet(third, neighborC.Id, out string error);

            Assert.IsTrue(firstMoveSucceeds);
            Assert.IsTrue(secondMoveSucceeds, "Deux flottes simultanees sont permises sans recherche depuis la Phase 17.");
            Assert.IsFalse(thirdMoveSucceeds, "La troisieme depasse le plafond : il faut rechercher la Logistique.");
            Assert.IsNotNull(error);
        }

        // --- GetFleetsForEmpire (Phase 14) -----------------------------------------------

        [Test]
        public void GetFleetsForEmpire_ReturnsAllFleetsAcrossSystems()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor);
            neighbor.OwnerId = PlayerId;
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 1);
            RecruitAndComplete(military, neighbor, infantry, 1);

            IReadOnlyList<Fleet> fleets = military.GetFleetsForEmpire(PlayerId);

            Assert.AreEqual(2, fleets.Count);
            Assert.IsFalse(military.GetFleetsForEmpire(OtherEmpireId).Count > 0);
        }

        [Test]
        public void Fleet_GetsAutomaticName()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 1);

            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet fleet);

            Assert.IsFalse(string.IsNullOrEmpty(fleet.Name));
        }

        // --- Amiraux (Phase 15) ------------------------------------------------------------

        [Test]
        public void TryMoveFleet_AdmiralSpeedBonus_ArrivesEarlier()
        {
            StarSystemState home1 = MakeSystem(0, Vector2.zero, PlayerId);
            StarSystemState neighbor1 = MakeSystem(1, new Vector2(100f, 0f));
            StarSystemState home2 = MakeSystem(2, new Vector2(0f, 200f), OtherEmpireId);
            StarSystemState neighbor2 = MakeSystem(3, new Vector2(100f, 200f));
            var links = new[]
            {
                new HyperlaneLink(home1.Id, neighbor1.Id),
                new HyperlaneLink(home2.Id, neighbor2.Id)
            };
            var map = new GalaxyMap(new[] { home1, neighbor1, home2, neighbor2 }, links);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 5f);
            MilitaryService military = MakeMilitary(map, economy, infantry);

            // 3 Infanterie chacune : l'exigence de colonisation des systemes vises (Phase 16).
            military.RestoreGarrison(home1.Id, PlayerId, new UnitBundle(infantry: 3), admiral: new Admiral("Neutre", 0f, 0f, 0f));
            military.RestoreGarrison(home2.Id, OtherEmpireId, new UnitBundle(infantry: 3), admiral: new Admiral("Rapide", 0f, 0.5f, 0f));

            military.TryGetStationedFleet(home1.Id, PlayerId, out Fleet baselineFleet);
            military.TryGetStationedFleet(home2.Id, OtherEmpireId, out Fleet boostedFleet);

            bool baselineMoved = military.TryMoveFleet(baselineFleet, neighbor1.Id, out string baselineError);
            bool boostedMoved = military.TryMoveFleet(boostedFleet, neighbor2.Id, out string boostedError);

            Assert.IsTrue(baselineMoved, baselineError);
            Assert.IsTrue(boostedMoved, boostedError);
            Assert.Less(boostedFleet.ArrivalDate.Value, baselineFleet.ArrivalDate.Value, "+50% de vitesse doit reduire la duree du trajet.");
        }

        [Test]
        public void ResolveBattle_AttackerAdmiralBonus_IncreasesAttackerPower()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, OtherEmpireId);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, power: 10f, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            _diplomacy.SetStatus(PlayerId, OtherEmpireId, DiplomaticStatus.War);

            military.RestoreGarrison(home.Id, PlayerId, new UnitBundle(infantry: 5), admiral: new Admiral("Fort", 0.2f, 0f, 0f));
            military.RestoreGarrison(neighbor.Id, OtherEmpireId, new UnitBundle(infantry: 5), admiral: new Admiral("Neutre", 0f, 0f, 0f));
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet attackers);

            float? attackerPower = null;
            _eventBus.Subscribe<BattleResolvedEvent>(e => attackerPower = e.AttackerPower);

            military.TryMoveFleet(attackers, neighbor.Id, out _);
            _eventBus.Publish(new DayAdvancedEvent(attackers.ArrivalDate.Value));

            Assert.IsNotNull(attackerPower);
            // Base 5*10=50 ; moral=1 ; Expansionniste (joueur) CommandModifier=1.0 ; +20% Amiral -> 50 * 1.2 = 60.
            Assert.AreEqual(60f, attackerPower.Value, FloatTolerance);
        }

        [Test]
        public void ResolveBattle_DefenderAdmiralBonus_IncreasesDefenderPower()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, OtherEmpireId);
            neighbor.DevelopmentLevel = 0; // neutralise le bonus de terrain pour isoler l'effet de l'Amiral
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, power: 10f, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            _diplomacy.SetStatus(PlayerId, OtherEmpireId, DiplomaticStatus.War);

            military.RestoreGarrison(home.Id, PlayerId, new UnitBundle(infantry: 5), admiral: new Admiral("Neutre", 0f, 0f, 0f));
            military.RestoreGarrison(neighbor.Id, OtherEmpireId, new UnitBundle(infantry: 5), admiral: new Admiral("Fort", 0f, 0f, 0.2f));
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet attackers);

            float? defenderPower = null;
            _eventBus.Subscribe<BattleResolvedEvent>(e => defenderPower = e.DefenderPower);

            military.TryMoveFleet(attackers, neighbor.Id, out _);
            _eventBus.Publish(new DayAdvancedEvent(attackers.ArrivalDate.Value));

            Assert.IsNotNull(defenderPower);
            // Base 5*10=50 ; moral=1 ; terrain neutralise ; Militariste (voisin) CommandModifier=1.15 ; +20% Amiral -> 50 * 1.15 * 1.2 = 69.
            Assert.AreEqual(69f, defenderPower.Value, FloatTolerance);
        }

        [Test]
        public void ResolveBattle_UndefendedSystem_DoesNotThrowWithNullDefenderFleet()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out StarSystemState neighbor, OtherEmpireId);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            military.RestoreGarrison(home.Id, PlayerId, new UnitBundle(infantry: 3));
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet attackers);
            _diplomacy.SetStatus(PlayerId, OtherEmpireId, DiplomaticStatus.War);

            Assert.DoesNotThrow(() =>
            {
                military.TryMoveFleet(attackers, neighbor.Id, out _);
                _eventBus.Publish(new DayAdvancedEvent(attackers.ArrivalDate.Value));
            }, "ComputeDefenderModifier doit accepter un defenderFleet nul (systeme sans garnison).");

            Assert.AreEqual(PlayerId, neighbor.OwnerId);
        }

        [Test]
        public void RestoreGarrison_WithAdmiral_PreservesAdmiralOnGarrison()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            MilitaryService military = MakeMilitary(map, economy);
            var admiral = new Admiral("Amiral Test", 0.05f, -0.03f, 0.02f);

            military.RestoreGarrison(home.Id, PlayerId, new UnitBundle(infantry: 1), admiral: admiral);

            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet fleet);
            Assert.AreEqual(admiral, fleet.Admiral);
        }

        [Test]
        public void RestoreGarrison_WithoutAdmiral_GeneratesDeterministicAdmiral()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            MilitaryService military = MakeMilitary(map, economy);

            military.RestoreGarrison(home.Id, PlayerId, new UnitBundle(infantry: 1));

            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet fleet);
            Assert.AreEqual(Admiral.Compute(fleet.Id, fleet.OwnerId), fleet.Admiral);
        }

        [Test]
        public void TryDetachFleet_DetachedFleet_HasOwnGeneratedAdmiral()
        {
            GalaxyMap map = MakeAdjacentPair(out StarSystemState home, out _);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, PlayerId, 1000f);
            RecruitAndComplete(military, home, infantry, 5);
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet garrison);

            military.TryDetachFleet(home.Id, PlayerId, new UnitBundle(infantry: 2), out Fleet detached, out _);

            Assert.AreEqual(Admiral.Compute(detached.Id, detached.OwnerId), detached.Admiral);
            Assert.AreNotEqual(garrison.Admiral, detached.Admiral, "Deux flottes distinctes (ids differents) devraient avoir des Amiraux differents.");
        }

        // --- Rencontres spatiales (Phase 17) -----------------------------------------------

        /// <summary>
        /// Deux bases opposees reliees par un long couloir central A - B. Le tronçon A-B est
        /// volontairement tres long (50 unites contre 10 pour les acces) : les deux flottes y
        /// restent plusieurs jours, ce qui garantit un chevauchement et rend la rencontre
        /// deterministe quel que soit le bonus de vitesse tire par chaque Amiral.
        /// </summary>
        private static GalaxyMap MakeSharedCorridor(
            out StarSystemState westBase, out StarSystemState corridorA, out StarSystemState corridorB, out StarSystemState eastBase)
        {
            westBase = MakeSystem(0, Vector2.zero, PlayerId);
            corridorA = MakeSystem(1, new Vector2(10f, 0f));
            corridorB = MakeSystem(2, new Vector2(60f, 0f));
            eastBase = MakeSystem(3, new Vector2(70f, 0f), OtherEmpireId);

            var links = new[]
            {
                new HyperlaneLink(westBase.Id, corridorA.Id),
                new HyperlaneLink(corridorA.Id, corridorB.Id),
                new HyperlaneLink(corridorB.Id, eastBase.Id)
            };

            return new GalaxyMap(new[] { westBase, corridorA, corridorB, eastBase }, links);
        }

        [Test]
        public void Encounter_TwoFleetsOnSameLeg_FreezesBothAndQueuesEncounter()
        {
            GalaxyMap map = MakeSharedCorridor(out StarSystemState west, out StarSystemState a, out StarSystemState b, out StarSystemState east);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 10f);
            MilitaryService military = MakeMilitary(map, economy, infantry);

            military.RestoreGarrison(west.Id, PlayerId, new UnitBundle(infantry: 6));
            military.RestoreGarrison(east.Id, OtherEmpireId, new UnitBundle(infantry: 6));
            military.TryGetStationedFleet(west.Id, PlayerId, out Fleet playerFleet);
            military.TryGetStationedFleet(east.Id, OtherEmpireId, out Fleet enemyFleet);

            // Les deux visent le meme couloir, en sens inverse : elles se croisent sur A-B.
            Assert.IsTrue(military.TryMoveFleet(playerFleet, b.Id, out string e1), e1);
            Assert.IsTrue(military.TryMoveFleet(enemyFleet, a.Id, out string e2), e2);

            for (int day = 1; day <= 10 && military.GetPendingEncounterFor(PlayerId) == null; day++)
            {
                _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(day)));
            }

            PendingEncounter encounter = military.GetPendingEncounterFor(PlayerId);
            Assert.IsNotNull(encounter, "Deux flottes empruntant le meme tronçon doivent se rencontrer.");
            Assert.AreEqual(FleetStatus.AwaitingEncounter, playerFleet.Status);
            Assert.AreEqual(FleetStatus.AwaitingEncounter, enemyFleet.Status);
        }

        [Test]
        public void Encounter_SameOwner_DoesNotTrigger()
        {
            GalaxyMap map = MakeSharedCorridor(out StarSystemState west, out StarSystemState a, out StarSystemState b, out StarSystemState east);
            east.OwnerId = PlayerId; // les deux flottes appartiennent au joueur
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 10f);
            MilitaryService military = MakeMilitary(map, economy, infantry);

            military.RestoreGarrison(west.Id, PlayerId, new UnitBundle(infantry: 6));
            military.RestoreGarrison(east.Id, PlayerId, new UnitBundle(infantry: 6));
            military.TryGetStationedFleet(west.Id, PlayerId, out Fleet first);
            military.TryGetStationedFleet(east.Id, PlayerId, out Fleet second);

            military.TryMoveFleet(first, b.Id, out _);
            military.TryMoveFleet(second, a.Id, out _);

            for (int day = 1; day <= 10; day++)
            {
                _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(day)));
            }

            Assert.IsNull(military.GetPendingEncounterFor(PlayerId), "Deux flottes du meme empire se croisent sans histoire.");
        }

        [Test]
        public void Encounter_FrozenFleetStillCountsAsDeployed()
        {
            // Le plafond de flottes simultanees compte les flottes « non stationnees », pas
            // seulement celles en mouvement : sinon laisser une rencontre en attente serait un
            // moyen de deployer une flotte supplementaire gratuitement.
            GalaxyMap map = MakeSharedCorridor(out StarSystemState west, out StarSystemState a, out StarSystemState b, out StarSystemState east);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 10f);
            MilitaryService military = MakeMilitary(map, economy, infantry);

            military.RestoreGarrison(west.Id, PlayerId, new UnitBundle(infantry: 6));
            military.RestoreGarrison(east.Id, OtherEmpireId, new UnitBundle(infantry: 6));
            military.TryGetStationedFleet(west.Id, PlayerId, out Fleet playerFleet);
            military.TryGetStationedFleet(east.Id, OtherEmpireId, out Fleet enemyFleet);

            military.TryMoveFleet(playerFleet, b.Id, out _);
            military.TryMoveFleet(enemyFleet, a.Id, out _);

            for (int day = 1; day <= 10 && military.GetPendingEncounterFor(PlayerId) == null; day++)
            {
                _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(day)));
            }

            Assert.IsNotNull(military.GetPendingEncounterFor(PlayerId), "Precondition : une rencontre est en attente.");
            Assert.AreEqual(FleetStatus.AwaitingEncounter, playerFleet.Status);

            IReadOnlyList<Fleet> deployed = military.GetFleetsInTransit();
            CollectionAssert.Contains(deployed, playerFleet, "Une flotte gelee reste deployee et occupe toujours un emplacement.");
        }

        [Test]
        public void Encounter_PlayerChoosesPassBy_BothFleetsResumeTheirJourney()
        {
            GalaxyMap map = MakeSharedCorridor(out StarSystemState west, out StarSystemState a, out StarSystemState b, out StarSystemState east);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 10f);
            MilitaryService military = MakeMilitary(map, economy, infantry);

            military.RestoreGarrison(west.Id, PlayerId, new UnitBundle(infantry: 6));
            military.RestoreGarrison(east.Id, OtherEmpireId, new UnitBundle(infantry: 6));
            military.TryGetStationedFleet(west.Id, PlayerId, out Fleet playerFleet);
            military.TryGetStationedFleet(east.Id, OtherEmpireId, out Fleet enemyFleet);

            military.TryMoveFleet(playerFleet, b.Id, out _);
            military.TryMoveFleet(enemyFleet, a.Id, out _);

            for (int day = 1; day <= 10 && military.GetPendingEncounterFor(PlayerId) == null; day++)
            {
                _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(day)));
            }

            PendingEncounter encounter = military.GetPendingEncounterFor(PlayerId);
            Assert.IsNotNull(encounter, "Precondition : une rencontre est en attente.");

            bool resolved = military.TryResolveEncounter(encounter.Id, PlayerId, EncounterOption.PassBy, out string error);

            Assert.IsTrue(resolved, error);
            Assert.AreEqual(FleetStatus.Moving, playerFleet.Status, "Les deux flottes reprennent leur route.");
            Assert.AreEqual(FleetStatus.Moving, enemyFleet.Status);
            Assert.IsNull(military.GetPendingEncounterFor(PlayerId), "La rencontre est consommee.");
        }

        [Test]
        public void Encounter_UnavailableOptionForStatus_IsRefused()
        {
            GalaxyMap map = MakeSharedCorridor(out StarSystemState west, out StarSystemState a, out StarSystemState b, out StarSystemState east);
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 10f);
            MilitaryService military = MakeMilitary(map, economy, infantry);

            military.RestoreGarrison(west.Id, PlayerId, new UnitBundle(infantry: 6));
            military.RestoreGarrison(east.Id, OtherEmpireId, new UnitBundle(infantry: 6));
            military.TryGetStationedFleet(west.Id, PlayerId, out Fleet playerFleet);
            military.TryGetStationedFleet(east.Id, OtherEmpireId, out Fleet enemyFleet);

            military.TryMoveFleet(playerFleet, b.Id, out _);
            military.TryMoveFleet(enemyFleet, a.Id, out _);

            for (int day = 1; day <= 10 && military.GetPendingEncounterFor(PlayerId) == null; day++)
            {
                _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(day)));
            }

            PendingEncounter encounter = military.GetPendingEncounterFor(PlayerId);
            Assert.IsNotNull(encounter, "Precondition : une rencontre est en attente.");

            // En paix, « Combattre » n'est pas propose.
            bool resolved = military.TryResolveEncounter(encounter.Id, PlayerId, EncounterOption.Fight, out string error);

            Assert.IsFalse(resolved);
            Assert.IsNotNull(error);
        }

        /// <summary>Recrute puis fait avancer le temps jusqu'a l'achevement, pour obtenir directement une garnison dans les tests.</summary>
        private void RecruitAndComplete(MilitaryService military, StarSystemState system, UnitTypeDefinition unitType, int count)
        {
            military.TryRecruitUnits(system.Id, unitType, count, out string error);
            Assert.IsNull(error, $"Le recrutement prealable au test a echoue : {error}");

            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(unitType.RecruitmentDays)));
        }
    }
}
