using Espace.Data;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie l'arithmetique et les comparaisons de <see cref="ResourceBundle"/>.</summary>
    [TestFixture]
    public sealed class ResourceBundleTests
    {
        [Test]
        public void Zero_HasAllComponentsAtZero()
        {
            ResourceBundle zero = ResourceBundle.Zero;

            Assert.AreEqual(0f, zero.Credits);
            Assert.AreEqual(0f, zero.Minerals);
            Assert.AreEqual(0f, zero.Energy);
            Assert.AreEqual(0f, zero.Food);
            Assert.AreEqual(0f, zero.Influence);
        }

        [Test]
        public void Get_ReturnsComponentMatchingType()
        {
            var bundle = new ResourceBundle(credits: 1, minerals: 2, energy: 3, food: 4, influence: 5);

            Assert.AreEqual(1f, bundle.Get(ResourceType.Credits));
            Assert.AreEqual(2f, bundle.Get(ResourceType.Minerals));
            Assert.AreEqual(3f, bundle.Get(ResourceType.Energy));
            Assert.AreEqual(4f, bundle.Get(ResourceType.Food));
            Assert.AreEqual(5f, bundle.Get(ResourceType.Influence));
        }

        [Test]
        public void Addition_SumsEachComponent()
        {
            var a = new ResourceBundle(credits: 10, minerals: 5);
            var b = new ResourceBundle(credits: 3, energy: 2);

            ResourceBundle sum = a + b;

            Assert.AreEqual(new ResourceBundle(credits: 13, minerals: 5, energy: 2), sum);
        }

        [Test]
        public void Subtraction_SubtractsEachComponent()
        {
            var a = new ResourceBundle(credits: 10, minerals: 5);
            var b = new ResourceBundle(credits: 3, minerals: 2);

            ResourceBundle result = a - b;

            Assert.AreEqual(new ResourceBundle(credits: 7, minerals: 3), result);
        }

        [Test]
        public void Multiplication_ScalesEachComponent()
        {
            var bundle = new ResourceBundle(credits: 10, minerals: 4);

            ResourceBundle scaled = bundle * 0.5f;

            Assert.AreEqual(new ResourceBundle(credits: 5, minerals: 2), scaled);
        }

        [Test]
        public void IsGreaterOrEqualTo_AllComponentsSufficient_ReturnsTrue()
        {
            var stockpile = new ResourceBundle(credits: 100, minerals: 50);
            var cost = new ResourceBundle(credits: 80, minerals: 50);

            Assert.IsTrue(stockpile.IsGreaterOrEqualTo(cost));
        }

        [Test]
        public void IsGreaterOrEqualTo_OneComponentInsufficient_ReturnsFalse()
        {
            var stockpile = new ResourceBundle(credits: 100, minerals: 10);
            var cost = new ResourceBundle(credits: 80, minerals: 50);

            Assert.IsFalse(stockpile.IsGreaterOrEqualTo(cost));
        }

        [Test]
        public void Equality_SameComponents_AreEqual()
        {
            var a = new ResourceBundle(credits: 1, minerals: 2, energy: 3, food: 4, influence: 5);
            var b = new ResourceBundle(credits: 1, minerals: 2, energy: 3, food: 4, influence: 5);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equality_DifferentComponents_AreNotEqual()
        {
            var a = new ResourceBundle(credits: 1);
            var b = new ResourceBundle(credits: 2);

            Assert.AreNotEqual(a, b);
            Assert.IsTrue(a != b);
        }
    }
}
