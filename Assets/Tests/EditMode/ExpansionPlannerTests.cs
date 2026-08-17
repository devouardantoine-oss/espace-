using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="ExpansionPlanner"/> (Phase 18) : choix determinist d'une cible de
    /// colonisation ou d'offensive dans tout le rayon d'expansion, et surtout le fait qu'aucun
    /// plan n'est renvoye tant qu'il n'est pas executable — c'est ce qui empeche l'IA de
    /// detacher une flotte qu'elle ne pourrait pas faire partir, et donc de fragmenter ses
    /// garnisons mois apres mois.
    /// </summary>
    [TestFixture]
    public sealed class ExpansionPlannerTests
    {
        private const int EmpireId = 3;
        private const int RivalId = 4;
        private const int Unowned = StarSystemState.UnownedOwnerId;
        private const int MinimumGarrisonToKeep = 2;

        private sealed class FakeMilitaryService : IMilitaryService
        {
            private readonly Dictionary<(StarSystemId, int), UnitBundle> _garrisons =
                new Dictionary<(StarSystemId, int), UnitBundle>();

            public void SetGarrison(StarSystemId systemId, int empireId, UnitBundle composition) =>
                _garrisons[(systemId, empireId)] = composition;

            public UnitBundle GetGarrison(StarSystemId systemId, int empireId) =>
                _garrisons.TryGetValue((systemId, empireId), out UnitBundle composition) ? composition : UnitBundle.Zero;

            public float EstimatePower(UnitBundle composition) => composition.TotalCount;

            // Sans objet ici : aucun de ces tests ne fait perdre de garnison.
            public int ReduceGarrison(StarSystemId systemId, int empireId, float lostFraction) => 0;

            public IReadOnlyList<UnitTypeDefinition> UnitCatalog => Array.Empty<UnitTypeDefinition>();
            public bool TryGetStationedFleet(StarSystemId systemId, int empireId, out Fleet fleet) { fleet = null; return false; }
            public IReadOnlyList<Fleet> GetFleetsAt(StarSystemId systemId) => Array.Empty<Fleet>();
            public IReadOnlyList<Fleet> GetFleetsForEmpire(int empireId) => Array.Empty<Fleet>();
            public bool TryRecruitUnits(StarSystemId systemId, UnitTypeDefinition unitType, int count, out string error) { error = null; return false; }
            public bool TryMoveFleet(Fleet fleet, StarSystemId destinationSystemId, out string error) { error = null; return false; }
            public bool CanDeployAnotherFleet(int empireId) => true;
            public bool TryDetachFleet(StarSystemId systemId, int empireId, UnitBundle unitsToDetach, out Fleet detachedFleet, out string error)
            {
                detachedFleet = null;
                error = null;
                return false;
            }

            public void RestoreGarrison(StarSystemId systemId, int empireId, UnitBundle composition, string fleetName = null, Admiral? admiral = null) { }
            public IReadOnlyList<Fleet> GetFleetsInTransit() => Array.Empty<Fleet>();
            public void ClearFleetsInTransit() { }
            public void RestoreFleetInTransit(
                int empireId, UnitBundle composition, string fleetName, Admiral? admiral,
                IReadOnlyList<StarSystemId> route, int routeIndex, StarSystemId originSystemId,
                GameDate journeyStartDate, GameDate departureDate, GameDate legArrivalDate, bool isRetreating) { }
        }

        /// <summary>Seul le statut guerre/paix compte ici, directement pilotable.</summary>
        private sealed class FakeDiplomacyService : IDiplomacyService
        {
            private readonly Dictionary<(int, int), DiplomaticStatus> _statuses = new Dictionary<(int, int), DiplomaticStatus>();

            private static (int, int) Key(int a, int b) => a <= b ? (a, b) : (b, a);

            public void SetStatus(int a, int b, DiplomaticStatus status) => _statuses[Key(a, b)] = status;

            public DiplomaticStatus GetStatus(int empireAId, int empireBId) =>
                _statuses.TryGetValue(Key(empireAId, empireBId), out DiplomaticStatus status) ? status : DiplomaticStatus.Peace;

            public float GetOpinion(int observerId, int targetId) => 0f;
            public bool HasTradeTreaty(int empireAId, int empireBId) => false;
            public bool IsEmbargoing(int fromEmpireId, int toEmpireId) => false;
            public IReadOnlyList<DiplomaticProposal> GetPendingProposalsFor(int empireId) => Array.Empty<DiplomaticProposal>();
            public bool TryDeclareWar(int declarerId, int targetId, out string error) { error = null; return false; }
            public bool TrySetEmbargo(int fromEmpireId, int toEmpireId, bool active, out string error) { error = null; return false; }
            public bool TryBreakPact(int fromEmpireId, int toEmpireId, out string error) { error = null; return false; }
            public bool TrySubmitProposal(
                int proposerId, int targetId, ProposalType type, ResourceBundle offeredResources, ResourceBundle requestedResources,
                StarSystemId? offeredSystemId, StarSystemId? requestedSystemId, out string error)
            {
                error = null;
                return false;
            }

            public bool TryRespondToProposal(int proposalId, bool accept, out string error) { error = null; return false; }
            public void ApplyOpinionShift(int observerId, int targetId, float delta) { }
            public void RestoreRelations(int empireAId, int empireBId, DiplomaticStatus status, bool hasTradeTreaty) { }
            public void RestoreOpinion(int observerId, int targetId, float value) { }
            public void RestoreEmbargo(int fromEmpireId, int toEmpireId) { }
        }

        private static StarSystemState MakeSystem(int id, int ownerId, int population = 1000, int developmentLevel = 0)
        {
            var system = new StarSystemState(
                new StarSystemId(id), $"S{id}", new Vector2(id * 10f, 0f), population, 500, developmentLevel, 1f, Array.Empty<ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        private static GalaxyMap MakeMap(StarSystemState[] systems, params (int, int)[] links)
        {
            var hyperlanes = new List<HyperlaneLink>();
            foreach ((int a, int b) in links)
            {
                hyperlanes.Add(new HyperlaneLink(new StarSystemId(a), new StarSystemId(b)));
            }

            return new GalaxyMap(systems, hyperlanes);
        }

        // --- Colonisation -----------------------------------------------------------------

        [Test]
        public void TryPlanColonization_ReachesBeyondDirectNeighbours()
        {
            // 0(a nous) - 1(libre) - 2(libre) : avant la Phase 18, seul le 1 etait envisageable.
            GalaxyMap map = MakeMap(
                new[] { MakeSystem(0, EmpireId), MakeSystem(1, Unowned, population: 3000), MakeSystem(2, Unowned, population: 1000) },
                (0, 1), (1, 2));

            var military = new FakeMilitaryService();
            military.SetGarrison(new StarSystemId(0), EmpireId, UnitBundle.Of(UnitType.Infantry, 6));

            Assert.IsTrue(ExpansionPlanner.TryPlanColonization(
                EmpireId, map, military, maxHops: 3, MinimumGarrisonToKeep, out ExpansionPlanner.ExpansionPlan plan));

            // Le systeme 2 est moins exigeant (1 fantassin) que le 1 (3 fantassins) : c'est lui
            // qui est vise, meme s'il est plus loin. L'exigence passe avant la distance.
            Assert.AreEqual(new StarSystemId(2), plan.TargetId);
            Assert.AreEqual(2, plan.Hops);
            Assert.AreEqual(1, plan.Force.Infantry);
        }

        [Test]
        public void TryPlanColonization_PrefersTheNearerTargetAtEqualRequirement()
        {
            GalaxyMap map = MakeMap(
                new[] { MakeSystem(0, EmpireId), MakeSystem(1, Unowned), MakeSystem(2, Unowned) },
                (0, 1), (1, 2));

            var military = new FakeMilitaryService();
            military.SetGarrison(new StarSystemId(0), EmpireId, UnitBundle.Of(UnitType.Infantry, 6));

            Assert.IsTrue(ExpansionPlanner.TryPlanColonization(
                EmpireId, map, military, maxHops: 3, MinimumGarrisonToKeep, out ExpansionPlanner.ExpansionPlan plan));

            Assert.AreEqual(new StarSystemId(1), plan.TargetId);
        }

        [Test]
        public void TryPlanColonization_NotEnoughInfantry_ReturnsNoPlan()
        {
            // Le point critique de la phase : pas de plan = pas de detachement, donc pas de
            // flotte orpheline. TryDetachFleet n'a aucun inverse.
            GalaxyMap map = MakeMap(
                new[] { MakeSystem(0, EmpireId), MakeSystem(1, Unowned, population: 6000) },
                (0, 1));

            var military = new FakeMilitaryService();
            military.SetGarrison(new StarSystemId(0), EmpireId, UnitBundle.Of(UnitType.Infantry, 2));

            Assert.IsFalse(ExpansionPlanner.TryPlanColonization(
                EmpireId, map, military, maxHops: 3, MinimumGarrisonToKeep, out _));
        }

        [Test]
        public void TryPlanColonization_WouldStripTheOriginGarrison_ReturnsNoPlan()
        {
            // 3 fantassins, exigence 1 : partir en laisserait 2, soit exactement la reserve.
            // Avec 2 fantassins seulement, il n'en resterait qu'un : refus.
            GalaxyMap map = MakeMap(new[] { MakeSystem(0, EmpireId), MakeSystem(1, Unowned) }, (0, 1));

            var military = new FakeMilitaryService();
            military.SetGarrison(new StarSystemId(0), EmpireId, UnitBundle.Of(UnitType.Infantry, 2));

            Assert.IsFalse(ExpansionPlanner.TryPlanColonization(
                EmpireId, map, military, maxHops: 3, MinimumGarrisonToKeep, out _));

            military.SetGarrison(new StarSystemId(0), EmpireId, UnitBundle.Of(UnitType.Infantry, 3));

            Assert.IsTrue(ExpansionPlanner.TryPlanColonization(
                EmpireId, map, military, maxHops: 3, MinimumGarrisonToKeep, out _));
        }

        [Test]
        public void TryPlanColonization_OriginIsTheNearestOwnedSystem()
        {
            // 0(a nous) - 1(libre) - 2(a nous) : la cible 1 doit partir du systeme 0 ou 2, celui
            // dont la garnison est reellement consultee. Le parcours rattache 1 au systeme 0.
            GalaxyMap map = MakeMap(
                new[] { MakeSystem(0, EmpireId), MakeSystem(1, Unowned), MakeSystem(2, EmpireId) },
                (0, 1), (1, 2));

            var military = new FakeMilitaryService();
            military.SetGarrison(new StarSystemId(0), EmpireId, UnitBundle.Of(UnitType.Infantry, 5));

            Assert.IsTrue(ExpansionPlanner.TryPlanColonization(
                EmpireId, map, military, maxHops: 3, MinimumGarrisonToKeep, out ExpansionPlanner.ExpansionPlan plan));

            Assert.AreEqual(new StarSystemId(0), plan.Origin.Id);
        }

        [Test]
        public void TryPlanColonization_NoFreeSystemInRange_ReturnsNoPlan()
        {
            GalaxyMap map = MakeMap(
                new[] { MakeSystem(0, EmpireId), MakeSystem(1, Unowned), MakeSystem(2, Unowned) },
                (0, 1), (1, 2));

            var military = new FakeMilitaryService();
            military.SetGarrison(new StarSystemId(0), EmpireId, UnitBundle.Of(UnitType.Infantry, 9));

            // Rayon nul : rien n'est a portee, meme le voisin direct.
            Assert.IsFalse(ExpansionPlanner.TryPlanColonization(
                EmpireId, map, military, maxHops: 0, MinimumGarrisonToKeep, out _));
        }

        // --- Offensive --------------------------------------------------------------------

        [Test]
        public void TryPlanOffensive_TargetsTheLeastDefendedEnemySystem()
        {
            //        1(rival, 5 unites)
            //       /
            // 0(nous)
            //       \
            //        2(rival, 1 unite)
            GalaxyMap map = MakeMap(
                new[] { MakeSystem(0, EmpireId), MakeSystem(1, RivalId), MakeSystem(2, RivalId) },
                (0, 1), (0, 2));

            var military = new FakeMilitaryService();
            military.SetGarrison(new StarSystemId(0), EmpireId, UnitBundle.Of(UnitType.Infantry, 4) + UnitBundle.Of(UnitType.Fighter, 2));
            military.SetGarrison(new StarSystemId(1), RivalId, UnitBundle.Of(UnitType.Infantry, 5));
            military.SetGarrison(new StarSystemId(2), RivalId, UnitBundle.Of(UnitType.Infantry, 1));

            var diplomacy = new FakeDiplomacyService();
            diplomacy.SetStatus(EmpireId, RivalId, DiplomaticStatus.War);

            Assert.IsTrue(ExpansionPlanner.TryPlanOffensive(
                EmpireId, map, military, diplomacy, maxHops: 3, MinimumGarrisonToKeep, out ExpansionPlanner.ExpansionPlan plan));

            Assert.AreEqual(new StarSystemId(2), plan.TargetId);
        }

        [Test]
        public void TryPlanOffensive_NoWarDeclared_ReturnsNoPlan()
        {
            GalaxyMap map = MakeMap(new[] { MakeSystem(0, EmpireId), MakeSystem(1, RivalId) }, (0, 1));

            var military = new FakeMilitaryService();
            military.SetGarrison(new StarSystemId(0), EmpireId, UnitBundle.Of(UnitType.Infantry, 6));

            var diplomacy = new FakeDiplomacyService();
            diplomacy.SetStatus(EmpireId, RivalId, DiplomaticStatus.Peace);

            Assert.IsFalse(ExpansionPlanner.TryPlanOffensive(
                EmpireId, map, military, diplomacy, maxHops: 3, MinimumGarrisonToKeep, out _));
        }

        [Test]
        public void TryPlanOffensive_NoInfantry_ReturnsNoPlan()
        {
            // Verrou d'invasion (Phase 16) : sans Infanterie, une victoire ne capture rien.
            GalaxyMap map = MakeMap(new[] { MakeSystem(0, EmpireId), MakeSystem(1, RivalId) }, (0, 1));

            var military = new FakeMilitaryService();
            military.SetGarrison(new StarSystemId(0), EmpireId, UnitBundle.Of(UnitType.Fighter, 6));

            var diplomacy = new FakeDiplomacyService();
            diplomacy.SetStatus(EmpireId, RivalId, DiplomaticStatus.War);

            Assert.IsFalse(ExpansionPlanner.TryPlanOffensive(
                EmpireId, map, military, diplomacy, maxHops: 3, MinimumGarrisonToKeep, out _));
        }

        [Test]
        public void TryPlanOffensive_KeepsTheReserveHome()
        {
            GalaxyMap map = MakeMap(new[] { MakeSystem(0, EmpireId), MakeSystem(1, RivalId) }, (0, 1));

            var military = new FakeMilitaryService();
            UnitBundle garrison = UnitBundle.Of(UnitType.Infantry, 3) + UnitBundle.Of(UnitType.Fighter, 3);
            military.SetGarrison(new StarSystemId(0), EmpireId, garrison);

            var diplomacy = new FakeDiplomacyService();
            diplomacy.SetStatus(EmpireId, RivalId, DiplomaticStatus.War);

            Assert.IsTrue(ExpansionPlanner.TryPlanOffensive(
                EmpireId, map, military, diplomacy, maxHops: 3, MinimumGarrisonToKeep, out ExpansionPlanner.ExpansionPlan plan));

            Assert.AreEqual(garrison.TotalCount - MinimumGarrisonToKeep, plan.Force.TotalCount);
            Assert.GreaterOrEqual(plan.Force.Infantry, 1, "Une force d'attaque doit pouvoir occuper ce qu'elle conquiert.");
        }

        [Test]
        public void TryPlanOffensive_GarrisonTooSmall_ReturnsNoPlan()
        {
            GalaxyMap map = MakeMap(new[] { MakeSystem(0, EmpireId), MakeSystem(1, RivalId) }, (0, 1));

            var military = new FakeMilitaryService();
            military.SetGarrison(new StarSystemId(0), EmpireId, UnitBundle.Of(UnitType.Infantry, 2));

            var diplomacy = new FakeDiplomacyService();
            diplomacy.SetStatus(EmpireId, RivalId, DiplomaticStatus.War);

            Assert.IsFalse(ExpansionPlanner.TryPlanOffensive(
                EmpireId, map, military, diplomacy, maxHops: 3, MinimumGarrisonToKeep, out _));
        }

        // --- Composition de la force -------------------------------------------------------

        [Test]
        public void SplitAttackForce_NeverKeepsTheLastInfantryHome()
        {
            UnitBundle garrison = UnitBundle.Of(UnitType.Infantry, 1) + UnitBundle.Of(UnitType.Fighter, 4);

            UnitBundle force = ExpansionPlanner.SplitAttackForce(garrison, MinimumGarrisonToKeep);

            Assert.AreEqual(1, force.Infantry, "Sans Infanterie, la conquete est impossible (Phase 16).");
        }

        [Test]
        public void SplitAttackForce_ReservesTheWeakestUnitsFirst()
        {
            UnitBundle garrison = UnitBundle.Of(UnitType.Infantry, 4) + UnitBundle.Of(UnitType.Battleship, 2);

            UnitBundle force = ExpansionPlanner.SplitAttackForce(garrison, MinimumGarrisonToKeep);

            Assert.AreEqual(2, force.Battleship, "Les unites les plus fortes partent, jamais la reserve.");
        }
    }
}
