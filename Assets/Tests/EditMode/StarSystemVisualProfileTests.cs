using System.Collections.Generic;
using Espace.Data;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    [TestFixture]
    public class StarSystemVisualProfileTests
    {
        private static StarSystemState MakeSystem(int id) =>
            new StarSystemState(new StarSystemId(id), $"System{id}", Vector2.zero, 1000, 500, 3, 0.8f, System.Array.Empty<ResourceType>());

        [Test]
        public void Compute_SameSystemAndSeed_IsDeterministic()
        {
            StarSystemState system = MakeSystem(42);

            StarSystemVisualProfile first = StarSystemVisualProfile.Compute(system, 20260730);
            StarSystemVisualProfile second = StarSystemVisualProfile.Compute(system, 20260730);

            Assert.AreEqual(first.BodyColor, second.BodyColor);
            Assert.AreEqual(first.HasRing, second.HasRing);
            Assert.AreEqual(first.MoonCount, second.MoonCount);
            Assert.AreEqual(first.SizeFactor, second.SizeFactor);
        }

        [Test]
        public void Compute_DifferentSeed_CanChangeProfile()
        {
            StarSystemState system = MakeSystem(7);

            StarSystemVisualProfile withSeedA = StarSystemVisualProfile.Compute(system, 1);
            StarSystemVisualProfile withSeedB = StarSystemVisualProfile.Compute(system, 2);

            bool anyDifference = withSeedA.BodyColor != withSeedB.BodyColor
                || withSeedA.HasRing != withSeedB.HasRing
                || withSeedA.MoonCount != withSeedB.MoonCount
                || !Mathf.Approximately(withSeedA.SizeFactor, withSeedB.SizeFactor);

            Assert.IsTrue(anyDifference, "Deux graines differentes devraient (presque toujours) donner un profil different.");
        }

        [Test]
        public void Compute_MoonCount_IsWithinValidRange()
        {
            for (int id = 0; id < 200; id++)
            {
                StarSystemVisualProfile profile = StarSystemVisualProfile.Compute(MakeSystem(id), 999);
                Assert.GreaterOrEqual(profile.MoonCount, 0);
                Assert.LessOrEqual(profile.MoonCount, StarSystemVisualProfile.MaxMoonCount);
            }
        }

        [Test]
        public void Compute_SizeFactor_IsWithinValidRange()
        {
            for (int id = 0; id < 200; id++)
            {
                StarSystemVisualProfile profile = StarSystemVisualProfile.Compute(MakeSystem(id), 999);
                Assert.GreaterOrEqual(profile.SizeFactor, 0.8f);
                Assert.LessOrEqual(profile.SizeFactor, 1.3f);
            }
        }

        [Test]
        public void Compute_AcrossManySystems_ProducesVariety()
        {
            var colors = new HashSet<Color>();
            var ringCounts = new[] { 0, 0 };
            var moonCounts = new HashSet<int>();

            for (int id = 0; id < 100; id++)
            {
                StarSystemVisualProfile profile = StarSystemVisualProfile.Compute(MakeSystem(id), 20260730);
                colors.Add(profile.BodyColor);
                ringCounts[profile.HasRing ? 1 : 0]++;
                moonCounts.Add(profile.MoonCount);
            }

            Assert.Greater(colors.Count, 1, "100 systemes devraient couvrir plusieurs couleurs de la palette.");
            Assert.Greater(ringCounts[0], 0, "Certains systemes ne devraient pas avoir d'anneau.");
            Assert.Greater(ringCounts[1], 0, "Certains systemes devraient avoir un anneau.");
            Assert.AreEqual(3, moonCounts.Count, "0, 1 et 2 lunes devraient toutes apparaitre sur 100 systemes.");
        }

        [Test]
        public void Compute_NullSystem_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => StarSystemVisualProfile.Compute(null, 1));
        }
    }
}
