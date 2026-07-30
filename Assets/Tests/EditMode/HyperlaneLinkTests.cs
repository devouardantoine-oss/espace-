using System;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie que <see cref="HyperlaneLink"/> traite un lien A-B comme identique a B-A.</summary>
    [TestFixture]
    public sealed class HyperlaneLinkTests
    {
        [Test]
        public void Equals_IsOrderIndependent()
        {
            var ab = new HyperlaneLink(new StarSystemId(1), new StarSystemId(2));
            var ba = new HyperlaneLink(new StarSystemId(2), new StarSystemId(1));

            Assert.AreEqual(ab, ba);
            Assert.AreEqual(ab.GetHashCode(), ba.GetHashCode());
        }

        [Test]
        public void Constructor_NormalizesOrder_SmallestFirst()
        {
            var link = new HyperlaneLink(new StarSystemId(5), new StarSystemId(2));

            Assert.AreEqual(new StarSystemId(2), link.SystemA);
            Assert.AreEqual(new StarSystemId(5), link.SystemB);
        }

        [Test]
        public void Constructor_SelfLink_Throws()
        {
            Assert.Throws<ArgumentException>(() => new HyperlaneLink(new StarSystemId(1), new StarSystemId(1)));
        }

        [Test]
        public void Contains_ReturnsTrueForEitherEndpoint()
        {
            var link = new HyperlaneLink(new StarSystemId(1), new StarSystemId(2));

            Assert.IsTrue(link.Contains(new StarSystemId(1)));
            Assert.IsTrue(link.Contains(new StarSystemId(2)));
            Assert.IsFalse(link.Contains(new StarSystemId(3)));
        }

        [Test]
        public void DistinctLinks_AreNotEqual()
        {
            var a = new HyperlaneLink(new StarSystemId(1), new StarSystemId(2));
            var b = new HyperlaneLink(new StarSystemId(1), new StarSystemId(3));

            Assert.AreNotEqual(a, b);
        }
    }
}
