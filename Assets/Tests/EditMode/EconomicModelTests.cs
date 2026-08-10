using Espace.Gameplay.Economy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie les quatre modeles economiques introduits en Phase 22 (P1 et P2).
    /// <para>
    /// Ce sont des fonctions pures : aucun service, aucune scene, aucun moteur. C'est ce qui
    /// permet de verifier des <b>proprietes</b> — « il existe un optimum », « la croissance
    /// s'arrete a la capacite », « un retard se rattrape » — et pas seulement des valeurs. Une
    /// courbe d'equilibrage fausse ne plante jamais : elle rend le jeu ennuyeux, et rien ne le
    /// signale.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class EconomicModelTests
    {
        // --- Fiscalite ------------------------------------------------------

        [Test]
        public void EffectiveRate_IsUntouchedBelowTheEvasionThreshold()
        {
            // Choix delibere : la formule historique reste exacte dans la plage ou le jeu se
            // joue habituellement, ce qui evite de reequilibrer tout le contenu existant.
            Assert.AreEqual(0f, TaxationModel.EffectiveRate(0f, 1f), 1e-5f);
            Assert.AreEqual(0.25f, TaxationModel.EffectiveRate(0.25f, 1f), 1e-5f);
            Assert.AreEqual(TaxationModel.EvasionThreshold, TaxationModel.EffectiveRate(TaxationModel.EvasionThreshold, 1f), 1e-5f);
        }

        [Test]
        public void EffectiveRate_FallsBehindTheDisplayedRateAboveTheThreshold()
        {
            Assert.Less(TaxationModel.EffectiveRate(0.60f, 1f), 0.60f);
            Assert.Less(TaxationModel.EffectiveRate(1f, 1f), 1f);
        }

        [Test]
        public void EffectiveRate_HasAnInteriorOptimum()
        {
            // LE test de cette phase. Si le rendement croissait jusqu'a 100 %, le curseur
            // n'aurait qu'une position correcte et la decision fiscale n'existerait pas.
            float best = 0f;
            float bestRate = 0f;

            for (int i = 0; i <= 100; i++)
            {
                float rate = i / 100f;
                float yield = TaxationModel.EffectiveRate(rate, 1f);
                if (yield > best)
                {
                    best = yield;
                    bestRate = rate;
                }
            }

            Assert.Greater(bestRate, TaxationModel.EvasionThreshold, "L'optimum doit se situer au-dela du seuil d'evasion.");
            Assert.Less(bestRate, 0.95f, "L'optimum ne doit pas etre au maximum du curseur.");
            Assert.Less(TaxationModel.EffectiveRate(1f, 1f), best, "Taxer a 100 % doit rapporter moins que l'optimum.");
        }

        /// <summary>Taux affiche qui rapporte le plus, a stabilite donnee. Balaye le curseur cran par cran, comme le ferait un joueur.</summary>
        private static float BestRateAt(float stability)
        {
            float best = 0f;
            float bestRate = 0f;

            for (int i = 0; i <= 100; i++)
            {
                float yield = TaxationModel.EffectiveRate(i / 100f, stability);
                if (yield > best)
                {
                    best = yield;
                    bestRate = i / 100f;
                }
            }

            return bestRate;
        }

        [Test]
        public void EffectiveRate_OptimumMovesWithStability()
        {
            // C'est ce qui empeche le joueur d'apprendre un chiffre une fois pour toutes :
            // un empire trouble ne supporte pas la pression qu'un empire stable encaisse.
            Assert.Greater(BestRateAt(1f), BestRateAt(0.5f), "Un empire stable doit supporter un taux plus eleve.");
        }

        [Test]
        public void EffectiveRate_NeverCollapsesToNothing()
        {
            Assert.Greater(TaxationModel.EffectiveRate(1f, 0.05f), 0f, "Meme sous une pression absurde, l'administration collecte quelque chose.");
        }

        [Test]
        public void Discontent_StartsBeforeEvasion()
        {
            // On rale avant de frauder : entre les deux seuils se trouve la plage ou l'on peut
            // financer une guerre en acceptant d'etre impopulaire sans ruiner ses finances.
            Assert.Less(TaxationModel.DiscontentThreshold, TaxationModel.EvasionThreshold);
            Assert.AreEqual(0f, TaxationModel.Discontent(0.25f));
            Assert.Greater(TaxationModel.Discontent(0.50f), 0f);
            Assert.Greater(TaxationModel.Discontent(0.90f), TaxationModel.Discontent(0.50f));
        }

        // --- Population -----------------------------------------------------

        [Test]
        public void Population_GrowsTowardsCapacityAndStopsThere()
        {
            int capacity = PopulationModel.CapacityFor(2);
            int population = 100;

            for (int month = 0; month < 1200; month++)
            {
                population = PopulationModel.Next(population, 2, 0.8f);
            }

            Assert.AreEqual(capacity, population, "Un siecle doit suffire a saturer un systeme, sans jamais le depasser.");
        }

        /// <summary>Croissance mensuelle rapportee a la population, au niveau de developpement 3 et stabilite parfaite.</summary>
        private static float MonthlyGrowthRate(int population)
        {
            return (PopulationModel.Next(population, 3, 1f) - population) / (float)population;
        }

        [Test]
        public void Population_GrowthRateSlowsAsItFillsUp()
        {
            // Croissance logistique : c'est le premier frein a l'emballement. Sans lui, le plus
            // gros empire grossit le plus vite et personne ne le rattrape.
            //
            // Attention a la propriete testee : l'accroissement *absolu* d'une logistique est
            // symetrique autour de la moitie de la capacite — a 10 % et a 90 % de remplissage il
            // vaut exactement la meme chose. Une premiere version de ce test comparait ces deux
            // points et echouait sur une egalite parfaitement correcte. C'est le taux *relatif*
            // qui decroit, et c'est lui qui compte : il dit de combien de pour cent un systeme
            // grandit chaque mois.
            int capacity = PopulationModel.CapacityFor(3);

            Assert.Greater(MonthlyGrowthRate(capacity / 10), MonthlyGrowthRate(capacity / 2));
            Assert.Greater(MonthlyGrowthRate(capacity / 2), MonthlyGrowthRate(capacity * 9 / 10));
        }

        [Test]
        public void Population_AbsoluteGrowthPeaksMidwayThenFalls()
        {
            // Le pendant du test precedent : passe la moitie, meme le nombre d'habitants gagnes
            // chaque mois diminue. Un empire mur cesse donc de creuser l'ecart.
            int capacity = PopulationModel.CapacityFor(3);

            int atHalf = PopulationModel.Next(capacity / 2, 3, 1f) - capacity / 2;
            int atNinety = PopulationModel.Next(capacity * 9 / 10, 3, 1f) - capacity * 9 / 10;

            Assert.Greater(atHalf, atNinety);
        }

        [Test]
        public void Population_DevelopmentRaisesTheCeiling()
        {
            // La raison de fond d'investir : on n'achete pas un bonus, on releve un plafond.
            Assert.Greater(PopulationModel.CapacityFor(3), PopulationModel.CapacityFor(1));
        }

        [Test]
        public void Population_StagnatesWhenUnstable()
        {
            int population = 500;
            Assert.AreEqual(population, PopulationModel.Next(population, 2, 0.2f), "On ne s'installe pas dans un systeme en troubles.");
        }

        [Test]
        public void Population_DeclinesWhenStarving()
        {
            Assert.Less(PopulationModel.Next(1000, 3, 1f, foodSatisfaction: 0.5f), 1000);
            Assert.Less(PopulationModel.Next(1000, 3, 1f, foodSatisfaction: 0f), PopulationModel.Next(1000, 3, 1f, foodSatisfaction: 0.5f));
        }

        [Test]
        public void Population_EmptySystemStaysEmpty()
        {
            Assert.AreEqual(0, PopulationModel.Next(0, 5, 1f), "La population n'apparait pas spontanement : elle arrive par colonisation.");
        }

        [Test]
        public void Population_ShrinksBackWhenTheCeilingDrops()
        {
            // Cas reel : un sabotage fait perdre un niveau de developpement.
            int overcrowded = PopulationModel.CapacityFor(4);
            Assert.Less(PopulationModel.Next(overcrowded, 1, 1f), overcrowded);
        }

        [Test]
        public void Population_NeverGoesNegative()
        {
            int population = 5;
            for (int month = 0; month < 500; month++)
            {
                population = PopulationModel.Next(population, 0, 1f, foodSatisfaction: 0f);
                Assert.GreaterOrEqual(population, 0);
            }
        }

        // --- Richesse -------------------------------------------------------

        [Test]
        public void Wealth_LowTaxBuildsMoreThanHighTax()
        {
            // La contrepartie qui manquait a la fiscalite : taxer fort appauvrit la base
            // imposable. Le joueur arbitre entre son present et son futur.
            int low = 100;
            int high = 100;

            for (int month = 0; month < 120; month++)
            {
                low = WealthModel.Next(low, 2000, 3, 0.10f, 0.9f);
                high = WealthModel.Next(high, 2000, 3, 0.70f, 0.9f);
            }

            Assert.Greater(low, high, "Dix ans a 10 % doivent laisser une base imposable plus large qu'a 70 %.");
        }

        [Test]
        public void Wealth_ConvergesBelowItsCapacity()
        {
            int capacity = Mathf.CeilToInt(WealthModel.CapacityFor(2000, 3));
            int wealth = 50;

            for (int month = 0; month < 1200; month++)
            {
                wealth = WealthModel.Next(wealth, 2000, 3, 0.20f, 0.9f);
                Assert.LessOrEqual(wealth, capacity);
            }

            Assert.Greater(wealth, capacity / 2, "La richesse doit tout de meme s'approcher serieusement de son plafond.");
        }

        [Test]
        public void Wealth_ErodesWhenNothingSustainsIt()
        {
            // C'est l'erosion qui ouvre la porte au rattrapage : une richesse acquise n'est pas
            // acquise pour toujours.
            Assert.Less(WealthModel.Next(1000, 0, 0, 0.25f, 0.9f), 1000);
        }

        [Test]
        public void Wealth_StartsFromNothingWhenPopulated()
        {
            Assert.Greater(WealthModel.Next(0, 500, 1, 0.20f, 0.9f), 0, "Une colonie neuve doit pouvoir s'enrichir depuis zero.");
        }

        [Test]
        public void Wealth_NeverGoesNegative()
        {
            int wealth = 10;
            for (int month = 0; month < 500; month++)
            {
                wealth = WealthModel.Next(wealth, 0, 0, 1f, 0f);
                Assert.GreaterOrEqual(wealth, 0);
            }
        }

        // --- Stabilite ------------------------------------------------------

        [Test]
        public void Stability_RecoversAfterARevolt()
        {
            // Avant la Phase 22, une revolte etait definitive : la stabilite ne remontait
            // jamais. Une punition permanente est moins interessante qu'un creux dont on se
            // releve.
            float stability = 0.2f;
            for (int month = 0; month < 60; month++)
            {
                stability = StabilityModel.Next(stability, 3, 0.25f);
            }

            Assert.Greater(stability, 0.7f);
        }

        [Test]
        public void Stability_SettlesLowerUnderHeavyTaxation()
        {
            float light = 0.7f;
            float heavy = 0.7f;

            for (int month = 0; month < 120; month++)
            {
                light = StabilityModel.Next(light, 3, 0.20f);
                heavy = StabilityModel.Next(heavy, 3, 0.80f);
            }

            Assert.Greater(light, heavy);
        }

        [Test]
        public void Stability_ReactsSlowlyEnoughToAllowAWarEffort()
        {
            // L'arbitrage vise : serrer la vis quelques mois puis relacher avant que ca casse.
            float stability = 0.9f;
            for (int month = 0; month < 6; month++)
            {
                stability = StabilityModel.Next(stability, 3, 0.75f);
            }

            Assert.Greater(stability, 0.6f, "Six mois de pression forte ne doivent pas effondrer un empire sain.");
        }

        [Test]
        public void Stability_StaysWithinItsBounds()
        {
            foreach (float tax in new[] { 0f, 0.5f, 1f })
            {
                float stability = 0.5f;
                for (int month = 0; month < 200; month++)
                {
                    stability = StabilityModel.Next(stability, 5, tax, externalPressure: tax);
                    Assert.GreaterOrEqual(stability, 0f);
                    Assert.LessOrEqual(stability, 1f);
                }
            }
        }

        [Test]
        public void Stability_DevelopmentHelpsHoldTheLine()
        {
            Assert.Greater(StabilityModel.TargetFor(5, 0.4f), StabilityModel.TargetFor(0, 0.4f));
        }
    }
}
