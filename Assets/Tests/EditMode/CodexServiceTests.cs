using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Chronicle;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Research;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie la delivrance et la memoire des fragments (Phase 24, etape 3).
    /// <para>
    /// Les regles elles-memes sont couvertes par <see cref="CodexTests"/>, sans service ni scene.
    /// Ce qui se joue ici est ce que la fonction pure ne peut pas porter : <b>l'obtention est
    /// definitive</b>, elle n'arrive <b>qu'une fois</b>, et le joueur ne recupere jamais ses
    /// propres archives.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class CodexServiceTests
    {
        private const int PlayerId = 0;
        private const int KethraId = 1;
        private const int OskarId = 2;

        /// <summary>Seule <c>GetAdministrativePressure</c> compte ici ; le reste satisfait le compilateur.</summary>
        private sealed class FakeEconomy : IEconomyService
        {
            public float Pressure;

            public ResourceBundle Treasury => ResourceBundle.Zero;
            public float TaxRate => 0f;
            public IReadOnlyList<BuildingType> BuildingCatalog => new BuildingType[0];

            public float GetAdministrativePressure(int empireId) => Pressure;

            public ResourceBundle GetTreasury(int empireId) => ResourceBundle.Zero;
            public float GetTaxRate(int empireId) => 0f;
            public void SetTaxRate(float rate) { }
            public void SetTaxRate(int empireId, float rate) { }
            public bool TrySpend(int empireId, ResourceBundle cost, out string error) { error = null; return true; }
            public void Grant(int empireId, ResourceBundle amount) { }
            public bool TryStartConstruction(StarSystemId systemId, BuildingType buildingType, out string error) { error = null; return false; }
            public float GetInvestmentCost(StarSystemId systemId) => 0f;
            public bool TryInvestInDevelopment(StarSystemId systemId, out string error) { error = null; return false; }
            public IReadOnlyList<BuildingInstance> GetBuildings(StarSystemId systemId) => new BuildingInstance[0];
            public void RestoreCompletedBuilding(StarSystemId systemId, BuildingType buildingType) { }
        }

        private EventBus _eventBus;
        private CodexService _codex;
        private FakeEconomy _economy;
        private GalaxyMap _map;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _economy = new FakeEconomy();

            // Trois systemes, un par empire : de quoi rendre un empire aneantissable en changeant
            // un seul proprietaire.
            var systems = new List<StarSystemState>
            {
                MakeSystem(1, PlayerId),
                MakeSystem(2, KethraId),
                MakeSystem(3, OskarId)
            };

            _map = new GalaxyMap(systems, new List<HyperlaneLink>());

            var empires = new List<Empire>
            {
                new Empire(PlayerId, "Joueur", Color.blue, EmpirePersonality.Expansionist, true, FactionLineage.Aube),
                new Empire(KethraId, "Kethra", Color.red, EmpirePersonality.Expansionist, false, FactionLineage.Kethra),
                new Empire(OskarId, "Oskar", Color.green, EmpirePersonality.Mercantile, false, FactionLineage.Oskar)
            };

            ServiceLocator.Register<IEconomyService>(_economy);
            ServiceLocator.Register(new EmpireRegistry(empires));

            _codex = new CodexService(_eventBus, _map);
            _codex.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _codex.Shutdown();
            ServiceLocator.Clear();
        }

        private static StarSystemState MakeSystem(int id, int ownerId)
        {
            return new StarSystemState(
                new StarSystemId(id), $"Systeme{id}", Vector2.zero,
                population: 100, wealth: 10, developmentLevel: 1, stability: 1f,
                resourceDeposits: new ResourceType[0])
            {
                OwnerId = ownerId
            };
        }

        private void AdvanceOneDay()
        {
            _eventBus.Publish(new DayAdvancedEvent(GameDate.StartOfGame));
        }

        // --- Delivrance ----------------------------------------------------------

        [Test]
        public void NewGame_StartsWithAnEmptyCodex()
        {
            AdvanceOneDay();

            Assert.AreEqual(0, _codex.UnlockedCount, "Une partie qui commence ne doit rien offrir.");
        }

        [Test]
        public void RisingPressure_DeliversTheFirstFragment()
        {
            _economy.Pressure = 0.05f;
            AdvanceOneDay();

            Assert.IsTrue(_codex.IsUnlocked(1));
            Assert.IsFalse(_codex.IsUnlocked(2), "Le seuil du second n'est pas franchi.");
        }

        [Test]
        public void AFragmentIsNeverDeliveredTwice()
        {
            _economy.Pressure = 0.05f;

            AdvanceOneDay();
            AdvanceOneDay();
            AdvanceOneDay();

            Assert.AreEqual(1, _codex.UnlockedCount, "Trois jours de pression ne donnent pas trois fois le meme fragment.");
        }

        [Test]
        public void AnObtainedFragment_SurvivesTheEmpireShrinking()
        {
            // Le point le plus important de ce service. La pression retombe des que l'empire se
            // contracte — mais on ne desapprend pas ce qu'on a lu. Sans cette permanence, un
            // joueur qui perd deux systemes verrait son codex se vider, ce qui n'a aucun sens.
            _economy.Pressure = 0.3f;
            AdvanceOneDay();

            int obtained = _codex.UnlockedCount;
            Assert.Greater(obtained, 0);

            _economy.Pressure = 0f;
            AdvanceOneDay();

            Assert.AreEqual(obtained, _codex.UnlockedCount, "Un fragment obtenu ne se reprend pas.");
        }

        // --- Archives ------------------------------------------------------------

        [Test]
        public void LosingItsLastSystem_DeliversAFactionArchives()
        {
            CodexFragment kethra = CodexLibrary.ByNumber(9);
            Assert.AreEqual(FactionLineage.Kethra, kethra.Lineage);

            AdvanceOneDay();
            Assert.IsFalse(_codex.IsUnlocked(kethra.Number), "Kethra possede encore un systeme.");

            _map.Systems[1].OwnerId = PlayerId;
            AdvanceOneDay();

            Assert.IsTrue(_codex.IsUnlocked(kethra.Number));
        }

        [Test]
        public void OnlyTheAnnihilatedFactionGivesUpItsArchives()
        {
            _map.Systems[1].OwnerId = PlayerId;
            AdvanceOneDay();

            Assert.IsTrue(_codex.IsUnlocked(9), "Kethra est aneantie.");
            Assert.IsFalse(_codex.IsUnlocked(6), "Oskar tient encore son systeme.");
        }

        [Test]
        public void ThePlayerNeverRecoversTheirOwnArchives()
        {
            // Un joueur reduit a rien a perdu la partie ; il n'a pas mis la main sur ses propres
            // registres. Sans cette exclusion, la defaite delivrerait un fragment — et la
            // Federation de l'Aube n'en a de toute facon aucun a donner.
            _map.Systems[0].OwnerId = KethraId;
            AdvanceOneDay();

            foreach (CodexFragment fragment in CodexLibrary.All)
            {
                if (fragment.TriggerKind == FragmentTriggerKind.FactionAnnihilated)
                {
                    Assert.IsFalse(
                        _codex.IsUnlocked(fragment.Number),
                        $"Fragment {fragment.Numeral} delivre par la defaite du joueur.");
                }
            }
        }

        [Test]
        public void PlayingAnotherFaction_MakesTheAubeArchivesReachable()
        {
            // La raison d'etre du fragment XII. Tant que l'Aube n'avait pas d'archives, un joueur
            // qui ne la jouait pas plafonnait une unite plus bas que les autres, sans que rien ne
            // le lui dise. Ici le joueur est Vharin : l'Aube devient une cible comme une autre.
            CodexFragment aube = CodexLibrary.ByNumber(12);
            Assert.AreEqual(FactionLineage.Aube, aube.Lineage);

            ServiceLocator.Unregister<EmpireRegistry>();
            ServiceLocator.Register(new EmpireRegistry(new List<Empire>
            {
                new Empire(PlayerId, "Joueur", Color.blue, EmpirePersonality.Pacifist, true, FactionLineage.Vharin),
                new Empire(KethraId, "Aube", Color.white, EmpirePersonality.Expansionist, false, FactionLineage.Aube),
                new Empire(OskarId, "Oskar", Color.green, EmpirePersonality.Mercantile, false, FactionLineage.Oskar)
            }));

            AdvanceOneDay();
            Assert.IsFalse(_codex.IsUnlocked(aube.Number), "L'Aube tient encore un systeme.");

            _map.Systems[1].OwnerId = PlayerId;
            AdvanceOneDay();

            Assert.IsTrue(_codex.IsUnlocked(aube.Number));
        }

        [Test]
        public void OnesOwnArchives_StayBeyondReachWhicheverFactionIsPlayed()
        {
            // Meme situation, mais c'est Vharin — la faction du joueur — qui disparaitrait. Le
            // service exclut le joueur du balayage, donc rien ne doit se debloquer.
            ServiceLocator.Unregister<EmpireRegistry>();
            ServiceLocator.Register(new EmpireRegistry(new List<Empire>
            {
                new Empire(PlayerId, "Joueur", Color.blue, EmpirePersonality.Pacifist, true, FactionLineage.Vharin),
                new Empire(KethraId, "Aube", Color.white, EmpirePersonality.Expansionist, false, FactionLineage.Aube),
                new Empire(OskarId, "Oskar", Color.green, EmpirePersonality.Mercantile, false, FactionLineage.Oskar)
            }));

            _map.Systems[0].OwnerId = KethraId;
            AdvanceOneDay();

            Assert.IsFalse(_codex.IsUnlocked(8), "Vharin est la faction du joueur : ses archives restent hors d'atteinte.");
        }

        // --- Restauration --------------------------------------------------------

        [Test]
        public void Restore_BringsBackWhatWasSaved()
        {
            _codex.Restore(new[] { 2, 5, 9 });

            Assert.AreEqual(3, _codex.UnlockedCount);
            Assert.IsTrue(_codex.IsUnlocked(5));
            Assert.IsFalse(_codex.IsUnlocked(4));
        }

        [Test]
        public void Restore_IgnoresNumbersItDoesNotKnow()
        {
            // Une sauvegarde ecrite par une version qui comptait plus de fragments ne doit pas
            // empecher de charger la partie.
            _codex.Restore(new[] { 1, 99, -3, 0, CodexLibrary.Count + 1 });

            Assert.AreEqual(1, _codex.UnlockedCount);
            Assert.IsTrue(_codex.IsUnlocked(1));
        }

        [Test]
        public void Restore_CollapsesDuplicates()
        {
            _codex.Restore(new[] { 3, 3, 3 });

            Assert.AreEqual(1, _codex.UnlockedCount);
        }

        [Test]
        public void Restore_ToleratesNothingToRestore()
        {
            _codex.Restore(new[] { 4 });
            _codex.Restore(null);

            Assert.AreEqual(0, _codex.UnlockedCount, "Restaurer rien remet le codex a zero, sans lever.");
        }

        [Test]
        public void AnOldSave_RecoversItsFragmentsFromTheStateOfTheWorld()
        {
            // La raison pour laquelle la version 5 du format n'a besoin d'aucune migration : une
            // partie enregistree sans codex retrouve des le lendemain ce que sa situation
            // justifie, puisque les regles relisent l'etat plutot qu'un historique.
            _codex.Restore(new int[0]);
            _economy.Pressure = 0.3f;

            AdvanceOneDay();

            Assert.Greater(_codex.UnlockedCount, 0);
        }
    }
}
