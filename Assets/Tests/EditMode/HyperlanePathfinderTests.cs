using System;
using System.Collections.Generic;
using Espace.Data;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="HyperlanePathfinder"/> : itineraire le plus court en distance,
    /// application du filtre aux seuls points de passage, determinisme.
    /// </summary>
    [TestFixture]
    public sealed class HyperlanePathfinderTests
    {
        private const float FloatTolerance = 0.001f;

        private static StarSystemState MakeSystem(int id, Vector2 position, int ownerId = StarSystemState.UnownedOwnerId)
        {
            var system = new StarSystemState(
                new StarSystemId(id), $"System{id}", position, 1000, 50, 3, 1f, Array.Empty<ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        /// <summary>
        /// Graphe en losange : 0 et 2 relies soit par 1 (tres court), soit par 3 (tres long).
        /// <code>
        ///   0(0,0) --- 1(1,0) --- 2(2,0)
        ///     \                    /
        ///      ---- 3(0,10) ------
        /// </code>
        /// </summary>
        private static GalaxyMap MakeDiamond(out StarSystemState s0, out StarSystemState s1, out StarSystemState s2, out StarSystemState s3)
        {
            s0 = MakeSystem(0, Vector2.zero);
            s1 = MakeSystem(1, new Vector2(1f, 0f));
            s2 = MakeSystem(2, new Vector2(2f, 0f));
            s3 = MakeSystem(3, new Vector2(0f, 10f));

            var links = new[]
            {
                new HyperlaneLink(s0.Id, s1.Id),
                new HyperlaneLink(s1.Id, s2.Id),
                new HyperlaneLink(s0.Id, s3.Id),
                new HyperlaneLink(s3.Id, s2.Id)
            };

            return new GalaxyMap(new[] { s0, s1, s2, s3 }, links);
        }

        [Test]
        public void TryFindPath_MultiHop_ReturnsShortestByDistance()
        {
            GalaxyMap map = MakeDiamond(out StarSystemState s0, out StarSystemState s1, out StarSystemState s2, out _);

            bool found = HyperlanePathfinder.TryFindPath(map, s0.Id, s2.Id, null, out IReadOnlyList<StarSystemId> path);

            Assert.IsTrue(found);
            CollectionAssert.AreEqual(new[] { s0.Id, s1.Id, s2.Id }, path, "Le detour court doit primer sur le detour long.");
        }

        [Test]
        public void TryFindPath_IncludesBothEndpoints()
        {
            GalaxyMap map = MakeDiamond(out StarSystemState s0, out _, out StarSystemState s2, out _);

            HyperlanePathfinder.TryFindPath(map, s0.Id, s2.Id, null, out IReadOnlyList<StarSystemId> path);

            Assert.AreEqual(s0.Id, path[0]);
            Assert.AreEqual(s2.Id, path[path.Count - 1]);
        }

        [Test]
        public void TryFindPath_SameOriginAndDestination_ReturnsSingleElement()
        {
            GalaxyMap map = MakeDiamond(out _, out StarSystemState s1, out _, out _);

            bool found = HyperlanePathfinder.TryFindPath(map, s1.Id, s1.Id, null, out IReadOnlyList<StarSystemId> path);

            Assert.IsTrue(found);
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(s1.Id, path[0]);
        }

        [Test]
        public void TryFindPath_BlockedIntermediate_TakesDetour()
        {
            GalaxyMap map = MakeDiamond(out StarSystemState s0, out StarSystemState s1, out StarSystemState s2, out StarSystemState s3);

            bool found = HyperlanePathfinder.TryFindPath(
                map, s0.Id, s2.Id, system => !system.Id.Equals(s1.Id), out IReadOnlyList<StarSystemId> path);

            Assert.IsTrue(found);
            CollectionAssert.AreEqual(new[] { s0.Id, s3.Id, s2.Id }, path, "Le point de passage interdit doit etre contourne.");
        }

        [Test]
        public void TryFindPath_FilterDoesNotApplyToDestination()
        {
            // Le cas le plus facile a casser : un systeme ennemi est une destination legale
            // (on vient l'attaquer) sans etre un point de passage legal.
            GalaxyMap map = MakeDiamond(out StarSystemState s0, out StarSystemState s1, out StarSystemState s2, out _);

            bool found = HyperlanePathfinder.TryFindPath(
                map, s0.Id, s2.Id, system => !system.Id.Equals(s2.Id), out IReadOnlyList<StarSystemId> path);

            Assert.IsTrue(found, "Le filtre ne doit jamais rejeter la destination elle-meme.");
            CollectionAssert.AreEqual(new[] { s0.Id, s1.Id, s2.Id }, path);
        }

        [Test]
        public void TryFindPath_AllIntermediatesBlocked_ReturnsFalse()
        {
            StarSystemState a = MakeSystem(0, Vector2.zero);
            StarSystemState b = MakeSystem(1, new Vector2(1f, 0f));
            StarSystemState c = MakeSystem(2, new Vector2(2f, 0f));
            var map = new GalaxyMap(
                new[] { a, b, c },
                new[] { new HyperlaneLink(a.Id, b.Id), new HyperlaneLink(b.Id, c.Id) });

            bool found = HyperlanePathfinder.TryFindPath(
                map, a.Id, c.Id, system => !system.Id.Equals(b.Id), out IReadOnlyList<StarSystemId> path);

            Assert.IsFalse(found);
            Assert.IsNull(path);
        }

        [Test]
        public void TryFindPath_DisconnectedComponents_ReturnsFalse()
        {
            StarSystemState a = MakeSystem(0, Vector2.zero);
            StarSystemState b = MakeSystem(1, new Vector2(1f, 0f));
            StarSystemState c = MakeSystem(2, new Vector2(50f, 0f));
            StarSystemState d = MakeSystem(3, new Vector2(51f, 0f));
            var map = new GalaxyMap(
                new[] { a, b, c, d },
                new[] { new HyperlaneLink(a.Id, b.Id), new HyperlaneLink(c.Id, d.Id) });

            Assert.IsFalse(HyperlanePathfinder.TryFindPath(map, a.Id, c.Id, null, out _));
        }

        [Test]
        public void TryFindPath_IsDeterministic()
        {
            // Losange parfaitement symetrique : les deux itineraires font exactement la meme
            // longueur, seul le departage sur l'identifiant peut trancher.
            StarSystemState origin = MakeSystem(0, Vector2.zero);
            StarSystemState upper = MakeSystem(1, new Vector2(5f, 5f));
            StarSystemState lower = MakeSystem(2, new Vector2(5f, -5f));
            StarSystemState target = MakeSystem(3, new Vector2(10f, 0f));
            var map = new GalaxyMap(
                new[] { origin, upper, lower, target },
                new[]
                {
                    new HyperlaneLink(origin.Id, upper.Id), new HyperlaneLink(upper.Id, target.Id),
                    new HyperlaneLink(origin.Id, lower.Id), new HyperlaneLink(lower.Id, target.Id)
                });

            HyperlanePathfinder.TryFindPath(map, origin.Id, target.Id, null, out IReadOnlyList<StarSystemId> first);
            HyperlanePathfinder.TryFindPath(map, origin.Id, target.Id, null, out IReadOnlyList<StarSystemId> second);

            CollectionAssert.AreEqual(first, second, "Deux itineraires de meme longueur doivent toujours donner le meme resultat.");
        }

        [Test]
        public void TryFindPath_UnknownSystem_ReturnsFalse()
        {
            GalaxyMap map = MakeDiamond(out StarSystemState s0, out _, out _, out _);

            Assert.IsFalse(HyperlanePathfinder.TryFindPath(map, s0.Id, new StarSystemId(999), null, out _));
            Assert.IsFalse(HyperlanePathfinder.TryFindPath(map, new StarSystemId(999), s0.Id, null, out _));
        }

        [Test]
        public void TotalDistance_SumsEachLeg()
        {
            GalaxyMap map = MakeDiamond(out StarSystemState s0, out StarSystemState s1, out StarSystemState s2, out _);

            float total = HyperlanePathfinder.TotalDistance(map, new[] { s0.Id, s1.Id, s2.Id });

            Assert.AreEqual(2f, total, FloatTolerance, "Deux troncons de 1 unite chacun.");
        }

        [Test]
        public void TotalDistance_TrivialPaths_AreZero()
        {
            GalaxyMap map = MakeDiamond(out StarSystemState s0, out _, out _, out _);

            Assert.AreEqual(0f, HyperlanePathfinder.TotalDistance(map, null));
            Assert.AreEqual(0f, HyperlanePathfinder.TotalDistance(map, new[] { s0.Id }));
        }
    }
}
