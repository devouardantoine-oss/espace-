using System;
using System.Collections.Generic;
using System.Linq;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie le determinisme et l'unicite garantie de <see cref="StarSystemNameGenerator"/>.</summary>
    [TestFixture]
    public sealed class StarSystemNameGeneratorTests
    {
        [Test]
        public void GenerateUnique_SameSeed_ProducesSameSequence()
        {
            var namesA = GenerateSequence(new Random(123), 30);
            var namesB = GenerateSequence(new Random(123), 30);

            CollectionAssert.AreEqual(namesA, namesB);
        }

        [Test]
        public void GenerateUnique_NeverRepeatsWithinOneCall()
        {
            var names = GenerateSequence(new Random(456), 200);

            Assert.AreEqual(names.Count, names.Distinct().Count());
        }

        [Test]
        public void GenerateUnique_NeverReturnsEmptyOrWhitespace()
        {
            var names = GenerateSequence(new Random(789), 50);

            Assert.IsTrue(names.All(name => !string.IsNullOrWhiteSpace(name)));
        }

        [Test]
        public void GenerateUnique_AlwaysStartsWithUppercase()
        {
            var names = GenerateSequence(new Random(101112), 50);

            Assert.IsTrue(names.All(name => char.IsUpper(name[0])));
        }

        [Test]
        public void GenerateUnique_NullRandom_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StarSystemNameGenerator.GenerateUnique(null, new HashSet<string>()));
        }

        [Test]
        public void GenerateUnique_NullUsedNames_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StarSystemNameGenerator.GenerateUnique(new Random(1), null));
        }

        [Test]
        public void GenerateUnique_WithPreFilledCollision_StillGrowsTheSetByOne()
        {
            // Meme graine que la premiere entree de 'used' : force une collision au premier
            // tirage, ce qui exerce la voie de re-tirage de GenerateUnique.
            string firstName = StarSystemNameGenerator.GenerateUnique(new Random(1), new HashSet<string>());
            var used = new HashSet<string> { firstName };

            string next = StarSystemNameGenerator.GenerateUnique(new Random(1), used);

            Assert.AreNotEqual(firstName, next);
            Assert.AreEqual(2, used.Count);
            Assert.IsTrue(used.Contains(next));
        }

        private static List<string> GenerateSequence(Random rng, int count)
        {
            var used = new HashSet<string>();
            var results = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                results.Add(StarSystemNameGenerator.GenerateUnique(rng, used));
            }

            return results;
        }
    }
}
