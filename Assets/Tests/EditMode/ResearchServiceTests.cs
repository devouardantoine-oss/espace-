using System;
using System.Reflection;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Research;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="ResearchService"/> : generation journaliere de points, progression
    /// et completion de palier, bonus cumulatif, choix du domaine actif, independance des
    /// progressions entre domaines et entre empires.
    /// </summary>
    [TestFixture]
    public sealed class ResearchServiceTests
    {
        private const float FloatTolerance = 0.001f;
        private const int EmpireA = 0;
        private const int EmpireB = 1;

        private EventBus _eventBus;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _eventBus.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _eventBus.Shutdown();
        }

        private static StarSystemState MakeSystem(int id, int ownerId, int population = 1000, int developmentLevel = 3, float stability = 1f)
        {
            var system = new StarSystemState(new StarSystemId(id), $"System{id}", Vector2.zero, population, 500, developmentLevel, stability, Array.Empty<ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        private static TechnologyDefinition MakeTechnology(ResearchDomain domain, int tier, float cost, float effect = 0.05f)
        {
            var technology = ScriptableObject.CreateInstance<TechnologyDefinition>();
            SetPrivateField(technology, "displayName", $"{domain}{tier}");
            SetPrivateField(technology, "domain", domain);
            SetPrivateField(technology, "tier", tier);
            SetPrivateField(technology, "researchPointCost", cost);
            SetPrivateField(technology, "effectMagnitude", effect);
            return technology;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Champ introuvable : {fieldName}");
            field.SetValue(target, value);
        }

        private ResearchService MakeResearch(GalaxyMap map, params TechnologyDefinition[] catalog)
        {
            var research = new ResearchService(map, _eventBus, catalog);
            research.Initialize();
            return research;
        }

        private static GalaxyMap MakeSingleSystemMap(int ownerId, int population = 1000, int developmentLevel = 3, float stability = 1f)
        {
            StarSystemState system = MakeSystem(0, ownerId, population, developmentLevel, stability);
            return new GalaxyMap(new[] { system }, Array.Empty<HyperlaneLink>());
        }

        // --- Construction ------------------------------------------------------------

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            GalaxyMap map = MakeSingleSystemMap(EmpireA);

            Assert.Throws<ArgumentNullException>(() => new ResearchService(null, _eventBus, Array.Empty<TechnologyDefinition>()));
            Assert.Throws<ArgumentNullException>(() => new ResearchService(map, null, Array.Empty<TechnologyDefinition>()));
        }

        // --- Etat par defaut -----------------------------------------------------------

        [Test]
        public void GetActiveDomain_NeverSet_ReturnsNull()
        {
            ResearchService research = MakeResearch(MakeSingleSystemMap(EmpireA));

            Assert.IsNull(research.GetActiveDomain(EmpireA));
        }

        [Test]
        public void GetNextTechnology_NoneCompleted_ReturnsFirstTier()
        {
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 100f);
            TechnologyDefinition tier2 = MakeTechnology(ResearchDomain.Economy, 2, 250f);
            ResearchService research = MakeResearch(MakeSingleSystemMap(EmpireA), tier1, tier2);

            Assert.AreEqual(tier1, research.GetNextTechnology(EmpireA, ResearchDomain.Economy));
        }

        [Test]
        public void GetNextTechnology_UnknownDomain_ReturnsNull()
        {
            ResearchService research = MakeResearch(MakeSingleSystemMap(EmpireA));

            Assert.IsNull(research.GetNextTechnology(EmpireA, ResearchDomain.Weapons));
        }

        [Test]
        public void GetBonus_NoCompletedTiers_ReturnsZero()
        {
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 100f);
            ResearchService research = MakeResearch(MakeSingleSystemMap(EmpireA), tier1);

            Assert.AreEqual(0f, research.GetBonus(EmpireA, ResearchDomain.Economy), FloatTolerance);
        }

        // --- Changement de domaine actif ------------------------------------------------

        [Test]
        public void TrySetActiveDomain_Success_PublishesEvent()
        {
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 100f);
            ResearchService research = MakeResearch(MakeSingleSystemMap(EmpireA), tier1);
            bool published = false;
            _eventBus.Subscribe<ActiveDomainChangedEvent>(e =>
            {
                published = true;
                Assert.AreEqual(EmpireA, e.EmpireId);
                Assert.AreEqual(ResearchDomain.Economy, e.Domain);
            });

            bool success = research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);
            Assert.IsTrue(published);
            Assert.AreEqual(ResearchDomain.Economy, research.GetActiveDomain(EmpireA));
        }

        [Test]
        public void TrySetActiveDomain_AlreadyActive_Fails()
        {
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 100f);
            ResearchService research = MakeResearch(MakeSingleSystemMap(EmpireA), tier1);
            research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out _);

            bool success = research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TrySetActiveDomain_DomainAlreadyMaxed_Fails()
        {
            TechnologyDefinition onlyTier = MakeTechnology(ResearchDomain.Economy, 1, 10f);
            GalaxyMap map = MakeSingleSystemMap(EmpireA, population: 100000, developmentLevel: 5);
            ResearchService research = MakeResearch(map, onlyTier);
            research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out _);
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1))); // genere largement de quoi completer l'unique palier

            Assert.IsNull(research.GetNextTechnology(EmpireA, ResearchDomain.Economy), "Precondition : le domaine doit etre au maximum.");

            bool success = research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
        }

        // --- Generation et progression --------------------------------------------------

        [Test]
        public void DayAdvanced_NoActiveDomain_NoProgress()
        {
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 100f);
            ResearchService research = MakeResearch(MakeSingleSystemMap(EmpireA), tier1);

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(0f, research.GetProgress(EmpireA, ResearchDomain.Economy), FloatTolerance);
        }

        [Test]
        public void DayAdvanced_UnownedSystem_GeneratesNoPoints()
        {
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 100f);
            GalaxyMap map = MakeSingleSystemMap(StarSystemState.UnownedOwnerId);
            ResearchService research = MakeResearch(map, tier1);
            research.TrySetActiveDomain(StarSystemState.UnownedOwnerId, ResearchDomain.Economy, out _);

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(0f, research.GetProgress(StarSystemState.UnownedOwnerId, ResearchDomain.Economy), FloatTolerance, "Le seul systeme de la carte n'a pas de proprietaire : aucun point genere pour personne.");
        }

        [Test]
        public void DayAdvanced_ActiveDomain_AccumulatesExpectedPoints()
        {
            // population=1000, developmentLevel=3, stability=1 => (1000*0.006 + 3*0.3)*1 = 6.9
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 1000f);
            GalaxyMap map = MakeSingleSystemMap(EmpireA, population: 1000, developmentLevel: 3, stability: 1f);
            ResearchService research = MakeResearch(map, tier1);
            research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out _);

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(6.9f, research.GetProgress(EmpireA, ResearchDomain.Economy), FloatTolerance);
        }

        [Test]
        public void DayAdvanced_ReachesThreshold_CompletesTierAndPublishesEvent()
        {
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 5f);
            GalaxyMap map = MakeSingleSystemMap(EmpireA, population: 1000, developmentLevel: 3, stability: 1f); // 6.9 pts/jour
            ResearchService research = MakeResearch(map, tier1);
            research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out _);

            bool published = false;
            _eventBus.Subscribe<TechnologyResearchedEvent>(e =>
            {
                published = true;
                Assert.AreEqual(EmpireA, e.EmpireId);
                Assert.AreEqual(tier1, e.Technology);
            });

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.IsTrue(published);
            Assert.AreEqual(1, research.GetCompletedTierCount(EmpireA, ResearchDomain.Economy));
        }

        [Test]
        public void DayAdvanced_Overflow_CarriesRemainderToNextTier()
        {
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 5f);
            TechnologyDefinition tier2 = MakeTechnology(ResearchDomain.Economy, 2, 1000f);
            GalaxyMap map = MakeSingleSystemMap(EmpireA, population: 1000, developmentLevel: 3, stability: 1f); // 6.9 pts/jour
            ResearchService research = MakeResearch(map, tier1, tier2);
            research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out _);

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(1, research.GetCompletedTierCount(EmpireA, ResearchDomain.Economy));
            Assert.AreEqual(1.9f, research.GetProgress(EmpireA, ResearchDomain.Economy), FloatTolerance, "6.9 - 5 (cout du palier 1) = 1.9 reportes sur le palier 2.");
        }

        [Test]
        public void DayAdvanced_MultipleTiersCompletedInOneDay_AllPublished()
        {
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 1f);
            TechnologyDefinition tier2 = MakeTechnology(ResearchDomain.Economy, 2, 1f);
            TechnologyDefinition tier3 = MakeTechnology(ResearchDomain.Economy, 3, 1f);
            GalaxyMap map = MakeSingleSystemMap(EmpireA, population: 1000, developmentLevel: 3, stability: 1f); // 6.9 pts/jour
            ResearchService research = MakeResearch(map, tier1, tier2, tier3);
            research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out _);

            int completedCount = 0;
            _eventBus.Subscribe<TechnologyResearchedEvent>(_ => completedCount++);

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(3, completedCount, "Les 3 paliers (cout 1 chacun) tiennent dans les 6.9 points generes.");
            Assert.IsNull(research.GetNextTechnology(EmpireA, ResearchDomain.Economy));
            Assert.AreEqual(0f, research.GetProgress(EmpireA, ResearchDomain.Economy), FloatTolerance, "Domaine au maximum : le reliquat n'est pas conserve.");
        }

        [Test]
        public void DayAdvanced_DomainAlreadyMaxed_PointsDiscardedWithoutException()
        {
            TechnologyDefinition onlyTier = MakeTechnology(ResearchDomain.Economy, 1, 1f);
            GalaxyMap map = MakeSingleSystemMap(EmpireA);
            ResearchService research = MakeResearch(map, onlyTier);
            research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out _);
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1))); // complete l'unique palier

            Assert.DoesNotThrow(() => _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(2))));
            Assert.AreEqual(0f, research.GetProgress(EmpireA, ResearchDomain.Economy), FloatTolerance);
        }

        // --- Bonus cumulatif -------------------------------------------------------------

        [Test]
        public void GetBonus_CumulativeAcrossCompletedTiers()
        {
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 1f, effect: 0.05f);
            TechnologyDefinition tier2 = MakeTechnology(ResearchDomain.Economy, 2, 1f, effect: 0.05f);
            GalaxyMap map = MakeSingleSystemMap(EmpireA, population: 1000, developmentLevel: 3, stability: 1f);
            ResearchService research = MakeResearch(map, tier1, tier2);
            research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out _);

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1))); // complete les deux paliers (cout 1 chacun, 6.9 generes)

            Assert.AreEqual(0.10f, research.GetBonus(EmpireA, ResearchDomain.Economy), FloatTolerance);
        }

        [Test]
        public void GetBonus_DifferentDomain_Unaffected()
        {
            TechnologyDefinition economyTier = MakeTechnology(ResearchDomain.Economy, 1, 1f);
            GalaxyMap map = MakeSingleSystemMap(EmpireA);
            ResearchService research = MakeResearch(map, economyTier);
            research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out _);
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.AreEqual(0f, research.GetBonus(EmpireA, ResearchDomain.Weapons), FloatTolerance);
        }

        // --- Independance des progressions ------------------------------------------------

        [Test]
        public void SwitchingActiveDomain_PreservesProgressOfPreviousDomain()
        {
            TechnologyDefinition economyTier = MakeTechnology(ResearchDomain.Economy, 1, 1000f);
            TechnologyDefinition weaponsTier = MakeTechnology(ResearchDomain.Weapons, 1, 1000f);
            GalaxyMap map = MakeSingleSystemMap(EmpireA, population: 1000, developmentLevel: 3, stability: 1f); // 6.9 pts/jour
            ResearchService research = MakeResearch(map, economyTier, weaponsTier);

            research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out _);
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));
            float economyProgressBeforeSwitch = research.GetProgress(EmpireA, ResearchDomain.Economy);

            research.TrySetActiveDomain(EmpireA, ResearchDomain.Weapons, out _);
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(2)));

            Assert.AreEqual(economyProgressBeforeSwitch, research.GetProgress(EmpireA, ResearchDomain.Economy), FloatTolerance, "Le domaine quitte garde sa progression, sans recevoir de nouveaux points.");
            Assert.Greater(research.GetProgress(EmpireA, ResearchDomain.Weapons), 0f);
        }

        [Test]
        public void DayAdvanced_TwoEmpires_IndependentProgress()
        {
            TechnologyDefinition tier1 = MakeTechnology(ResearchDomain.Economy, 1, 1000f);
            StarSystemState systemA = MakeSystem(0, EmpireA, population: 1000, developmentLevel: 3, stability: 1f);
            StarSystemState systemB = MakeSystem(1, EmpireB, population: 2000, developmentLevel: 3, stability: 1f);
            var map = new GalaxyMap(new[] { systemA, systemB }, Array.Empty<HyperlaneLink>());
            ResearchService research = MakeResearch(map, tier1);

            research.TrySetActiveDomain(EmpireA, ResearchDomain.Economy, out _);
            // EmpireB ne choisit jamais de domaine actif.

            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));

            Assert.Greater(research.GetProgress(EmpireA, ResearchDomain.Economy), 0f);
            Assert.AreEqual(0f, research.GetProgress(EmpireB, ResearchDomain.Economy), FloatTolerance, "EmpireB n'a jamais active de domaine : ses points (pourtant generes) ne vont nulle part.");
        }
    }
}
