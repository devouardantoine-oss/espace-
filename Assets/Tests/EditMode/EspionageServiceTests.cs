using System;
using System.Collections.Generic;
using System.Reflection;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Espionage;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.Research;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="EspionageService"/> : puissance/contre-espionnage, succes/echec
    /// deterministe selon le rapport de puissance, cout paye que la mission reussisse ou non,
    /// penalite d'opinion en cas d'echec, et l'effet propre a chacune des cinq missions.
    /// </summary>
    [TestFixture]
    public sealed class EspionageServiceTests
    {
        private const float FloatTolerance = 0.001f;
        private const int EmpireA = 0;
        private const int EmpireB = 1;

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

        /// <summary>Espionne les appels a <see cref="ApplyOpinionShift"/> sans avoir a construire un vrai <see cref="DiplomacyService"/> (qui exigerait un EmpireRegistry).</summary>
        private sealed class SpyDiplomacyService : IDiplomacyService
        {
            public readonly List<(int ObserverId, int TargetId, float Delta)> OpinionShifts = new List<(int, int, float)>();

            public DiplomaticStatus GetStatus(int empireAId, int empireBId) => DiplomaticStatus.Peace;
            public float GetOpinion(int observerId, int targetId) => 0f;
            public bool HasTradeTreaty(int empireAId, int empireBId) => false;
            public bool IsEmbargoing(int fromEmpireId, int toEmpireId) => false;
            public IReadOnlyList<DiplomaticProposal> GetPendingProposalsFor(int empireId) => Array.Empty<DiplomaticProposal>();
            public bool TryDeclareWar(int declarerId, int targetId, out string error) { error = "n/a"; return false; }
            public bool TrySetEmbargo(int fromEmpireId, int toEmpireId, bool active, out string error) { error = "n/a"; return false; }
            public bool TryBreakPact(int fromEmpireId, int toEmpireId, out string error) { error = "n/a"; return false; }

            public bool TrySubmitProposal(
                int proposerId, int targetId, ProposalType type, ResourceBundle offeredResources, ResourceBundle requestedResources,
                StarSystemId? offeredSystemId, StarSystemId? requestedSystemId, out string error)
            {
                error = "n/a";
                return false;
            }

            public bool TryRespondToProposal(int proposalId, bool accept, out string error) { error = "n/a"; return false; }

            public void ApplyOpinionShift(int observerId, int targetId, float delta)
            {
                OpinionShifts.Add((observerId, targetId, delta));
            }

            public void RestoreRelations(int empireAId, int empireBId, DiplomaticStatus status, bool hasTradeTreaty) { }
            public void RestoreOpinion(int observerId, int targetId, float value) { }
            public void RestoreEmbargo(int fromEmpireId, int toEmpireId) { }
        }

        /// <summary>Garnison controlee par systeme, pour verifier que la decouverte retourne des donnees reelles.</summary>
        private sealed class FakeMilitaryService : IMilitaryService
        {
            private readonly Dictionary<StarSystemId, UnitBundle> _garrisonsBySystem = new Dictionary<StarSystemId, UnitBundle>();

            public void SetGarrison(StarSystemId systemId, UnitBundle bundle) => _garrisonsBySystem[systemId] = bundle;

            public IReadOnlyList<UnitTypeDefinition> UnitCatalog => Array.Empty<UnitTypeDefinition>();
            public bool TryGetStationedFleet(StarSystemId systemId, int empireId, out Fleet fleet) { fleet = null; return false; }
            public IReadOnlyList<Fleet> GetFleetsAt(StarSystemId systemId) => Array.Empty<Fleet>();
            public UnitBundle GetGarrison(StarSystemId systemId, int empireId) => _garrisonsBySystem.TryGetValue(systemId, out UnitBundle bundle) ? bundle : UnitBundle.Zero;
            public float EstimatePower(UnitBundle composition) => composition.TotalCount;
            public bool TryRecruitUnits(StarSystemId systemId, UnitTypeDefinition unitType, int count, out string error) { error = "n/a"; return false; }
            public bool TryMoveFleet(Fleet fleet, StarSystemId destinationSystemId, out string error) { error = "n/a"; return false; }
            public bool TryDetachFleet(StarSystemId systemId, int empireId, UnitBundle unitsToDetach, out Fleet detachedFleet, out string error) { detachedFleet = null; error = "n/a"; return false; }
            public void RestoreGarrison(StarSystemId systemId, int empireId, UnitBundle composition) { }
        }

        private EventBus _eventBus;
        private FakeGameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _eventBus.Initialize();
            _clock = new FakeGameClock();
        }

        [TearDown]
        public void TearDown()
        {
            _eventBus.Shutdown();
            UnregisterIfPresent<IEconomyService>();
            UnregisterIfPresent<IResearchService>();
            UnregisterIfPresent<IMilitaryService>();
            UnregisterIfPresent<IDiplomacyService>();
        }

        private static void UnregisterIfPresent<T>()
        {
            if (ServiceLocator.IsRegistered<T>())
            {
                ServiceLocator.Unregister<T>();
            }
        }

        private static StarSystemState MakeSystem(int id, int ownerId, int developmentLevel = 3, float stability = 1f)
        {
            var system = new StarSystemState(new StarSystemId(id), $"System{id}", Vector2.zero, 1000, 500, developmentLevel, stability, Array.Empty<ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        private EspionageService MakeEspionage(GalaxyMap map)
        {
            var espionage = new EspionageService(map, _eventBus);
            espionage.Initialize();
            return espionage;
        }

        private EconomyService RegisterEconomy(GalaxyMap map)
        {
            var economy = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            economy.Initialize();
            ServiceLocator.Register<IEconomyService>(economy);
            return economy;
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

        private static TechnologyDefinition MakeTechnology(ResearchDomain domain, int tier, float cost = 1f)
        {
            var technology = ScriptableObject.CreateInstance<TechnologyDefinition>();
            SetPrivateField(technology, "displayName", $"{domain}{tier}");
            SetPrivateField(technology, "domain", domain);
            SetPrivateField(technology, "tier", tier);
            SetPrivateField(technology, "researchPointCost", cost);
            SetPrivateField(technology, "effectMagnitude", 0.05f);
            return technology;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Champ introuvable : {fieldName}");
            field.SetValue(target, value);
        }

        private ResearchService RegisterResearch(GalaxyMap map, params TechnologyDefinition[] catalog)
        {
            var research = new ResearchService(map, _eventBus, catalog);
            research.Initialize();
            ServiceLocator.Register<IResearchService>(research);
            return research;
        }

        // --- Construction ------------------------------------------------------------

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            var map = new GalaxyMap(new[] { MakeSystem(0, EmpireA) }, Array.Empty<HyperlaneLink>());

            Assert.Throws<ArgumentNullException>(() => new EspionageService(null, _eventBus));
            Assert.Throws<ArgumentNullException>(() => new EspionageService(map, null));
        }

        // --- Puissance -----------------------------------------------------------------

        [Test]
        public void GetEspionagePower_NoResearchRegistered_ReturnsBaseValue()
        {
            var map = new GalaxyMap(new[] { MakeSystem(0, EmpireA) }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);

            Assert.AreEqual(10f, espionage.GetEspionagePower(EmpireA), FloatTolerance);
        }

        [Test]
        public void GetEspionagePower_WithResearchBonus_ScalesUp()
        {
            var map = new GalaxyMap(new[] { MakeSystem(0, EmpireA) }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            ResearchService research = RegisterResearch(map, MakeTechnology(ResearchDomain.Espionage, 1, cost: 1f));
            research.TrySetActiveDomain(EmpireA, ResearchDomain.Espionage, out _);
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1))); // complete le palier (0.05 de bonus)

            Assert.AreEqual(10.5f, espionage.GetEspionagePower(EmpireA), FloatTolerance);
        }

        [Test]
        public void GetCounterEspionagePower_ScalesWithSystemStability()
        {
            StarSystemState system = MakeSystem(0, EmpireB, stability: 0.5f);
            var map = new GalaxyMap(new[] { system }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);

            Assert.AreEqual(5f, espionage.GetCounterEspionagePower(EmpireB, system.Id), FloatTolerance);
        }

        // --- Vol de technologie ----------------------------------------------------------

        [Test]
        public void TryStealTechnology_ResearchUnavailable_Fails()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB);
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            RegisterEconomy(map);

            bool success = espionage.TryStealTechnology(EmpireA, EmpireB, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryStealTechnology_NoAdvantage_Fails()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB);
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            RegisterEconomy(map);
            RegisterResearch(map, MakeTechnology(ResearchDomain.Economy, 1));

            bool success = espionage.TryStealTechnology(EmpireA, EmpireB, out string error);

            Assert.IsFalse(success);
            StringAssert.Contains("avance", error);
        }

        [Test]
        public void TryStealTechnology_TargetHasAdvantage_GrantsMostValuableDomain()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB, stability: 0.5f); // contre-espionnage (5) < puissance du proposeur (10) : succes garanti
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            EconomyService economy = RegisterEconomy(map);
            GiveCredits(economy, home, EmpireA, 1000f);

            TechnologyDefinition economyTier1 = MakeTechnology(ResearchDomain.Economy, 1, cost: 1f);
            TechnologyDefinition weaponsTier1 = MakeTechnology(ResearchDomain.Weapons, 1, cost: 1f);
            TechnologyDefinition weaponsTier2 = MakeTechnology(ResearchDomain.Weapons, 2, cost: 1f);
            ResearchService research = RegisterResearch(map, economyTier1, weaponsTier1, weaponsTier2);

            // EmpireB prend de l'avance : 1 palier d'Economie, 2 paliers d'Armement (le plus grand ecart).
            research.TrySetActiveDomain(EmpireB, ResearchDomain.Economy, out _);
            enemy.Wealth = 1; // production quasi nulle pour ne pas fausser le calcul de points
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));
            research.TrySetActiveDomain(EmpireB, ResearchDomain.Weapons, out _);
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(2)));

            Assert.AreEqual(1, research.GetCompletedTierCount(EmpireB, ResearchDomain.Economy), "Precondition.");
            Assert.AreEqual(2, research.GetCompletedTierCount(EmpireB, ResearchDomain.Weapons), "Precondition : le plus grand ecart doit etre sur l'Armement.");

            bool success = espionage.TryStealTechnology(EmpireA, EmpireB, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(1, research.GetCompletedTierCount(EmpireA, ResearchDomain.Weapons), "Doit voler le domaine au plus grand ecart (Armement), pas l'Economie.");
        }

        [Test]
        public void TryStealTechnology_InsufficientFunds_Fails()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB);
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            RegisterEconomy(map); // aucun credit donne
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1);
            ResearchService research = RegisterResearch(map, tier1);
            research.TrySetActiveDomain(EmpireB, ResearchDomain.Economy, out _);
            enemy.Wealth = 100000;
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));
            Assert.AreEqual(1, research.GetCompletedTierCount(EmpireB, ResearchDomain.Economy), "Precondition.");

            bool success = espionage.TryStealTechnology(EmpireA, EmpireB, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        // --- Sabotage --------------------------------------------------------------------

        [Test]
        public void TrySabotage_UnknownSystem_Fails()
        {
            var map = new GalaxyMap(new[] { MakeSystem(0, EmpireA) }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);

            bool success = espionage.TrySabotage(EmpireA, new StarSystemId(999), out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TrySabotage_UnownedSystem_Fails()
        {
            StarSystemState system = MakeSystem(0, StarSystemState.UnownedOwnerId);
            var map = new GalaxyMap(new[] { system }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);

            bool success = espionage.TrySabotage(EmpireA, system.Id, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TrySabotage_SelfTarget_Fails()
        {
            StarSystemState system = MakeSystem(0, EmpireA);
            var map = new GalaxyMap(new[] { system }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            RegisterEconomy(map);

            bool success = espionage.TrySabotage(EmpireA, system.Id, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TrySabotage_Success_ReducesDevelopmentLevel()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB, developmentLevel: 3, stability: 0f); // contre-espionnage nul : succes garanti
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            EconomyService economy = RegisterEconomy(map);
            GiveCredits(economy, home, EmpireA, 1000f);

            bool success = espionage.TrySabotage(EmpireA, enemy.Id, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(2, enemy.DevelopmentLevel);
        }

        [Test]
        public void TrySabotage_Failure_AppliesOpinionPenaltyAndStillCosts()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB, stability: 1f); // contre-espionnage = puissance de base : egalite -> echec
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            EconomyService economy = RegisterEconomy(map);
            GiveCredits(economy, home, EmpireA, 1000f);
            var diplomacySpy = new SpyDiplomacyService();
            ServiceLocator.Register<IDiplomacyService>(diplomacySpy);
            float treasuryBefore = economy.GetTreasury(EmpireA).Credits;

            bool success = espionage.TrySabotage(EmpireA, enemy.Id, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
            Assert.AreEqual(3, enemy.DevelopmentLevel, "Echec : aucun effet.");
            Assert.Less(economy.GetTreasury(EmpireA).Credits, treasuryBefore, "Le cout est paye meme en cas d'echec.");
            Assert.AreEqual(1, diplomacySpy.OpinionShifts.Count);
            Assert.AreEqual((EmpireB, EmpireA, -10f), diplomacySpy.OpinionShifts[0]);
        }

        // --- Incitation a la revolte -------------------------------------------------------

        [Test]
        public void TryInciteRevolt_Success_ReducesStability()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB, stability: 0f);
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            EconomyService economy = RegisterEconomy(map);
            GiveCredits(economy, home, EmpireA, 1000f);

            bool success = espionage.TryInciteRevolt(EmpireA, enemy.Id, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(0f, enemy.Stability, FloatTolerance, "Deja au plancher : ne peut pas devenir negative.");
        }

        [Test]
        public void TryInciteRevolt_StabilityClampedAtZero()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB, stability: 0.1f);
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            EconomyService economy = RegisterEconomy(map);
            GiveCredits(economy, home, EmpireA, 1000f);

            bool success = espionage.TryInciteRevolt(EmpireA, enemy.Id, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(0f, enemy.Stability, FloatTolerance, "0.1 - 0.3 doit etre borne a 0, jamais negatif.");
        }

        // --- Influence de gouvernement -------------------------------------------------------

        [Test]
        public void TryInfluenceGovernment_Success_ImprovesTargetOpinionOfProposer()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB, stability: 0f);
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            EconomyService economy = RegisterEconomy(map);
            GiveCredits(economy, home, EmpireA, 1000f);
            var diplomacySpy = new SpyDiplomacyService();
            ServiceLocator.Register<IDiplomacyService>(diplomacySpy);

            bool success = espionage.TryInfluenceGovernment(EmpireA, EmpireB, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(1, diplomacySpy.OpinionShifts.Count);
            Assert.AreEqual((EmpireB, EmpireA, 15f), diplomacySpy.OpinionShifts[0]);
        }

        [Test]
        public void TryInfluenceGovernment_TargetOwnsNoSystem_Fails()
        {
            var map = new GalaxyMap(new[] { MakeSystem(0, EmpireA) }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            RegisterEconomy(map);

            bool success = espionage.TryInfluenceGovernment(EmpireA, EmpireB, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        // --- Decouverte d'armees ------------------------------------------------------------

        [Test]
        public void TryDiscoverArmies_Success_ReturnsRealGarrison()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB, stability: 0f);
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            EconomyService economy = RegisterEconomy(map);
            GiveCredits(economy, home, EmpireA, 1000f);
            var fakeMilitary = new FakeMilitaryService();
            fakeMilitary.SetGarrison(enemy.Id, new UnitBundle(infantry: 7));
            ServiceLocator.Register<IMilitaryService>(fakeMilitary);

            bool success = espionage.TryDiscoverArmies(EmpireA, EmpireB, enemy.Id, out UnitBundle discovered, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(new UnitBundle(infantry: 7), discovered);
        }

        [Test]
        public void TryDiscoverArmies_WrongSystemOwner_Fails()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState neutral = MakeSystem(1, StarSystemState.UnownedOwnerId);
            var map = new GalaxyMap(new[] { home, neutral }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            RegisterEconomy(map);

            bool success = espionage.TryDiscoverArmies(EmpireA, EmpireB, neutral.Id, out _, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryDiscoverArmies_MilitaryUnavailable_SucceedsWithZeroGarrison()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB, stability: 0f);
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            EconomyService economy = RegisterEconomy(map);
            GiveCredits(economy, home, EmpireA, 1000f);

            bool success = espionage.TryDiscoverArmies(EmpireA, EmpireB, enemy.Id, out UnitBundle discovered, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(UnitBundle.Zero, discovered);
        }

        // --- Divers --------------------------------------------------------------------

        [Test]
        public void Mission_EconomyUnavailable_Fails()
        {
            StarSystemState home = MakeSystem(0, EmpireA);
            StarSystemState enemy = MakeSystem(1, EmpireB);
            var map = new GalaxyMap(new[] { home, enemy }, Array.Empty<HyperlaneLink>());
            EspionageService espionage = MakeEspionage(map);
            // IEconomyService jamais enregistre.

            bool success = espionage.TrySabotage(EmpireA, enemy.Id, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }
    }
}
