using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.UI;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie le lisere d'etat (Phase 23, tranche B).
    /// <para>
    /// Le lisere remplace des nombres exacts par quatre barres. Ce qu'il faut proteger, ce n'est
    /// donc pas une valeur mais une <b>correspondance</b> : une situation degradee doit toujours
    /// produire un segment court et alarmant. Une regression ici ne planterait pas — elle
    /// rassurerait le joueur a tort.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class EmpireStateBandTests
    {
        private const float FullResearch = 0.5f;

        private static EmpireAssessment Healthy()
        {
            return new EmpireAssessment(
                financialRunwayMonths: EmpireStateBand.ComfortableRunwayMonths,
                averageStability: 0.95f,
                militaryRatio: EmpireAssessment.DominantRatio,
                growthHeadroom: 0.5f,
                systemCount: 8);
        }

        [Test]
        public void Band_AlwaysHasItsFourSegments()
        {
            Assert.AreEqual(EmpireStateBand.SegmentCount, EmpireStateBand.From(Healthy(), FullResearch).Length);
        }

        [Test]
        public void Band_FillsStayWithinZeroAndOne()
        {
            // Des valeurs absurdes ne doivent jamais produire un segment qui deborde de sa part.
            var extreme = new EmpireAssessment(9999f, 5f, 99f, 2f, 40);

            foreach (StateSegment segment in EmpireStateBand.From(extreme, 12f))
            {
                Assert.GreaterOrEqual(segment.Fill, 0f);
                Assert.LessOrEqual(segment.Fill, 1f);
            }
        }

        [Test]
        public void HealthyEmpire_RaisesNoAlarm()
        {
            foreach (StateSegment segment in EmpireStateBand.From(Healthy(), FullResearch))
            {
                Assert.IsFalse(segment.Alarming, $"Segment {segment.Label} ne devrait pas alerter sur un empire sain.");
            }
        }

        [Test]
        public void BrokeEmpire_AlarmsOnTreasuryOnly()
        {
            var broke = new EmpireAssessment(0.4f, 0.95f, EmpireAssessment.DominantRatio, 0.5f, 8);
            StateSegment[] band = EmpireStateBand.From(broke, FullResearch);

            Assert.IsTrue(band[0].Alarming, "La tresorerie doit alerter.");
            Assert.IsFalse(band[1].Alarming);
            Assert.IsFalse(band[2].Alarming);
        }

        [Test]
        public void ThreatenedEmpire_AlarmsOnForces()
        {
            var threatened = new EmpireAssessment(
                EmpireStateBand.ComfortableRunwayMonths, 0.95f, EmpireAssessment.ThreatenedRatio - 0.1f, 0.5f, 8);

            Assert.IsTrue(EmpireStateBand.From(threatened, FullResearch)[2].Alarming);
        }

        [Test]
        public void UnstableEmpire_AlarmsOnStability()
        {
            var unstable = new EmpireAssessment(
                EmpireStateBand.ComfortableRunwayMonths, EmpireAssessment.CriticalStability - 0.05f,
                EmpireAssessment.DominantRatio, 0.5f, 8);

            Assert.IsTrue(EmpireStateBand.From(unstable, FullResearch)[1].Alarming);
        }

        [Test]
        public void Research_NeverAlarms()
        {
            // Une recherche lente est un choix ou une consequence, jamais une urgence. Reserver
            // l'alerte aux trois autres segments est ce qui lui garde son sens : un lisere ou
            // tout clignote n'attire plus l'attention sur rien.
            Assert.IsFalse(EmpireStateBand.From(Healthy(), 0f)[3].Alarming);
            Assert.IsFalse(EmpireStateBand.From(Healthy(), 1f)[3].Alarming);
        }

        [Test]
        public void Fills_FallAsTheSituationDegrades()
        {
            var comfortable = Healthy();
            var strained = new EmpireAssessment(
                EmpireAssessment.CriticalRunwayMonths, 0.4f, 0.3f, 0.5f, 8);

            StateSegment[] good = EmpireStateBand.From(comfortable, 0.9f);
            StateSegment[] bad = EmpireStateBand.From(strained, 0.1f);

            for (int i = 0; i < EmpireStateBand.SegmentCount; i++)
            {
                Assert.Greater(good[i].Fill, bad[i].Fill, $"Segment {good[i].Label}");
            }
        }

        [Test]
        public void TreasuryFill_SaturatesRatherThanGrowingForever()
        {
            // Au-dela du confortable, accumuler ne change plus aucune decision : le segment est
            // deja plein et ne doit pas donner l'illusion d'une marge supplementaire.
            var rich = new EmpireAssessment(EmpireStateBand.ComfortableRunwayMonths * 10f, 0.95f, 1f, 0.5f, 8);

            Assert.AreEqual(1f, EmpireStateBand.From(rich, FullResearch)[0].Fill, 1e-4f);
        }

        // --- Teintes ------------------------------------------------------------

        [Test]
        public void Colors_SpeakTheSameLanguageAsThePlanets()
        {
            // La carte et le lisere partagent leurs trois teintes : un lisere qui vire a l'ambre
            // annonce des halos qui virent a l'ambre. Si quelqu'un dissocie les deux palettes,
            // les deux moities de l'interface cessent de se repondre.
            AssertSameColor(SystemGlyph.CalmHalo, EmpireStateBand.HealthColor(10f, 1f, 5f));
            AssertSameColor(SystemGlyph.StrainedHalo, EmpireStateBand.HealthColor(1f, 1f, 5f));
            AssertSameColor(SystemGlyph.TroubledHalo, EmpireStateBand.HealthColor(0f, 1f, 5f));
        }

        [Test]
        public void StabilityColor_IsExactlyThePlanetHalo()
        {
            for (int i = 0; i <= 10; i++)
            {
                float stability = i / 10f;
                var assessment = new EmpireAssessment(6f, stability, 1f, 0.5f, 8);

                AssertSameColor(SystemGlyph.HaloColorFor(stability), EmpireStateBand.From(assessment, 0.5f)[1].Color);
            }
        }

        [Test]
        public void HealthColor_ToleratesDegenerateBounds()
        {
            // Un seuil d'alarme egal au niveau confortable ferait diviser par zero dans une
            // interpolation naive.
            Assert.DoesNotThrow(() => EmpireStateBand.HealthColor(1f, 2f, 2f));
        }

        /// <summary>
        /// Comparaison canal par canal. <c>Color.Lerp</c> n'est pas exact a ses extremes : une
        /// egalite stricte echouerait ici comme dans Unity.
        /// </summary>
        private static void AssertSameColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f, "canal rouge");
            Assert.AreEqual(expected.g, actual.g, 1e-3f, "canal vert");
            Assert.AreEqual(expected.b, actual.b, 1e-3f, "canal bleu");
        }
    }
}
