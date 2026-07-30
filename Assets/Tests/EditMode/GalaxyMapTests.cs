using System;
using System.Collections.Generic;
using Espace.Data;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="GalaxyMap"/> independamment du generateur : construction,
    /// validation des donnees d'entree, requetes de voisinage.
    /// </summary>
    [TestFixture]
    public sealed class GalaxyMapTests
    {
        private static StarSystemState MakeSystem(int id, string name = "Test") =>
            new StarSystemState(new StarSystemId(id), name, Vector2.zero, 0, 50, 1, 0.5f, Array.Empty<ResourceType>());

        [Test]
        public void Constructor_ValidData_ExposesSystemsAndLinks()
        {
            var systems = new[] { MakeSystem(0, "A"), MakeSystem(1, "B") };
            var links = new[] { new HyperlaneLink(new StarSystemId(0), new StarSystemId(1)) };

            var map = new GalaxyMap(systems, links);

            Assert.AreEqual(2, map.Systems.Count);
            Assert.AreEqual(1, map.Links.Count);
        }

        [Test]
        public void Constructor_DuplicateSystemId_Throws()
        {
            var systems = new[] { MakeSystem(0, "A"), MakeSystem(0, "B") };

            Assert.Throws<ArgumentException>(() => new GalaxyMap(systems, Array.Empty<HyperlaneLink>()));
        }

        [Test]
        public void Constructor_LinkToUnknownSystem_Throws()
        {
            var systems = new[] { MakeSystem(0, "A") };
            var links = new[] { new HyperlaneLink(new StarSystemId(0), new StarSystemId(99)) };

            Assert.Throws<ArgumentException>(() => new GalaxyMap(systems, links));
        }

        [Test]
        public void Constructor_NullSystems_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new GalaxyMap(null, Array.Empty<HyperlaneLink>()));
        }

        [Test]
        public void Constructor_NullLinks_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new GalaxyMap(Array.Empty<StarSystemState>(), null));
        }

        [Test]
        public void GetSystem_KnownId_ReturnsSystem()
        {
            var system = MakeSystem(3, "Vexor Prime");
            var map = new GalaxyMap(new[] { system }, Array.Empty<HyperlaneLink>());

            Assert.AreSame(system, map.GetSystem(new StarSystemId(3)));
        }

        [Test]
        public void GetSystem_UnknownId_Throws()
        {
            var map = new GalaxyMap(new[] { MakeSystem(0) }, Array.Empty<HyperlaneLink>());

            Assert.Throws<KeyNotFoundException>(() => map.GetSystem(new StarSystemId(999)));
        }

        [Test]
        public void TryGetSystem_UnknownId_ReturnsFalse()
        {
            var map = new GalaxyMap(new[] { MakeSystem(0) }, Array.Empty<HyperlaneLink>());

            bool found = map.TryGetSystem(new StarSystemId(999), out StarSystemState system);

            Assert.IsFalse(found);
            Assert.IsNull(system);
        }

        [Test]
        public void GetNeighbors_ReturnsSystemsLinkedDirectly()
        {
            var systems = new[] { MakeSystem(0, "A"), MakeSystem(1, "B"), MakeSystem(2, "C") };
            var links = new[]
            {
                new HyperlaneLink(new StarSystemId(0), new StarSystemId(1)),
                new HyperlaneLink(new StarSystemId(1), new StarSystemId(2))
            };
            var map = new GalaxyMap(systems, links);

            CollectionAssert.AreEquivalent(new[] { new StarSystemId(0), new StarSystemId(2) }, map.GetNeighbors(new StarSystemId(1)));
            CollectionAssert.AreEquivalent(new[] { new StarSystemId(1) }, map.GetNeighbors(new StarSystemId(0)));
        }

        [Test]
        public void GetNeighbors_SystemWithNoLinks_ReturnsEmpty()
        {
            var map = new GalaxyMap(new[] { MakeSystem(0) }, Array.Empty<HyperlaneLink>());

            Assert.AreEqual(0, map.GetNeighbors(new StarSystemId(0)).Count);
        }

        [Test]
        public void AreLinked_IsSymmetric()
        {
            var systems = new[] { MakeSystem(0, "A"), MakeSystem(1, "B") };
            var links = new[] { new HyperlaneLink(new StarSystemId(0), new StarSystemId(1)) };
            var map = new GalaxyMap(systems, links);

            Assert.IsTrue(map.AreLinked(new StarSystemId(0), new StarSystemId(1)));
            Assert.IsTrue(map.AreLinked(new StarSystemId(1), new StarSystemId(0)));
        }

        [Test]
        public void AreLinked_UnlinkedSystems_ReturnsFalse()
        {
            var systems = new[] { MakeSystem(0, "A"), MakeSystem(1, "B") };
            var map = new GalaxyMap(systems, Array.Empty<HyperlaneLink>());

            Assert.IsFalse(map.AreLinked(new StarSystemId(0), new StarSystemId(1)));
        }
    }
}
