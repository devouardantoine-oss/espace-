using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="EmpireHoldings"/> (Phase 18) : la notion de territoire partagee par
    /// les cinq decision makers de l'IA, qui remplace leurs copies privees de
    /// <c>FindPrimarySystem</c>.
    /// </summary>
    [TestFixture]
    public sealed class EmpireHoldingsTests
    {
        private const int EmpireId = 3;
        private const int RivalId = 4;
        private const int ThirdPartyId = 5;

        /// <summary>
        /// Doublure minimale : seule <see cref="IMilitaryService.EstimatePower"/> et
        /// <see cref="IMilitaryService.GetGarrison"/> sont utilisees par cette classe.
        /// La puissance vaut simplement le nombre d'unites, ce qui rend les assertions lisibles.
        /// </summary>
        private sealed class FakeMilitaryService : IMilitaryService
        {
            private readonly Dictionary<(StarSystemId, int), UnitBundle> _garrisons =
                new Dictionary<(StarSystemId, int), UnitBundle>();

            public void SetGarrison(StarSystemId systemId, int empireId, UnitBundle composition) =>
                _garrisons[(systemId, empireId)] = composition;

            public UnitBundle GetGarrison(StarSystemId systemId, int empireId) =>
                _garrisons.TryGetValue((systemId, empireId), out UnitBundle composition) ? composition : UnitBundle.Zero;

            public float EstimatePower(UnitBundle composition) => composition.TotalCount;

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

        private static StarSystemState MakeSystem(int id, int ownerId, int developmentLevel = 0)
        {
            var system = new StarSystemState(
                new StarSystemId(id), $"S{id}", new Vector2(id * 10f, 0f), 1000, 500, developmentLevel, 1f, Array.Empty<ResourceType>());
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

        // --- Capitale ---------------------------------------------------------------------

        [Test]
        public void Capital_IsTheMostDevelopedOwnedSystem()
        {
            StarSystemState colony = MakeSystem(0, EmpireId, developmentLevel: 1);
            StarSystemState capital = MakeSystem(1, EmpireId, developmentLevel: 4);
            GalaxyMap map = MakeMap(new[] { colony, capital });

            Assert.AreEqual(capital.Id, EmpireHoldings.Capital(EmpireId, map).Id);
        }

        [Test]
        public void Capital_FreshColonyWithLowerId_NeverDisplacesTheCapital()
        {
            // Le defaut exact corrige par la Phase 18 : « le premier systeme de map.Systems »
            // designait la colonie des qu'elle avait un identifiant plus faible.
            StarSystemState freshColony = MakeSystem(0, EmpireId, developmentLevel: 0);
            StarSystemState realCapital = MakeSystem(1, EmpireId, developmentLevel: 3);
            GalaxyMap map = MakeMap(new[] { freshColony, realCapital });

            Assert.AreEqual(realCapital.Id, EmpireHoldings.Capital(EmpireId, map).Id);
        }

        [Test]
        public void Capital_EqualDevelopment_PicksLowestId()
        {
            StarSystemState low = MakeSystem(2, EmpireId, developmentLevel: 2);
            StarSystemState high = MakeSystem(7, EmpireId, developmentLevel: 2);
            GalaxyMap map = MakeMap(new[] { high, low });

            Assert.AreEqual(low.Id, EmpireHoldings.Capital(EmpireId, map).Id, "Departage deterministe, aucun tirage.");
        }

        [Test]
        public void Capital_EmpireOwnsNothing_ReturnsNull()
        {
            GalaxyMap map = MakeMap(new[] { MakeSystem(0, RivalId) });

            Assert.IsNull(EmpireHoldings.Capital(EmpireId, map));
        }

        // --- Territoire -------------------------------------------------------------------

        [Test]
        public void OwnedSystems_ReturnsOnlyOwnedOnes_InMapOrder()
        {
            StarSystemState mine0 = MakeSystem(0, EmpireId);
            StarSystemState theirs = MakeSystem(1, RivalId);
            StarSystemState mine2 = MakeSystem(2, EmpireId);
            GalaxyMap map = MakeMap(new[] { mine0, theirs, mine2 });

            List<StarSystemState> owned = EmpireHoldings.OwnedSystems(EmpireId, map);

            Assert.AreEqual(2, owned.Count);
            Assert.AreEqual(mine0.Id, owned[0].Id);
            Assert.AreEqual(mine2.Id, owned[1].Id);
        }

        // --- Puissance --------------------------------------------------------------------

        [Test]
        public void TotalPower_SumsEveryOwnedSystem()
        {
            StarSystemState a = MakeSystem(0, EmpireId);
            StarSystemState b = MakeSystem(1, EmpireId);
            StarSystemState enemy = MakeSystem(2, RivalId);
            GalaxyMap map = MakeMap(new[] { a, b, enemy });

            var military = new FakeMilitaryService();
            military.SetGarrison(a.Id, EmpireId, UnitBundle.Of(UnitType.Infantry, 3));
            military.SetGarrison(b.Id, EmpireId, UnitBundle.Of(UnitType.Fighter, 4));
            military.SetGarrison(enemy.Id, RivalId, UnitBundle.Of(UnitType.Battleship, 9));

            Assert.AreEqual(7f, EmpireHoldings.TotalPower(EmpireId, map, military), 0.001f);
        }

        [Test]
        public void TotalPower_SingleSystemEmpire_MatchesThatSystemAlone()
        {
            // Garantit que les seuils diplomatiques regles avant la Phase 18 gardent le meme
            // sens sur une partie ou chaque empire n'a qu'un systeme.
            StarSystemState only = MakeSystem(0, EmpireId);
            GalaxyMap map = MakeMap(new[] { only });

            var military = new FakeMilitaryService();
            military.SetGarrison(only.Id, EmpireId, UnitBundle.Of(UnitType.Infantry, 5));

            Assert.AreEqual(
                military.EstimatePower(military.GetGarrison(only.Id, EmpireId)),
                EmpireHoldings.TotalPower(EmpireId, map, military),
                0.001f);
        }

        // --- Voisinage --------------------------------------------------------------------

        [Test]
        public void NeighboringEmpires_IncludesEmpiresBorderingOnlyAColony()
        {
            // capitale(0) - colonie(1) - rival(2). Le rival ne borde que la colonie : avant la
            // Phase 18, la diplomatie ne le voyait tout simplement pas.
            StarSystemState capital = MakeSystem(0, EmpireId, developmentLevel: 3);
            StarSystemState colony = MakeSystem(1, EmpireId);
            StarSystemState rival = MakeSystem(2, RivalId);
            GalaxyMap map = MakeMap(new[] { capital, colony, rival }, (0, 1), (1, 2));

            CollectionAssert.AreEqual(new[] { RivalId }, EmpireHoldings.NeighboringEmpires(EmpireId, map));
        }

        [Test]
        public void NeighboringEmpires_NoDuplicates_AndSorted()
        {
            // Deux systemes a nous bordent chacun les deux memes rivaux.
            StarSystemState mineA = MakeSystem(0, EmpireId);
            StarSystemState mineB = MakeSystem(1, EmpireId);
            StarSystemState third = MakeSystem(2, ThirdPartyId);
            StarSystemState rival = MakeSystem(3, RivalId);
            GalaxyMap map = MakeMap(
                new[] { mineA, mineB, third, rival },
                (0, 2), (0, 3), (1, 2), (1, 3));

            CollectionAssert.AreEqual(new[] { RivalId, ThirdPartyId }, EmpireHoldings.NeighboringEmpires(EmpireId, map));
        }

        [Test]
        public void NeighboringEmpires_IgnoresUnownedSystems()
        {
            StarSystemState mine = MakeSystem(0, EmpireId);
            StarSystemState free = MakeSystem(1, StarSystemState.UnownedOwnerId);
            GalaxyMap map = MakeMap(new[] { mine, free }, (0, 1));

            CollectionAssert.IsEmpty(EmpireHoldings.NeighboringEmpires(EmpireId, map));
        }

        [Test]
        public void FirstBorderSystemOf_ReturnsNullWhenEmpiresDoNotTouch()
        {
            StarSystemState mine = MakeSystem(0, EmpireId);
            StarSystemState free = MakeSystem(1, StarSystemState.UnownedOwnerId);
            StarSystemState rival = MakeSystem(2, RivalId);
            GalaxyMap map = MakeMap(new[] { mine, free, rival }, (0, 1), (1, 2));

            Assert.IsNull(EmpireHoldings.FirstBorderSystemOf(EmpireId, RivalId, map));
        }
    }
}
