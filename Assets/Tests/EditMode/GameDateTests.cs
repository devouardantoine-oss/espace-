using System;
using Espace.Core;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie l'arithmetique de <see cref="GameDate"/> : validation, conversions, comparaisons.</summary>
    [TestFixture]
    public sealed class GameDateTests
    {
        [Test]
        public void Constructor_ValidDate_ExposesComponents()
        {
            var date = new GameDate(3, 7, 12);

            Assert.AreEqual(3, date.Year);
            Assert.AreEqual(7, date.Month);
            Assert.AreEqual(12, date.Day);
        }

        [TestCase(0, 1, 1)]
        [TestCase(-1, 1, 1)]
        public void Constructor_InvalidYear_Throws(int year, int month, int day)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(year, month, day));
        }

        [TestCase(1, 0, 1)]
        [TestCase(1, 13, 1)]
        public void Constructor_InvalidMonth_Throws(int year, int month, int day)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(year, month, day));
        }

        [TestCase(1, 1, 0)]
        [TestCase(1, 1, 31)]
        public void Constructor_InvalidDay_Throws(int year, int month, int day)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(year, month, day));
        }

        [Test]
        public void StartOfGame_IsYear1Month1Day1()
        {
            var date = GameDate.StartOfGame;

            Assert.AreEqual(new GameDate(1, 1, 1), date);
        }

        [Test]
        public void ToDayIndex_StartOfGame_IsZero()
        {
            Assert.AreEqual(0, GameDate.StartOfGame.ToDayIndex());
        }

        [Test]
        public void FromDayIndex_ThenToDayIndex_RoundTrips()
        {
            for (int index = 0; index < 2000; index += 37)
            {
                GameDate date = GameDate.FromDayIndex(index);
                Assert.AreEqual(index, date.ToDayIndex(), $"Round-trip a echoue pour l'index {index}.");
            }
        }

        [Test]
        public void FromDayIndex_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GameDate.FromDayIndex(-1));
        }

        [Test]
        public void AddDays_WithinSameMonth_IncrementsDay()
        {
            var date = new GameDate(1, 1, 1);

            GameDate result = date.AddDays(5);

            Assert.AreEqual(new GameDate(1, 1, 6), result);
        }

        [Test]
        public void AddDays_CrossesMonthBoundary()
        {
            var date = new GameDate(1, 1, 30);

            GameDate result = date.AddDays(1);

            Assert.AreEqual(new GameDate(1, 2, 1), result);
        }

        [Test]
        public void AddDays_CrossesYearBoundary()
        {
            var date = new GameDate(1, 12, 30);

            GameDate result = date.AddDays(1);

            Assert.AreEqual(new GameDate(2, 1, 1), result);
        }

        [Test]
        public void AddDays_FullYear_AdvancesExactlyOneYear()
        {
            var date = new GameDate(1, 1, 1);

            GameDate result = date.AddDays(GameDate.DaysPerYear);

            Assert.AreEqual(new GameDate(2, 1, 1), result);
        }

        [Test]
        public void ComparisonOperators_OrderByDayIndex()
        {
            var earlier = new GameDate(1, 1, 1);
            var earlierCopy = new GameDate(1, 1, 1);
            var later = new GameDate(1, 1, 2);

            Assert.IsTrue(earlier < later);
            Assert.IsTrue(later > earlier);
            Assert.IsTrue(earlier <= earlierCopy);
            Assert.IsTrue(earlier >= earlierCopy);
            Assert.IsFalse(later < earlier);
        }

        [Test]
        public void Equality_SameComponents_AreEqual()
        {
            var a = new GameDate(2, 3, 4);
            var b = new GameDate(2, 3, 4);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void ToString_UsesZeroPaddedIsoLikeFormat()
        {
            var date = new GameDate(3, 7, 2);

            Assert.AreEqual("0003-07-02", date.ToString());
        }
    }
}
