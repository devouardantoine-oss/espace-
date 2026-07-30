using System.Collections.Generic;
using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Resolution automatique d'une bataille entre deux compositions d'unites.
    /// <para>
    /// <b>Deterministe, sans alea :</b> a composition et modificateurs egaux, le meme combat
    /// donne toujours le meme resultat — testable sans stub de generateur aleatoire, et le
    /// joueur ne peut pas re-tenter une bataille en esperant un tirage different. La seule
    /// source de variation vient des choix (compositions, moment de l'attaque), pas du hasard.
    /// </para>
    /// <para>
    /// <b>Formule :</b> la puissance de chaque camp est la somme <c>quantite × puissance</c>
    /// de son catalogue, mise a l'echelle par un modificateur (moral, commandement, terrain —
    /// calcules par l'appelant a partir de l'empire et du systeme, pour garder cette classe
    /// ignorante de tout ce qui n'est pas des nombres). La fraction de pertes de chaque camp
    /// est proportionnelle a la puissance <b>adverse</b> relative au total : un camp bien plus
    /// fort inflige proportionnellement plus de pertes qu'il n'en subit. Le camp avec la plus
    /// grande puissance gagne (egalite = victoire du defenseur, l'avantage du terrain rend une
    /// egalite exacte quasiment impossible en pratique).
    /// </para>
    /// </summary>
    public static class CombatResolver
    {
        /// <summary>Resultat d'une bataille resolue.</summary>
        public readonly struct BattleOutcome
        {
            public readonly bool AttackerWon;
            public readonly float AttackerPower;
            public readonly float DefenderPower;
            public readonly UnitBundle AttackerSurvivors;
            public readonly UnitBundle DefenderSurvivors;
            public readonly UnitBundle AttackerLosses;
            public readonly UnitBundle DefenderLosses;

            public BattleOutcome(
                bool attackerWon, float attackerPower, float defenderPower,
                UnitBundle attackerSurvivors, UnitBundle defenderSurvivors,
                UnitBundle attackerLosses, UnitBundle defenderLosses)
            {
                AttackerWon = attackerWon;
                AttackerPower = attackerPower;
                DefenderPower = defenderPower;
                AttackerSurvivors = attackerSurvivors;
                DefenderSurvivors = defenderSurvivors;
                AttackerLosses = attackerLosses;
                DefenderLosses = defenderLosses;
            }
        }

        /// <summary>
        /// Puissance totale d'une composition selon <paramref name="catalog"/> : somme de
        /// <c>quantite × puissance</c> sur les quatre types d'unites. Exposee publiquement
        /// pour que l'IA et l'interface puissent estimer une force sans dupliquer la formule.
        /// </summary>
        public static float ComputePower(UnitBundle composition, IReadOnlyList<UnitTypeDefinition> catalog)
        {
            float total = 0f;
            foreach (UnitTypeDefinition unitType in catalog)
            {
                if (unitType == null)
                {
                    continue;
                }

                total += composition.Get(unitType.UnitType) * unitType.Power;
            }

            return total;
        }

        /// <summary>
        /// Resout une bataille entre <paramref name="attackerComposition"/> et
        /// <paramref name="defenderComposition"/>.
        /// </summary>
        /// <param name="attackerModifier">
        /// Produit des modificateurs de l'attaquant (moral, commandement...) — 1 = neutre.
        /// </param>
        /// <param name="defenderModifier">
        /// Produit des modificateurs du defenseur, terrain (fortifications) inclus — 1 = neutre.
        /// </param>
        public static BattleOutcome Resolve(
            UnitBundle attackerComposition, float attackerModifier,
            UnitBundle defenderComposition, float defenderModifier,
            IReadOnlyList<UnitTypeDefinition> catalog)
        {
            float attackerPower = ComputePower(attackerComposition, catalog) * attackerModifier;
            float defenderPower = ComputePower(defenderComposition, catalog) * defenderModifier;
            float totalPower = attackerPower + defenderPower;

            if (totalPower <= 0f)
            {
                // Aucune puissance d'aucun cote (compositions vides, ou modificateurs nuls) :
                // par convention le defenseur l'emporte, rien n'est detruit.
                return new BattleOutcome(false, 0f, 0f, attackerComposition, defenderComposition, UnitBundle.Zero, UnitBundle.Zero);
            }

            float attackerCasualtyFraction = Mathf.Clamp01(defenderPower / totalPower);
            float defenderCasualtyFraction = Mathf.Clamp01(attackerPower / totalPower);

            UnitBundle attackerSurvivors = attackerComposition.Scale(1f - attackerCasualtyFraction);
            UnitBundle defenderSurvivors = defenderComposition.Scale(1f - defenderCasualtyFraction);

            bool attackerWon = attackerPower > defenderPower;

            return new BattleOutcome(
                attackerWon, attackerPower, defenderPower,
                attackerSurvivors, defenderSurvivors,
                attackerComposition - attackerSurvivors, defenderComposition - defenderSurvivors);
        }
    }
}
