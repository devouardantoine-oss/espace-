using System;
using System.Collections.Generic;
using Espace.Data;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="FleetRouting"/> (Phase 18) : la regle de traversabilite partagee avec
    /// <c>MilitaryService.TryPlanRoute</c>, et le parcours en largeur multi-source qui sert a
    /// l'IA a choisir ses cibles sans lancer un Dijkstra par candidat.
    /// </summary>
    [TestFixture]
    public sealed class FleetRoutingTests
    {
        private const int EmpireId = 3;
        private const int RivalId = 4;
        private const int ThirdPartyId = 5;
        private const int Unowned = StarSystemState.UnownedOwnerId;

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

        /// <summary>Chaine 0 - 1 - 2 - 3 - 4, le 0 a nous, le reste libre sauf indication.</summary>
        private static GalaxyMap MakeChain(params int[] owners)
        {
            var systems = new StarSystemState[owners.Length];
            var links = new List<(int, int)>();
            for (int i = 0; i < owners.Length; i++)
            {
                systems[i] = MakeSystem(i, owners[i]);
                if (i > 0)
                {
                    links.Add((i - 1, i));
                }
            }

            return MakeMap(systems, links.ToArray());
        }

        // --- Traversabilite ---------------------------------------------------------------

        [Test]
        public void IsTraversableWaypoint_FreeOrOwn_IsTraversable()
        {
            Assert.IsTrue(FleetRouting.IsTraversableWaypoint(MakeSystem(0, Unowned), EmpireId));
            Assert.IsTrue(FleetRouting.IsTraversableWaypoint(MakeSystem(0, EmpireId), EmpireId));
        }

        [Test]
        public void IsTraversableWaypoint_ForeignTerritory_IsNot()
        {
            Assert.IsFalse(FleetRouting.IsTraversableWaypoint(MakeSystem(0, RivalId), EmpireId));
        }

        [Test]
        public void IsTraversableWaypoint_Null_IsNot()
        {
            Assert.IsFalse(FleetRouting.IsTraversableWaypoint(null, EmpireId));
        }

        // --- Parcours multi-source --------------------------------------------------------

        [Test]
        public void HopDistances_CountsHopsFromTheOwnedTerritory()
        {
            GalaxyMap map = MakeChain(EmpireId, Unowned, Unowned, Unowned);

            Dictionary<StarSystemId, FleetRouting.Reach> reach = FleetRouting.HopDistances(EmpireId, map, maxHops: 5);

            Assert.AreEqual(1, reach[new StarSystemId(1)].Hops);
            Assert.AreEqual(2, reach[new StarSystemId(2)].Hops);
            Assert.AreEqual(3, reach[new StarSystemId(3)].Hops);
        }

        [Test]
        public void HopDistances_ExcludesTheOwnedSystemsThemselves()
        {
            GalaxyMap map = MakeChain(EmpireId, Unowned);

            Dictionary<StarSystemId, FleetRouting.Reach> reach = FleetRouting.HopDistances(EmpireId, map, maxHops: 5);

            Assert.IsFalse(reach.ContainsKey(new StarSystemId(0)), "Un systeme deja possede n'est pas une cible.");
        }

        [Test]
        public void HopDistances_OriginIsTheNearestOwnedSystem()
        {
            // 0(a nous) - 1 - 2 - 3(a nous) : le systeme 2 doit etre rattache au 3, pas au 0.
            GalaxyMap map = MakeChain(EmpireId, Unowned, Unowned, EmpireId);

            Dictionary<StarSystemId, FleetRouting.Reach> reach = FleetRouting.HopDistances(EmpireId, map, maxHops: 5);

            Assert.AreEqual(new StarSystemId(0), reach[new StarSystemId(1)].Origin);
            Assert.AreEqual(new StarSystemId(3), reach[new StarSystemId(2)].Origin);
            Assert.AreEqual(1, reach[new StarSystemId(2)].Hops);
        }

        [Test]
        public void HopDistances_ForeignSystemIsReachedButNeverTraversed()
        {
            // 0(a nous) - 1(rival) - 2(libre) : le rival est une cible atteignable, mais rien
            // ne passe a travers lui. C'est la regle « le predicat ne s'applique pas aux
            // extremites » de la Phase 17, transposee au parcours en largeur.
            GalaxyMap map = MakeChain(EmpireId, RivalId, Unowned);

            Dictionary<StarSystemId, FleetRouting.Reach> reach = FleetRouting.HopDistances(EmpireId, map, maxHops: 5);

            Assert.IsTrue(reach.ContainsKey(new StarSystemId(1)), "Le systeme rival doit rester une cible possible.");
            Assert.IsFalse(reach.ContainsKey(new StarSystemId(2)), "Rien ne doit passer a travers le territoire d'un tiers.");
        }

        [Test]
        public void HopDistances_RespectsMaxHops()
        {
            GalaxyMap map = MakeChain(EmpireId, Unowned, Unowned, Unowned);

            Dictionary<StarSystemId, FleetRouting.Reach> reach = FleetRouting.HopDistances(EmpireId, map, maxHops: 2);

            Assert.IsTrue(reach.ContainsKey(new StarSystemId(2)));
            Assert.IsFalse(reach.ContainsKey(new StarSystemId(3)), "Au-dela du rayon d'expansion, rien n'est propose.");
        }

        [Test]
        public void HopDistances_ZeroMaxHops_ReturnsNothing()
        {
            GalaxyMap map = MakeChain(EmpireId, Unowned);

            Assert.IsEmpty(FleetRouting.HopDistances(EmpireId, map, maxHops: 0));
        }

        [Test]
        public void HopDistances_EmpireOwnsNothing_ReturnsNothing()
        {
            GalaxyMap map = MakeChain(RivalId, Unowned);

            Assert.IsEmpty(FleetRouting.HopDistances(EmpireId, map, maxHops: 5));
        }

        [Test]
        public void HopDistances_DetourAroundForeignTerritory()
        {
            //    1(tiers)
            //   /        \
            // 0(nous)     3(libre)
            //   \        /
            //    2(libre)
            // Le seul chemin praticable vers 3 passe par 2 : deux sauts, pas deux via le tiers.
            var systems = new[]
            {
                MakeSystem(0, EmpireId),
                MakeSystem(1, ThirdPartyId),
                MakeSystem(2, Unowned),
                MakeSystem(3, Unowned),
            };
            GalaxyMap map = MakeMap(systems, (0, 1), (1, 3), (0, 2), (2, 3));

            Dictionary<StarSystemId, FleetRouting.Reach> reach = FleetRouting.HopDistances(EmpireId, map, maxHops: 5);

            Assert.AreEqual(2, reach[new StarSystemId(3)].Hops);
            Assert.AreEqual(1, reach[new StarSystemId(1)].Hops, "Le tiers reste atteignable en tant que destination.");
        }

        [Test]
        public void HopDistances_IsDeterministic()
        {
            GalaxyMap map = MakeChain(EmpireId, Unowned, Unowned, EmpireId, Unowned);

            Dictionary<StarSystemId, FleetRouting.Reach> first = FleetRouting.HopDistances(EmpireId, map, maxHops: 4);
            Dictionary<StarSystemId, FleetRouting.Reach> second = FleetRouting.HopDistances(EmpireId, map, maxHops: 4);

            Assert.AreEqual(first.Count, second.Count);
            foreach (KeyValuePair<StarSystemId, FleetRouting.Reach> entry in first)
            {
                Assert.AreEqual(entry.Value.Hops, second[entry.Key].Hops);
                Assert.AreEqual(entry.Value.Origin, second[entry.Key].Origin);
            }
        }
    }
}
