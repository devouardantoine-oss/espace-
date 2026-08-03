using System.Collections.Generic;
using System.Reflection;
using Espace.Gameplay.Military;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Couverture de l'arithmetique de voyage et de la prevision d'offensive (Phase 20).
    /// <para>
    /// La propriete qui compte : <b>echelonner ses flottes, c'est se faire battre en detail</b>.
    /// La prevision doit le montrer, sinon elle promet au joueur une victoire que le jeu ne lui
    /// donnera pas — <c>MilitaryService</c> resolvant une bataille par arrivee, jamais une
    /// bataille combinee.
    /// </para>
    /// </summary>
    [TestFixture]
    public class OffensivePlannerTests
    {
        private UnitTypeDefinition[] _catalog;

        private static UnitTypeDefinition MakeUnitType(UnitType type, float power, float speed)
        {
            var unitType = ScriptableObject.CreateInstance<UnitTypeDefinition>();
            SetPrivateField(unitType, "displayName", type.ToString());
            SetPrivateField(unitType, "unitType", type);
            SetPrivateField(unitType, "power", power);
            SetPrivateField(unitType, "speed", speed);
            return unitType;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Champ introuvable : {fieldName}");
            field.SetValue(target, value);
        }

        [SetUp]
        public void SetUp()
        {
            _catalog = new[]
            {
                MakeUnitType(UnitType.Infantry, 10f, 4f),
                MakeUnitType(UnitType.Fighter, 20f, 8f),
                MakeUnitType(UnitType.Cruiser, 50f, 6f),
            };
        }

        private static OffensiveWave Wave(int id, UnitBundle composition, int days) =>
            new OffensiveWave(id, composition, days, 1f);

        // ------------------------------------------------------------- voyage

        [Test]
        public void SlowestSpeed_IsThatOfTheSlowestEmbarkedUnit()
        {
            UnitBundle mixed = UnitBundle.Of(UnitType.Infantry, 1) + UnitBundle.Of(UnitType.Fighter, 3);

            Assert.AreEqual(4f, FleetTravel.SlowestSpeed(mixed, _catalog), 1e-4f,
                "Un Chasseur escortant de l'Infanterie n'ira pas plus vite qu'elle.");
        }

        [Test]
        public void SlowestSpeed_EmptyComposition_FallsBackRatherThanReturningInfinity()
        {
            Assert.AreEqual(1f, FleetTravel.SlowestSpeed(UnitBundle.Zero, _catalog), 1e-4f);
            Assert.AreEqual(1f, FleetTravel.SlowestSpeed(UnitBundle.Of(UnitType.Infantry, 2), null), 1e-4f);
        }

        [Test]
        public void EffectiveSpeed_AppliesResearchAndAdmiral()
        {
            UnitBundle fighters = UnitBundle.Of(UnitType.Fighter, 2);

            // 8 de base, +25 % de Logistique, +10 % d'Amiral.
            Assert.AreEqual(8f * 1.25f * 1.10f, FleetTravel.EffectiveSpeed(fighters, _catalog, 1.25f, 0.10f), 1e-3f);
        }

        [Test]
        public void EffectiveSpeed_NeverZero()
        {
            Assert.Greater(FleetTravel.EffectiveSpeed(UnitBundle.Of(UnitType.Fighter, 1), _catalog, 0f, -1f), 0f,
                "Une vitesse nulle diviserait par zero dans le calcul de duree.");
        }

        [Test]
        public void DaysToLeg_RoundsUpOnce_AndGuaranteesOneDayPerLeg()
        {
            // 21 unites a vitesse 5 : 4,2 jours -> 5.
            Assert.AreEqual(5, FleetTravel.DaysToLeg(21f, 5f, legIndex: 0));

            // Trois etapes tres courtes : le plancher garantit malgre tout trois jours.
            Assert.AreEqual(3, FleetTravel.DaysToLeg(1f, 100f, legIndex: 2));
        }

        [Test]
        public void DaysToLeg_IsMonotonic()
        {
            int previous = 0;
            for (int leg = 0; leg < 8; leg++)
            {
                int days = FleetTravel.DaysToLeg(leg * 12f, 5f, leg);
                Assert.Greater(days, previous, "Les dates d'arrivee doivent etre strictement croissantes.");
                previous = days;
            }
        }

        // ---------------------------------------------------------- prevision

        [Test]
        public void Simulate_NoWave_ChangesNothing()
        {
            UnitBundle garrison = UnitBundle.Of(UnitType.Cruiser, 3);

            OffensiveOutcome outcome = OffensivePlanner.Simulate(
                new List<OffensiveWave>(), garrison, 1f, _catalog);

            Assert.AreEqual(0, outcome.WaveCount);
            Assert.IsFalse(outcome.SystemCaptured);
            Assert.AreEqual(3, outcome.SurvivingDefenders);
        }

        [Test]
        public void Simulate_OverwhelmingForceWithInfantry_TakesTheSystem()
        {
            var waves = new[] { Wave(1, UnitBundle.Of(UnitType.Cruiser, 8) + UnitBundle.Of(UnitType.Infantry, 2), 5) };

            OffensiveOutcome outcome = OffensivePlanner.Simulate(waves, UnitBundle.Of(UnitType.Infantry, 1), 1f, _catalog);

            Assert.IsTrue(outcome.SystemCaptured);
            Assert.AreEqual(5, outcome.TravelDays);
            Assert.AreEqual(0, outcome.SurvivingDefenders);
        }

        [Test]
        public void Simulate_OverwhelmingForceWithoutInfantry_DestroysTheGarrisonButTakesNothing()
        {
            var waves = new[] { Wave(1, UnitBundle.Of(UnitType.Cruiser, 8), 4) };

            OffensiveOutcome outcome = OffensivePlanner.Simulate(waves, UnitBundle.Of(UnitType.Infantry, 1), 1f, _catalog);

            Assert.IsFalse(outcome.SystemCaptured, "Sans Infanterie survivante, personne n'occupe le systeme.");
            Assert.IsTrue(outcome.WonWithoutOccupation);
        }

        [Test]
        public void Simulate_WeakWave_IsRepelledAndTheDefenderSurvives()
        {
            var waves = new[] { Wave(1, UnitBundle.Of(UnitType.Infantry, 1), 3) };

            OffensiveOutcome outcome = OffensivePlanner.Simulate(waves, UnitBundle.Of(UnitType.Cruiser, 6), 1f, _catalog);

            Assert.IsFalse(outcome.SystemCaptured);
            Assert.IsTrue(outcome.AllWavesRepelled);
            Assert.Greater(outcome.SurvivingDefenders, 0);
        }

        /// <summary>
        /// Le resultat de la phase : la meme force, envoyee groupee, prend le systeme ; envoyee
        /// en deux temps, elle echoue. Sans cette propriete, la fiche promettrait une victoire
        /// que le jeu ne donnerait pas.
        /// </summary>
        [Test]
        public void Simulate_StaggeredWaves_LoseWhereTheSameForceCombinedWouldWin()
        {
            UnitBundle half = UnitBundle.Of(UnitType.Cruiser, 3) + UnitBundle.Of(UnitType.Infantry, 1);
            UnitBundle garrison = UnitBundle.Of(UnitType.Cruiser, 5);

            OffensiveOutcome together = OffensivePlanner.Simulate(
                new[] { Wave(1, half + half, 6) }, garrison, 1f, _catalog);

            OffensiveOutcome staggered = OffensivePlanner.Simulate(
                new[] { Wave(1, half, 4), Wave(2, half, 9) }, garrison, 1f, _catalog);

            Assert.IsTrue(together.SystemCaptured, "Groupee, la force l'emporte.");
            Assert.IsFalse(staggered.SystemCaptured, "Echelonnee, la meme force se fait battre en detail.");
            Assert.Greater(staggered.AttackerLosses, 0);
        }

        [Test]
        public void Simulate_IsIndependentOfTheOrderOfTheInput()
        {
            UnitBundle a = UnitBundle.Of(UnitType.Cruiser, 2) + UnitBundle.Of(UnitType.Infantry, 1);
            UnitBundle b = UnitBundle.Of(UnitType.Cruiser, 4) + UnitBundle.Of(UnitType.Infantry, 1);
            UnitBundle garrison = UnitBundle.Of(UnitType.Cruiser, 3);

            OffensiveOutcome forward = OffensivePlanner.Simulate(
                new[] { Wave(1, a, 3), Wave(2, b, 7) }, garrison, 1f, _catalog);

            OffensiveOutcome reversed = OffensivePlanner.Simulate(
                new[] { Wave(2, b, 7), Wave(1, a, 3) }, garrison, 1f, _catalog);

            Assert.AreEqual(forward.SystemCaptured, reversed.SystemCaptured);
            Assert.AreEqual(forward.AttackerLosses, reversed.AttackerLosses);
            Assert.AreEqual(forward.SurvivingDefenders, reversed.SurvivingDefenders);
        }

        [Test]
        public void Simulate_WavesArrivingAfterTheCapture_FightNothingAndLoseNothing()
        {
            UnitBundle strike = UnitBundle.Of(UnitType.Cruiser, 8) + UnitBundle.Of(UnitType.Infantry, 2);
            UnitBundle escort = UnitBundle.Of(UnitType.Infantry, 2);

            OffensiveOutcome outcome = OffensivePlanner.Simulate(
                new[] { Wave(1, strike, 3), Wave(2, escort, 12) }, UnitBundle.Of(UnitType.Infantry, 1), 1f, _catalog);

            Assert.IsTrue(outcome.SystemCaptured);
            Assert.AreEqual(12, outcome.TravelDays, "La derniere arrivee reste celle annoncee au joueur.");
            Assert.AreEqual(strike.TotalCount + escort.TotalCount, outcome.UnitCount);
        }

        [Test]
        public void VisibleDefenderModifier_GrowsWithFortifications()
        {
            Assert.AreEqual(1f, OffensivePlanner.VisibleDefenderModifier(1f, 0), 1e-4f);
            Assert.AreEqual(1.5f, OffensivePlanner.VisibleDefenderModifier(1f, 5), 1e-4f);
            Assert.Less(OffensivePlanner.VisibleDefenderModifier(0.4f, 2),
                OffensivePlanner.VisibleDefenderModifier(0.9f, 2),
                "Un systeme au bord de la revolte se defend moins bien.");
        }

        [Test]
        public void Simulate_NullArguments_Throw()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => OffensivePlanner.Simulate(null, UnitBundle.Zero, 1f, _catalog));
            Assert.Throws<System.ArgumentNullException>(
                () => OffensivePlanner.Simulate(new List<OffensiveWave>(), UnitBundle.Zero, 1f, null));
        }
    }
}
