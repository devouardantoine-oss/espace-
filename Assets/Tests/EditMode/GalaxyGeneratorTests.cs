using System.Collections.Generic;
using System.Linq;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie les proprietes que <see cref="GalaxyGenerator"/> doit garantir quelle que
    /// soit la graine : determinisme, connexite totale, respect des contraintes de
    /// placement, coherence du graphe genere.
    /// <para>
    /// Les tests utilisent de petites galaxies (10 a 30 systemes) pour rester rapides ;
    /// la galaxie reelle du jeu (100 systemes) est couverte separement par
    /// <see cref="Generate_FullSizeGalaxy_ProducesConnectedNetwork"/>.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class GalaxyGeneratorTests
    {
        private static GalaxyGenerationParameters SmallParameters(int seed, int systemCount = 20)
        {
            return new GalaxyGenerationParameters(
                seed,
                systemCount,
                galaxyRadius: 20f,
                minSystemDistance: 2f,
                maxPlacementAttempts: 30,
                targetAverageDegree: 2.5f);
        }

        [Test]
        public void Generate_SameSeed_ProducesIdenticalGalaxy()
        {
            GalaxyMap first = GalaxyGenerator.Generate(SmallParameters(seed: 1234));
            GalaxyMap second = GalaxyGenerator.Generate(SmallParameters(seed: 1234));

            Assert.AreEqual(first.Systems.Count, second.Systems.Count);
            for (int i = 0; i < first.Systems.Count; i++)
            {
                StarSystemState a = first.Systems[i];
                StarSystemState b = second.Systems[i];

                Assert.AreEqual(a.Id, b.Id);
                Assert.AreEqual(a.Name, b.Name);
                Assert.AreEqual(a.Position, b.Position);
                Assert.AreEqual(a.Population, b.Population);
                Assert.AreEqual(a.Wealth, b.Wealth);
                Assert.AreEqual(a.DevelopmentLevel, b.DevelopmentLevel);
                Assert.AreEqual(a.Stability, b.Stability);
                CollectionAssert.AreEqual(a.ResourceDeposits, b.ResourceDeposits);
            }

            CollectionAssert.AreEquivalent(first.Links, second.Links);
        }

        [Test]
        public void Generate_DifferentSeeds_ProduceDifferentGalaxies()
        {
            GalaxyMap first = GalaxyGenerator.Generate(SmallParameters(seed: 1));
            GalaxyMap second = GalaxyGenerator.Generate(SmallParameters(seed: 2));

            bool anyPositionDiffers = first.Systems
                .Zip(second.Systems, (a, b) => a.Position != b.Position)
                .Any(differs => differs);

            Assert.IsTrue(anyPositionDiffers, "Deux graines differentes ne devraient pas produire la meme disposition.");
        }

        [Test]
        public void Generate_ProducesExactlyRequestedSystemCount()
        {
            GalaxyMap map = GalaxyGenerator.Generate(SmallParameters(seed: 7, systemCount: 37));

            Assert.AreEqual(37, map.Systems.Count);
        }

        [Test]
        public void Generate_AllSystemIdsAreUniqueAndSequential()
        {
            GalaxyMap map = GalaxyGenerator.Generate(SmallParameters(seed: 42));

            var ids = map.Systems.Select(s => s.Id.Value).OrderBy(v => v).ToArray();
            var expected = Enumerable.Range(0, map.Systems.Count).ToArray();

            CollectionAssert.AreEqual(expected, ids);
        }

        [Test]
        public void Generate_AllSystemNamesAreUnique()
        {
            GalaxyMap map = GalaxyGenerator.Generate(SmallParameters(seed: 99, systemCount: 100));

            var distinctNames = map.Systems.Select(s => s.Name).Distinct().Count();

            Assert.AreEqual(map.Systems.Count, distinctNames);
        }

        [Test]
        public void Generate_RespectsMinimumSystemDistance_WhenSpaceAllows()
        {
            // Galaxie clairsemee (rayon large, peu de systemes) : la distance minimale doit
            // toujours pouvoir etre respectee, donc aucune violation n'est toleree ici.
            var parameters = new GalaxyGenerationParameters(
                seed: 5,
                systemCount: 15,
                galaxyRadius: 40f,
                minSystemDistance: 3f,
                maxPlacementAttempts: 30,
                targetAverageDegree: 2.5f);

            GalaxyMap map = GalaxyGenerator.Generate(parameters);

            for (int i = 0; i < map.Systems.Count; i++)
            {
                for (int j = i + 1; j < map.Systems.Count; j++)
                {
                    float distance = Vector2.Distance(map.Systems[i].Position, map.Systems[j].Position);
                    Assert.GreaterOrEqual(distance, parameters.MinSystemDistance - 0.01f,
                        $"Systemes {i} et {j} sont plus proches que la distance minimale.");
                }
            }
        }

        [Test]
        public void Generate_AllPositionsWithinGalaxyRadius()
        {
            var parameters = SmallParameters(seed: 8);
            GalaxyMap map = GalaxyGenerator.Generate(parameters);

            foreach (StarSystemState system in map.Systems)
            {
                Assert.LessOrEqual(system.Position.magnitude, parameters.GalaxyRadius + 0.01f);
            }
        }

        [Test]
        public void Generate_NetworkIsFullyConnected()
        {
            GalaxyMap map = GalaxyGenerator.Generate(SmallParameters(seed: 21, systemCount: 25));

            HashSet<StarSystemId> reachable = BreadthFirstReach(map, map.Systems[0].Id);

            Assert.AreEqual(map.Systems.Count, reachable.Count, "Tous les systemes doivent etre atteignables depuis n'importe quel autre.");
        }

        [Test]
        public void Generate_FullSizeGalaxy_ProducesConnectedNetwork()
        {
            GalaxyMap map = GalaxyGenerator.Generate(GalaxyGenerationParameters.Default);

            Assert.AreEqual(100, map.Systems.Count);
            Assert.GreaterOrEqual(map.Links.Count, 99, "Au moins l'arbre couvrant minimal doit etre present.");

            HashSet<StarSystemId> reachable = BreadthFirstReach(map, map.Systems[0].Id);
            Assert.AreEqual(100, reachable.Count);
        }

        [Test]
        public void Generate_ContainsNoDuplicateLinks()
        {
            GalaxyMap map = GalaxyGenerator.Generate(SmallParameters(seed: 13, systemCount: 30));

            int distinctCount = map.Links.Distinct().Count();

            Assert.AreEqual(map.Links.Count, distinctCount);
        }

        [Test]
        public void Generate_NoSystemLinkedToItself()
        {
            GalaxyMap map = GalaxyGenerator.Generate(SmallParameters(seed: 14));

            foreach (HyperlaneLink link in map.Links)
            {
                Assert.AreNotEqual(link.SystemA, link.SystemB);
            }
        }

        [Test]
        public void Generate_SingleSystem_ProducesNoLinks()
        {
            var parameters = new GalaxyGenerationParameters(
                seed: 0,
                systemCount: 1,
                galaxyRadius: 10f,
                minSystemDistance: 1f,
                maxPlacementAttempts: 10,
                targetAverageDegree: 2.5f);

            GalaxyMap map = GalaxyGenerator.Generate(parameters);

            Assert.AreEqual(1, map.Systems.Count);
            Assert.AreEqual(0, map.Links.Count);
        }

        [Test]
        public void Generate_StatsAreWithinDocumentedRanges()
        {
            GalaxyMap map = GalaxyGenerator.Generate(SmallParameters(seed: 55, systemCount: 40));

            foreach (StarSystemState system in map.Systems)
            {
                Assert.GreaterOrEqual(system.Population, 0);
                Assert.LessOrEqual(system.Population, 4000);
                Assert.GreaterOrEqual(system.Wealth, 5);
                Assert.LessOrEqual(system.Wealth, 100);
                Assert.GreaterOrEqual(system.DevelopmentLevel, 0);
                Assert.LessOrEqual(system.DevelopmentLevel, 5);
                Assert.GreaterOrEqual(system.Stability, 0.2f - 0.001f);
                Assert.LessOrEqual(system.Stability, 1f);
                Assert.LessOrEqual(system.ResourceDeposits.Length, 2);
                Assert.AreEqual(system.ResourceDeposits.Distinct().Count(), system.ResourceDeposits.Length,
                    "Un systeme ne doit pas avoir deux fois le meme gisement.");
            }
        }

        [Test]
        public void Generate_AllSystemsStartUnowned()
        {
            GalaxyMap map = GalaxyGenerator.Generate(SmallParameters(seed: 3));

            Assert.IsTrue(map.Systems.All(s => s.OwnerId == StarSystemState.UnownedOwnerId));
        }

        /// <summary>Parcours en largeur du graphe de la galaxie, utilise pour verifier la connexite.</summary>
        private static HashSet<StarSystemId> BreadthFirstReach(GalaxyMap map, StarSystemId start)
        {
            var visited = new HashSet<StarSystemId> { start };
            var queue = new Queue<StarSystemId>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                StarSystemId current = queue.Dequeue();
                foreach (StarSystemId neighbor in map.GetNeighbors(current))
                {
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return visited;
        }
    }
}
