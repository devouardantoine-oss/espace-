using UnityEngine;

namespace Espace.Gameplay.Empires
{
    /// <summary>Ligne de conduite qu'un empire se donne pour le mois a venir.</summary>
    public enum StrategicPosture
    {
        /// <summary>Tresorerie exsangue ou provinces agitees : on remet de l'ordre avant tout.</summary>
        Consolidating,

        /// <summary>Situation saine et place disponible : on grandit.</summary>
        Expanding,

        /// <summary>Un voisin est nettement plus fort : on se prepare sans provoquer.</summary>
        Defending,

        /// <summary>Avantage net et moyens disponibles : on peut frapper.</summary>
        Aggressive
    }

    /// <summary>
    /// Photographie de la situation d'un empire, calculee une fois par mois et partagee par
    /// tous ses preneurs de decision (Phase 22, P7).
    /// <para>
    /// <b>Le defaut corrige.</b> Les cinq modules de decision — economie, recherche, espionnage,
    /// diplomatie, militaire — s'executaient l'un apres l'autre <b>sans partager la moindre
    /// evaluation</b>. Le module economique reaffirmait un taux d'imposition fixe issu de la
    /// personnalite (0,20 pour un pacifiste, 0,35 pour un militariste) quelle que soit la
    /// situation. Il n'y avait ni objectif, ni priorite, ni estimation de risque : une suite de
    /// regles locales, pas une strategie.
    /// </para>
    /// <para>
    /// <b>Ce qui remplace.</b> Une structure pure qui repond a quatre questions — ai-je de
    /// l'argent, mes provinces tiennent-elles, suis-je le plus fort, ai-je de la place — et en
    /// deduit une posture. Les modules lisent la meme photographie, donc leurs decisions
    /// cessent de se contredire.
    /// </para>
    /// <para>
    /// <b>Aucun bonus cache.</b> L'evaluation ne lit que ce que le joueur voit lui-meme sur son
    /// interface. Une IA mieux informee que le joueur serait une triche, et une IA qui triche
    /// n'apprend rien au joueur sur le jeu.
    /// </para>
    /// <para>
    /// <b>Lisible par construction.</b> Chaque posture s'explique en une phrase a partir des
    /// quatre indicateurs, ce qui etait le critere retenu dans l'audit : le joueur doit pouvoir
    /// comprendre pourquoi un voisin vient de baisser ses impots.
    /// </para>
    /// </summary>
    public readonly struct EmpireAssessment
    {
        /// <summary>Mois de depenses courantes couverts par le tresor. En dessous de 1, l'empire vit au jour le jour.</summary>
        public readonly float FinancialRunwayMonths;

        /// <summary>Stabilite moyenne des systemes possedes, entre 0 et 1.</summary>
        public readonly float AverageStability;

        /// <summary>Puissance militaire rapportee a celle du voisin le plus fort. 1 signifie a egalite.</summary>
        public readonly float MilitaryRatio;

        /// <summary>Part de la capacite de peuplement encore libre, entre 0 et 1.</summary>
        public readonly float GrowthHeadroom;

        /// <summary>Nombre de systemes possedes.</summary>
        public readonly int SystemCount;

        /// <summary>Ligne de conduite deduite des quatre indicateurs.</summary>
        public readonly StrategicPosture Posture;

        /// <summary>Tresorerie en dessous de laquelle un empire renonce a tout projet nouveau.</summary>
        public const float CriticalRunwayMonths = 1.5f;

        /// <summary>Stabilite moyenne en dessous de laquelle l'empire doit se reprendre avant d'agir.</summary>
        public const float CriticalStability = 0.55f;

        /// <summary>Rapport de forces en dessous duquel un empire se sait menace.</summary>
        public const float ThreatenedRatio = 0.7f;

        /// <summary>Rapport de forces au-dela duquel un empire se sait en position de frapper.</summary>
        public const float DominantRatio = 1.6f;

        /// <summary>Place libre au-dela de laquelle il reste manifestement a grandir chez soi.</summary>
        public const float AmpleHeadroom = 0.25f;

        public EmpireAssessment(float financialRunwayMonths, float averageStability, float militaryRatio, float growthHeadroom, int systemCount)
        {
            FinancialRunwayMonths = Mathf.Max(0f, financialRunwayMonths);
            AverageStability = Mathf.Clamp01(averageStability);
            MilitaryRatio = Mathf.Max(0f, militaryRatio);
            GrowthHeadroom = Mathf.Clamp01(growthHeadroom);
            SystemCount = Mathf.Max(0, systemCount);

            Posture = DerivePosture(FinancialRunwayMonths, AverageStability, MilitaryRatio, GrowthHeadroom);
        }

        /// <summary>
        /// Ordre de priorite volontaire : <b>survivre, puis se defendre, puis grandir, puis
        /// frapper</b>. Un empire au bord de la faillite ne part pas en guerre, meme s'il est le
        /// plus fort — c'est exactement ce que l'ancienne IA faisait.
        /// </summary>
        private static StrategicPosture DerivePosture(float runway, float stability, float militaryRatio, float headroom)
        {
            if (runway < CriticalRunwayMonths || stability < CriticalStability)
            {
                return StrategicPosture.Consolidating;
            }

            if (militaryRatio < ThreatenedRatio)
            {
                return StrategicPosture.Defending;
            }

            if (militaryRatio >= DominantRatio && headroom < AmpleHeadroom)
            {
                // On ne frappe que lorsqu'on domine *et* qu'il n'y a plus a grandir chez soi :
                // conquerir est toujours plus cher que developper.
                return StrategicPosture.Aggressive;
            }

            return StrategicPosture.Expanding;
        }

        /// <summary>
        /// Taux d'imposition que la situation appelle, avant le temperament de l'empire.
        /// <para>
        /// C'est le remplacement direct du taux fixe par personnalite. Un empire a sec serre la
        /// vis ; un empire stable et prospere relache pour laisser croitre sa base imposable. La
        /// personnalite ne disparait pas — elle devient un decalage applique a ce taux, et non
        /// plus la reponse entiere.
        /// </para>
        /// </summary>
        public float SuggestedTaxRate()
        {
            switch (Posture)
            {
                // A sec : on prend ce qu'on peut, tout en restant sous le seuil ou l'evasion
                // rendrait la manoeuvre contre-productive.
                case StrategicPosture.Consolidating:
                    return FinancialRunwayMonths < CriticalRunwayMonths ? 0.45f : 0.20f;

                // En guerre ou en preparation : on accepte d'etre impopulaire un moment.
                case StrategicPosture.Aggressive:
                case StrategicPosture.Defending:
                    return 0.40f;

                // Rien d'urgent : on laisse la richesse s'accumuler.
                default:
                    return 0.22f;
            }
        }

        /// <summary>Vrai si l'empire doit renoncer a de nouvelles depenses militaires ce mois-ci.</summary>
        public bool ShouldCutMilitarySpending()
        {
            return Posture == StrategicPosture.Consolidating;
        }

        /// <summary>Explication en une phrase, pour la journalisation et le futur panneau de diplomatie.</summary>
        public string Explain()
        {
            switch (Posture)
            {
                case StrategicPosture.Consolidating:
                    return FinancialRunwayMonths < CriticalRunwayMonths
                        ? $"tresorerie a {FinancialRunwayMonths:F1} mois de depenses"
                        : $"stabilite moyenne a {AverageStability:P0}";
                case StrategicPosture.Defending:
                    return $"puissance militaire a {MilitaryRatio:P0} du voisin le plus fort";
                case StrategicPosture.Aggressive:
                    return $"avantage militaire de {MilitaryRatio:F1}x et plus de place a coloniser";
                default:
                    return $"situation saine, {GrowthHeadroom:P0} de capacite libre";
            }
        }
    }
}
