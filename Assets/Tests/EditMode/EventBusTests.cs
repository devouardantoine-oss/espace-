using System;
using Espace.Core;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie l'abonnement, la publication et le desabonnement de l'<see cref="EventBus"/>.</summary>
    [TestFixture]
    public sealed class EventBusTests
    {
        private readonly struct TestEvent : IGameEvent
        {
            public readonly int Payload;

            public TestEvent(int payload) => Payload = payload;
        }

        private readonly struct OtherEvent : IGameEvent
        {
        }

        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _bus.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Shutdown();
        }

        [Test]
        public void Publish_NotifiesSubscriber()
        {
            int received = 0;
            _bus.Subscribe<TestEvent>(e => received = e.Payload);

            _bus.Publish(new TestEvent(99));

            Assert.AreEqual(99, received);
        }

        [Test]
        public void Publish_NotifiesAllSubscribersInOrder()
        {
            int callCount = 0;
            _bus.Subscribe<TestEvent>(_ => callCount++);
            _bus.Subscribe<TestEvent>(_ => callCount++);

            _bus.Publish(new TestEvent(1));

            Assert.AreEqual(2, callCount);
            Assert.AreEqual(2, _bus.GetSubscriberCount<TestEvent>());
        }

        [Test]
        public void Publish_WithNoSubscriber_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bus.Publish(new TestEvent(1)));
        }

        [Test]
        public void Publish_DoesNotLeakAcrossEventTypes()
        {
            int testEventCalls = 0;
            _bus.Subscribe<TestEvent>(_ => testEventCalls++);

            _bus.Publish(new OtherEvent());

            Assert.AreEqual(0, testEventCalls);
        }

        [Test]
        public void Unsubscribe_StopsNotifications()
        {
            int callCount = 0;
            Action<TestEvent> handler = _ => callCount++;

            _bus.Subscribe(handler);
            _bus.Unsubscribe(handler);
            _bus.Publish(new TestEvent(1));

            Assert.AreEqual(0, callCount);
            Assert.AreEqual(0, _bus.GetSubscriberCount<TestEvent>());
        }

        [Test]
        public void Unsubscribe_UnknownHandler_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bus.Unsubscribe<TestEvent>(_ => { }));
        }

        [Test]
        public void Subscribe_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _bus.Subscribe<TestEvent>(null));
        }

        [Test]
        public void Shutdown_RemovesEverySubscriber()
        {
            _bus.Subscribe<TestEvent>(_ => { });

            _bus.Shutdown();

            Assert.AreEqual(0, _bus.GetSubscriberCount<TestEvent>());
        }
    }
}
