using UnityEngine;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Besoins recurrents d'un empire en nourriture et en energie (Phase 22, P3).
    /// <para>
    /// <b>Le defaut corrige.</b> Nourriture et Energie etaient produites chaque jour et
    /// <em>jamais consommees</em> : deux ressources sur cinq s'accumulaient indefiniment sans
    /// servir a rien. Une ressource qu'on ne depense pas n'est pas une ressource, c'est un
    /// compteur.
    /// </para>
    /// <para>
    /// <b>Deux contraintes de nature differente</b>, et c'est voulu. La nourriture manque et la
    /// population decline — sanction lente, structurelle, qui punit l'expansion sans
    /// consolidation. L'energie manque et la production des batiments tombe au prorata —
    /// sanction immediate et reversible, qui se corrige en construisant une centrale. L'une se
    /// paie sur des annees, l'autre sur un mois.
    /// </para>
    /// <para>
    /// <b>Les stocks comptent.</b> La consommation est prelevee sur le tresor, donc un empire
    /// prevoyant traverse une mauvaise passe sur ses reserves. C'est ce qui distingue une
    /// contrainte d'une punition : elle se prepare.
    /// </para>
    /// <para>
    /// <b>Calibrage.</b> Les coefficients sont poses pour qu'un systeme <em>equilibre</em> —
    /// peuple a sa capacite, sans gisement — s'auto-suffise tout juste. Un systeme a gisement
    /// alimentaire degage un surplus exportable vers les autres ; un systeme minier depend des
    /// autres. La dependance entre systemes emerge donc de la geographie plutot que d'une regle.
    /// </para>
    /// </summary>
    public static class SubsistenceModel
    {
        /// <summary>
        /// Nourriture consommee par point de population et par mois.
        /// <para>
        /// A comparer a la production : <c>0,012 par point et par jour</c>, soit <c>0,36</c> par
        /// mois. Le besoin est fixe legerement en dessous pour qu'un monde ordinaire vive de sa
        /// propre agriculture — la pénurie doit sanctionner l'expansion mal preparee, pas
        /// l'existence.
        /// </para>
        /// </summary>
        public const float FoodPerPopulationPerMonth = 0.30f;

        /// <summary>Energie consommee par batiment acheve et par mois : une infrastructure se branche.</summary>
        public const float EnergyPerBuildingPerMonth = 2.0f;

        /// <summary>Energie consommee par niveau de developpement et par mois : une societe avancee consomme plus.</summary>
        public const float EnergyPerDevelopmentLevelPerMonth = 1.2f;

        /// <summary>
        /// Part de la production des batiments encore assuree quand l'energie manque. Une
        /// coupure totale n'eteint pas tout : le minimum vital reste.
        /// </summary>
        public const float MinimumEnergySatisfaction = 0.25f;

        /// <summary>Besoin alimentaire mensuel d'un empire.</summary>
        public static float FoodDemand(int totalPopulation)
        {
            return Mathf.Max(0, totalPopulation) * FoodPerPopulationPerMonth;
        }

        /// <summary>Besoin energetique mensuel d'un empire.</summary>
        public static float EnergyDemand(int completedBuildings, int totalDevelopmentLevels)
        {
            return Mathf.Max(0, completedBuildings) * EnergyPerBuildingPerMonth
                + Mathf.Max(0, totalDevelopmentLevels) * EnergyPerDevelopmentLevelPerMonth;
        }

        /// <summary>
        /// Part du besoin couverte, entre 0 et 1. Un besoin nul est satisfait par definition —
        /// un empire sans population n'a pas faim.
        /// </summary>
        public static float Satisfaction(float available, float demand)
        {
            if (demand <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(Mathf.Max(0f, available) / demand);
        }

        /// <summary>
        /// Multiplicateur applique a la production des batiments selon l'energie disponible.
        /// Borne par le bas : une panne generale ne doit pas figer un empire sans retour.
        /// </summary>
        public static float BuildingOutputFactor(float energySatisfaction)
        {
            return Mathf.Lerp(MinimumEnergySatisfaction, 1f, Mathf.Clamp01(energySatisfaction));
        }
    }
}
