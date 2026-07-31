using System;
using Espace.Data;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="ColonizationRules"/> : bornes sur toutes les plages generees par
    /// <c>GalaxyGenerator</c> (population 0-4000, developpement 0-5, stabilite 0.2-1.0),
    /// monotonie, et coherence des pertes.
    /// </summary>
    [TestFixture]
    public sealed class ColonizationRulesTests
    {
        /// <summary>Bornes effectivement produites par <c>GalaxyGenerator</c> — toute la plage a couvrir.</summary>
        private const int MaxGeneratedPopulation = 4000;
        private const int MaxGeneratedDevelopment = 5;
        private const float MinGeneratedStability = 0.2f;

        private static StarSystemState MakeSystem(int population, int developmentLevel, float stability) =>
            new StarSystemState(
                new StarSystemId(1), "Cible", Vector2.zero, population, 50, developmentLevel, stability,
                Array.Empty<ResourceType>());

        [Test]
        public void RequiredInfantry_AcrossAllGeneratedRanges_StaysWithinOneToSix()
        {
            for (int population = 0; population <= MaxGeneratedPopulation; population += 100)
            {
                for (int development = 0; development <= MaxGeneratedDevelopment; development++)
                {
                    int required = ColonizationRules.RequiredInfantry(MakeSystem(population, development, 1f));

                    Assert.GreaterOrEqual(required, 1, $"pop={population}, dev={development}");
                    Assert.LessOrEqual(required, 6, $"pop={population}, dev={development} : doit rester sous le plafond de 10 unites par flotte.");
                }
            }
        }

        [Test]
        public void RequiredInfantry_IsMonotonicInDevelopment()
        {
            int previous = 0;

            for (int development = 0; development <= MaxGeneratedDevelopment; development++)
            {
                int required = ColonizationRules.RequiredInfantry(MakeSystem(1000, development, 1f));
                Assert.GreaterOrEqual(required, previous, "Un systeme plus developpe ne doit jamais exiger moins d'Infanterie.");
                previous = required;
            }
        }

        [Test]
        public void RequiredInfantry_IsMonotonicInPopulation()
        {
            int previous = 0;

            for (int population = 0; population <= MaxGeneratedPopulation; population += 250)
            {
                int required = ColonizationRules.RequiredInfantry(MakeSystem(population, 3, 1f));
                Assert.GreaterOrEqual(required, previous, "Un systeme plus peuple ne doit jamais exiger moins d'Infanterie.");
                previous = required;
            }
        }

        [Test]
        public void RequiredInfantry_KnownValues()
        {
            // Systeme vierge minimal.
            Assert.AreEqual(1, ColonizationRules.RequiredInfantry(MakeSystem(0, 0, 1f)));
            // Defaut des systemes de test (Pop 1000, Dev 3) : 1 + 0 + 2.
            Assert.AreEqual(3, ColonizationRules.RequiredInfantry(MakeSystem(1000, 3, 1f)));
            // Maximum genere (Pop 4000, Dev 5) : 1 + 2 + 3.
            Assert.AreEqual(6, ColonizationRules.RequiredInfantry(MakeSystem(4000, 5, 1f)));
        }

        [Test]
        public void RequiredInfantry_NullTarget_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ColonizationRules.RequiredInfantry(null));
        }

        [Test]
        public void InfantryLost_AlwaysBetweenOneAndRequired()
        {
            for (int population = 0; population <= MaxGeneratedPopulation; population += 250)
            {
                for (int development = 0; development <= MaxGeneratedDevelopment; development++)
                {
                    for (float stability = MinGeneratedStability; stability <= 1f; stability += 0.1f)
                    {
                        StarSystemState system = MakeSystem(population, development, stability);
                        int required = ColonizationRules.RequiredInfantry(system);
                        int lost = ColonizationRules.InfantryLost(system);

                        Assert.GreaterOrEqual(lost, 1, "Des colons restent toujours sur place.");
                        Assert.LessOrEqual(lost, required, "On ne peut pas perdre plus d'Infanterie qu'on en a engagee.");
                    }
                }
            }
        }

        [Test]
        public void InfantryLost_LowerStabilityCostsMoreOrEqual()
        {
            int previous = int.MaxValue;

            for (float stability = MinGeneratedStability; stability <= 1f; stability += 0.1f)
            {
                int lost = ColonizationRules.InfantryLost(MakeSystem(4000, 5, stability));
                Assert.LessOrEqual(lost, previous, "Une meilleure stabilite ne doit jamais couter plus de colons.");
                previous = lost;
            }
        }

        [Test]
        public void InfantryLost_AtMinimumStability_ConsumesEverything()
        {
            StarSystemState system = MakeSystem(4000, 5, MinGeneratedStability);

            Assert.AreEqual(
                ColonizationRules.RequiredInfantry(system), ColonizationRules.InfantryLost(system),
                "Un systeme au bord de la revolte consomme toute la vague de colons.");
        }

        [Test]
        public void InfantryLost_AtFullStability_KnownValues()
        {
            // Exigence 3, stabilite 1.0 -> ceil(3 * 0.25) = 1.
            Assert.AreEqual(1, ColonizationRules.InfantryLost(MakeSystem(1000, 3, 1f)));
            // Exigence 6, stabilite 1.0 -> ceil(6 * 0.25) = 2.
            Assert.AreEqual(2, ColonizationRules.InfantryLost(MakeSystem(4000, 5, 1f)));
        }

        [Test]
        public void Rules_AreDeterministic()
        {
            StarSystemState system = MakeSystem(2500, 4, 0.6f);

            Assert.AreEqual(ColonizationRules.RequiredInfantry(system), ColonizationRules.RequiredInfantry(system));
            Assert.AreEqual(ColonizationRules.InfantryLost(system), ColonizationRules.InfantryLost(system));
        }
    }
}
