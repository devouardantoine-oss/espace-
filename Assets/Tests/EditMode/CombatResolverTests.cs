using System.Reflection;
using Espace.Gameplay.Military;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="CombatResolver"/> : determinisme, vainqueur selon le ratio de
    /// puissance, fractions de pertes, cas limites (defense vide, deux camps a puissance nulle).
    /// </summary>
    [TestFixture]
    public sealed class CombatResolverTests
    {
        private const float FloatTolerance = 0.001f;

        private static UnitTypeDefinition MakeUnitType(UnitType type, float power)
        {
            var unitType = ScriptableObject.CreateInstance<UnitTypeDefinition>();
            SetPrivateField(unitType, "displayName", type.ToString());
            SetPrivateField(unitType, "unitType", type);
            SetPrivateField(unitType, "power", power);
            SetPrivateField(unitType, "speed", 5f);
            return unitType;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Champ introuvable : {fieldName}");
            field.SetValue(target, value);
        }

        private static readonly UnitTypeDefinition[] Catalog =
        {
            MakeUnitType(UnitType.Infantry, 10f),
            MakeUnitType(UnitType.Armored, 25f),
            MakeUnitType(UnitType.SpecialForces, 40f),
            MakeUnitType(UnitType.SpaceFleet, 60f),
        };

        [Test]
        public void ComputePower_SumsQuantityTimesPowerAcrossTypes()
        {
            var composition = new UnitBundle(infantry: 3, armored: 2);

            float power = CombatResolver.ComputePower(composition, Catalog);

            Assert.AreEqual(3 * 10f + 2 * 25f, power, FloatTolerance);
        }

        [Test]
        public void ComputePower_EmptyComposition_IsZero()
        {
            Assert.AreEqual(0f, CombatResolver.ComputePower(UnitBundle.Zero, Catalog));
        }

        [Test]
        public void Resolve_AttackerStronger_AttackerWins()
        {
            var attacker = new UnitBundle(infantry: 10); // 100 puissance
            var defender = new UnitBundle(infantry: 5); // 50 puissance

            CombatResolver.BattleOutcome outcome = CombatResolver.Resolve(attacker, 1f, defender, 1f, Catalog);

            Assert.IsTrue(outcome.AttackerWon);
            Assert.AreEqual(100f, outcome.AttackerPower, FloatTolerance);
            Assert.AreEqual(50f, outcome.DefenderPower, FloatTolerance);
        }

        [Test]
        public void Resolve_DefenderStronger_DefenderWins()
        {
            var attacker = new UnitBundle(infantry: 5);
            var defender = new UnitBundle(infantry: 10);

            CombatResolver.BattleOutcome outcome = CombatResolver.Resolve(attacker, 1f, defender, 1f, Catalog);

            Assert.IsFalse(outcome.AttackerWon);
        }

        [Test]
        public void Resolve_EqualPower_DefenderWinsByConvention()
        {
            var attacker = new UnitBundle(infantry: 5);
            var defender = new UnitBundle(infantry: 5);

            CombatResolver.BattleOutcome outcome = CombatResolver.Resolve(attacker, 1f, defender, 1f, Catalog);

            Assert.IsFalse(outcome.AttackerWon, "Une egalite stricte doit revenir au defenseur.");
        }

        [Test]
        public void Resolve_CasualtyFractions_MatchFormula()
        {
            // Attaquant 100, defenseur 50 : total 150.
            // Fraction de pertes attaquant = puissance adverse / total = 50/150 = 1/3.
            // Fraction de pertes defenseur = puissance attaquant / total = 100/150 = 2/3.
            var attacker = new UnitBundle(infantry: 30); // 300 puissance, facilite les arrondis
            var defender = new UnitBundle(infantry: 15); // 150 puissance

            CombatResolver.BattleOutcome outcome = CombatResolver.Resolve(attacker, 1f, defender, 1f, Catalog);

            float total = 300f + 150f;
            float expectedAttackerCasualtyFraction = 150f / total;
            float expectedDefenderCasualtyFraction = 300f / total;

            int expectedAttackerSurvivors = Mathf.FloorToInt(30 * (1f - expectedAttackerCasualtyFraction));
            int expectedDefenderSurvivors = Mathf.FloorToInt(15 * (1f - expectedDefenderCasualtyFraction));

            Assert.AreEqual(expectedAttackerSurvivors, outcome.AttackerSurvivors.Infantry);
            Assert.AreEqual(expectedDefenderSurvivors, outcome.DefenderSurvivors.Infantry);
            Assert.AreEqual(30 - expectedAttackerSurvivors, outcome.AttackerLosses.Infantry);
            Assert.AreEqual(15 - expectedDefenderSurvivors, outcome.DefenderLosses.Infantry);
        }

        [Test]
        public void Resolve_UnopposedAttack_AttackerTakesNoLosses()
        {
            var attacker = new UnitBundle(infantry: 10);

            CombatResolver.BattleOutcome outcome = CombatResolver.Resolve(attacker, 1f, UnitBundle.Zero, 1f, Catalog);

            Assert.IsTrue(outcome.AttackerWon);
            Assert.AreEqual(new UnitBundle(infantry: 10), outcome.AttackerSurvivors);
            Assert.AreEqual(UnitBundle.Zero, outcome.AttackerLosses);
        }

        [Test]
        public void Resolve_BothSidesEmpty_DefenderWinsNoLosses()
        {
            CombatResolver.BattleOutcome outcome = CombatResolver.Resolve(UnitBundle.Zero, 1f, UnitBundle.Zero, 1f, Catalog);

            Assert.IsFalse(outcome.AttackerWon);
            Assert.AreEqual(0f, outcome.AttackerPower);
            Assert.AreEqual(0f, outcome.DefenderPower);
            Assert.AreEqual(UnitBundle.Zero, outcome.AttackerLosses);
            Assert.AreEqual(UnitBundle.Zero, outcome.DefenderLosses);
        }

        [Test]
        public void Resolve_ZeroModifier_RemovesThatSidesContribution()
        {
            var attacker = new UnitBundle(infantry: 100);
            var defender = new UnitBundle(infantry: 1);

            // Un modificateur nul (ex. moral totalement effondre) annule la puissance malgre
            // une composition nombreuse.
            CombatResolver.BattleOutcome outcome = CombatResolver.Resolve(attacker, 0f, defender, 1f, Catalog);

            Assert.IsFalse(outcome.AttackerWon);
            Assert.AreEqual(0f, outcome.AttackerPower);
        }

        [Test]
        public void Resolve_DefenderModifierCanReverseOutcome()
        {
            // Sans bonus, l'attaquant l'emporterait (100 > 80). Un bonus de terrain suffisant
            // doit pouvoir renverser le resultat.
            var attacker = new UnitBundle(infantry: 10); // 100
            var defender = new UnitBundle(infantry: 8); // 80

            CombatResolver.BattleOutcome withoutBonus = CombatResolver.Resolve(attacker, 1f, defender, 1f, Catalog);
            Assert.IsTrue(withoutBonus.AttackerWon);

            CombatResolver.BattleOutcome withBonus = CombatResolver.Resolve(attacker, 1f, defender, 1.5f, Catalog);
            Assert.IsFalse(withBonus.AttackerWon);
        }

        [Test]
        public void Resolve_IsDeterministic()
        {
            var attacker = new UnitBundle(infantry: 7, armored: 3);
            var defender = new UnitBundle(infantry: 4, specialForces: 1);

            CombatResolver.BattleOutcome first = CombatResolver.Resolve(attacker, 1f, defender, 1f, Catalog);
            CombatResolver.BattleOutcome second = CombatResolver.Resolve(attacker, 1f, defender, 1f, Catalog);

            Assert.AreEqual(first.AttackerWon, second.AttackerWon);
            Assert.AreEqual(first.AttackerSurvivors, second.AttackerSurvivors);
            Assert.AreEqual(first.DefenderSurvivors, second.DefenderSurvivors);
        }
    }
}
