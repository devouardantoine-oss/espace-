using UnityEngine;

namespace Espace.Gameplay.Espionage
{
    /// <summary>
    /// Traduit la vigilance d'une cible en une phrase (Phase 24, etape 4).
    /// <para>
    /// <b>Pourquoi les mots et le chiffre, et pas seulement le chiffre.</b> « Vigilance 1,8 »
    /// n'apprend rien a qui ne connait pas le plafond ni la vitesse a laquelle elle retombe.
    /// « Reseau grille » se lit d'un coup d'oeil, et le nombre reste a cote pour ceux qui veulent
    /// comparer deux cibles. Le panneau Espionnage porte les deux.
    /// </para>
    /// <para>
    /// <b>Le vrai sujet du panneau, c'est celui-la.</b> La mise et la reussite estimee dependent
    /// de la vigilance ; savoir sur qui on peut encore frapper est la seule question que le
    /// joueur se pose en ouvrant cette entree.
    /// </para>
    /// <para>
    /// Fonction pure : elle ne connait que deux nombres, et se verifie sans service ni scene.
    /// </para>
    /// </summary>
    public static class VigilanceReading
    {
        /// <summary>Part du plafond au-dela de laquelle la cible est consideree sur ses gardes.</summary>
        public const float AlertFraction = 0.35f;

        /// <summary>Part du plafond au-dela de laquelle il vaut mieux laisser retomber.</summary>
        public const float BurnedFraction = 0.75f;

        /// <summary>
        /// L'etat du reseau chez la cible, en trois mots.
        /// </summary>
        /// <param name="vigilance">Vigilance acquise par la cible.</param>
        /// <param name="maximum">Plafond de vigilance. Une valeur nulle ou negative est traitee comme « pas de plafond connu ».</param>
        public static string Describe(float vigilance, float maximum)
        {
            float share = ShareOfCeiling(vigilance, maximum);

            if (share >= BurnedFraction)
            {
                return "reseau grille — laisser retomber";
            }

            return share >= AlertFraction ? "cible sur ses gardes" : "cible tranquille";
        }

        /// <summary>
        /// Vigilance rapportee a son plafond, entre 0 et 1.
        /// <para>
        /// C'est cette part, et non la valeur brute, qui remplit la jauge : un plafond qui
        /// changerait un jour ne doit pas obliger a retoucher l'affichage.
        /// </para>
        /// </summary>
        public static float ShareOfCeiling(float vigilance, float maximum)
        {
            if (maximum <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(vigilance / maximum);
        }

        /// <summary>Vrai s'il vaut mieux attendre que frapper.</summary>
        public static bool ShouldLetItCoolDown(float vigilance, float maximum)
        {
            return ShareOfCeiling(vigilance, maximum) >= BurnedFraction;
        }
    }
}
