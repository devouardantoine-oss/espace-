using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Decisions;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie la levee, la reponse et l'echeancier (Phase 24, etape 5).
    /// <para>
    /// Le catalogue est couvert par <see cref="DecisionTests"/>, sans service. Ce qui se joue ici
    /// est ce qu'une fonction pure ne peut pas porter : <b>une question ne se repose pas tant que
    /// l'ardoise precedente n'est pas soldee</b>, et <b>l'ardoise finit par tomber</b> — sans
    /// quoi le differe ne serait qu'une promesse, et l'option la plus chere a terme deviendrait
    /// la moins chere.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class DecisionServiceTests
    {
        private const int PlayerId = 0;

        private sealed class FakeClock : IGameClock
        {
            public GameDate CurrentDate { get; set; } = GameDate.StartOfGame;
            public GameSpeed CurrentSpeed => GameSpeed.Normal;
            public bool IsPaused { get; private set; }
            public float CurrentMultiplier => 1f;
            public void Pause() { IsPaused = true; }
            public void Resume() { IsPaused = false; }
            public void TogglePause() { IsPaused = !IsPaused; }
            public void SetSpeed(GameSpeed speed) { }
            public void ResetToStart() { CurrentDate = GameDate.StartOfGame; }
            public void Tick(float deltaSeconds) { }
            public void AdvanceDays(int days) { }
            public void SetDate(GameDate date) { CurrentDate = date; }
        }

        private sealed class FakeEconomy : IEconomyService
        {
            public float Credits;

            public ResourceBundle Treasury => new ResourceBundle(credits: Credits);
            public float TaxRate => 0f;
            public IReadOnlyList<BuildingType> BuildingCatalog => new BuildingType[0];

            public void Grant(int empireId, ResourceBundle amount) { Credits += amount.Credits; }

            public ResourceBundle GetTreasury(int empireId) => new ResourceBundle(credits: Credits);
            public float GetTaxRate(int empireId) => 0f;
            public float GetAdministrativePressure(int empireId) => 0f;
            public void SetTaxRate(float rate) { }
            public void SetTaxRate(int empireId, float rate) { }
            public bool TrySpend(int empireId, ResourceBundle cost, out string error) { error = null; return true; }
            public bool TryStartConstruction(StarSystemId systemId, BuildingType buildingType, out string error) { error = null; return false; }
            public float GetInvestmentCost(StarSystemId systemId) => 0f;
            public bool TryInvestInDevelopment(StarSystemId systemId, out string error) { error = null; return false; }
            public IReadOnlyList<BuildingInstance> GetBuildings(StarSystemId systemId) => new BuildingInstance[0];
            public void RestoreCompletedBuilding(StarSystemId systemId, BuildingType buildingType) { }
        }

        private sealed class SpyMilitary : IMilitaryService
        {
            public readonly List<float> Reductions = new List<float>();

            public int ReduceGarrison(StarSystemId systemId, int empireId, float lostFraction)
            {
                Reductions.Add(lostFraction);
                return 1;
            }

            public IReadOnlyList<UnitTypeDefinition> UnitCatalog => new UnitTypeDefinition[0];
            public bool TryGetStationedFleet(StarSystemId systemId, int empireId, out Fleet fleet) { fleet = null; return false; }
            public IReadOnlyList<Fleet> GetFleetsAt(StarSystemId systemId) => new Fleet[0];
            public IReadOnlyList<Fleet> GetFleetsForEmpire(int empireId) => new Fleet[0];
            public UnitBundle GetGarrison(StarSystemId systemId, int empireId) => UnitBundle.Zero;
            public float EstimatePower(UnitBundle composition) => composition.TotalCount;
            public bool TryRecruitUnits(StarSystemId systemId, UnitTypeDefinition unitType, int count, out string error) { error = null; return false; }
            public bool TryMoveFleet(Fleet fleet, StarSystemId destinationSystemId, out string error) { error = null; return false; }
            public bool CanDeployAnotherFleet(int empireId) => true;
            public bool TryDetachFleet(StarSystemId systemId, int empireId, UnitBundle unitsToDetach, out Fleet detachedFleet, out string error) { detachedFleet = null; error = null; return false; }
            public void RestoreGarrison(StarSystemId systemId, int empireId, UnitBundle composition, string fleetName = null, Admiral? admiral = null) { }
            public IReadOnlyList<Fleet> GetFleetsInTransit() => new Fleet[0];
            public void ClearFleetsInTransit() { }
            public void RestoreFleetInTransit(
                int empireId, UnitBundle composition, string fleetName, Admiral? admiral,
                IReadOnlyList<StarSystemId> route, int routeIndex, StarSystemId originSystemId,
                GameDate journeyStartDate, GameDate departureDate, GameDate legArrivalDate, bool isRetreating) { }
        }

        private EventBus _eventBus;
        private DecisionService _decisions;
        private FakeClock _clock;
        private FakeEconomy _economy;
        private SpyMilitary _military;
        private GalaxyMap _map;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _clock = new FakeClock();
            _economy = new FakeEconomy();
            _military = new SpyMilitary();

            _map = new GalaxyMap(
                new List<StarSystemState> { MakeSystem(1, 0.9f) },
                new List<HyperlaneLink>());

            ServiceLocator.Register<IGameClock>(_clock);
            ServiceLocator.Register<IEconomyService>(_economy);
            ServiceLocator.Register<IMilitaryService>(_military);

            _decisions = new DecisionService(_eventBus, _map);
            _decisions.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _decisions.Shutdown();
            ServiceLocator.Clear();
        }

        private static StarSystemState MakeSystem(int id, float stability)
        {
            var system = new StarSystemState(
                new StarSystemId(id), $"Systeme{id}", Vector2.zero,
                population: 100, wealth: 10, developmentLevel: 2,
                stability: stability, resourceDeposits: new ResourceType[0]);

            system.OwnerId = PlayerId;
            return system;
        }

        private void AdvanceOneDay()
        {
            _clock.CurrentDate = _clock.CurrentDate.AddDays(1);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate));
        }

        private void AdvanceDays(int days)
        {
            for (int i = 0; i < days; i++)
            {
                AdvanceOneDay();
            }
        }

        // --- Levee ---------------------------------------------------------------

        [Test]
        public void AHealthySystem_IsNeverAsked()
        {
            AdvanceOneDay();

            Assert.AreEqual(0, _decisions.Pending.Count);
        }

        [Test]
        public void ASystemBelowTheThreshold_PosesItsQuestion()
        {
            _map.Systems[0].Stability = DecisionCatalogue.UnrestThreshold - 0.05f;
            AdvanceOneDay();

            Assert.AreEqual(1, _decisions.Pending.Count);
            Assert.AreEqual(DecisionKind.Unrest, _decisions.Pending[0].Kind);
        }

        [Test]
        public void TheSameSystem_DoesNotAskTwiceWhileTheQuestionStands()
        {
            _map.Systems[0].Stability = 0.1f;

            AdvanceDays(5);

            Assert.AreEqual(1, _decisions.Pending.Count, "Cinq jours de troubles ne posent pas cinq fois la meme question.");
        }

        [Test]
        public void AForeignSystem_IsNeverAsked()
        {
            _map.Systems[0].Stability = 0.1f;
            _map.Systems[0].OwnerId = 4;

            AdvanceOneDay();

            Assert.AreEqual(0, _decisions.Pending.Count, "Le joueur ne repond que de ses propres systemes.");
        }

        // --- Reponse -------------------------------------------------------------

        [Test]
        public void Answering_AppliesTheImmediateCostAndClearsTheQuestion()
        {
            _map.Systems[0].Stability = 0.1f;
            AdvanceOneDay();

            // « Ceder » : 600 Credits tout de suite.
            PendingDecision decision = _decisions.Pending[0];
            int ceder = IndexOf(decision, "Ceder");

            Assert.IsTrue(_decisions.Answer(decision.Id, ceder));

            Assert.AreEqual(0, _decisions.Pending.Count);
            Assert.AreEqual(-600f, _economy.Credits, 0.01f);
            Assert.Greater(_map.Systems[0].Stability, 0.1f, "Ceder devait remonter la stabilite.");
        }

        [Test]
        public void Answering_CostsMenWhenTheOptionSaysSo()
        {
            _map.Systems[0].Stability = 0.1f;
            AdvanceOneDay();

            PendingDecision decision = _decisions.Pending[0];
            _decisions.Answer(decision.Id, IndexOf(decision, "Reprimer"));

            Assert.AreEqual(1, _military.Reductions.Count);
            Assert.AreEqual(0.25f, _military.Reductions[0], 0.001f);
        }

        [Test]
        public void Answering_TakesOnABill()
        {
            _map.Systems[0].Stability = 0.1f;
            AdvanceOneDay();

            _decisions.Answer(_decisions.Pending[0].Id, 0);

            Assert.AreEqual(1, _decisions.Scheduled.Count, "Toute option laisse une ardoise : c'est la regle du catalogue.");
        }

        [Test]
        public void AnUnknownDecisionOrOption_IsRefusedRatherThanGuessed()
        {
            _map.Systems[0].Stability = 0.1f;
            AdvanceOneDay();

            int id = _decisions.Pending[0].Id;

            Assert.IsFalse(_decisions.Answer(9999, 0), "Une decision inconnue ne doit rien appliquer.");
            Assert.IsFalse(_decisions.Answer(id, 42));
            Assert.IsFalse(_decisions.Answer(id, -1));
            Assert.AreEqual(1, _decisions.Pending.Count);
        }

        // --- L'ardoise -----------------------------------------------------------

        [Test]
        public void TheBillFalls_WhenItsDayComes()
        {
            _map.Systems[0].Stability = 0.1f;
            AdvanceOneDay();

            PendingDecision decision = _decisions.Pending[0];
            _decisions.Answer(decision.Id, IndexOf(decision, "Ceder"));

            _economy.Credits = 0f;

            AdvanceDays(DecisionCatalogue.DeferredDelayDays - 1);
            Assert.AreEqual(0f, _economy.Credits, 0.01f, "L'ardoise ne doit pas tomber avant son terme.");

            AdvanceDays(2);
            Assert.AreEqual(-900f, _economy.Credits, 0.01f, "Les voisins reclament leur du.");
            Assert.AreEqual(0, _decisions.Scheduled.Count);
        }

        [Test]
        public void ASystemIsLeftAlone_UntilItsBillIsSettled()
        {
            // La regle qui remplace un minuteur : l'echeance en cours *est* le delai de repos.
            _map.Systems[0].Stability = 0.1f;
            AdvanceOneDay();

            PendingDecision decision = _decisions.Pending[0];
            _decisions.Answer(decision.Id, IndexOf(decision, "Temporiser"));

            // Temporiser laisse la stabilite au plus bas : sans cette regle, la question
            // reviendrait des le lendemain.
            AdvanceDays(5);
            Assert.AreEqual(0, _decisions.Pending.Count, "Une ardoise en cours tient lieu de repit.");
        }

        [Test]
        public void OnceTheBillIsSettled_TheSystemCanBeAskedAgain()
        {
            _map.Systems[0].Stability = 0.1f;
            AdvanceOneDay();

            PendingDecision first = _decisions.Pending[0];
            _decisions.Answer(first.Id, IndexOf(first, "Temporiser"));

            AdvanceDays(DecisionCatalogue.DeferredDelayDays + 2);

            Assert.AreEqual(1, _decisions.Pending.Count, "La situation n'a pas ete reglee : la question revient.");
            Assert.AreNotEqual(first.Id, _decisions.Pending[0].Id, "Une nouvelle question, pas l'ancienne.");
        }

        // --- Sauvegarde -----------------------------------------------------------

        [Test]
        public void Restore_BringsBackQuestionsAndBills()
        {
            var pending = new List<PendingDecision>
            {
                DecisionCatalogue.Unrest(12, _map.Systems[0], GameDate.StartOfGame)
            };

            var scheduled = new List<ScheduledConsequence>
            {
                new ScheduledConsequence(
                    _map.Systems[0].Id, GameDate.StartOfGame.AddDays(10),
                    500f, 0f, -0.1f, "ardoise")
            };

            _decisions.Restore(pending, scheduled, nextId: 13);

            Assert.AreEqual(1, _decisions.Pending.Count);
            Assert.AreEqual(12, _decisions.Pending[0].Id);
            Assert.AreEqual(1, _decisions.Scheduled.Count);
            Assert.AreEqual(13, _decisions.NextId, "Un rechargement ne doit pas reattribuer un identifiant deja utilise.");
        }

        [Test]
        public void ARestoredBill_StillFalls()
        {
            // Le point qui compte pour la sauvegarde : un joueur qui enregistre apres avoir
            // choisi et recharge avant l'echeance ne doit pas echapper au cout.
            _decisions.Restore(
                new List<PendingDecision>(),
                new List<ScheduledConsequence>
                {
                    new ScheduledConsequence(
                        _map.Systems[0].Id, GameDate.StartOfGame.AddDays(3),
                        750f, 0f, 0f, "ardoise restauree")
                },
                nextId: 1);

            AdvanceDays(4);

            Assert.AreEqual(-750f, _economy.Credits, 0.01f);
            Assert.AreEqual(0, _decisions.Scheduled.Count);
        }

        [Test]
        public void Restore_ToleratesNothingToRestore()
        {
            Assert.DoesNotThrow(() => _decisions.Restore(null, null, 0));
            Assert.AreEqual(0, _decisions.Pending.Count);
            Assert.AreEqual(0, _decisions.Scheduled.Count);
            Assert.GreaterOrEqual(_decisions.NextId, 1, "Un identifiant nul rendrait deux decisions indiscernables.");
        }

        private static int IndexOf(PendingDecision decision, string label)
        {
            for (int i = 0; i < decision.Options.Count; i++)
            {
                if (decision.Options[i].Label == label)
                {
                    return i;
                }
            }

            Assert.Fail($"Option « {label} » absente du catalogue.");
            return -1;
        }
    }
}
