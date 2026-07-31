using System.Collections.Generic;
using Espace.Gameplay.Military;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie <see cref="Admiral"/> : determinisme, plages de bonus, malus garanti, variete, restauration.</summary>
    [TestFixture]
    public sealed class AdmiralTests
    {
        [Test]
        public void Compute_SameFleetIdAndOwner_IsDeterministic()
        {
            Admiral first = Admiral.Compute(42, 0);
            Admiral second = Admiral.Compute(42, 0);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void Compute_DifferentOwnerId_CanChangeAdmiral()
        {
            Admiral withOwnerA = Admiral.Compute(1, 0);
            Admiral withOwnerB = Admiral.Compute(1, 1);

            Assert.AreNotEqual(withOwnerA, withOwnerB, "Meme fleetId, proprietaires differents : l'Amiral doit differer (decorrelation).");
        }

        [Test]
        public void Compute_DifferentFleetId_CanChangeAdmiral()
        {
            Admiral withIdA = Admiral.Compute(1, 0);
            Admiral withIdB = Admiral.Compute(2, 0);

            Assert.AreNotEqual(withIdA, withIdB);
        }

        [Test]
        public void Compute_AllBonuses_AreWithinValidRange()
        {
            for (int id = 0; id < 300; id++)
            {
                Admiral admiral = Admiral.Compute(id, id % 6);
                Assert.GreaterOrEqual(admiral.AttackBonus, -0.15f);
                Assert.LessOrEqual(admiral.AttackBonus, 0.20f);
                Assert.GreaterOrEqual(admiral.SpeedBonus, -0.15f);
                Assert.LessOrEqual(admiral.SpeedBonus, 0.20f);
                Assert.GreaterOrEqual(admiral.DefenseBonus, -0.15f);
                Assert.LessOrEqual(admiral.DefenseBonus, 0.20f);
            }
        }

        [Test]
        public void Compute_ExactlyOneBonusIsNegative()
        {
            for (int id = 0; id < 300; id++)
            {
                Admiral admiral = Admiral.Compute(id, id % 6);
                int negativeCount = 0;
                if (admiral.AttackBonus < 0f) negativeCount++;
                if (admiral.SpeedBonus < 0f) negativeCount++;
                if (admiral.DefenseBonus < 0f) negativeCount++;

                Assert.AreEqual(1, negativeCount, $"fleetId={id} devrait avoir exactement un malus.");
            }
        }

        [Test]
        public void Compute_Name_IsNeverNullOrEmpty()
        {
            for (int id = 0; id < 100; id++)
            {
                Admiral admiral = Admiral.Compute(id, 0);
                Assert.IsFalse(string.IsNullOrEmpty(admiral.Name));
            }
        }

        [Test]
        public void Compute_AcrossManyFleets_ProducesVariety()
        {
            var names = new HashSet<string>();

            for (int id = 0; id < 100; id++)
            {
                names.Add(Admiral.Compute(id, 0).Name);
            }

            Assert.Greater(names.Count, 1, "100 flottes devraient produire plusieurs noms d'Amiral differents.");
        }

        [Test]
        public void Constructor_DirectValues_RoundTripExactly()
        {
            var admiral = new Admiral("Amiral de Test", 0.1f, -0.05f, 0.15f);

            Assert.AreEqual("Amiral de Test", admiral.Name);
            Assert.AreEqual(0.1f, admiral.AttackBonus);
            Assert.AreEqual(-0.05f, admiral.SpeedBonus);
            Assert.AreEqual(0.15f, admiral.DefenseBonus);
        }

        [Test]
        public void Equality_SameValues_AreEqual()
        {
            var a = new Admiral("X", 0.1f, 0.1f, -0.1f);
            var b = new Admiral("X", 0.1f, 0.1f, -0.1f);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equality_DifferentValues_AreNotEqual()
        {
            var a = new Admiral("X", 0.1f, 0.1f, -0.1f);
            var b = new Admiral("Y", 0.1f, 0.1f, -0.1f);

            Assert.AreNotEqual(a, b);
            Assert.IsTrue(a != b);
        }
    }
}
