using UnityEngine;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Rendement reel de l'impot (Phase 22, P2).
    /// <para>
    /// <b>Le defaut corrige.</b> Jusqu'ici le taux multipliait lineairement les credits, et
    /// n'etait lu nulle part ailleurs : le meilleur reglage etait 100 %, toujours, sans avoir a
    /// reflechir. Une decision dont la reponse est constante n'est pas une decision.
    /// </para>
    /// <para>
    /// <b>Ce qui remplace :</b> au-dela d'un seuil, une part croissante de l'activite echappe a
    /// l'impot. Le taux <em>percu</em> cesse alors de suivre le taux <em>affiche</em>, puis
    /// finit par redescendre — presser plus fort rapporte moins. Il existe donc un optimum, et
    /// il n'est pas affiche.
    /// </para>
    /// <para>
    /// <b>L'optimum depend de la stabilite</b>, ce qui est le point qui rend la mecanique
    /// interessante : un empire stable supporte une pression que l'empire trouble ne supporte
    /// pas. Le meme taux n'est donc pas le bon partout ni tout le temps, et le joueur ne peut
    /// pas apprendre un chiffre une fois pour toutes.
    /// </para>
    /// <para>
    /// <b>En dessous du seuil, la fonction est l'identite</b> — choix delibere : la formule
    /// historique reste exacte dans la plage ou le jeu se joue habituellement, ce qui evite de
    /// reequilibrer tout le contenu existant et laisse valides les tests de production qui
    /// utilisent le taux par defaut de 25 %.
    /// </para>
    /// </summary>
    public static class TaxationModel
    {
        /// <summary>Taux en dessous duquel personne ne cherche a echapper a l'impot.</summary>
        public const float EvasionThreshold = 0.35f;

        /// <summary>
        /// Vitesse a laquelle l'evasion s'installe au-dela du seuil. La perte croit avec le
        /// <b>carre</b> du depassement : un point de trop coute peu, dix points coutent cher.
        /// C'est ce qui cree le maximum, et non un plafond arbitraire.
        /// </summary>
        public const float EvasionSharpness = 1.6f;

        /// <summary>Stabilite plancher dans le calcul : sans elle, un empire au bord de l'effondrement diviserait par zero.</summary>
        private const float MinimumStability = 0.2f;

        /// <summary>Taux percu plancher : meme sous une pression absurde, une administration continue de collecter quelque chose.</summary>
        private const float MinimumEffectiveRate = 0.05f;

        /// <summary>Seuil au-dela duquel la pression fiscale commence a peser sur la stabilite.</summary>
        public const float DiscontentThreshold = 0.30f;

        /// <summary>Perte de stabilite visee a 100 % d'imposition. Interpolee lineairement depuis <see cref="DiscontentThreshold"/>.</summary>
        private const float MaximumDiscontent = 0.45f;

        /// <summary>
        /// Taux reellement percu, une fois l'evasion deduite.
        /// </summary>
        /// <param name="nominalRate">Taux affiche, entre 0 et 1.</param>
        /// <param name="stability">Stabilite du systeme impose, entre 0 et 1. Une population confiante fraude moins.</param>
        public static float EffectiveRate(float nominalRate, float stability)
        {
            float nominal = Mathf.Clamp01(nominalRate);
            if (nominal <= EvasionThreshold)
            {
                return nominal;
            }

            float excess = nominal - EvasionThreshold;
            float evasion = EvasionSharpness * excess * excess / Mathf.Max(MinimumStability, Mathf.Clamp01(stability));

            return Mathf.Max(MinimumEffectiveRate, nominal - evasion);
        }

        /// <summary>
        /// Perte de stabilite mensuelle imputable a la pression fiscale, entre 0 et
        /// <see cref="MaximumDiscontent"/>.
        /// <para>
        /// Le seuil de mecontentement est <b>plus bas</b> que celui de l'evasion : on rale avant
        /// de frauder. Entre les deux se trouve la plage ou un empire peut financer une guerre
        /// en acceptant d'etre impopulaire sans ruiner ses finances — exactement le genre
        /// d'arbitrage qu'on cherche a rendre possible.
        /// </para>
        /// </summary>
        public static float Discontent(float nominalRate)
        {
            float nominal = Mathf.Clamp01(nominalRate);
            if (nominal <= DiscontentThreshold)
            {
                return 0f;
            }

            return MaximumDiscontent * (nominal - DiscontentThreshold) / (1f - DiscontentThreshold);
        }

        /// <summary>
        /// Frein applique a la croissance de la richesse : ce que l'impot preleve n'est pas
        /// reinvesti. Vaut 1 a taux nul et tombe a 0 quand tout est preleve.
        /// </summary>
        public static float ReinvestmentFactor(float nominalRate)
        {
            return 1f - Mathf.Clamp01(nominalRate);
        }
    }
}
