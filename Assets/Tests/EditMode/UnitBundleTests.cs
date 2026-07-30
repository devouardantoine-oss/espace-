using Espace.Gameplay.Military;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie l'arithmetique et les comparaisons de <see cref="UnitBundle"/>.</summary>
    [TestFixture]
    public sealed class UnitBundleTests
    {
        [Test]
        public void Zero_HasAllComponentsAtZero()
        {
            UnitBundle zero = UnitBundle.Zero;

            Assert.AreEqual(0, zero.Infantry);
            Assert.AreEqual(0, zero.Armored);
            Assert.AreEqual(0, zero.SpecialForces);
            Assert.AreEqual(0, zero.SpaceFleet);
            Assert.IsTrue(zero.IsEmpty);
            Assert.AreEqual(0, zero.TotalCount);
        }

        [Test]
        public void TotalCount_SumsAllTypes()
        {
            var bundle = new UnitBundle(infantry: 3, armored: 2, specialForces: 1, spaceFleet: 4);

            Assert.AreEqual(10, bundle.TotalCount);
            Assert.IsFalse(bundle.IsEmpty);
        }

        [Test]
        public void Get_ReturnsComponentMatchingType()
        {
            var bundle = new UnitBundle(infantry: 1, armored: 2, specialForces: 3, spaceFleet: 4);

            Assert.AreEqual(1, bundle.Get(UnitType.Infantry));
            Assert.AreEqual(2, bundle.Get(UnitType.Armored));
            Assert.AreEqual(3, bundle.Get(UnitType.SpecialForces));
            Assert.AreEqual(4, bundle.Get(UnitType.SpaceFleet));
        }

        [Test]
        public void Of_CreatesSingleTypeBundle()
        {
            UnitBundle bundle = UnitBundle.Of(UnitType.Armored, 5);

            Assert.AreEqual(new UnitBundle(armored: 5), bundle);
        }

        [Test]
        public void Addition_SumsEachComponent()
        {
            var a = new UnitBundle(infantry: 3, armored: 1);
            var b = new UnitBundle(infantry: 2, specialForces: 4);

            UnitBundle sum = a + b;

            Assert.AreEqual(new UnitBundle(infantry: 5, armored: 1, specialForces: 4), sum);
        }

        [Test]
        public void Subtraction_ClampsAtZeroPerType()
        {
            var a = new UnitBundle(infantry: 3, armored: 1);
            var b = new UnitBundle(infantry: 5, armored: 1);

            UnitBundle result = a - b;

            Assert.AreEqual(new UnitBundle(infantry: 0, armored: 0), result);
        }

        [Test]
        public void IsGreaterOrEqualTo_SufficientOnAllTypes_ReturnsTrue()
        {
            var stock = new UnitBundle(infantry: 5, armored: 2);
            var need = new UnitBundle(infantry: 3, armored: 2);

            Assert.IsTrue(stock.IsGreaterOrEqualTo(need));
        }

        [Test]
        public void IsGreaterOrEqualTo_InsufficientOnOneType_ReturnsFalse()
        {
            var stock = new UnitBundle(infantry: 5, armored: 1);
            var need = new UnitBundle(infantry: 3, armored: 2);

            Assert.IsFalse(stock.IsGreaterOrEqualTo(need));
        }

        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(0f)]
        public void Scale_RoundsDownAndNeverNegative(float fraction)
        {
            var bundle = new UnitBundle(infantry: 5, armored: 3);

            UnitBundle scaled = bundle.Scale(fraction);

            Assert.GreaterOrEqual(scaled.Infantry, 0);
            Assert.GreaterOrEqual(scaled.Armored, 0);
            Assert.LessOrEqual(scaled.Infantry, bundle.Infantry);
            Assert.LessOrEqual(scaled.Armored, bundle.Armored);
        }

        [Test]
        public void Scale_Half_RoundsDown()
        {
            var bundle = new UnitBundle(infantry: 5);

            UnitBundle scaled = bundle.Scale(0.5f);

            Assert.AreEqual(2, scaled.Infantry, "5 * 0.5 = 2.5, arrondi vers le bas.");
        }

        [Test]
        public void Scale_NonPositiveFraction_ReturnsZero()
        {
            var bundle = new UnitBundle(infantry: 5, armored: 3);

            Assert.AreEqual(UnitBundle.Zero, bundle.Scale(0f));
            Assert.AreEqual(UnitBundle.Zero, bundle.Scale(-1f));
        }

        [Test]
        public void Equality_SameComponents_AreEqual()
        {
            var a = new UnitBundle(1, 2, 3, 4);
            var b = new UnitBundle(1, 2, 3, 4);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equality_DifferentComponents_AreNotEqual()
        {
            var a = new UnitBundle(infantry: 1);
            var b = new UnitBundle(infantry: 2);

            Assert.AreNotEqual(a, b);
            Assert.IsTrue(a != b);
        }
    }
}
