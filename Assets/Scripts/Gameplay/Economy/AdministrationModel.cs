using UnityEngine;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Cout d'administration d'un empire, paye en influence (Phase 22, P4).
    /// <para>
    /// <b>Le frein central contre l'emballement.</b> Jusqu'ici, conquerir un systeme ajoutait sa
    /// production a la sienne et la retirait a l'adversaire, sans aucune contrepartie : le
    /// premier a prendre l'avantage ne pouvait plus etre rattrape. C'etait le defaut structurel
    /// le plus grave releve par l'audit.
    /// </para>
    /// <para>
    /// <b>Le cout croit plus vite que la taille.</b> Doubler le nombre de systemes plus que
    /// double la charge administrative : un empire etale finit par depenser en gestion ce qu'il
    /// gagne en conquete. Ce n'est pas un plafond arbitraire — rien n'interdit de s'etendre —
    /// mais chaque conquete supplementaire coute plus cher que la precedente.
    /// </para>
    /// <para>
    /// <b>Enfin un debouche pour l'influence.</b> Elle etait produite et n'avait aucun
    /// consommateur. Elle devient la ressource qui arbitre entre <em>tenir ce qu'on a</em> et
    /// <em>agir a l'exterieur</em> : la meme influence paie l'administration, l'espionnage et la
    /// diplomatie. Un empire immense n'a plus les moyens de comploter.
    /// </para>
    /// <para>
    /// <b>Le deficit se paie en stabilite, pas en interdiction.</b> Manquer d'influence ne
    /// bloque rien : les provinces se tiennent simplement moins bien, ce qui reduit toute la
    /// production, ce qui rend la flotte impayable. La spirale est lente et lisible, et le
    /// joueur peut en sortir en consolidant plutot qu'en subissant un message d'erreur.
    /// </para>
    /// </summary>
    public static class AdministrationModel
    {
        /// <summary>
        /// Nombre de systemes administres sans frais. Un petit empire ne doit pas etre punI
        /// d'exister ; c'est l'expansion qui se paie, pas la presence.
        /// </summary>
        public const int FreeSystems = 3;

        /// <summary>
        /// Influence mensuelle par systeme au-dela de l'allocation gratuite.
        /// <para>
        /// <b>Cale sur la production reelle, pas choisi a vue.</b> Un systeme de developpement 3
        /// produit <c>3 x 0,4 x 30 = 36</c> d'influence par mois. Un premier reglage a 1,6
        /// donnait 210 d'influence de charge pour 1440 produites a quarante systemes : le frein
        /// ne freinait rien du tout, ce que seule la simulation a revele. A 12, l'empire reste a
        /// l'aise jusqu'a la vingtaine de systemes, devient juste vers trente et bascule en
        /// deficit au-dela de quarante.
        /// </para>
        /// </summary>
        public const float InfluencePerSystemPerMonth = 12f;

        /// <summary>
        /// Exposant de la croissance du cout. Au-dessus de 1, la charge par systeme augmente
        /// avec la taille : c'est ce terme, et lui seul, qui produit les rendements decroissants
        /// de la conquete.
        /// </summary>
        public const float ScalingExponent = 1.35f;

        /// <summary>Pression de stabilite maximale imputable a un deficit d'influence.</summary>
        public const float MaximumPressure = 0.35f;

        /// <summary>
        /// Deficit, rapporte au besoin, a partir duquel la pression atteint son maximum. Un
        /// manque de moitie est deja une crise ; au-dela, la sanction cesse de croitre pour ne
        /// pas rendre la situation irrattrapable.
        /// </summary>
        private const float FullPressureDeficitRatio = 0.5f;

        /// <summary>
        /// Part du cout d'administration que la Coercition retire a l'Influence (Phase 24, etape 7).
        /// <para>
        /// La garnison remplace l'administration : ce qui n'est plus paye en Influence l'est en
        /// Credits, et <b>plus cher</b> — voir <see cref="CoercionCreditsPerInfluence"/>. Tenir
        /// par la force n'est pas une economie, c'est un echange de ruine.
        /// </para>
        /// </summary>
        public const float CoercionInfluenceRelief = 0.60f;

        /// <summary>
        /// Credits exiges pour chaque point d'Influence ainsi soulage.
        /// <para>
        /// <b>Superieur a 1 deliberement.</b> Si la Coercition coutait moins que ce qu'elle
        /// remplace, elle serait la bonne reponse en toute circonstance et la courbe cesserait
        /// d'etre un probleme — la meme regle que « aucune option gratuite » a l'etape 5. Un test
        /// verifie que ce facteur reste au-dessus de 1.
        /// </para>
        /// </summary>
        public const float CoercionCreditsPerInfluence = 1.5f;

        /// <summary>Influence exigee chaque mois pour tenir <paramref name="systemCount"/> systemes.</summary>
        public static float InfluenceUpkeep(int systemCount)
        {
            int taxable = Mathf.Max(0, systemCount - FreeSystems);
            if (taxable == 0)
            {
                return 0f;
            }

            return InfluencePerSystemPerMonth * Mathf.Pow(taxable, ScalingExponent);
        }

        /// <summary>
        /// Pression de stabilite resultant d'un paiement incomplet, entre 0 et
        /// <see cref="MaximumPressure"/>. Se branche sur le parametre <c>externalPressure</c>
        /// que <see cref="StabilityModel"/> prevoyait deja.
        /// </summary>
        /// <param name="paid">Influence effectivement versee ce mois-ci.</param>
        /// <param name="required">Influence exigee (voir <see cref="InfluenceUpkeep"/>).</param>
        public static float Pressure(float paid, float required)
        {
            if (required <= 0f)
            {
                return 0f;
            }

            float deficitRatio = Mathf.Clamp01((required - Mathf.Max(0f, paid)) / required);
            return MaximumPressure * Mathf.Clamp01(deficitRatio / FullPressureDeficitRatio);
        }
    }
}
