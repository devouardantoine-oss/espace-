using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>Un segment du liseré d'état : un indicateur, son remplissage et sa teinte.</summary>
    public readonly struct StateSegment
    {
        /// <summary>Libellé court, affiché seulement quand la place le permet.</summary>
        public readonly string Label;

        /// <summary>Part du segment remplie, entre 0 et 1.</summary>
        public readonly float Fill;

        /// <summary>Teinte du remplissage.</summary>
        public readonly Color Color;

        /// <summary>Vrai si l'indicateur appelle une décision. Le rendu peut alors le souligner.</summary>
        public readonly bool Alarming;

        public StateSegment(string label, float fill, Color color, bool alarming)
        {
            Label = label;
            Fill = Mathf.Clamp01(fill);
            Color = color;
            Alarming = alarming;
        }
    }

    /// <summary>
    /// Liseré d'état de l'empire : quatre segments lus d'un coup d'œil, en haut de l'écran
    /// (Phase 23, tranche B).
    /// <para>
    /// <b>Le problème résolu.</b> La barre supérieure occupait 44 unités sur les 286 garanties,
    /// soit 15 % de la hauteur, en permanence et sur toute la largeur. Elle affichait des nombres
    /// exacts — trésor, impôt, date — que personne ne lit en continu. Un liseré de quelques
    /// unités ne donne aucun chiffre mais répond aux seules questions qu'on se pose sans
    /// s'arrêter : <em>suis-je solvable, mes provinces tiennent-elles, suis-je menacé, ma
    /// recherche avance-t-elle ?</em>
    /// </para>
    /// <para>
    /// <b>Ce sont les quatre indicateurs d'<see cref="EmpireAssessment"/>.</b> Pas un choix
    /// esthétique : depuis la Phase 22, c'est exactement sur ces quatre-là que chaque IA décide
    /// de sa posture. Le joueur regarde donc le même tableau de bord que ses adversaires, avec
    /// les mêmes seuils — et il apprend à lire leurs revirements en apprenant à lire le sien.
    /// </para>
    /// <para>
    /// <b>Aucun seuil nouveau.</b> Chaque bascule de teinte reprend une constante déjà publiée
    /// par <see cref="EmpireAssessment"/>. Les teintes elles-mêmes sont celles du halo des
    /// planètes (<see cref="SystemGlyph"/>) : la carte et le liseré parlent la même langue, donc
    /// un liseré qui vire à l'ambre annonce des planètes qui virent à l'ambre.
    /// </para>
    /// <para>
    /// Fonction pure, vérifiable sans scène — comme <see cref="HudFormatter"/>, la seule autre
    /// logique testable de la couche interface.
    /// </para>
    /// </summary>
    public static class EmpireStateBand
    {
        /// <summary>Nombre de segments. Le liseré se divise en parts égales.</summary>
        public const int SegmentCount = 4;

        /// <summary>
        /// Trésorerie considérée comme confortable, en mois de dépenses : quatre fois le seuil
        /// critique d'<see cref="EmpireAssessment.CriticalRunwayMonths"/>. Au-delà, le segment
        /// est plein — accumuler davantage ne change plus aucune décision.
        /// </summary>
        public const float ComfortableRunwayMonths = EmpireAssessment.CriticalRunwayMonths * 4f;

        /// <summary>
        /// Construit les quatre segments.
        /// </summary>
        /// <param name="assessment">Situation de l'empire du joueur, calculée comme celle des IA.</param>
        /// <param name="researchFraction">
        /// Avancement vers le prochain palier, entre 0 et 1. Passé en paramètre plutôt que déduit :
        /// il dépend du catalogue de recherche, que ce modèle n'a aucune raison de connaître.
        /// </param>
        public static StateSegment[] From(EmpireAssessment assessment, float researchFraction)
        {
            return new[]
            {
                TreasurySegment(assessment),
                StabilitySegment(assessment),
                ThreatSegment(assessment),
                ResearchSegment(researchFraction),
            };
        }

        private static StateSegment TreasurySegment(EmpireAssessment assessment)
        {
            float fill = Mathf.Clamp01(assessment.FinancialRunwayMonths / ComfortableRunwayMonths);
            bool alarming = assessment.FinancialRunwayMonths < EmpireAssessment.CriticalRunwayMonths;

            return new StateSegment(
                "TRESOR",
                fill,
                HealthColor(assessment.FinancialRunwayMonths, EmpireAssessment.CriticalRunwayMonths, ComfortableRunwayMonths),
                alarming);
        }

        /// <summary>
        /// Stabilité moyenne. La teinte vient directement du halo des planètes : un liseré ambre
        /// et des halos ambre sont le même fait, dit deux fois.
        /// </summary>
        private static StateSegment StabilitySegment(EmpireAssessment assessment)
        {
            return new StateSegment(
                "STABILITE",
                assessment.AverageStability,
                SystemGlyph.HaloColorFor(assessment.AverageStability),
                assessment.AverageStability < EmpireAssessment.CriticalStability);
        }

        /// <summary>
        /// Rapport de forces face au voisin le plus fort. Le segment est plein dès
        /// <see cref="EmpireAssessment.DominantRatio"/> : au-delà, « je domine » et « j'écrase »
        /// appellent la même décision.
        /// </summary>
        private static StateSegment ThreatSegment(EmpireAssessment assessment)
        {
            float fill = Mathf.Clamp01(assessment.MilitaryRatio / EmpireAssessment.DominantRatio);

            return new StateSegment(
                "FORCES",
                fill,
                HealthColor(assessment.MilitaryRatio, EmpireAssessment.ThreatenedRatio, EmpireAssessment.DominantRatio),
                assessment.MilitaryRatio < EmpireAssessment.ThreatenedRatio);
        }

        /// <summary>
        /// Avancement de la recherche. <b>Jamais alarmant</b> : une recherche lente est un choix
        /// ou une conséquence, pas une urgence. Réserver l'alerte aux trois autres segments est
        /// ce qui lui garde son sens.
        /// </summary>
        private static StateSegment ResearchSegment(float researchFraction)
        {
            return new StateSegment("RECHERCHE", researchFraction, SystemGlyph.CalmHalo, alarming: false);
        }

        /// <summary>
        /// Teinte d'un indicateur : rouge à zéro, ambre au seuil d'alarme, froide à partir du
        /// niveau confortable. Même progression que le halo des planètes, sur des bornes propres
        /// à chaque indicateur.
        /// </summary>
        public static Color HealthColor(float value, float alarmThreshold, float comfortableValue)
        {
            if (value >= alarmThreshold)
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(alarmThreshold, comfortableValue, value));
                return Color.Lerp(SystemGlyph.StrainedHalo, SystemGlyph.CalmHalo, t);
            }

            float below = Mathf.Clamp01(Mathf.InverseLerp(0f, alarmThreshold, value));
            return Color.Lerp(SystemGlyph.TroubledHalo, SystemGlyph.StrainedHalo, below);
        }
    }
}
