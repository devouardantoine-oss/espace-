using System;
using System.Globalization;
using Espace.Core;

namespace Espace.UI
{
    /// <summary>
    /// Mise en forme textuelle pure des valeurs affichees par l'interface : dates, montants de
    /// ressources, variations signees, pourcentages.
    /// <para>
    /// <b>Extrait en classe statique sans dependance UnityEngine (sauf pour les calculs, qui
    /// n'en utilisent aucun) :</b> seule logique de la Phase 11 qui n'implique ni <c>OnGUI</c>
    /// ni <c>ServiceLocator</c> — elle est donc testable en EditMode et recoupee
    /// independamment en Python, contrairement au reste de cette phase (purement de
    /// l'agencement visuel, verifiable seulement en Play Mode par l'utilisateur).
    /// </para>
    /// <para>
    /// <b>Toutes les mises en forme numeriques passent par <see cref="CultureInfo.InvariantCulture"/> :</b>
    /// sans cela, le separateur decimal suit la langue du systeme et la meme valeur s'affiche
    /// « 1.5k » sur un poste anglais et « 1,5k » sur un poste francais. Le projet tient au
    /// determinisme complet — meme entree, meme sortie, sur n'importe quelle machine — et un
    /// affichage dependant du panneau de configuration de l'utilisateur y contrevient
    /// directement, en plus de rendre les tests infidelisables.
    /// </para>
    /// </summary>
    public static class HudFormatter
    {
        /// <summary>Au-dela de ce seuil, exprime en milliers ("k").</summary>
        private const float ThousandThreshold = 1000f;

        /// <summary>Au-dela de ce seuil, exprime en millions ("M").</summary>
        private const float MillionThreshold = 1_000_000f;

        /// <summary>Date de jeu au format lisible, ex. "An 3, Mois 7, Jour 12".</summary>
        public static string FormatDate(GameDate date) => $"An {date.Year}, Mois {date.Month}, Jour {date.Day}";

        /// <summary>
        /// Montant de ressource abrege : entier en-dessous de 1000, "12.3k" jusqu'a un million,
        /// "4.5M" au-dela. Toujours positif en sortie ; voir <see cref="FormatSigned"/> pour un
        /// signe explicite.
        /// </summary>
        public static string FormatResource(float value)
        {
            float magnitude = Math.Abs(value);
            string sign = value < 0f ? "-" : string.Empty;

            if (magnitude < ThousandThreshold)
            {
                return sign + magnitude.ToString("0", CultureInfo.InvariantCulture);
            }

            if (magnitude < MillionThreshold)
            {
                return sign + (magnitude / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + "k";
            }

            return sign + (magnitude / MillionThreshold).ToString("0.0", CultureInfo.InvariantCulture) + "M";
        }

        /// <summary>Comme <see cref="FormatResource"/>, prefixe de "+" si strictement positif (ex. un revenu net).</summary>
        public static string FormatSigned(float value)
        {
            if (value > 0f)
            {
                return $"+{FormatResource(value)}";
            }

            return FormatResource(value);
        }

        /// <summary>Fraction (0..1) en pourcentage entier, ex. 0.42 -> "42%".</summary>
        public static string FormatPercent(float fraction) => (fraction * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
