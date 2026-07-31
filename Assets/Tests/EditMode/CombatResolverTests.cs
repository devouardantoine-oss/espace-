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

        /// <summary>
        /// Catalogue recree avant chaque test.
        /// <para>
        /// <b>Surtout pas un <c>static readonly</c> initialise a la volee :</b> un
        /// <see cref="ScriptableObject"/> est un objet natif Unity, detruit au rechargement du
        /// domaine (recompilation, entree en Play Mode). L'enveloppe manageee survit dans le
        /// champ statique mais pointe alors sur un objet detruit, que Unity fait passer pour
        /// <c>null</c> — <c>CombatResolver</c> ignore silencieusement ces entrees et renvoie une
        /// puissance nulle, faisant echouer la moitie de cette classe de facon impossible a
        /// reproduire d'une machine a l'autre. Les creer dans <c>SetUp</c> et les detruire dans
        /// <c>TearDown</c> est le seul cycle de vie fiable.
        /// </para>
        /// </summary>
        private UnitTypeDefinition[] Catalog;

        [SetUp]
        public void SetUp()
        {
            Catalog = new[]
            {
                MakeUnitType(UnitType.Infantry, 10f),
                MakeUnitType(UnitType.Armored, 25f),
                MakeUnitType(UnitType.SpecialForces, 40f),
                MakeUnitType(UnitType.Fighter, 60f),
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnitTypeDefinition unitType in Catalog)
            {
                Object.DestroyImmediate(unitType);
            }

            Catalog = null;
        }

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
            // Attaquant 300, defenseur 100 : total 400.
            // Fraction de pertes attaquant = puissance adverse / total = 100/400 = 1/4.
            // Fraction de pertes defenseur = puissance attaquant / total = 300/400 = 3/4.
            //
            // Les quarts sont exacts en binaire, contrairement aux tiers de la version
            // precedente (300 contre 150) : 30 x (1 - 1/3) vaut mathematiquement 20 pile, mais
            // en virgule flottante tombe a 19,999998 ou 20,000002 selon l'ordre d'evaluation et
            // la precision du JIT. FloorToInt renvoyait alors 19 sur certaines machines et 20
            // sur d'autres — un test qui depend du processeur ne prouve rien.
            var attacker = new UnitBundle(infantry: 30); // 300 puissance
            var defender = new UnitBundle(infantry: 10); // 100 puissance

            CombatResolver.BattleOutcome outcome = CombatResolver.Resolve(attacker, 1f, defender, 1f, Catalog);

            // Valeurs ecrites en dur plutot que recalculees : recopier la formule de la
            // production dans le test ne verifierait que la copie.
            Assert.AreEqual(22, outcome.AttackerSurvivors.Infantry, "30 x (1 - 1/4) = 22,5 -> 22.");
            Assert.AreEqual(2, outcome.DefenderSurvivors.Infantry, "10 x (1 - 3/4) = 2,5 -> 2.");
            Assert.AreEqual(8, outcome.AttackerLosses.Infantry);
            Assert.AreEqual(8, outcome.DefenderLosses.Infantry);
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
