using UnityEngine;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Evolution de la stabilite d'un systeme (Phase 22, P2).
    /// <para>
    /// <b>Le defaut corrige.</b> <c>Stability</c> ne bougeait que par revolte declenchee a
    /// l'espionnage, et jamais dans l'autre sens : une fois abaissee, elle le restait pour
    /// toujours. Comme elle multiplie <em>toute</em> la production d'un systeme, c'etait une
    /// punition definitive plutot qu'une situation a gerer.
    /// </para>
    /// <para>
    /// <b>Convergence vers une cible, pas addition d'effets.</b> Empiler des bonus et des malus
    /// donne des valeurs qui derivent et qu'il faut ensuite borner arbitrairement. Ici la
    /// stabilite se rapproche chaque mois d'un niveau <em>merite</em> par la situation : elle
    /// remonte d'elle-meme quand on cesse de la maltraiter, et redescend quand la pression
    /// revient. Une revolte devient un creux dont on se releve, ce qui est bien plus interessant
    /// a jouer qu'une cicatrice.
    /// </para>
    /// <para>
    /// <b>L'inertie est le point de conception.</b> La stabilite ne suit pas immediatement le
    /// taux d'imposition : on peut serrer la vis quelques mois pour financer une guerre et
    /// relacher avant que ca ne casse. C'est exactement l'arbitrage recherche — et il serait
    /// impossible avec un effet instantane.
    /// </para>
    /// </summary>
    public static class StabilityModel
    {
        /// <summary>Stabilite visee par un systeme sans developpement, sans impot et sans troubles.</summary>
        public const float BaseTarget = 0.70f;

        /// <summary>Bonus de cible par niveau de developpement : une administration installee tient mieux son monde.</summary>
        public const float TargetPerDevelopmentLevel = 0.05f;

        /// <summary>Cible maximale : un systeme parfaitement gere reste perfectible.</summary>
        public const float MaximumTarget = 0.95f;

        /// <summary>
        /// Part du chemin parcouru chaque mois vers la cible. A 12 %, il faut environ six mois
        /// pour combler la moitie d'un ecart — assez lent pour qu'un choix fiscal se paie sur la
        /// duree, assez rapide pour qu'une correction se voie dans la meme partie.
        /// </summary>
        public const float MonthlyAdaptation = 0.12f;

        /// <summary>
        /// Cible de stabilite d'un systeme, compte tenu de sa gestion.
        /// </summary>
        /// <param name="developmentLevel">Niveau de developpement.</param>
        /// <param name="nominalTaxRate">Taux affiche : c'est la pression ressentie, pas le rendement, qui mecontente.</param>
        /// <param name="externalPressure">
        /// Pression exterieure supplementaire, entre 0 et 1 : occupation recente, sabotage,
        /// surextension administrative (P4). Zero par defaut, pour que le modele accueille la
        /// phase suivante sans etre rouvert.
        /// </param>
        public static float TargetFor(int developmentLevel, float nominalTaxRate, float externalPressure = 0f)
        {
            float target = BaseTarget + Mathf.Max(0, developmentLevel) * TargetPerDevelopmentLevel;
            target = Mathf.Min(target, MaximumTarget);

            target -= TaxationModel.Discontent(nominalTaxRate);
            target -= Mathf.Clamp01(externalPressure);

            return Mathf.Clamp01(target);
        }

        /// <summary>Stabilite du mois suivant, rapprochee de sa cible.</summary>
        public static float Next(float stability, int developmentLevel, float nominalTaxRate, float externalPressure = 0f)
        {
            float target = TargetFor(developmentLevel, nominalTaxRate, externalPressure);
            return Mathf.Clamp01(Mathf.Lerp(Mathf.Clamp01(stability), target, MonthlyAdaptation));
        }
    }
}
