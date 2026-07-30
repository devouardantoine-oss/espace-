using System;
using System.Reflection;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="AIDecisionMaker"/> : application du taux d'imposition, priorite de
    /// construction par personnalite, repli sur l'investissement, absence de plantage quand
    /// rien n'est finançable.
    /// </summary>
    [TestFixture]
    public sealed class AIDecisionMakerTests
    {
        private const float FloatTolerance = 0.001f;
        private const int AiEmpireId = 3;

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

        private static StarSystemState MakeOwnedSystem(int ownerId, int population = 1000, int wealth = 500, int developmentLevel = 3)
        {
            var system = new StarSystemState(new StarSystemId(0), "HomeSystem", Vector2.zero, population, wealth, developmentLevel, 1f, Array.Empty<ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        private static GalaxyMap MakeMap(params StarSystemState[] systems) => new GalaxyMap(systems, Array.Empty<HyperlaneLink>());

        private static Empire MakeEmpire(EmpirePersonality personality, int id = AiEmpireId) =>
            new Empire(id, "Empire de test", Color.red, personality, isPlayerControlled: false);

        private static BuildingType MakeBuildingType(string name, ResourceType produces, float productionPerDay, float creditsCost, int minDevelopment = 0)
        {
            var building = ScriptableObject.CreateInstance<BuildingType>();
            SetPrivateField(building, "displayName", name);
            SetPrivateField(building, "producedResource", produces);
            SetPrivateField(building, "productionPerDay", productionPerDay);
            SetPrivateField(building, "creditsCost", creditsCost);
            SetPrivateField(building, "constructionDurationDays", 5);
            SetPrivateField(building, "minimumDevelopmentLevel", minDevelopment);
            return building;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Champ introuvable : {fieldName}");
            field.SetValue(target, value);
        }

        /// <summary>
        /// Fait produire au tresor de <paramref name="empireId"/> au moins
        /// <paramref name="minimumAmount"/> de Credits, par un jour de production a richesse
        /// et impot gonfles — meme technique que dans <c>EconomyServiceTests</c>.
        /// </summary>
        private float GiveCredits(EconomyService service, StarSystemState system, int empireId, float minimumAmount)
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

        [TestCase(EmpirePersonality.Pacifist, 0.20f)]
        [TestCase(EmpirePersonality.Expansionist, 0.20f)]
        [TestCase(EmpirePersonality.Mercantile, 0.35f)]
        [TestCase(EmpirePersonality.Militarist, 0.30f)]
        [TestCase(EmpirePersonality.Opportunist, 0.25f)]
        public void DecideAndAct_AppliesPersonalityPreferredTaxRate(EmpirePersonality personality, float expectedRate)
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            Empire empire = MakeEmpire(personality);

            AIDecisionMaker.DecideAndAct(empire, map, service);

            Assert.AreEqual(expectedRate, service.GetTaxRate(AiEmpireId), FloatTolerance);
        }

        [Test]
        public void DecideAndAct_EmpireOwnsNoSystem_DoesNothing()
        {
            StarSystemState unowned = new StarSystemState(new StarSystemId(0), "Unowned", Vector2.zero, 1000, 500, 3, 1f, Array.Empty<ResourceType>());
            GalaxyMap map = MakeMap(unowned);
            var service = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            Empire empire = MakeEmpire(EmpirePersonality.Militarist);

            Assert.DoesNotThrow(() => AIDecisionMaker.DecideAndAct(empire, map, service));
            Assert.AreEqual(3, unowned.DevelopmentLevel, "Aucun systeme possede : rien ne doit changer.");
        }

        [Test]
        public void DecideAndAct_Mercantile_PrioritizesCreditsBuildingOverOthers()
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId);
            BuildingType minerals = MakeBuildingType("Extracteur", ResourceType.Minerals, 5f, 100f);
            BuildingType energy = MakeBuildingType("Centrale", ResourceType.Energy, 4f, 100f);
            BuildingType credits = MakeBuildingType("Marche", ResourceType.Credits, 8f, 100f);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, new[] { minerals, energy, credits });
            service.Initialize();
            GiveCredits(service, system, AiEmpireId, 1000f);
            Empire empire = MakeEmpire(EmpirePersonality.Mercantile);

            AIDecisionMaker.DecideAndAct(empire, map, service);

            var buildings = service.GetBuildings(system.Id);
            Assert.AreEqual(1, buildings.Count);
            Assert.AreEqual(credits, buildings[0].Type);
        }

        [Test]
        public void DecideAndAct_Militarist_PrioritizesMineralsBuildingOverOthers()
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId);
            BuildingType credits = MakeBuildingType("Marche", ResourceType.Credits, 8f, 100f);
            BuildingType minerals = MakeBuildingType("Extracteur", ResourceType.Minerals, 5f, 100f);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, new[] { credits, minerals });
            service.Initialize();
            GiveCredits(service, system, AiEmpireId, 1000f);
            Empire empire = MakeEmpire(EmpirePersonality.Militarist);

            AIDecisionMaker.DecideAndAct(empire, map, service);

            var buildings = service.GetBuildings(system.Id);
            Assert.AreEqual(1, buildings.Count);
            Assert.AreEqual(minerals, buildings[0].Type);
        }

        [Test]
        public void DecideAndAct_Opportunist_PicksCheapestAffordableRegardlessOfResource()
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId);
            BuildingType expensive = MakeBuildingType("Cher", ResourceType.Credits, 8f, 500f);
            BuildingType cheap = MakeBuildingType("Abordable", ResourceType.Food, 3f, 80f);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, new[] { expensive, cheap });
            service.Initialize();
            GiveCredits(service, system, AiEmpireId, 1000f);
            Empire empire = MakeEmpire(EmpirePersonality.Opportunist);

            AIDecisionMaker.DecideAndAct(empire, map, service);

            var buildings = service.GetBuildings(system.Id);
            Assert.AreEqual(1, buildings.Count);
            Assert.AreEqual(cheap, buildings[0].Type);
        }

        [Test]
        public void DecideAndAct_TopPriorityAlreadyBuilt_MovesToNextPriority()
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId);
            BuildingType credits = MakeBuildingType("Marche", ResourceType.Credits, 8f, 100f);
            BuildingType energy = MakeBuildingType("Centrale", ResourceType.Energy, 4f, 100f);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, new[] { credits, energy });
            service.Initialize();
            Empire empire = MakeEmpire(EmpirePersonality.Mercantile); // priorite : Credits, Energie, ...

            GiveCredits(service, system, AiEmpireId, 1000f);
            AIDecisionMaker.DecideAndAct(empire, map, service); // construit Credits (priorite 1)

            GiveCredits(service, system, AiEmpireId, 1000f);
            AIDecisionMaker.DecideAndAct(empire, map, service); // Credits deja construit -> Energie

            var buildings = service.GetBuildings(system.Id);
            Assert.AreEqual(2, buildings.Count);
            CollectionAssert.Contains(new[] { buildings[0].Type, buildings[1].Type }, credits);
            CollectionAssert.Contains(new[] { buildings[0].Type, buildings[1].Type }, energy);
        }

        [Test]
        public void DecideAndAct_DevelopmentTooLowForTopPriority_SkipsToNextEligible()
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId, developmentLevel: 0);
            BuildingType credits = MakeBuildingType("Marche avance", ResourceType.Credits, 8f, 100f, minDevelopment: 2);
            BuildingType energy = MakeBuildingType("Centrale", ResourceType.Energy, 4f, 100f, minDevelopment: 0);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, new[] { credits, energy });
            service.Initialize();
            GiveCredits(service, system, AiEmpireId, 1000f);
            Empire empire = MakeEmpire(EmpirePersonality.Mercantile); // priorite : Credits (inaccessible), puis Energie

            AIDecisionMaker.DecideAndAct(empire, map, service);

            var buildings = service.GetBuildings(system.Id);
            Assert.AreEqual(1, buildings.Count);
            Assert.AreEqual(energy, buildings[0].Type);
        }

        [Test]
        public void DecideAndAct_NoAffordableBuilding_InvestsInDevelopmentInstead()
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId, developmentLevel: 1);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            Empire empire = MakeEmpire(EmpirePersonality.Expansionist); // eagerness 1.1

            float cost = service.GetInvestmentCost(system.Id);
            GiveCredits(service, system, AiEmpireId, cost * 1.1f + 10f);

            AIDecisionMaker.DecideAndAct(empire, map, service);

            Assert.AreEqual(2, system.DevelopmentLevel);
        }

        [Test]
        public void DecideAndAct_TreasuryBelowInvestmentEagernessMargin_DoesNotInvest()
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId, developmentLevel: 1);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            Empire empire = MakeEmpire(EmpirePersonality.Pacifist); // eagerness 1.5, prudent

            float cost = service.GetInvestmentCost(system.Id);
            // Finance exactement le cout mais pas la marge de prudence exigee (x1.5).
            GiveCredits(service, system, AiEmpireId, cost * 1.05f);

            AIDecisionMaker.DecideAndAct(empire, map, service);

            Assert.AreEqual(1, system.DevelopmentLevel);
        }

        [Test]
        public void DecideAndAct_NothingAffordable_DoesNotThrowAndChangesNothing()
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId, wealth: 0, population: 0, developmentLevel: 0);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            Empire empire = MakeEmpire(EmpirePersonality.Militarist);

            Assert.DoesNotThrow(() => AIDecisionMaker.DecideAndAct(empire, map, service));
            Assert.AreEqual(0, system.DevelopmentLevel);
            Assert.AreEqual(0, service.GetBuildings(system.Id).Count);
        }

        [Test]
        public void DecideAndAct_BuildSucceeds_DoesNotAlsoInvestSameCall()
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId, developmentLevel: 3);
            BuildingType cheapBuilding = MakeBuildingType("Peu cher", ResourceType.Food, 3f, 50f);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, new[] { cheapBuilding });
            service.Initialize();
            // Assez pour le batiment ET l'investissement, si les deux etaient tentes.
            GiveCredits(service, system, AiEmpireId, 5000f);
            Empire empire = MakeEmpire(EmpirePersonality.Pacifist); // priorite : Food en premier

            AIDecisionMaker.DecideAndAct(empire, map, service);

            Assert.AreEqual(1, service.GetBuildings(system.Id).Count);
            Assert.AreEqual(3, system.DevelopmentLevel, "Un seul type d'action par appel : la construction a eu lieu, pas l'investissement.");
        }
    }
}
