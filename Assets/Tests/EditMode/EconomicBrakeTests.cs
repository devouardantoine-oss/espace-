using Espace.Gameplay.Economy;
using Espace.Gameplay.Military;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie les trois freins introduits en Phase 22 (P3, P4 et P5) : subsistance, cout
    /// d'administration et attrition de flotte.
    /// <para>
    /// L'audit avait releve que le jeu n'avait <b>que des moteurs</b> — population, production,
    /// conquete, plus de production — et aucun frein. Une faction en avance ne pouvait plus
    /// etre rattrapee. Ces tests verifient que les freins freinent reellement, et surtout
    /// <b>qu'ils ne bloquent pas</b> : une contrainte qui rend une situation irrattrapable est
    /// aussi mauvaise qu'une absence de contrainte.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class EconomicBrakeTests
    {
        // --- P3 · Subsistance ------------------------------------------------

        [Test]
        public void Food_AnOrdinarySystemFeedsItself()
        {
            // Calibrage voulu : la penurie doit sanctionner l'expansion mal preparee, pas
            // l'existence. Un monde peuple sans gisement doit vivre de sa propre agriculture.
            const int population = 2200;
            float monthlyProduction = population * 0.012f * 30f;

            Assert.Greater(monthlyProduction, SubsistenceModel.FoodDemand(population));
        }

        [Test]
        public void Food_DemandGrowsWithPopulation()
        {
            Assert.Greater(SubsistenceModel.FoodDemand(2000), SubsistenceModel.FoodDemand(1000));
            Assert.AreEqual(0f, SubsistenceModel.FoodDemand(0));
        }

        [Test]
        public void Satisfaction_IsTotalWhenNothingIsNeeded()
        {
            // Un empire sans population n'a pas faim : sans ce cas, il declencherait une famine.
            Assert.AreEqual(1f, SubsistenceModel.Satisfaction(0f, 0f));
        }

        [Test]
        public void Satisfaction_IsProportionalToWhatIsCovered()
        {
            Assert.AreEqual(0.5f, SubsistenceModel.Satisfaction(50f, 100f), 1e-5f);
            Assert.AreEqual(1f, SubsistenceModel.Satisfaction(500f, 100f), 1e-5f, "Un surplus ne fait pas mieux que rassasier.");
            Assert.AreEqual(0f, SubsistenceModel.Satisfaction(-10f, 100f), 1e-5f);
        }

        [Test]
        public void Energy_ShortageThrottlesBuildingsWithoutSilencingThem()
        {
            // Sanction immediate mais reversible, contrairement a la famine : on la corrige en
            // construisant une centrale. Une coupure totale ne doit pas figer un empire sans
            // retour possible.
            Assert.AreEqual(1f, SubsistenceModel.BuildingOutputFactor(1f), 1e-5f);
            Assert.Less(SubsistenceModel.BuildingOutputFactor(0.5f), 1f);
            Assert.Greater(SubsistenceModel.BuildingOutputFactor(0f), 0f);
        }

        [Test]
        public void Energy_DemandGrowsWithInfrastructure()
        {
            Assert.Greater(SubsistenceModel.EnergyDemand(5, 3), SubsistenceModel.EnergyDemand(1, 3));
            Assert.Greater(SubsistenceModel.EnergyDemand(2, 5), SubsistenceModel.EnergyDemand(2, 1));
        }

        // --- P4 · Administration ---------------------------------------------

        [Test]
        public void Administration_SmallEmpiresPayNothing()
        {
            // Ce n'est pas la presence qui se paie, c'est l'expansion.
            Assert.AreEqual(0f, AdministrationModel.InfluenceUpkeep(1));
            Assert.AreEqual(0f, AdministrationModel.InfluenceUpkeep(AdministrationModel.FreeSystems));
        }

        [Test]
        public void Administration_CostGrowsFasterThanTheEmpire()
        {
            // LE frein contre l'emballement : chaque conquete supplementaire coute plus cher que
            // la precedente. Sans ce terme, le premier a prendre l'avantage ne peut plus etre
            // rattrape.
            float ten = AdministrationModel.InfluenceUpkeep(13);
            float twenty = AdministrationModel.InfluenceUpkeep(23);

            Assert.Greater(twenty, ten * 2f, "Doubler les possessions doit plus que doubler la charge.");
        }

        [Test]
        public void Administration_CostPerSystemRises()
        {
            float perSystemAtTen = AdministrationModel.InfluenceUpkeep(13) / 10f;
            float perSystemAtThirty = AdministrationModel.InfluenceUpkeep(33) / 30f;

            Assert.Greater(perSystemAtThirty, perSystemAtTen);
        }

        [Test]
        public void Administration_PayingInFullCostsNoStability()
        {
            float required = AdministrationModel.InfluenceUpkeep(15);
            Assert.AreEqual(0f, AdministrationModel.Pressure(required, required));
        }

        [Test]
        public void Administration_DeficitPressureRisesThenStops()
        {
            float required = 100f;

            float small = AdministrationModel.Pressure(90f, required);
            float large = AdministrationModel.Pressure(50f, required);
            float total = AdministrationModel.Pressure(0f, required);

            Assert.Greater(large, small);

            // Plafonnee : au-dela d'un manque de moitie, la sanction cesse de croitre. Une
            // spirale sans fond rendrait la situation irrattrapable, ce qui est exactement ce
            // qu'on cherche a eviter.
            Assert.AreEqual(large, total, 1e-5f);
            Assert.LessOrEqual(total, AdministrationModel.MaximumPressure);
        }

        [Test]
        public void Administration_PressureFeedsStability()
        {
            // La pression se branche sur le parametre que StabilityModel prevoyait deja.
            float pressure = AdministrationModel.Pressure(0f, 100f);

            Assert.Less(
                StabilityModel.TargetFor(3, 0.25f, pressure),
                StabilityModel.TargetFor(3, 0.25f));
        }

        [Test]
        public void Administration_AnOverextendedEmpireCanStillRecover()
        {
            // Verification du point le plus important : le frein ne doit pas etre un piege. Un
            // empire surextendu qui cesse de s'etendre doit remonter la pente.
            float stability = 0.4f;
            for (int month = 0; month < 60; month++)
            {
                stability = StabilityModel.Next(stability, 3, 0.25f, externalPressure: 0f);
            }

            Assert.Greater(stability, 0.7f);
        }

        // --- P5 · Attrition de flotte -----------------------------------------

        [Test]
        public void Upkeep_CostsMineralsAsWellAsCredits()
        {
            // Relie la puissance militaire a la capacite industrielle : un empire riche mais
            // sans mines ne peut plus armer sans limite.
            Assert.Greater(FleetUpkeepModel.MineralUpkeepFor(100f), 0f);
            Assert.Less(FleetUpkeepModel.MineralUpkeepFor(100f), 100f, "Les minerais restent un appoint, pas le cout principal.");
        }

        [Test]
        public void Attrition_IsZeroWhenFullyPaid()
        {
            Assert.AreEqual(0f, FleetUpkeepModel.AttritionFraction(500f, 500f));
            Assert.AreEqual(1f, FleetUpkeepModel.SurvivingFraction(500f, 500f));
        }

        [Test]
        public void Attrition_IsZeroWithoutAFleet()
        {
            Assert.AreEqual(0f, FleetUpkeepModel.AttritionFraction(0f, 0f));
        }

        [Test]
        public void Attrition_GrowsWithWhatWasNotPaid()
        {
            Assert.Greater(
                FleetUpkeepModel.AttritionFraction(0f, 500f),
                FleetUpkeepModel.AttritionFraction(250f, 500f));
        }

        [Test]
        public void Attrition_IsGradualEnoughToReactTo()
        {
            // Une desertion instantanee transformerait une erreur de tresorerie en defaite
            // definitive. Un mois d'impaye total doit couter cher sans etre fatal.
            float surviving = FleetUpkeepModel.SurvivingFraction(0f, 500f);

            Assert.Greater(surviving, 0.8f, "Un mois d'impaye ne doit pas emporter une flotte.");
            Assert.Less(surviving, 1f, "Mais il doit couter quelque chose.");
        }

        [Test]
        public void Attrition_UnpaidFleetMeltsOverTime()
        {
            // Le pendant du test precedent : ignorer durablement l'entretien doit finir par
            // dissoudre la flotte, sinon le cout redevient facultatif.
            float remaining = 1f;
            for (int month = 0; month < 24; month++)
            {
                remaining *= FleetUpkeepModel.SurvivingFraction(0f, 500f);
            }

            Assert.Less(remaining, 0.1f, "Deux ans sans payer doivent laisser moins d'un dixieme de la flotte.");
        }
    }
}
