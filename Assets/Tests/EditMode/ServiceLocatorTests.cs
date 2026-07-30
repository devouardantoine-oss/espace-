using System;
using Espace.Core;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie le contrat du <see cref="ServiceLocator"/>.
    /// Le registre etant statique, chaque test repart d'un etat vide via <see cref="SetUp"/>.
    /// </summary>
    [TestFixture]
    public sealed class ServiceLocatorTests
    {
        /// <summary>Service factice : suffit a valider l'enregistrement par interface.</summary>
        private interface IDummyService
        {
            int Value { get; }
        }

        private sealed class DummyService : IDummyService
        {
            public DummyService(int value) => Value = value;

            public int Value { get; }
        }

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        [Test]
        public void Register_ThenGet_ReturnsSameInstance()
        {
            var service = new DummyService(42);

            ServiceLocator.Register<IDummyService>(service);

            Assert.AreSame(service, ServiceLocator.Get<IDummyService>());
            Assert.AreEqual(1, ServiceLocator.Count);
        }

        [Test]
        public void Register_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ServiceLocator.Register<IDummyService>(null));
        }

        [Test]
        public void Register_Twice_Throws()
        {
            ServiceLocator.Register<IDummyService>(new DummyService(1));

            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Register<IDummyService>(new DummyService(2)));
        }

        [Test]
        public void Get_Unregistered_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IDummyService>());
        }

        [Test]
        public void TryGet_Unregistered_ReturnsFalseAndNull()
        {
            bool found = ServiceLocator.TryGet(out IDummyService service);

            Assert.IsFalse(found);
            Assert.IsNull(service);
        }

        [Test]
        public void Unregister_RemovesService()
        {
            ServiceLocator.Register<IDummyService>(new DummyService(7));

            Assert.IsTrue(ServiceLocator.Unregister<IDummyService>());
            Assert.IsFalse(ServiceLocator.IsRegistered<IDummyService>());
            Assert.IsFalse(ServiceLocator.Unregister<IDummyService>(), "Un second retrait doit etre sans effet.");
        }

        [Test]
        public void Register_UsesGenericTypeAsKey_NotConcreteType()
        {
            // Enregistre sous l'interface : le type concret ne doit pas etre une cle valide.
            var service = new DummyService(3);
            ServiceLocator.Register<IDummyService>(service);

            Assert.IsTrue(ServiceLocator.IsRegistered<IDummyService>());
            Assert.IsFalse(ServiceLocator.IsRegistered<DummyService>());
        }
    }
}
