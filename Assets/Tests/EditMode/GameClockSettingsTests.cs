using System;
using Espace.Core;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie la validation et la resolution de multiplicateur de <see cref="GameClockSettings"/>.</summary>
    [TestFixture]
    public sealed class GameClockSettingsTests
    {
        private static GameClockSettings MakeSettings(
            float secondsPerDay = 2f, float fast = 2f, float faster = 4f, float fastest = 8f)
        {
            return new GameClockSettings(GameDate.StartOfGame, secondsPerDay, fast, faster, fastest);
        }

        [Test]
        public void Constructor_NonPositiveSecondsPerDay_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MakeSettings(secondsPerDay: 0f));
        }

        [Test]
        public void Constructor_FastMultiplierNotAboveOne_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MakeSettings(fast: 1f));
        }

        [Test]
        public void Constructor_FasterNotAboveFast_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MakeSettings(fast: 3f, faster: 3f));
        }

        [Test]
        public void Constructor_FastestNotAboveFaster_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MakeSettings(faster: 5f, fastest: 5f));
        }

        [Test]
        public void GetMultiplier_Paused_IsZero()
        {
            Assert.AreEqual(0f, MakeSettings().GetMultiplier(GameSpeed.Paused));
        }

        [Test]
        public void GetMultiplier_Normal_IsOne()
        {
            Assert.AreEqual(1f, MakeSettings().GetMultiplier(GameSpeed.Normal));
        }

        [Test]
        public void GetMultiplier_ReturnsConfiguredValuesPerTier()
        {
            GameClockSettings settings = MakeSettings(fast: 3f, faster: 6f, fastest: 12f);

            Assert.AreEqual(3f, settings.GetMultiplier(GameSpeed.Fast));
            Assert.AreEqual(6f, settings.GetMultiplier(GameSpeed.Faster));
            Assert.AreEqual(12f, settings.GetMultiplier(GameSpeed.Fastest));
        }

        [Test]
        public void Default_HasStrictlyIncreasingMultipliers()
        {
            GameClockSettings settings = GameClockSettings.Default;

            float normal = settings.GetMultiplier(GameSpeed.Normal);
            float fast = settings.GetMultiplier(GameSpeed.Fast);
            float faster = settings.GetMultiplier(GameSpeed.Faster);
            float fastest = settings.GetMultiplier(GameSpeed.Fastest);

            Assert.Less(normal, fast);
            Assert.Less(fast, faster);
            Assert.Less(faster, fastest);
        }

        [Test]
        public void Default_StartsAtStartOfGame()
        {
            Assert.AreEqual(GameDate.StartOfGame, GameClockSettings.Default.StartDate);
        }
    }
}
