using System;
using System.Reflection;
using Espace.Core;
using Espace.Data;
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
        }

        private EventBus _eventBus;
        private FakeGameClock _clock;
        private EmpireRegistry _empireRegistry;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _eventBus.Initialize();
            _clock = new FakeGameClock();
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
            var military = new MilitaryService(map, _clock, _eventBus, economy, _empireRegistry, catalog);
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

            Assert.Throws<ArgumentNullException>(() => new MilitaryService(null, _clock, _eventBus, economy, _empireRegistry, catalog));
            Assert.Throws<ArgumentNullException>(() => new MilitaryService(map, null, _eventBus, economy, _empireRegistry, catalog));
            Assert.Throws<ArgumentNullException>(() => new MilitaryService(map, _clock, null, economy, _empireRegistry, catalog));
            Assert.Throws<ArgumentNullException>(() => new MilitaryService(map, _clock, _eventBus, null, _empireRegistry, catalog));
            Assert.Throws<ArgumentNullException>(() => new MilitaryService(map, _clock, _eventBus, economy, null, catalog));
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
        public void TryMoveFleet_NonAdjacentDestination_Fails()
        {
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
            RecruitAndComplete(military, home, infantry, 1);
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
            RecruitAndComplete(military, home, infantry, 1);
            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet fleet);
            military.TryMoveFleet(fleet, neighbor.Id, out _);

            bool colonizedPublished = false;
            _eventBus.Subscribe<SystemColonizedEvent>(e =>
            {
                colonizedPublished = true;
                Assert.AreEqual(neighbor.Id, e.SystemId);
                Assert.AreEqual(PlayerId, e.EmpireId);
            });

            _eventBus.Publish(new DayAdvancedEvent(fleet.ArrivalDate.Value));

            Assert.IsTrue(colonizedPublished);
            Assert.AreEqual(PlayerId, neighbor.OwnerId);
            Assert.IsFalse(military.TryGetStationedFleet(neighbor.Id, PlayerId, out _), "La flotte de colons se dissout, elle ne devient pas une garnison.");
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
            RecruitAndComplete(military, neighbor, infantry, 20); // defenseur tres fort

            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet attackers);
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
            RecruitAndComplete(military, neighbor, infantry, 500); // ecrasant : l'attaquant est annihile

            military.TryGetStationedFleet(home.Id, PlayerId, out Fleet attackers);
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
            military.TryMoveFleet(attackers, neighbor.Id, out _);
            _eventBus.Publish(new DayAdvancedEvent(attackers.ArrivalDate.Value));

            Assert.AreEqual(OtherEmpireId, neighbor.OwnerId, "A puissance brute egale, le bonus de terrain doit favoriser le defenseur.");
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

        /// <summary>Recrute puis fait avancer le temps jusqu'a l'achevement, pour obtenir directement une garnison dans les tests.</summary>
        private void RecruitAndComplete(MilitaryService military, StarSystemState system, UnitTypeDefinition unitType, int count)
        {
            military.TryRecruitUnits(system.Id, unitType, count, out string error);
            Assert.IsNull(error, $"Le recrutement prealable au test a echoue : {error}");

            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(unitType.RecruitmentDays)));
        }
    }
}
