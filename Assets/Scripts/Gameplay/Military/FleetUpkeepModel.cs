using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Consequences d'un entretien de flotte impaye (Phase 22, P5).
    /// <para>
    /// <b>Le defaut corrige.</b> Un entretien existait deja — de 0,5 a 5 credits par unite et par
    /// jour — mais <c>ChargeUpkeep</c> appelait <c>TrySpend</c> et <b>ignorait l'echec</b>. Le
    /// commentaire l'assumait : « aucune dette, aucune desertion ». Une flotte impayee
    /// continuait donc d'exister et de se battre normalement. Le cout etait facultatif, et la
    /// question « puis-je reellement entretenir cette flotte ? » n'avait pas de sens.
    /// </para>
    /// <para>
    /// <b>L'attrition est progressive, jamais brutale.</b> Un empire momentanement a sec perd
    /// quelques unites, pas sa flotte : il a le temps de vendre, de faire la paix ou de baisser
    /// la voile. Une desertion instantanee transformerait une erreur de tresorerie en defaite
    /// definitive, ce qui punirait l'inattention plutot que la mauvaise strategie.
    /// </para>
    /// <para>
    /// <b>L'entretien devient double.</b> Aux credits s'ajoutent des minerais : une flotte
    /// consomme des pieces, pas seulement des soldes. Cela relie enfin la puissance militaire a
    /// la capacite industrielle — un empire riche mais sans mines ne peut plus armer sans
    /// limite.
    /// </para>
    /// <para>
    /// <b>Periode de grace au chargement.</b> Une sauvegarde d'avant cette phase contient des
    /// flottes constituees sans que l'entretien ait jamais mordu. Les faire fondre a la
    /// premiere seconde rendrait ces parties injouables ; l'appelant differe donc l'attrition
    /// (voir <see cref="GraceMonths"/>).
    /// </para>
    /// </summary>
    public static class FleetUpkeepModel
    {
        /// <summary>
        /// Minerais consommes par jour, rapportes au cout en credits. Un cinquieme : sensible
        /// pour une grande flotte, negligeable pour une escadre.
        /// </summary>
        public const float MineralsPerCreditOfUpkeep = 0.2f;

        /// <summary>Part de la flotte perdue en un mois d'impaye total.</summary>
        public const float MaximumMonthlyAttrition = 0.12f;

        /// <summary>
        /// Mois de repit accordes apres le chargement d'une sauvegarde anterieure a cette phase.
        /// Assez pour reagir, trop court pour etre exploite.
        /// </summary>
        public const int GraceMonths = 3;

        /// <summary>Entretien en minerais correspondant a un entretien en credits donne.</summary>
        public static float MineralUpkeepFor(float creditUpkeep)
        {
            return Mathf.Max(0f, creditUpkeep) * MineralsPerCreditOfUpkeep;
        }

        /// <summary>
        /// Part de la flotte a retirer ce mois-ci, entre 0 et
        /// <see cref="MaximumMonthlyAttrition"/>.
        /// </summary>
        /// <param name="paid">Entretien effectivement regle.</param>
        /// <param name="due">Entretien du.</param>
        public static float AttritionFraction(float paid, float due)
        {
            if (due <= 0f)
            {
                return 0f;
            }

            float unpaidRatio = Mathf.Clamp01((due - Mathf.Max(0f, paid)) / due);
            return MaximumMonthlyAttrition * unpaidRatio;
        }

        /// <summary>
        /// Facteur de conservation a appliquer a une composition de flotte.
        /// <para>
        /// Renvoyer un facteur plutot qu'un nombre d'unites laisse l'arrondi a
        /// <c>UnitBundle.Scale</c>, deja ecrit et deja teste, plutot que de dupliquer ici une
        /// regle d'arrondi qui divergerait tot ou tard.
        /// </para>
        /// </summary>
        public static float SurvivingFraction(float paid, float due)
        {
            return 1f - AttritionFraction(paid, due);
        }
    }
}
