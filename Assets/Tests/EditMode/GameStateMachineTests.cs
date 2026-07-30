using System;
using System.Collections.Generic;
using Espace.Core;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie l'ordre des transitions de la <see cref="GameStateMachine"/>, y compris le
    /// cas reentrant (un etat qui declenche la transition suivante depuis son <c>Enter</c>).
    /// </summary>
    [TestFixture]
    public sealed class GameStateMachineTests
    {
        /// <summary>Etat espion : journalise ses appels dans une trace partagee.</summary>
        private sealed class SpyState : IGameState
        {
            private readonly List<string> _trace;
            private readonly string _name;
            private readonly Action _onEnter;

            public SpyState(List<string> trace, string name, Action onEnter = null)
            {
                _trace = trace;
                _name = name;
                _onEnter = onEnter;
            }

            public int TickCount { get; private set; }

            public void Enter()
            {
                _trace.Add($"{_name}.Enter");
                _onEnter?.Invoke();
            }

            public void Tick(float deltaTime)
            {
                TickCount++;
            }

            public void Exit()
            {
                _trace.Add($"{_name}.Exit");
            }
        }

        private List<string> _trace;
        private GameStateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _trace = new List<string>();
            _machine = new GameStateMachine();
        }

        [Test]
        public void ChangeState_FirstTransition_CallsEnterOnly()
        {
            var stateA = new SpyState(_trace, "A");

            _machine.ChangeState(stateA);

            CollectionAssert.AreEqual(new[] { "A.Enter" }, _trace);
            Assert.AreSame(stateA, _machine.CurrentState);
        }

        [Test]
        public void ChangeState_ExitsPreviousBeforeEnteringNext()
        {
            var stateA = new SpyState(_trace, "A");
            var stateB = new SpyState(_trace, "B");

            _machine.ChangeState(stateA);
            _machine.ChangeState(stateB);

            CollectionAssert.AreEqual(new[] { "A.Enter", "A.Exit", "B.Enter" }, _trace);
            Assert.AreSame(stateB, _machine.CurrentState);
        }

        [Test]
        public void ChangeState_ToSameState_IsIgnored()
        {
            var stateA = new SpyState(_trace, "A");

            _machine.ChangeState(stateA);
            _machine.ChangeState(stateA);

            CollectionAssert.AreEqual(new[] { "A.Enter" }, _trace);
        }

        [Test]
        public void ChangeState_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _machine.ChangeState(null));
        }

        [Test]
        public void ChangeState_RequestedFromEnter_IsProcessed()
        {
            // C'est exactement le scenario BootState -> MainMenuState du bootstrap.
            var stateB = new SpyState(_trace, "B");
            var stateA = new SpyState(_trace, "A", () => _machine.ChangeState(stateB));

            _machine.ChangeState(stateA);

            CollectionAssert.AreEqual(new[] { "A.Enter", "A.Exit", "B.Enter" }, _trace);
            Assert.AreSame(stateB, _machine.CurrentState);
        }

        [Test]
        public void StateChanged_IsRaisedWithPreviousAndNext()
        {
            var stateA = new SpyState(_trace, "A");
            var stateB = new SpyState(_trace, "B");

            IGameState capturedPrevious = null;
            IGameState capturedNext = null;
            _machine.StateChanged += (previous, next) =>
            {
                capturedPrevious = previous;
                capturedNext = next;
            };

            _machine.ChangeState(stateA);
            Assert.IsNull(capturedPrevious, "La premiere transition n'a pas d'etat precedent.");
            Assert.AreSame(stateA, capturedNext);

            _machine.ChangeState(stateB);
            Assert.AreSame(stateA, capturedPrevious);
            Assert.AreSame(stateB, capturedNext);
        }

        [Test]
        public void Tick_ForwardsToCurrentStateOnly()
        {
            var stateA = new SpyState(_trace, "A");
            var stateB = new SpyState(_trace, "B");

            _machine.ChangeState(stateA);
            _machine.Tick(0.016f);
            _machine.ChangeState(stateB);
            _machine.Tick(0.016f);

            Assert.AreEqual(1, stateA.TickCount);
            Assert.AreEqual(1, stateB.TickCount);
        }

        [Test]
        public void Tick_WithoutState_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _machine.Tick(0.016f));
        }

        [Test]
        public void Stop_ExitsCurrentStateAndClearsIt()
        {
            var stateA = new SpyState(_trace, "A");
            _machine.ChangeState(stateA);

            _machine.Stop();

            CollectionAssert.AreEqual(new[] { "A.Enter", "A.Exit" }, _trace);
            Assert.IsNull(_machine.CurrentState);
        }
    }
}
