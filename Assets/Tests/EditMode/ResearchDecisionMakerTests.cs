using System;
using System.Reflection;
using Espace.Core;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Research;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="ResearchDecisionMaker"/> : choisit le domaine actif selon la
    /// priorite de la personnalite, ignore les domaines deja au maximum, ne change rien si le
    /// domaine actif progresse encore.
    /// <para>
    /// Utilise le vrai <see cref="ResearchService"/> plutot qu'un faux : contrairement a
    /// l'opinion diplomatique (Phase 7), un domaine « deja au maximum » se simule simplement
    /// en ne lui donnant aucune entree de catalogue — pas besoin de simuler des jours de jeu.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ResearchDecisionMakerTests
    {
        private const int EmpireId = 1;

        private EventBus _eventBus;
        private GalaxyMap _map;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _eventBus.Initialize();

            var unowned = new StarSystemState(new StarSystemId(0), "System0", Vector2.zero, 1000, 500, 3, 1f, Array.Empty<Espace.Data.ResourceType>());
            _map = new GalaxyMap(new[] { unowned }, Array.Empty<HyperlaneLink>());
        }

        [TearDown]
        public void TearDown()
        {
            _eventBus.Shutdown();
        }

        private static TechnologyDefinition MakeTechnology(ResearchDomain domain, int tier = 1, float cost = 999999f)
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

        private ResearchService MakeResearch(params TechnologyDefinition[] catalog)
        {
            var research = new ResearchService(_map, _eventBus, catalog);
            research.Initialize();
            return research;
        }

        private static Empire MakeEmpire(EmpirePersonality personality) => new Empire(EmpireId, "Test", Color.white, personality, isPlayerControlled: false);

        [Test]
        public void DecideAndAct_NoActiveDomain_PicksTopPriorityAvailableDomain()
        {
            // Priorite Militariste : Weapons, Logistics, Industry, Energy, Economy, Espionage, Diplomacy.
            ResearchService research = MakeResearch(MakeTechnology(ResearchDomain.Weapons), MakeTechnology(ResearchDomain.Economy));
            Empire militarist = MakeEmpire(EmpirePersonality.Militarist);

            ResearchDecisionMaker.DecideAndAct(militarist, research, AssessmentFixtures.Healthy());

            Assert.AreEqual(ResearchDomain.Weapons, research.GetActiveDomain(EmpireId));
        }

        [Test]
        public void DecideAndAct_TopPriorityDomainUnavailable_PicksNextInOrder()
        {
            // Aucune entree de catalogue pour Weapons (domaine "vide" = deja au maximum) :
            // doit sauter au suivant dans l'ordre de preference du Militariste (Logistics).
            ResearchService research = MakeResearch(MakeTechnology(ResearchDomain.Logistics));
            Empire militarist = MakeEmpire(EmpirePersonality.Militarist);

            ResearchDecisionMaker.DecideAndAct(militarist, research, AssessmentFixtures.Healthy());

            Assert.AreEqual(ResearchDomain.Logistics, research.GetActiveDomain(EmpireId));
        }

        [Test]
        public void DecideAndAct_ActiveDomainStillProgressing_DoesNotSwitch()
        {
            ResearchService research = MakeResearch(MakeTechnology(ResearchDomain.Weapons), MakeTechnology(ResearchDomain.Logistics));
            Empire militarist = MakeEmpire(EmpirePersonality.Militarist);
            ResearchDecisionMaker.DecideAndAct(militarist, research, AssessmentFixtures.Healthy());
            Assert.AreEqual(ResearchDomain.Weapons, research.GetActiveDomain(EmpireId), "Precondition.");

            ResearchDecisionMaker.DecideAndAct(militarist, research, AssessmentFixtures.Healthy());

            Assert.AreEqual(ResearchDomain.Weapons, research.GetActiveDomain(EmpireId), "Le domaine actif progresse encore : pas de changement.");
        }

        [Test]
        public void DecideAndAct_ActiveDomainMaxed_SwitchesToNextPriorityDomain()
        {
            // Un seul palier tres bon marche pour Weapons : complete instantanement des le
            // premier jour de jeu genere par un systeme possede, puis DecideAndAct doit
            // basculer vers le domaine suivant de la liste de preference du Militariste.
            TechnologyDefinition cheapWeapons = MakeTechnology(ResearchDomain.Weapons, tier: 1, cost: 1f);
            TechnologyDefinition logistics = MakeTechnology(ResearchDomain.Logistics);

            var owned = new StarSystemState(new StarSystemId(1), "System1", Vector2.zero, 1000, 500, 3, 1f, Array.Empty<Espace.Data.ResourceType>());
            owned.OwnerId = EmpireId;
            var map = new GalaxyMap(new[] { owned }, Array.Empty<HyperlaneLink>());
            var research = new ResearchService(map, _eventBus, new[] { cheapWeapons, logistics });
            research.Initialize();
            research.TrySetActiveDomain(EmpireId, ResearchDomain.Weapons, out _);
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame.AddDays(1)));
            Assert.IsNull(research.GetNextTechnology(EmpireId, ResearchDomain.Weapons), "Precondition : Weapons doit etre au maximum.");

            Empire militarist = MakeEmpire(EmpirePersonality.Militarist);
            ResearchDecisionMaker.DecideAndAct(militarist, research, AssessmentFixtures.Healthy());

            Assert.AreEqual(ResearchDomain.Logistics, research.GetActiveDomain(EmpireId));
        }

        [Test]
        public void DecideAndAct_AllDomainsMaxed_DoesNothing()
        {
            ResearchService research = MakeResearch(); // catalogue vide : tous les domaines sont vacuously "au maximum"
            Empire militarist = MakeEmpire(EmpirePersonality.Militarist);

            Assert.DoesNotThrow(() => ResearchDecisionMaker.DecideAndAct(militarist, research, AssessmentFixtures.Healthy()));
            Assert.IsNull(research.GetActiveDomain(EmpireId));
        }

        [Test]
        public void DecideAndAct_DifferentPersonalities_RespectTheirOwnPriorityOrder()
        {
            ResearchService research = MakeResearch(MakeTechnology(ResearchDomain.Economy), MakeTechnology(ResearchDomain.Diplomacy));
            Empire mercantile = MakeEmpire(EmpirePersonality.Mercantile); // priorite : Economy, Diplomacy, ...

            ResearchDecisionMaker.DecideAndAct(mercantile, research, AssessmentFixtures.Healthy());

            Assert.AreEqual(ResearchDomain.Economy, research.GetActiveDomain(EmpireId));
        }
        // --- Posture strategique (Phase 22, P7) -------------------------------------------

        [Test]
        public void DecideAndAct_Threatened_InterruptsAProgressingDomainForWeapons()
        {
            // Le defaut corrige : la recherche etait le seul module totalement sourd a la
            // situation. Un empire menace d'invasion continuait a chercher la Diplomatie pendant
            // vingt ans parce que sa personnalite l'avait mise en tete de liste.
            ResearchService research = MakeResearch(MakeTechnology(ResearchDomain.Diplomacy), MakeTechnology(ResearchDomain.Weapons));
            Empire pacifist = MakeEmpire(EmpirePersonality.Pacifist); // priorite : Diplomacy d'abord
            ResearchDecisionMaker.DecideAndAct(pacifist, research, AssessmentFixtures.Healthy());
            Assert.AreEqual(ResearchDomain.Diplomacy, research.GetActiveDomain(EmpireId), "Precondition.");

            ResearchDecisionMaker.DecideAndAct(pacifist, research, AssessmentFixtures.Threatened());

            Assert.AreEqual(ResearchDomain.Weapons, research.GetActiveDomain(EmpireId));
        }

        [Test]
        public void DecideAndAct_ReturningToASafeSituation_GivesThePersonalityItsListBack()
        {
            // L'interruption ne doit pas etre definitive : la promotion est une exception
            // justifiee par une urgence, pas une reecriture de la personnalite. Le retour n'est
            // possible que parce que ResearchService conserve la progression domaine par
            // domaine — abandonner Weapons ne perd rien.
            ResearchService research = MakeResearch(MakeTechnology(ResearchDomain.Diplomacy), MakeTechnology(ResearchDomain.Weapons));
            Empire pacifist = MakeEmpire(EmpirePersonality.Pacifist);
            ResearchDecisionMaker.DecideAndAct(pacifist, research, AssessmentFixtures.Threatened());
            Assert.AreEqual(ResearchDomain.Weapons, research.GetActiveDomain(EmpireId), "Precondition.");

            ResearchDecisionMaker.DecideAndAct(pacifist, research, AssessmentFixtures.Healthy());

            Assert.AreEqual(
                ResearchDomain.Weapons, research.GetActiveDomain(EmpireId),
                "Weapons progresse encore : l'ancienne regle « on ne change pas sans raison » reprend la main.");
        }

        [Test]
        public void DecideAndAct_PromotedDomainAlreadyMaxed_FallsBackToThePersonalityOrder()
        {
            // Reorienter vers un domaine deja au maximum ferait perdre chaque point produit
            // (voir ResearchService, « points perdus si le domaine actif est deja au maximum »).
            // Catalogue sans Weapons : le domaine promu par la posture defensive n'a rien a offrir.
            ResearchService research = MakeResearch(MakeTechnology(ResearchDomain.Diplomacy));
            Empire pacifist = MakeEmpire(EmpirePersonality.Pacifist);

            ResearchDecisionMaker.DecideAndAct(pacifist, research, AssessmentFixtures.Threatened());

            Assert.AreEqual(ResearchDomain.Diplomacy, research.GetActiveDomain(EmpireId));
        }

        [Test]
        public void DecideAndAct_Broke_TurnsToEconomyWhateverThePersonality()
        {
            // Consolidating se declenche sur une tresorerie exsangue, et l'Economie est le seul
            // domaine qui agisse sur le probleme.
            ResearchService research = MakeResearch(MakeTechnology(ResearchDomain.Weapons), MakeTechnology(ResearchDomain.Economy));
            Empire militarist = MakeEmpire(EmpirePersonality.Militarist); // priorite : Weapons d'abord

            ResearchDecisionMaker.DecideAndAct(militarist, research, AssessmentFixtures.Broke());

            Assert.AreEqual(ResearchDomain.Economy, research.GetActiveDomain(EmpireId));
        }

    }
}
