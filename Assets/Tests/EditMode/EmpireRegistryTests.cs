using System;
using Espace.Gameplay.Empires;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie le registre passif <see cref="EmpireRegistry"/>.</summary>
    [TestFixture]
    public sealed class EmpireRegistryTests
    {
        private static Empire MakePlayer(int id = 0) => new Empire(id, "Joueur", Color.blue, EmpirePersonality.Expansionist, isPlayerControlled: true);

        private static Empire MakeAi(int id, string name = "IA") => new Empire(id, name, Color.red, EmpirePersonality.Militarist, isPlayerControlled: false);

        [Test]
        public void Constructor_NullOrEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(() => new EmpireRegistry(null));
            Assert.Throws<ArgumentException>(() => new EmpireRegistry(Array.Empty<Empire>()));
        }

        [Test]
        public void Constructor_NullEntry_Throws()
        {
            Assert.Throws<ArgumentException>(() => new EmpireRegistry(new[] { MakePlayer(), null }));
        }

        [Test]
        public void Constructor_DuplicateId_Throws()
        {
            Assert.Throws<ArgumentException>(() => new EmpireRegistry(new[] { MakePlayer(0), MakeAi(0) }));
        }

        [Test]
        public void Constructor_NoPlayerEmpire_Throws()
        {
            Assert.Throws<ArgumentException>(() => new EmpireRegistry(new[] { MakeAi(1), MakeAi(2) }));
        }

        [Test]
        public void Constructor_MultiplePlayerEmpires_Throws()
        {
            Assert.Throws<ArgumentException>(() => new EmpireRegistry(new[] { MakePlayer(0), MakePlayer(1) }));
        }

        [Test]
        public void PlayerEmpire_ReturnsTheFlaggedEmpire()
        {
            Empire player = MakePlayer();
            Empire ai = MakeAi(1);
            var registry = new EmpireRegistry(new[] { player, ai });

            Assert.AreSame(player, registry.PlayerEmpire);
        }

        [Test]
        public void GetEmpire_KnownId_ReturnsIt()
        {
            Empire player = MakePlayer();
            Empire ai = MakeAi(1);
            var registry = new EmpireRegistry(new[] { player, ai });

            Assert.AreSame(ai, registry.GetEmpire(1));
        }

        [Test]
        public void GetEmpire_UnknownId_Throws()
        {
            var registry = new EmpireRegistry(new[] { MakePlayer() });

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => registry.GetEmpire(99));
        }

        [Test]
        public void TryGetEmpire_UnknownId_ReturnsFalse()
        {
            var registry = new EmpireRegistry(new[] { MakePlayer() });

            bool found = registry.TryGetEmpire(99, out Empire empire);

            Assert.IsFalse(found);
            Assert.IsNull(empire);
        }

        [Test]
        public void Empires_ContainsAllProvided()
        {
            Empire player = MakePlayer();
            Empire ai1 = MakeAi(1, "IA Un");
            Empire ai2 = MakeAi(2, "IA Deux");
            var registry = new EmpireRegistry(new[] { player, ai1, ai2 });

            Assert.AreEqual(3, registry.Empires.Count);
            CollectionAssert.Contains(registry.Empires, ai1);
            CollectionAssert.Contains(registry.Empires, ai2);
        }
    }
}
