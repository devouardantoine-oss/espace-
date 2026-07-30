using System;
using System.IO;
using System.Reflection;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.Research;
using Espace.Gameplay.Save;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="SaveService"/> : capture puis application d'une sauvegarde
    /// reproduit fidelement l'etat (systemes, batiments, tresors, garnisons, diplomatie,
    /// recherche, horloge), robustesse face a un fichier absent ou corrompu, et silence
    /// (aucun evenement publie) pendant l'application.
    /// </summary>
    [TestFixture]
    public sealed class SaveServiceTests
    {
        private const float FloatTolerance = 0.001f;
        private const int PlayerId = EconomyService.PlayerOwnerId;
        private const int AiId = 1;

        private sealed class FakeGameClock : IGameClock
        {
            public GameDate CurrentDate { get; set; } = GameDate.StartOfGame;
            public GameSpeed CurrentSpeed { get; private set; } = GameSpeed.Normal;
            public float CurrentMultiplier => 1f;
            public bool IsPaused => CurrentSpeed == GameSpeed.Paused;
            public void Pause() => CurrentSpeed = GameSpeed.Paused;
            public void Resume() => CurrentSpeed = GameSpeed.Normal;
            public void TogglePause() => CurrentSpeed = IsPaused ? GameSpeed.Normal : GameSpeed.Paused;
            public void SetSpeed(GameSpeed speed) => CurrentSpeed = speed;
            public void SetDate(GameDate date) => CurrentDate = date;
        }

        private EventBus _eventBus;
        private FakeGameClock _clock;
        private string _filePath;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _eventBus.Initialize();
            _clock = new FakeGameClock();
            _filePath = Path.Combine(Path.GetTempPath(), $"espace_save_test_{Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            _eventBus.Shutdown();
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        private static StarSystemState MakeSystem(int id, int ownerId, int population, int wealth, int developmentLevel, float stability)
        {
            var system = new StarSystemState(new StarSystemId(id), $"System{id}", Vector2.zero, population, wealth, developmentLevel, stability, Array.Empty<ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        private static BuildingType MakeBuildingType(string displayName)
        {
            var buildingType = ScriptableObject.CreateInstance<BuildingType>();
            SetPrivateField(buildingType, "displayName", displayName);
            SetPrivateField(buildingType, "producedResource", ResourceType.Credits);
            SetPrivateField(buildingType, "productionPerDay", 1f);
            SetPrivateField(buildingType, "creditsCost", 10f);
            SetPrivateField(buildingType, "constructionDurationDays", 1);
            SetPrivateField(buildingType, "minimumDevelopmentLevel", 0);
            return buildingType;
        }

        private static TechnologyDefinition MakeTechnology(ResearchDomain domain, int tier, float cost = 100f)
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

        /// <summary>Construit un scenario complet (galaxie, empires, tous les services) prets a etre captures.</summary>
        private sealed class Scenario
        {
            public GalaxyMap Map;
            public StarSystemState PlayerSystem;
            public StarSystemState AiSystem;
            public EmpireRegistry EmpireRegistry;
            public EconomyService Economy;
            public DiplomacyService Diplomacy;
            public MilitaryService Military;
            public ResearchService Research;
            public BuildingType BuildingType;
        }

        private Scenario BuildScenario()
        {
            var scenario = new Scenario
            {
                PlayerSystem = MakeSystem(0, PlayerId, population: 1200, wealth: 300, developmentLevel: 2, stability: 0.8f),
                AiSystem = MakeSystem(1, AiId, population: 900, wealth: 150, developmentLevel: 1, stability: 0.6f)
            };
            scenario.Map = new GalaxyMap(new[] { scenario.PlayerSystem, scenario.AiSystem }, new[] { new HyperlaneLink(scenario.PlayerSystem.Id, scenario.AiSystem.Id) });

            scenario.EmpireRegistry = new EmpireRegistry(new[]
            {
                new Empire(PlayerId, "Joueur", Color.blue, EmpirePersonality.Expansionist, isPlayerControlled: true),
                new Empire(AiId, "IA", Color.red, EmpirePersonality.Militarist, isPlayerControlled: false),
            });

            scenario.BuildingType = MakeBuildingType("Mine");
            scenario.Economy = new EconomyService(scenario.Map, _clock, _eventBus, new[] { scenario.BuildingType });
            scenario.Economy.Initialize();

            scenario.Diplomacy = new DiplomacyService(scenario.EmpireRegistry, scenario.Economy, scenario.Map, _eventBus);
            scenario.Diplomacy.Initialize();

            scenario.Military = new MilitaryService(scenario.Map, _clock, _eventBus, scenario.Economy, scenario.Diplomacy, scenario.EmpireRegistry, Array.Empty<UnitTypeDefinition>());
            scenario.Military.Initialize();

            scenario.Research = new ResearchService(scenario.Map, _eventBus, new[] { MakeTechnology(ResearchDomain.Economy, 1), MakeTechnology(ResearchDomain.Weapons, 1) });
            scenario.Research.Initialize();

            return scenario;
        }

        private SaveService MakeSaveService(Scenario scenario)
        {
            return new SaveService(scenario.Map, _clock, scenario.Economy, scenario.Military, scenario.Diplomacy, scenario.Research, scenario.EmpireRegistry, _filePath);
        }

        // --- Construction ------------------------------------------------------------

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            Scenario scenario = BuildScenario();

            Assert.Throws<ArgumentNullException>(() => new SaveService(null, _clock, scenario.Economy, scenario.Military, scenario.Diplomacy, scenario.Research, scenario.EmpireRegistry, _filePath));
            Assert.Throws<ArgumentNullException>(() => new SaveService(scenario.Map, null, scenario.Economy, scenario.Military, scenario.Diplomacy, scenario.Research, scenario.EmpireRegistry, _filePath));
            Assert.Throws<ArgumentException>(() => new SaveService(scenario.Map, _clock, scenario.Economy, scenario.Military, scenario.Diplomacy, scenario.Research, scenario.EmpireRegistry, null));
            Assert.Throws<ArgumentException>(() => new SaveService(scenario.Map, _clock, scenario.Economy, scenario.Military, scenario.Diplomacy, scenario.Research, scenario.EmpireRegistry, ""));
        }

        // --- Fichier ---------------------------------------------------------------------

        [Test]
        public void SaveFileExists_BeforeAnySave_ReturnsFalse()
        {
            SaveService save = MakeSaveService(BuildScenario());

            Assert.IsFalse(save.SaveFileExists);
        }

        [Test]
        public void SaveNow_WritesFile()
        {
            SaveService save = MakeSaveService(BuildScenario());

            save.SaveNow();

            Assert.IsTrue(save.SaveFileExists);
            Assert.IsTrue(File.Exists(_filePath));
        }

        [Test]
        public void TryLoadAndApply_NoFile_FailsWithError()
        {
            SaveService save = MakeSaveService(BuildScenario());

            bool success = save.TryLoadAndApply(out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryLoadAndApply_CorruptedFile_FailsWithoutThrowing()
        {
            File.WriteAllText(_filePath, "{ ceci n'est pas du JSON valide !!");
            SaveService save = MakeSaveService(BuildScenario());

            bool success = false;
            Assert.DoesNotThrow(() => success = save.TryLoadAndApply(out string _));
            Assert.IsFalse(success);
        }

        // --- Aller-retour complet ---------------------------------------------------------

        [Test]
        public void RoundTrip_RestoresSystemState()
        {
            Scenario scenario = BuildScenario();
            SaveService save = MakeSaveService(scenario);
            save.SaveNow();

            // Modifie l'etat courant pour verifier que le rechargement l'ecrase bien.
            scenario.PlayerSystem.OwnerId = AiId;
            scenario.PlayerSystem.Population = 1;
            scenario.PlayerSystem.Wealth = 1;
            scenario.PlayerSystem.DevelopmentLevel = 0;
            scenario.PlayerSystem.Stability = 0f;

            bool success = save.TryLoadAndApply(out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(PlayerId, scenario.PlayerSystem.OwnerId);
            Assert.AreEqual(1200, scenario.PlayerSystem.Population);
            Assert.AreEqual(300, scenario.PlayerSystem.Wealth);
            Assert.AreEqual(2, scenario.PlayerSystem.DevelopmentLevel);
            Assert.AreEqual(0.8f, scenario.PlayerSystem.Stability, FloatTolerance);
        }

        [Test]
        public void RoundTrip_RestoresTreasuryAndTaxRate()
        {
            Scenario scenario = BuildScenario();
            scenario.Economy.Grant(PlayerId, new ResourceBundle(credits: 500f, minerals: 40f));
            scenario.Economy.SetTaxRate(PlayerId, 0.42f);
            SaveService save = MakeSaveService(scenario);
            save.SaveNow();

            // Reinitialise une nouvelle economie vierge pour verifier que la sauvegarde la repeuple entierement.
            var freshEconomy = new EconomyService(scenario.Map, _clock, _eventBus, new[] { scenario.BuildingType });
            freshEconomy.Initialize();
            var freshSave = new SaveService(scenario.Map, _clock, freshEconomy, scenario.Military, scenario.Diplomacy, scenario.Research, scenario.EmpireRegistry, _filePath);

            bool success = freshSave.TryLoadAndApply(out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(500f, freshEconomy.GetTreasury(PlayerId).Credits, FloatTolerance);
            Assert.AreEqual(40f, freshEconomy.GetTreasury(PlayerId).Minerals, FloatTolerance);
            Assert.AreEqual(0.42f, freshEconomy.GetTaxRate(PlayerId), FloatTolerance);
        }

        [Test]
        public void RoundTrip_RestoresOnlyCompletedBuildings()
        {
            Scenario scenario = BuildScenario();
            scenario.Economy.Grant(PlayerId, new ResourceBundle(credits: 1000f));
            scenario.Economy.TryStartConstruction(scenario.PlayerSystem.Id, scenario.BuildingType, out string constructionError);
            Assert.IsNull(constructionError);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(1))); // termine la construction (duree 1 jour)
            Assert.AreEqual(BuildingStatus.Completed, scenario.Economy.GetBuildings(scenario.PlayerSystem.Id)[0].Status, "Precondition.");

            SaveService save = MakeSaveService(scenario);
            save.SaveNow();

            var freshEconomy = new EconomyService(scenario.Map, _clock, _eventBus, new[] { scenario.BuildingType });
            freshEconomy.Initialize();
            var freshSave = new SaveService(scenario.Map, _clock, freshEconomy, scenario.Military, scenario.Diplomacy, scenario.Research, scenario.EmpireRegistry, _filePath);

            freshSave.TryLoadAndApply(out string error);

            Assert.IsNull(error);
            Assert.AreEqual(1, freshEconomy.GetBuildings(scenario.PlayerSystem.Id).Count);
            Assert.AreEqual(BuildingStatus.Completed, freshEconomy.GetBuildings(scenario.PlayerSystem.Id)[0].Status);
            Assert.AreEqual(scenario.BuildingType, freshEconomy.GetBuildings(scenario.PlayerSystem.Id)[0].Type);
        }

        [Test]
        public void RoundTrip_RestoresGarrisons()
        {
            Scenario scenario = BuildScenario();
            scenario.Military.RestoreGarrison(scenario.PlayerSystem.Id, PlayerId, new UnitBundle(infantry: 5, armored: 2));
            SaveService save = MakeSaveService(scenario);
            save.SaveNow();

            var freshMilitary = new MilitaryService(scenario.Map, _clock, _eventBus, scenario.Economy, scenario.Diplomacy, scenario.EmpireRegistry, Array.Empty<UnitTypeDefinition>());
            freshMilitary.Initialize();
            var freshSave = new SaveService(scenario.Map, _clock, scenario.Economy, freshMilitary, scenario.Diplomacy, scenario.Research, scenario.EmpireRegistry, _filePath);

            bool success = freshSave.TryLoadAndApply(out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(new UnitBundle(infantry: 5, armored: 2), freshMilitary.GetGarrison(scenario.PlayerSystem.Id, PlayerId));
        }

        [Test]
        public void RoundTrip_RestoresDiplomacyRelationsAndOpinions()
        {
            Scenario scenario = BuildScenario();
            scenario.Diplomacy.TryDeclareWar(PlayerId, AiId, out _);
            SaveService save = MakeSaveService(scenario);
            save.SaveNow();

            var freshDiplomacy = new DiplomacyService(scenario.EmpireRegistry, scenario.Economy, scenario.Map, _eventBus);
            freshDiplomacy.Initialize();
            var freshSave = new SaveService(scenario.Map, _clock, scenario.Economy, scenario.Military, freshDiplomacy, scenario.Research, scenario.EmpireRegistry, _filePath);

            bool success = freshSave.TryLoadAndApply(out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(DiplomaticStatus.War, freshDiplomacy.GetStatus(PlayerId, AiId));
            Assert.Less(freshDiplomacy.GetOpinion(AiId, PlayerId), 0f, "L'opinion negative issue de la declaration de guerre doit avoir ete restauree.");
        }

        [Test]
        public void RoundTrip_RestoresResearchProgressAndActiveDomain()
        {
            Scenario scenario = BuildScenario();
            scenario.Research.TrySetActiveDomain(PlayerId, ResearchDomain.Weapons, out _);
            scenario.Research.RestoreProgress(PlayerId, ResearchDomain.Economy, completedTiers: 1, progress: 0f);
            SaveService save = MakeSaveService(scenario);
            save.SaveNow();

            var freshResearch = new ResearchService(scenario.Map, _eventBus, new[] { MakeTechnology(ResearchDomain.Economy, 1), MakeTechnology(ResearchDomain.Weapons, 1) });
            freshResearch.Initialize();
            var freshSave = new SaveService(scenario.Map, _clock, scenario.Economy, scenario.Military, scenario.Diplomacy, freshResearch, scenario.EmpireRegistry, _filePath);

            bool success = freshSave.TryLoadAndApply(out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(1, freshResearch.GetCompletedTierCount(PlayerId, ResearchDomain.Economy));
            Assert.AreEqual(ResearchDomain.Weapons, freshResearch.GetActiveDomain(PlayerId));
        }

        [Test]
        public void RoundTrip_RestoresClockDate()
        {
            Scenario scenario = BuildScenario();
            _clock.CurrentDate = new GameDate(3, 6, 15);
            SaveService save = MakeSaveService(scenario);
            save.SaveNow();

            _clock.CurrentDate = GameDate.StartOfGame;

            bool success = save.TryLoadAndApply(out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(new GameDate(3, 6, 15), _clock.CurrentDate);
        }

        // --- Silence pendant l'application --------------------------------------------------

        [Test]
        public void TryLoadAndApply_RestoreMethodsPublishNoEvents()
        {
            Scenario scenario = BuildScenario();
            scenario.Diplomacy.TryDeclareWar(PlayerId, AiId, out _); // etat non trivial a restaurer
            scenario.Research.TrySetActiveDomain(PlayerId, ResearchDomain.Economy, out _);
            SaveService save = MakeSaveService(scenario);
            save.SaveNow();

            bool anyUnexpectedEvent = false;
            _eventBus.Subscribe<DiplomaticStatusChangedEvent>(_ => anyUnexpectedEvent = true);
            _eventBus.Subscribe<ActiveDomainChangedEvent>(_ => anyUnexpectedEvent = true);
            _eventBus.Subscribe<TechnologyResearchedEvent>(_ => anyUnexpectedEvent = true);

            save.TryLoadAndApply(out string error);

            Assert.IsNull(error);
            Assert.IsFalse(anyUnexpectedEvent, "Les methodes Restore* ne doivent publier aucun evenement.");
        }
    }
}
