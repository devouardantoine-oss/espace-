using System.Collections.Generic;
using Espace.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="GameClock"/> : avance du temps, seuils de jour, pause/reprise,
    /// changements de vitesse, evenements publies, plafond de rattrapage.
    /// </summary>
    [TestFixture]
    public sealed class GameClockTests
    {
        private const float SecondsPerDay = 2f;

        private EventBus _eventBus;
        private GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _eventBus.Initialize();

            var settings = new GameClockSettings(GameDate.StartOfGame, SecondsPerDay, fastMultiplier: 2f, fasterMultiplier: 4f, fastestMultiplier: 8f);
            _clock = new GameClock(settings, _eventBus);
            _clock.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _clock.Shutdown();
            _eventBus.Shutdown();
        }

        [Test]
        public void Initialize_SetsStartDateAndNormalSpeed()
        {
            Assert.AreEqual(GameDate.StartOfGame, _clock.CurrentDate);
            Assert.AreEqual(GameSpeed.Normal, _clock.CurrentSpeed);
            Assert.IsFalse(_clock.IsPaused);
            Assert.AreEqual(1f, _clock.CurrentMultiplier);
        }

        [Test]
        public void Tick_BelowThreshold_DoesNotAdvanceDate()
        {
            _clock.Tick(SecondsPerDay * 0.5f);

            Assert.AreEqual(GameDate.StartOfGame, _clock.CurrentDate);
        }

        [Test]
        public void Tick_ReachingThreshold_AdvancesExactlyOneDay()
        {
            var received = new List<GameDate>();
            _eventBus.Subscribe<DayAdvancedEvent>(e => received.Add(e.Date));

            _clock.Tick(SecondsPerDay);

            Assert.AreEqual(GameDate.StartOfGame.AddDays(1), _clock.CurrentDate);
            CollectionAssert.AreEqual(new[] { GameDate.StartOfGame.AddDays(1) }, received);
        }

        [Test]
        public void Tick_KeepsRemainderAcrossFrames()
        {
            // Deux tiers du seuil, puis encore deux tiers : la somme (4/3) depasse le seuil
            // une fois, avec un reste conserve plutot que perdu.
            _clock.Tick(SecondsPerDay * (2f / 3f));
            Assert.AreEqual(GameDate.StartOfGame, _clock.CurrentDate);

            _clock.Tick(SecondsPerDay * (2f / 3f));
            Assert.AreEqual(GameDate.StartOfGame.AddDays(1), _clock.CurrentDate);
        }

        [Test]
        public void Tick_AtFastSpeed_AdvancesProportionallyFaster()
        {
            _clock.SetSpeed(GameSpeed.Fast); // x2

            _clock.Tick(SecondsPerDay * 0.5f);

            Assert.AreEqual(GameDate.StartOfGame.AddDays(1), _clock.CurrentDate);
        }

        [Test]
        public void Tick_WhilePaused_DoesNotAdvance()
        {
            _clock.Pause();

            _clock.Tick(SecondsPerDay * 10f);

            Assert.AreEqual(GameDate.StartOfGame, _clock.CurrentDate);
        }

        [Test]
        public void Tick_MultipleDaysAtOnce_PublishesOneEventPerDay()
        {
            var received = new List<GameDate>();
            _eventBus.Subscribe<DayAdvancedEvent>(e => received.Add(e.Date));

            _clock.Tick(SecondsPerDay * 3f);

            Assert.AreEqual(3, received.Count);
            Assert.AreEqual(GameDate.StartOfGame.AddDays(3), _clock.CurrentDate);
        }

        [Test]
        public void Tick_CrossingMonthBoundary_AlsoPublishesMonthAdvanced()
        {
            var monthEvents = new List<GameDate>();
            _eventBus.Subscribe<MonthAdvancedEvent>(e => monthEvents.Add(e.Date));

            // Du jour 30 (dernier jour du mois) au jour 1 du mois suivant.
            for (int i = 0; i < 29; i++)
            {
                _clock.Tick(SecondsPerDay);
            }
            Assert.AreEqual(new GameDate(1, 1, 30), _clock.CurrentDate);

            _clock.Tick(SecondsPerDay);

            Assert.AreEqual(new GameDate(1, 2, 1), _clock.CurrentDate);
            CollectionAssert.AreEqual(new[] { new GameDate(1, 2, 1) }, monthEvents);
        }

        [Test]
        public void Tick_CrossingYearBoundary_AlsoPublishesYearAdvanced()
        {
            var yearEvents = new List<GameDate>();
            _eventBus.Subscribe<YearAdvancedEvent>(e => yearEvents.Add(e.Date));

            for (int i = 0; i < GameDate.DaysPerYear - 1; i++)
            {
                _clock.Tick(SecondsPerDay);
            }

            _clock.Tick(SecondsPerDay);

            Assert.AreEqual(new GameDate(2, 1, 1), _clock.CurrentDate);
            CollectionAssert.AreEqual(new[] { new GameDate(2, 1, 1) }, yearEvents);
        }

        [Test]
        public void Pause_ThenResume_RestoresPriorSpeed()
        {
            _clock.SetSpeed(GameSpeed.Faster);

            _clock.Pause();
            Assert.IsTrue(_clock.IsPaused);

            _clock.Resume();

            Assert.AreEqual(GameSpeed.Faster, _clock.CurrentSpeed);
            Assert.IsFalse(_clock.IsPaused);
        }

        [Test]
        public void TogglePause_SwitchesBetweenPausedAndPriorSpeed()
        {
            _clock.TogglePause();
            Assert.IsTrue(_clock.IsPaused);

            _clock.TogglePause();
            Assert.AreEqual(GameSpeed.Normal, _clock.CurrentSpeed);
        }

        [Test]
        public void SetSpeed_Paused_IsEquivalentToPause()
        {
            _clock.SetSpeed(GameSpeed.Paused);

            Assert.IsTrue(_clock.IsPaused);
        }

        [Test]
        public void SetSpeed_WhilePaused_ResumesAtNewSpeed()
        {
            _clock.Pause();

            _clock.SetSpeed(GameSpeed.Fastest);

            Assert.IsFalse(_clock.IsPaused);
            Assert.AreEqual(GameSpeed.Fastest, _clock.CurrentSpeed);
        }

        [Test]
        public void SetSpeed_SameSpeed_DoesNotPublishDuplicateEvent()
        {
            int eventCount = 0;
            _eventBus.Subscribe<GameSpeedChangedEvent>(_ => eventCount++);

            _clock.SetSpeed(GameSpeed.Normal); // deja la vitesse courante

            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public void SetSpeed_DifferentSpeed_PublishesEventOnce()
        {
            var received = new List<GameSpeed>();
            _eventBus.Subscribe<GameSpeedChangedEvent>(e => received.Add(e.NewSpeed));

            _clock.SetSpeed(GameSpeed.Fast);

            CollectionAssert.AreEqual(new[] { GameSpeed.Fast }, received);
        }

        [Test]
        public void Tick_ExtremeDeltaTime_ClampsAdvanceAndLogsWarning()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*plafonnee.*"));

            // De quoi franchir des centaines de seuils de jour en une seule frame.
            _clock.Tick(SecondsPerDay * 1000f);

            Assert.AreEqual(GameDate.StartOfGame.AddDays(30), _clock.CurrentDate);
        }

        [Test]
        public void Tick_NegativeOrZeroDeltaTime_DoesNothing()
        {
            _clock.Tick(0f);
            _clock.Tick(-1f);

            Assert.AreEqual(GameDate.StartOfGame, _clock.CurrentDate);
        }
    }
}
