using System;
using System.Collections.Generic;
using Espace.Core;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie la reutilisation, le prechauffage et le plafonnement de l'<see cref="ObjectPool{T}"/>.</summary>
    [TestFixture]
    public sealed class ObjectPoolTests
    {
        /// <summary>Objet factice : represente une unite ou un projectile mis en pool.</summary>
        private sealed class Poolable
        {
            public bool IsActive;
        }

        [Test]
        public void Get_OnEmptyPool_CreatesInstance()
        {
            var pool = new ObjectPool<Poolable>(() => new Poolable());

            Poolable instance = pool.Get();

            Assert.IsNotNull(instance);
            Assert.AreEqual(1, pool.TotalCreated);
            Assert.AreEqual(1, pool.ActiveCount);
            Assert.AreEqual(0, pool.AvailableCount);
        }

        [Test]
        public void Release_ThenGet_ReusesSameInstance()
        {
            var pool = new ObjectPool<Poolable>(() => new Poolable());
            Poolable first = pool.Get();

            pool.Release(first);
            Poolable second = pool.Get();

            Assert.AreSame(first, second, "Le pool doit reutiliser l'instance au lieu d'en creer une nouvelle.");
            Assert.AreEqual(1, pool.TotalCreated);
        }

        [Test]
        public void Prewarm_CreatesInstancesUpFront()
        {
            var pool = new ObjectPool<Poolable>(() => new Poolable(), prewarmCount: 5);

            Assert.AreEqual(5, pool.AvailableCount);
            Assert.AreEqual(5, pool.TotalCreated);
            Assert.AreEqual(0, pool.ActiveCount);
        }

        [Test]
        public void Callbacks_AreInvokedOnGetAndRelease()
        {
            var pool = new ObjectPool<Poolable>(
                () => new Poolable(),
                onGet: p => p.IsActive = true,
                onRelease: p => p.IsActive = false);

            Poolable instance = pool.Get();
            Assert.IsTrue(instance.IsActive, "onGet doit activer l'instance.");

            pool.Release(instance);
            Assert.IsFalse(instance.IsActive, "onRelease doit desactiver l'instance.");
        }

        [Test]
        public void Prewarm_AppliesReleaseCallback()
        {
            // Les instances prechauffees doivent etre dans l'etat « au repos ».
            var created = new List<Poolable>();
            var pool = new ObjectPool<Poolable>(
                () =>
                {
                    var p = new Poolable { IsActive = true };
                    created.Add(p);
                    return p;
                },
                onRelease: p => p.IsActive = false,
                prewarmCount: 3);

            Assert.AreEqual(3, pool.AvailableCount);
            CollectionAssert.AreEquivalent(new[] { false, false, false }, created.ConvertAll(p => p.IsActive));
        }

        [Test]
        public void Release_BeyondMaxSize_DiscardsInstance()
        {
            var pool = new ObjectPool<Poolable>(() => new Poolable(), maxSize: 1);
            Poolable a = pool.Get();
            Poolable b = pool.Get();

            Assert.IsTrue(pool.Release(a), "La premiere instance doit etre conservee.");
            Assert.IsFalse(pool.Release(b), "Le pool est plein : la seconde doit etre abandonnee.");
            Assert.AreEqual(1, pool.AvailableCount);
        }

        [Test]
        public void Clear_InvokesDisposeAndEmptiesPool()
        {
            var disposed = new List<Poolable>();
            var pool = new ObjectPool<Poolable>(() => new Poolable(), prewarmCount: 4);

            pool.Clear(disposed.Add);

            Assert.AreEqual(0, pool.AvailableCount);
            Assert.AreEqual(4, disposed.Count);
        }

        [Test]
        public void Constructor_NullFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ObjectPool<Poolable>(null));
        }

        [Test]
        public void Constructor_NonPositiveMaxSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectPool<Poolable>(() => new Poolable(), maxSize: 0));
        }

        [Test]
        public void Get_WhenFactoryReturnsNull_Throws()
        {
            var pool = new ObjectPool<Poolable>(() => null);

            Assert.Throws<InvalidOperationException>(() => pool.Get());
        }

        [Test]
        public void Release_Null_Throws()
        {
            var pool = new ObjectPool<Poolable>(() => new Poolable());

            Assert.Throws<ArgumentNullException>(() => pool.Release(null));
        }
    }
}
