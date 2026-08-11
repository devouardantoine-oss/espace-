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
            public void ResetToStart() { }
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
        /// <paramref name="minimumAmount"/> de Credits, par des jours de production a richesse
        /// et impot gonfles — meme technique que dans <c>EconomyServiceTests</c>.
        /// <para>
        /// <b>Produit autant de jours qu'il en faut, au lieu d'un seul (Phase 22).</b> L'ancienne
        /// version fixait le taux a 1,0 et dimensionnait la richesse en supposant que la totalite
        /// du taux nominal se convertissait en Credits. Depuis <see cref="TaxationModel"/> (P1),
        /// ce n'est plus vrai : au-dela du seuil d'evasion, le taux <i>effectif</i> redescend, et
        /// un taux nominal de 1,0 n'en rend qu'environ un tiers. Le helper accordait donc trois
        /// fois moins que ce qu'il annoncait, et les tests qui s'appuyaient dessus verifiaient
        /// « l'IA n'a pas les moyens » en croyant verifier « l'IA a les moyens ».
        /// </para>
        /// <para>
        /// Boucler jusqu'au montage voulu rend le helper <b>independant de la courbe fiscale</b> :
        /// un futur reglage de l'evasion ne le cassera pas une seconde fois.
        /// </para>
        /// </summary>
        private float GiveCredits(EconomyService service, StarSystemState system, int empireId, float minimumAmount)
        {
            const int MaximumDays = 200;

            int originalWealth = system.Wealth;
            float originalTax = service.GetTaxRate(empireId);

            system.Wealth = Mathf.CeilToInt(minimumAmount / 0.05f) + 1;
            service.SetTaxRate(empireId, 1f);

            int day = 0;
            while (service.GetTreasury(empireId).Credits < minimumAmount && day < MaximumDays)
            {
                day++;
                _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(day)));
            }

            float granted = service.GetTreasury(empireId).Credits;
            Assert.GreaterOrEqual(
                granted, minimumAmount,
                $"Le helper n'a pas pu produire {minimumAmount} Credits en {MaximumDays} jours : le test verifierait autre chose que ce qu'il annonce.");

            system.Wealth = originalWealth;
            service.SetTaxRate(empireId, originalTax);

            return granted;
        }

        /// <summary>
        /// Taux applique par chaque personnalite dans une situation saine.
        /// <para>
        /// <b>Ce test verifiait auparavant que le taux <i>etait</i> celui de la personnalite</b>
        /// (0,20 pour un Pacifiste, 0,35 pour un Mercantile...). Depuis la Phase 22 (P7), la
        /// personnalite ne fixe plus le taux : elle <b>decale</b> un taux issu de la situation
        /// (voir <c>AIDecisionMaker.BlendTaxRate</c>, 66 % situation / 34 % temperament). Les
        /// valeurs attendues ci-dessous sont ce melange sous la posture <c>Expanding</c>, dont le
        /// taux suggere est 0,22.
        /// </para>
        /// </summary>
        [TestCase(EmpirePersonality.Pacifist, 0.2132f)]
        [TestCase(EmpirePersonality.Expansionist, 0.2132f)]
        [TestCase(EmpirePersonality.Mercantile, 0.2642f)]
        [TestCase(EmpirePersonality.Militarist, 0.2472f)]
        [TestCase(EmpirePersonality.Opportunist, 0.2302f)]
        public void DecideAndAct_BlendsThePersonalityIntoTheSituationalTaxRate(EmpirePersonality personality, float expectedRate)
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            Empire empire = MakeEmpire(personality);

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

            Assert.AreEqual(expectedRate, service.GetTaxRate(AiEmpireId), FloatTolerance);
        }

        [Test]
        public void DecideAndAct_PersonalityStillOrdersTheTaxRates()
        {
            // La propriete qui compte, au-dela des valeurs exactes : le temperament doit rester
            // observable. Si le melange l'ecrasait, les cinq personnalites deviendraient
            // indiscernables a l'ecran et le decalage n'aurait plus de raison d'exister.
            Assert.Greater(TaxRateOf(EmpirePersonality.Mercantile), TaxRateOf(EmpirePersonality.Militarist));
            Assert.Greater(TaxRateOf(EmpirePersonality.Militarist), TaxRateOf(EmpirePersonality.Opportunist));
            Assert.Greater(TaxRateOf(EmpirePersonality.Opportunist), TaxRateOf(EmpirePersonality.Pacifist));
        }

        [Test]
        public void DecideAndAct_BrokeEmpireTaxesHarderThanAHealthyOne_WhateverItsTemperament()
        {
            // Le vrai apport de P7 : c'est la situation qui commande. Un Pacifiste a sec doit
            // serrer la vis plus qu'un Mercantile prospere, alors que l'ancien systeme donnait
            // l'inverse en toutes circonstances.
            Assert.Greater(
                TaxRateOf(EmpirePersonality.Pacifist, AssessmentFixtures.Broke()),
                TaxRateOf(EmpirePersonality.Mercantile, AssessmentFixtures.Healthy()));
        }

        /// <summary>Taux d'imposition retenu par <paramref name="personality"/> dans une situation donnee.</summary>
        private float TaxRateOf(EmpirePersonality personality, EmpireAssessment? assessment = null)
        {
            StarSystemState system = MakeOwnedSystem(AiEmpireId);
            GalaxyMap map = MakeMap(system);
            var service = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();

            AIDecisionMaker.DecideAndAct(MakeEmpire(personality), map, service, assessment ?? AssessmentFixtures.Healthy());

            return service.GetTaxRate(AiEmpireId);
        }

        [Test]
        public void DecideAndAct_EmpireOwnsNoSystem_DoesNothing()
        {
            StarSystemState unowned = new StarSystemState(new StarSystemId(0), "Unowned", Vector2.zero, 1000, 500, 3, 1f, Array.Empty<ResourceType>());
            GalaxyMap map = MakeMap(unowned);
            var service = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            Empire empire = MakeEmpire(EmpirePersonality.Militarist);

            Assert.DoesNotThrow(() => AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy()));
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

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

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

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

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

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

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
            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy()); // construit Credits (priorite 1)

            GiveCredits(service, system, AiEmpireId, 1000f);
            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy()); // Credits deja construit -> Energie

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

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

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

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

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

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

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

            Assert.DoesNotThrow(() => AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy()));
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

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

            Assert.AreEqual(1, service.GetBuildings(system.Id).Count);
            Assert.AreEqual(3, system.DevelopmentLevel, "Un seul type d'action par appel : la construction a eu lieu, pas l'investissement.");
        }

        // --- Multi-systeme (Phase 18) -----------------------------------------------------

        /// <summary>Systeme possede a un identifiant choisi, pour composer un empire a plusieurs systemes.</summary>
        private static StarSystemState MakeOwnedSystemWithId(int id, int ownerId, int developmentLevel, int wealth = 500)
        {
            var system = new StarSystemState(
                new StarSystemId(id), $"S{id}", new Vector2(id * 10f, 0f), 1000, wealth, developmentLevel, 1f, Array.Empty<ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        [Test]
        public void DecideAndAct_InvestsInTheLeastDevelopedSystem()
        {
            // Le defaut corrige par la Phase 18 : la colonie restait au developpement 0 a
            // jamais, l'IA ne s'occupant que d'un seul systeme.
            StarSystemState capital = MakeOwnedSystemWithId(0, AiEmpireId, developmentLevel: 3);
            StarSystemState colony = MakeOwnedSystemWithId(1, AiEmpireId, developmentLevel: 0);
            GalaxyMap map = MakeMap(capital, colony);
            var service = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            GiveCredits(service, capital, AiEmpireId, 100000f);
            Empire empire = MakeEmpire(EmpirePersonality.Expansionist);

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

            Assert.AreEqual(1, colony.DevelopmentLevel, "La colonie en retard doit rattraper : c'est aussi le moins cher.");
            Assert.AreEqual(3, capital.DevelopmentLevel, "Une seule action par appel.");
        }

        [Test]
        public void DecideAndAct_Militarist_InvestsInItsCapitalInstead()
        {
            // Seule personnalite a DevelopsCapitalFirst : ses meilleures unites exigent un
            // developpement 4, cinq systemes mediocres ne lui donneraient aucun Cuirasse.
            StarSystemState capital = MakeOwnedSystemWithId(0, AiEmpireId, developmentLevel: 3);
            StarSystemState colony = MakeOwnedSystemWithId(1, AiEmpireId, developmentLevel: 0);
            GalaxyMap map = MakeMap(capital, colony);
            var service = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            GiveCredits(service, capital, AiEmpireId, 100000f);
            Empire empire = MakeEmpire(EmpirePersonality.Militarist);

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

            Assert.AreEqual(4, capital.DevelopmentLevel, "Le Militariste concentre son effort sur son bastion.");
            Assert.AreEqual(0, colony.DevelopmentLevel);
        }

        [Test]
        public void DecideAndAct_UndevelopedColony_IsAlwaysAffordableToDevelop()
        {
            // La regle de rattrapage ne peut jamais faire perdre un mois : une colonie neuve ne
            // peut souvent rien construire (aucun batiment n'atteint son developpement requis),
            // mais son investissement — (niveau + 1) x 200 — est par construction le moins cher
            // de l'empire. Aucun repli sur un autre systeme n'est donc necessaire.
            StarSystemState capital = MakeOwnedSystemWithId(0, AiEmpireId, developmentLevel: 5);
            StarSystemState colony = MakeOwnedSystemWithId(1, AiEmpireId, developmentLevel: 0);
            BuildingType advancedBuilding = MakeBuildingType("Avance", ResourceType.Food, 3f, 50f, minDevelopment: 5);
            GalaxyMap map = MakeMap(capital, colony);
            var service = new EconomyService(map, _clock, _eventBus, new[] { advancedBuilding });
            service.Initialize();
            GiveCredits(service, capital, AiEmpireId, 100000f);
            Empire empire = MakeEmpire(EmpirePersonality.Pacifist); // priorite Food, rattrapage

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

            Assert.AreEqual(1, colony.DevelopmentLevel, "Le mois n'est jamais perdu : la colonie est developpee.");
            Assert.AreEqual(0, service.GetBuildings(capital.Id).Count, "Une seule action par appel.");
        }

        [Test]
        public void DecideAndAct_AllSystemsEquallyDeveloped_ActsOnTheLowestId()
        {
            // A developpement egal, le rattrapage et la capitale designent le meme systeme :
            // le departage sur l'identifiant est la seule regle, aucun tirage.
            StarSystemState first = MakeOwnedSystemWithId(0, AiEmpireId, developmentLevel: 2);
            StarSystemState second = MakeOwnedSystemWithId(1, AiEmpireId, developmentLevel: 2);
            GalaxyMap map = MakeMap(second, first); // ordre de carte volontairement inverse
            var service = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            service.Initialize();
            GiveCredits(service, first, AiEmpireId, 100000f);
            Empire empire = MakeEmpire(EmpirePersonality.Expansionist);

            AIDecisionMaker.DecideAndAct(empire, map, service, AssessmentFixtures.Healthy());

            Assert.AreEqual(3, first.DevelopmentLevel);
            Assert.AreEqual(2, second.DevelopmentLevel);
        }

    }
}
