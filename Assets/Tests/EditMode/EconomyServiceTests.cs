using System;
using System.Reflection;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="EconomyService"/> : formule de production, cycle de construction,
    /// investissement, impots, evenements publies.
    /// </summary>
    [TestFixture]
    public sealed class EconomyServiceTests
    {
        private const float FloatTolerance = 0.001f;

        /// <summary>Horloge factice : seul <see cref="CurrentDate"/> est pilote par les tests.</summary>
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
        }

        private static StarSystemState MakeSystem(
            int id = 0, int population = 1000, int wealth = 200, int developmentLevel = 3,
            float stability = 1f, ResourceType[] deposits = null, bool ownedByPlayer = true)
        {
            var system = new StarSystemState(
                new StarSystemId(id), $"System{id}", Vector2.zero,
                population, wealth, developmentLevel, stability, deposits ?? Array.Empty<ResourceType>());

            if (ownedByPlayer)
            {
                system.OwnerId = EconomyService.PlayerOwnerId;
            }

            return system;
        }

        private static GalaxyMap MakeMap(params StarSystemState[] systems)
        {
            return new GalaxyMap(systems, Array.Empty<HyperlaneLink>());
        }

        /// <summary>
        /// <see cref="BuildingType"/> n'expose intentionnellement aucun setter public (le
        /// contenu vit dans des assets, pas dans du code) : les tests construisent une
        /// instance via reflection sur les champs serialises prives plutot que d'affaiblir
        /// l'API reelle pour leur seul besoin.
        /// </summary>
        private static BuildingType MakeBuildingType(
            string name, ResourceType produces, float productionPerDay, float creditsCost,
            int durationDays = 1, int minDevelopment = 0)
        {
            var building = ScriptableObject.CreateInstance<BuildingType>();
            SetPrivateField(building, "displayName", name);
            SetPrivateField(building, "producedResource", produces);
            SetPrivateField(building, "productionPerDay", productionPerDay);
            SetPrivateField(building, "creditsCost", creditsCost);
            SetPrivateField(building, "constructionDurationDays", durationDays);
            SetPrivateField(building, "minimumDevelopmentLevel", minDevelopment);
            return building;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Champ introuvable : {fieldName}");
            field.SetValue(target, value);
        }

        private static void AssertBundleApproximately(ResourceBundle expected, ResourceBundle actual)
        {
            Assert.AreEqual(expected.Credits, actual.Credits, FloatTolerance, "Credits");
            Assert.AreEqual(expected.Minerals, actual.Minerals, FloatTolerance, "Minerals");
            Assert.AreEqual(expected.Energy, actual.Energy, FloatTolerance, "Energy");
            Assert.AreEqual(expected.Food, actual.Food, FloatTolerance, "Food");
            Assert.AreEqual(expected.Influence, actual.Influence, FloatTolerance, "Influence");
        }

        // --- Construction --------------------------------------------------

        [Test]
        public void Constructor_NullMap_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new EconomyService(null, _clock, _eventBus, Array.Empty<BuildingType>()));
        }

        [Test]
        public void Constructor_NullClock_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new EconomyService(MakeMap(), null, _eventBus, Array.Empty<BuildingType>()));
        }

        [Test]
        public void Constructor_NullEventBus_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new EconomyService(MakeMap(), _clock, null, Array.Empty<BuildingType>()));
        }

        [Test]
        public void Initialize_SetsDefaultTaxRateAndEmptyTreasury()
        {
            var service = new EconomyService(MakeMap(), _clock, _eventBus, Array.Empty<BuildingType>());

            service.Initialize();

            Assert.AreEqual(ResourceBundle.Zero, service.Treasury);
            Assert.AreEqual(0.25f, service.TaxRate, FloatTolerance);
        }

        // --- Production ------------------------------------------------------

        [Test]
        public void DayAdvanced_OwnedSystem_ProducesAccordingToFormula()
        {
            // population=1000, wealth=200, developmentLevel=3, stability=1, taxRate=0.25 (defaut), aucun gisement.
            StarSystemState system = MakeSystem();
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            var expected = new ResourceBundle(
                credits: 200 * 0.05f * 0.25f,
                minerals: 1000 * 0.01f,
                energy: 1000 * 0.008f,
                food: 1000 * 0.012f,
                influence: 3 * 0.4f);
            AssertBundleApproximately(expected, service.Treasury);
        }

        [Test]
        public void DayAdvanced_SystemWithDeposit_DoublesThatResourceOnly()
        {
            StarSystemState withDeposit = MakeSystem(deposits: new[] { ResourceType.Minerals });
            StarSystemState withoutDeposit = MakeSystem(id: 1, deposits: Array.Empty<ResourceType>());

            var serviceWith = new EconomyService(MakeMap(withDeposit), _clock, _eventBus, Array.Empty<BuildingType>());
            serviceWith.Initialize();
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            float mineralsWithDeposit = serviceWith.Treasury.Minerals;
            float creditsWithDeposit = serviceWith.Treasury.Credits;

            Assert.AreEqual(1000 * 0.01f * 2f, mineralsWithDeposit, FloatTolerance);
            // Le gisement de Minerais ne double pas les Credits.
            Assert.AreEqual(200 * 0.05f * 0.25f, creditsWithDeposit, FloatTolerance);
        }

        [Test]
        public void DayAdvanced_UnstableSystem_ScalesEntireProductionDown()
        {
            StarSystemState system = MakeSystem(stability: 0.5f);
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(1000 * 0.01f * 0.5f, service.Treasury.Minerals, FloatTolerance);
        }

        [Test]
        public void DayAdvanced_UnownedSystem_ProducesNothing()
        {
            StarSystemState system = MakeSystem(ownedByPlayer: false);
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(ResourceBundle.Zero, service.Treasury);
        }

        [Test]
        public void DayAdvanced_NoOwnedSystems_DoesNotPublishEvents()
        {
            StarSystemState system = MakeSystem(ownedByPlayer: false);
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            int eventCount = 0;
            _eventBus.Subscribe<TreasuryChangedEvent>(_ => eventCount++);

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public void DayAdvanced_OwnedSystem_PublishesResourceProducedAndTreasuryChanged()
        {
            StarSystemState system = MakeSystem();
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            bool producedPublished = false;
            bool treasuryPublished = false;
            _eventBus.Subscribe<ResourceProducedEvent>(_ => producedPublished = true);
            _eventBus.Subscribe<TreasuryChangedEvent>(_ => treasuryPublished = true);

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.IsTrue(producedPublished);
            Assert.IsTrue(treasuryPublished);
        }

        [Test]
        public void SetTaxRate_ClampsBelowZero()
        {
            var service = new EconomyService(MakeMap(), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            service.SetTaxRate(-0.5f);

            Assert.AreEqual(0f, service.TaxRate);
        }

        [Test]
        public void SetTaxRate_ClampsAboveOne()
        {
            var service = new EconomyService(MakeMap(), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            service.SetTaxRate(1.5f);

            Assert.AreEqual(1f, service.TaxRate);
        }

        [Test]
        public void SetTaxRate_AffectsNextDayCreditsProduction()
        {
            StarSystemState system = MakeSystem();
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            service.SetTaxRate(1f);

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(200 * 0.05f * 1f, service.Treasury.Credits, FloatTolerance);
        }

        [Test]
        public void Shutdown_StopsProducingOnFurtherDays()
        {
            StarSystemState system = MakeSystem();
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            service.Shutdown();

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(ResourceBundle.Zero, service.Treasury);
        }

        // --- Construction de batiments ----------------------------------------

        [Test]
        public void TryStartConstruction_UnknownSystem_Fails()
        {
            var service = new EconomyService(MakeMap(), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            BuildingType building = MakeBuildingType("Mine", ResourceType.Minerals, 5f, 100f);

            bool success = service.TryStartConstruction(new StarSystemId(99), building, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryStartConstruction_NullBuildingType_Fails()
        {
            StarSystemState system = MakeSystem();
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            bool success = service.TryStartConstruction(system.Id, null, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryStartConstruction_UnownedSystem_Fails()
        {
            StarSystemState system = MakeSystem(ownedByPlayer: false);
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            BuildingType building = MakeBuildingType("Mine", ResourceType.Minerals, 5f, 100f);

            bool success = service.TryStartConstruction(system.Id, building, out string error);

            Assert.IsFalse(success);
            StringAssert.Contains("appartient", error);
        }

        [Test]
        public void TryStartConstruction_BelowMinimumDevelopment_Fails()
        {
            StarSystemState system = MakeSystem(developmentLevel: 0);
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            BuildingType building = MakeBuildingType("Centre culturel", ResourceType.Influence, 3f, 100f, minDevelopment: 2);

            bool success = service.TryStartConstruction(system.Id, building, out string error);

            Assert.IsFalse(success);
            StringAssert.Contains("developpement", error);
        }

        [Test]
        public void TryStartConstruction_InsufficientCredits_Fails()
        {
            StarSystemState system = MakeSystem();
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            BuildingType building = MakeBuildingType("Mine", ResourceType.Minerals, 5f, 999999f);

            bool success = service.TryStartConstruction(system.Id, building, out string error);

            Assert.IsFalse(success);
            StringAssert.Contains("insuffisants", error);
        }

        [Test]
        public void TryStartConstruction_Success_DeductsCostAndRegistersBuilding()
        {
            StarSystemState system = MakeSystem();
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            float granted = GiveCredits(service, system, 500f);
            BuildingType building = MakeBuildingType("Mine", ResourceType.Minerals, 5f, 300f);

            bool success = service.TryStartConstruction(system.Id, building, out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);
            Assert.AreEqual(granted - 300f, service.Treasury.Credits, FloatTolerance);
            Assert.AreEqual(1, service.GetBuildings(system.Id).Count);
            Assert.AreEqual(BuildingStatus.UnderConstruction, service.GetBuildings(system.Id)[0].Status);
        }

        [Test]
        public void TryStartConstruction_DuplicateBuildingType_Fails()
        {
            StarSystemState system = MakeSystem();
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            GiveCredits(service, system, 1000f);
            BuildingType building = MakeBuildingType("Mine", ResourceType.Minerals, 5f, 100f);

            Assert.IsTrue(service.TryStartConstruction(system.Id, building, out _));
            bool secondAttempt = service.TryStartConstruction(system.Id, building, out string error);

            Assert.IsFalse(secondAttempt);
            StringAssert.Contains("deja", error);
        }

        [Test]
        public void TryStartConstruction_PublishesConstructionStartedEvent()
        {
            StarSystemState system = MakeSystem();
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            GiveCredits(service, system, 500f);
            BuildingType building = MakeBuildingType("Mine", ResourceType.Minerals, 5f, 300f);

            bool published = false;
            _eventBus.Subscribe<BuildingConstructionStartedEvent>(e =>
            {
                published = true;
                Assert.AreEqual(system.Id, e.SystemId);
                Assert.AreSame(building, e.BuildingType);
            });

            service.TryStartConstruction(system.Id, building, out _);

            Assert.IsTrue(published);
        }

        [Test]
        public void Construction_CompletesOnCompletionDateAndStartsProducing()
        {
            StarSystemState system = MakeSystem(population: 0, wealth: 0, developmentLevel: 0);
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            GiveCredits(service, system, 500f);
            BuildingType building = MakeBuildingType("Mine", ResourceType.Minerals, productionPerDay: 7f, creditsCost: 300f, durationDays: 3);

            _clock.CurrentDate = GameDate.StartOfGame;
            service.TryStartConstruction(system.Id, building, out _);

            bool completedEventPublished = false;
            _eventBus.Subscribe<BuildingCompletedEvent>(e =>
            {
                completedEventPublished = true;
                Assert.AreSame(building, e.BuildingType);
            });

            // Jours 1 et 2 : construction pas encore terminee (duree de 3 jours).
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));
            Assert.AreEqual(BuildingStatus.UnderConstruction, service.GetBuildings(system.Id)[0].Status);
            Assert.AreEqual(0f, service.Treasury.Minerals, FloatTolerance);

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(2)));
            Assert.AreEqual(BuildingStatus.UnderConstruction, service.GetBuildings(system.Id)[0].Status);

            // Jour 3 : la construction se termine et produit des maintenant.
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(3)));

            Assert.IsTrue(completedEventPublished);
            Assert.AreEqual(BuildingStatus.Completed, service.GetBuildings(system.Id)[0].Status);
            Assert.AreEqual(7f, service.Treasury.Minerals, FloatTolerance);
        }

        [Test]
        public void GetBuildings_SystemWithNoBuildings_ReturnsEmpty()
        {
            StarSystemState system = MakeSystem();
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            Assert.AreEqual(0, service.GetBuildings(system.Id).Count);
        }

        // --- Investissement ----------------------------------------------------

        [Test]
        public void GetInvestmentCost_ScalesWithCurrentDevelopmentLevel()
        {
            StarSystemState system = MakeSystem(developmentLevel: 2);
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            float cost = service.GetInvestmentCost(system.Id);

            Assert.AreEqual((2 + 1) * 200f, cost, FloatTolerance);
        }

        [Test]
        public void TryInvestInDevelopment_Success_IncreasesLevelAndDeductsCost()
        {
            StarSystemState system = MakeSystem(developmentLevel: 1);
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            float cost = service.GetInvestmentCost(system.Id);
            float granted = GiveCredits(service, system, cost);

            bool success = service.TryInvestInDevelopment(system.Id, out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);
            Assert.AreEqual(2, system.DevelopmentLevel);
            Assert.AreEqual(granted - cost, service.Treasury.Credits, FloatTolerance);
        }

        [Test]
        public void TryInvestInDevelopment_AtMaxLevel_Fails()
        {
            StarSystemState system = MakeSystem(developmentLevel: 5);
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            GiveCredits(service, system, 100000f);

            bool success = service.TryInvestInDevelopment(system.Id, out string error);

            Assert.IsFalse(success);
            StringAssert.Contains("maximal", error);
        }

        [Test]
        public void TryInvestInDevelopment_InsufficientCredits_Fails()
        {
            StarSystemState system = MakeSystem(developmentLevel: 1);
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            bool success = service.TryInvestInDevelopment(system.Id, out string error);

            Assert.IsFalse(success);
            StringAssert.Contains("insuffisants", error);
        }

        [Test]
        public void TryInvestInDevelopment_UnownedSystem_Fails()
        {
            StarSystemState system = MakeSystem(ownedByPlayer: false);
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            bool success = service.TryInvestInDevelopment(system.Id, out string error);

            Assert.IsFalse(success);
            StringAssert.Contains("appartient", error);
        }

        // --- Multi-empire (Phase 5) --------------------------------------------

        [Test]
        public void DayAdvanced_TwoDifferentEmpires_ProduceIntoSeparateTreasuries()
        {
            const int otherEmpireId = 7;
            StarSystemState playerSystem = MakeSystem(id: 0, wealth: 200, population: 1000);
            StarSystemState otherSystem = MakeSystem(id: 1, wealth: 400, population: 500, ownedByPlayer: false);
            otherSystem.OwnerId = otherEmpireId;

            var service = new EconomyService(MakeMap(playerSystem, otherSystem), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(200 * 0.05f * 0.25f, service.GetTreasury(EconomyService.PlayerOwnerId).Credits, FloatTolerance);
            Assert.AreEqual(400 * 0.05f * 0.25f, service.GetTreasury(otherEmpireId).Credits, FloatTolerance);
            Assert.AreNotEqual(service.GetTreasury(EconomyService.PlayerOwnerId), service.GetTreasury(otherEmpireId));
        }

        [Test]
        public void SetTaxRate_PerEmpire_DoesNotAffectOtherEmpires()
        {
            const int otherEmpireId = 7;
            var service = new EconomyService(MakeMap(), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            service.SetTaxRate(otherEmpireId, 0.9f);

            Assert.AreEqual(0.9f, service.GetTaxRate(otherEmpireId), FloatTolerance);
            Assert.AreEqual(0.25f, service.GetTaxRate(EconomyService.PlayerOwnerId), FloatTolerance, "Le taux par defaut du joueur ne doit pas etre affecte.");
        }

        [Test]
        public void SetTaxRate_EmpireOverload_ClampsToZeroOne()
        {
            const int otherEmpireId = 7;
            var service = new EconomyService(MakeMap(), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            service.SetTaxRate(otherEmpireId, -1f);
            Assert.AreEqual(0f, service.GetTaxRate(otherEmpireId));

            service.SetTaxRate(otherEmpireId, 2f);
            Assert.AreEqual(1f, service.GetTaxRate(otherEmpireId));
        }

        [Test]
        public void GetTreasury_UnknownEmpire_ReturnsZero()
        {
            var service = new EconomyService(MakeMap(), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            Assert.AreEqual(ResourceBundle.Zero, service.GetTreasury(42));
        }

        [Test]
        public void TryStartConstruction_NonPlayerEmpireOwnedSystem_Succeeds()
        {
            const int aiEmpireId = 7;
            StarSystemState system = MakeSystem(ownedByPlayer: false);
            system.OwnerId = aiEmpireId;
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            GiveCreditsToEmpire(service, system, aiEmpireId, 500f);
            BuildingType building = MakeBuildingType("Mine", ResourceType.Minerals, 5f, 300f);

            bool success = service.TryStartConstruction(system.Id, building, out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);
            Assert.AreEqual(1, service.GetBuildings(system.Id).Count);
            Assert.AreEqual(0f, service.GetTreasury(EconomyService.PlayerOwnerId).Credits, "Le tresor du joueur ne doit pas etre touche.");
        }

        [Test]
        public void TryInvestInDevelopment_NonPlayerEmpireOwnedSystem_Succeeds()
        {
            const int aiEmpireId = 7;
            StarSystemState system = MakeSystem(ownedByPlayer: false, developmentLevel: 1);
            system.OwnerId = aiEmpireId;
            var service = new EconomyService(MakeMap(system), _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            float cost = service.GetInvestmentCost(system.Id);
            GiveCreditsToEmpire(service, system, aiEmpireId, cost);

            bool success = service.TryInvestInDevelopment(system.Id, out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);
            Assert.AreEqual(2, system.DevelopmentLevel);
        }

        /// <summary>Variante de <see cref="GiveCredits"/> pour un empire quelconque, pas seulement le joueur.</summary>
        private float GiveCreditsToEmpire(EconomyService service, StarSystemState system, int empireId, float minimumAmount)
        {
            int originalWealth = system.Wealth;
            float originalTax = service.GetTaxRate(empireId);

            system.Wealth = Mathf.CeilToInt(minimumAmount / 0.05f) + 1;
            service.SetTaxRate(empireId, 1f);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(1)));
            float granted = service.GetTreasury(empireId).Credits;

            system.Wealth = originalWealth;
            service.SetTaxRate(empireId, originalTax);

            return granted;
        }

        /// <summary>
        /// Fait produire au tresor au moins <paramref name="minimumAmount"/> de Credits, en
        /// gonflant temporairement la richesse et l'impot pour un seul jour puis en
        /// restaurant les valeurs d'origine. Passe par le seul chemin reel d'entree
        /// d'argent (la production journaliere) plutot que d'exposer une methode
        /// reservee aux tests dans l'API. Retourne le montant reellement accorde (peut
        /// legerement depasser <paramref name="minimumAmount"/> a cause de l'arrondi) :
        /// les appelants doivent comparer des ecarts, jamais une valeur absolue attendue.
        /// </summary>
        private float GiveCredits(EconomyService service, StarSystemState system, float minimumAmount)
        {
            int originalWealth = system.Wealth;
            float originalTax = service.TaxRate;

            system.Wealth = Mathf.CeilToInt(minimumAmount / 0.05f) + 1;
            service.SetTaxRate(1f);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(1)));
            float granted = service.Treasury.Credits;

            system.Wealth = originalWealth;
            service.SetTaxRate(originalTax);

            return granted;
        }
    }
}
