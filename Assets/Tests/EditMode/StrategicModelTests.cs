using Espace.Gameplay.Empires;
using Espace.Gameplay.Espionage;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie l'espionnage gradue et l'evaluation strategique (Phase 22, P6 et P7).
    /// <para>
    /// Les deux systemes reposent sur des fonctions pures, ce qui permet de verifier des
    /// <b>distributions</b> et des <b>ordres de priorite</b> plutot que des valeurs isolees.
    /// Une IA qui prend une mauvaise decision ne plante jamais : elle joue mal, et rien ne le
    /// signale.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class StrategicModelTests
    {
        // --- P6 · Espionnage --------------------------------------------------

        [Test]
        public void SuccessChance_IsEvenBetweenEqualPowers()
        {
            // L'ancienne regle donnait la victoire a l'attaquant des que la defense etait
            // inferieure — et elle l'etait toujours, la defense valant 10 x stabilite.
            Assert.AreEqual(0.5f, EspionageResolution.SuccessChance(10f, 10f), 1e-4f);
            Assert.AreEqual(0.5f, EspionageResolution.SuccessChance(100f, 100f), 1e-4f, "Le rapport de forces, pas la puissance absolue.");
        }

        [Test]
        public void SuccessChance_IsNeverCertainNorHopeless()
        {
            Assert.LessOrEqual(EspionageResolution.SuccessChance(10000f, 1f), EspionageResolution.MaximumChance);
            Assert.GreaterOrEqual(EspionageResolution.SuccessChance(1f, 10000f), EspionageResolution.MinimumChance);
        }

        [Test]
        public void AttackPower_HasDiminishingReturnsOnInfluence()
        {
            // Sans rendements decroissants, un empire riche s'achete la certitude et l'on
            // revient au systeme binaire d'avant.
            float single = EspionageResolution.AttackPower(100f, 1f, 0f);
            float quadruple = EspionageResolution.AttackPower(400f, 1f, 0f);

            Assert.Greater(quadruple, single);
            Assert.Less(quadruple, single * 3f, "Quadrupler la mise ne doit pas quadrupler la puissance.");
        }

        [Test]
        public void DefencePower_RisesWithVigilance()
        {
            // Frapper deux fois au meme endroit doit devenir plus dur, sans qu'un delai
            // arbitraire soit impose.
            Assert.Greater(
                EspionageResolution.DefencePower(0.8f, 0f, vigilance: 1f),
                EspionageResolution.DefencePower(0.8f, 0f, vigilance: 0f));
        }

        [Test]
        public void Exposure_IsWorseWhenTheOperationFails()
        {
            // C'est en echouant qu'on laisse des traces.
            Assert.Greater(
                EspionageResolution.ExposureChance(10f, 10f, succeeded: false),
                EspionageResolution.ExposureChance(10f, 10f, succeeded: true));
        }

        [Test]
        public void Exposure_FallsWhenTheAttackerDominates()
        {
            Assert.Less(
                EspionageResolution.ExposureChance(100f, 10f, succeeded: true),
                EspionageResolution.ExposureChance(10f, 100f, succeeded: true));
        }

        [Test]
        public void Resolve_ProducesAllFourOutcomes()
        {
            // Quatre issues et non deux : reussir et se faire voir sont deux questions
            // distinctes. C'est cette separation qui rend un echec supportable et une reussite
            // parfois regrettable.
            bool discreet = false, attributed = false, failed = false, exposed = false;

            for (int i = 0; i < 20; i++)
            {
                for (int j = 0; j < 20; j++)
                {
                    EspionageOutcome outcome = EspionageResolution.Resolve(10f, 10f, i / 20f, j / 20f);
                    discreet |= outcome == EspionageOutcome.Discreet;
                    attributed |= outcome == EspionageOutcome.Attributed;
                    failed |= outcome == EspionageOutcome.Failed;
                    exposed |= outcome == EspionageOutcome.Exposed;
                }
            }

            Assert.IsTrue(discreet && attributed && failed && exposed, "Les quatre issues doivent etre atteignables a forces egales.");
        }

        [Test]
        public void Resolve_MoreInfluenceMeansMoreSuccesses()
        {
            // La propriete qui rend la decision continue : engager plus doit rapporter plus,
            // de facon monotone.
            int weak = CountSuccesses(EspionageResolution.AttackPower(25f, 1f, 0f), 10f);
            int strong = CountSuccesses(EspionageResolution.AttackPower(400f, 1f, 0f), 10f);

            Assert.Greater(strong, weak);
        }

        /// <summary>Nombre de reussites sur cent tirages regulierement repartis.</summary>
        private static int CountSuccesses(float attack, float defence)
        {
            int successes = 0;
            for (int i = 0; i < 100; i++)
            {
                if (EspionageResolution.Succeeded(EspionageResolution.Resolve(attack, defence, i / 100f, 0.5f)))
                {
                    successes++;
                }
            }

            return successes;
        }

        // --- P7 · Evaluation strategique ---------------------------------------

        [Test]
        public void Posture_BrokeEmpireConsolidatesEvenWhenDominant()
        {
            // Le defaut le plus visible de l'ancienne IA : elle partait en guerre sans regarder
            // sa tresorerie. Survivre passe avant frapper.
            var assessment = new EmpireAssessment(
                financialRunwayMonths: 0.5f, averageStability: 0.9f, militaryRatio: 3f, growthHeadroom: 0.05f, systemCount: 12);

            Assert.AreEqual(StrategicPosture.Consolidating, assessment.Posture);
        }

        [Test]
        public void Posture_UnstableEmpireConsolidates()
        {
            var assessment = new EmpireAssessment(6f, 0.4f, 2f, 0.05f, 10);
            Assert.AreEqual(StrategicPosture.Consolidating, assessment.Posture);
        }

        [Test]
        public void Posture_WeakEmpireDefends()
        {
            var assessment = new EmpireAssessment(6f, 0.85f, 0.4f, 0.5f, 5);
            Assert.AreEqual(StrategicPosture.Defending, assessment.Posture);
        }

        [Test]
        public void Posture_HealthyEmpireWithRoomExpands()
        {
            var assessment = new EmpireAssessment(6f, 0.85f, 1.1f, 0.6f, 6);
            Assert.AreEqual(StrategicPosture.Expanding, assessment.Posture);
        }

        [Test]
        public void Posture_DominantAndFullTurnsAggressive()
        {
            // On ne frappe que lorsqu'on domine *et* qu'il n'y a plus a grandir chez soi :
            // conquerir est toujours plus cher que developper.
            var assessment = new EmpireAssessment(6f, 0.85f, 2.5f, 0.05f, 15);
            Assert.AreEqual(StrategicPosture.Aggressive, assessment.Posture);
        }

        [Test]
        public void Posture_DominantButWithRoomStillExpands()
        {
            var assessment = new EmpireAssessment(6f, 0.85f, 2.5f, 0.6f, 15);
            Assert.AreEqual(StrategicPosture.Expanding, assessment.Posture);
        }

        [Test]
        public void TaxRate_TightensWhenBrokeAndRelaxesWhenSafe()
        {
            // Le remplacement direct du taux fixe par personnalite.
            var broke = new EmpireAssessment(0.5f, 0.9f, 1f, 0.3f, 8);
            var comfortable = new EmpireAssessment(8f, 0.9f, 1.1f, 0.5f, 8);

            Assert.Greater(broke.SuggestedTaxRate(), comfortable.SuggestedTaxRate());
        }

        [Test]
        public void TaxRate_NeverEntersTheCounterProductiveRange()
        {
            // Aucune posture ne doit conduire l'IA au-dela du point ou l'evasion fait baisser
            // le revenu reel. Elle joue avec les memes regles que le joueur, elle doit donc
            // aussi jouer avec la meme prudence.
            foreach (var assessment in new[]
            {
                new EmpireAssessment(0.2f, 0.9f, 1f, 0.3f, 8),
                new EmpireAssessment(6f, 0.4f, 1f, 0.3f, 8),
                new EmpireAssessment(6f, 0.9f, 0.3f, 0.3f, 8),
                new EmpireAssessment(6f, 0.9f, 3f, 0.05f, 8),
                new EmpireAssessment(6f, 0.9f, 1.1f, 0.8f, 8),
            })
            {
                Assert.LessOrEqual(assessment.SuggestedTaxRate(), 0.5f, $"Posture {assessment.Posture} : taux trop eleve.");
                Assert.GreaterOrEqual(assessment.SuggestedTaxRate(), 0.15f);
            }
        }

        [Test]
        public void MilitarySpending_IsCutOnlyWhenConsolidating()
        {
            Assert.IsTrue(new EmpireAssessment(0.5f, 0.9f, 1f, 0.3f, 8).ShouldCutMilitarySpending());
            Assert.IsFalse(new EmpireAssessment(6f, 0.9f, 1.1f, 0.5f, 8).ShouldCutMilitarySpending());
        }

        [Test]
        public void Explain_AlwaysSaysSomethingUseful()
        {
            // Critere retenu dans l'audit : le joueur doit pouvoir comprendre pourquoi un voisin
            // vient de changer de comportement.
            foreach (var assessment in new[]
            {
                new EmpireAssessment(0.2f, 0.9f, 1f, 0.3f, 8),
                new EmpireAssessment(6f, 0.4f, 1f, 0.3f, 8),
                new EmpireAssessment(6f, 0.9f, 0.3f, 0.3f, 8),
                new EmpireAssessment(6f, 0.9f, 3f, 0.05f, 8),
                new EmpireAssessment(6f, 0.9f, 1.1f, 0.8f, 8),
            })
            {
                Assert.IsTrue(assessment.Explain().Length > 10);
            }
        }

        [Test]
        public void Assessment_ClampsAbsurdInputs()
        {
            var assessment = new EmpireAssessment(-5f, 4f, -1f, 9f, -3);

            Assert.AreEqual(0f, assessment.FinancialRunwayMonths);
            Assert.AreEqual(1f, assessment.AverageStability);
            Assert.AreEqual(0f, assessment.MilitaryRatio);
            Assert.AreEqual(1f, assessment.GrowthHeadroom);
            Assert.AreEqual(0, assessment.SystemCount);
        }
    }
}
