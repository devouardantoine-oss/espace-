using UnityEngine;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Croissance demographique d'un systeme (Phase 22, P1).
    /// <para>
    /// <b>Le defaut corrige.</b> <c>Population</c> n'avait aucun point d'ecriture en cours de
    /// partie : fixee a la generation, relue par la sauvegarde, jamais modifiee. Comme elle est
    /// l'entree de trois des cinq productions, l'economie d'un empire etait une <em>constante</em>
    /// qui ne bougeait que par conquete. Ni croissance, ni declin, ni crise, ni rattrapage.
    /// </para>
    /// <para>
    /// <b>Croissance logistique, pas exponentielle.</b> Le taux est module par la place
    /// restante : plus un systeme approche de sa capacite, moins il croit. C'est le premier des
    /// freins que le jeu n'avait pas — sans lui, le plus gros empire grossit le plus vite et
    /// personne ne le rattrape jamais.
    /// </para>
    /// <para>
    /// <b>La capacite vient du developpement</b>, ce qui donne enfin une raison de fond
    /// d'investir : on n'achete pas un bonus, on releve un plafond. Un systeme au maximum de sa
    /// capacite ne rapporte plus rien de plus tant qu'on ne le developpe pas.
    /// </para>
    /// <para>
    /// <b>Mensuel, pas journalier</b> : une demographie recalculee soixante fois par seconde
    /// serait du bruit, et couterait cent fois plus cher pour un resultat identique.
    /// </para>
    /// </summary>
    public static class PopulationModel
    {
        /// <summary>Capacite d'un systeme sans aucun developpement. Un monde tout juste colonise peut deja nourrir du monde.</summary>
        public const int BaseCapacity = 400;

        /// <summary>Capacite ajoutee par niveau de developpement.</summary>
        public const int CapacityPerDevelopmentLevel = 600;

        /// <summary>Croissance mensuelle maximale, atteinte a capacite vide et stabilite parfaite. 1,2 % par mois double une population en cinq ans.</summary>
        public const float MaximumMonthlyGrowth = 0.012f;

        /// <summary>Declin mensuel maximal en cas de famine totale.</summary>
        public const float MaximumMonthlyDecline = 0.03f;

        /// <summary>En dessous de cette stabilite, la population cesse de croitre : on ne s'installe pas dans un systeme en troubles.</summary>
        public const float StabilityFloorForGrowth = 0.35f;

        /// <summary>Capacite d'accueil d'un systeme, en points de population.</summary>
        public static int CapacityFor(int developmentLevel)
        {
            return BaseCapacity + Mathf.Max(0, developmentLevel) * CapacityPerDevelopmentLevel;
        }

        /// <summary>
        /// Population du mois suivant.
        /// </summary>
        /// <param name="population">Population actuelle.</param>
        /// <param name="developmentLevel">Niveau de developpement, qui fixe la capacite.</param>
        /// <param name="stability">Stabilite du systeme, entre 0 et 1.</param>
        /// <param name="foodSatisfaction">
        /// Part des besoins alimentaires couverts, entre 0 et 1. Vaut 1 tant que la
        /// consommation de nourriture n'est pas branchee (P3) : le modele est donc deja pret
        /// sans que la phase suivante ait a le rouvrir.
        /// </param>
        public static int Next(int population, int developmentLevel, float stability, float foodSatisfaction = 1f)
        {
            int current = Mathf.Max(0, population);
            if (current == 0)
            {
                // Un systeme vide le reste : la population n'apparait pas spontanement, elle
                // arrive par colonisation.
                return 0;
            }

            float food = Mathf.Clamp01(foodSatisfaction);
            float stable = Mathf.Clamp01(stability);

            // La famine l'emporte sur tout le reste : un systeme prospere mais affame decline.
            if (food < 1f)
            {
                float shortage = 1f - food;
                int lost = Mathf.CeilToInt(current * MaximumMonthlyDecline * shortage);
                return Mathf.Max(0, current - lost);
            }

            if (stable < StabilityFloorForGrowth)
            {
                return current;
            }

            int capacity = CapacityFor(developmentLevel);
            if (current >= capacity)
            {
                // Au-dessus de la capacite — apres une baisse de developpement par sabotage, par
                // exemple — le systeme se degonfle lentement au lieu de rester fige.
                int excess = current - capacity;
                return current - Mathf.CeilToInt(excess * MaximumMonthlyDecline);
            }

            float headroom = 1f - current / (float)capacity;

            // La stabilite est rapportee a son plancher : juste au-dessus du seuil la croissance
            // demarre a peine, ce qui evite un saut brutal de zero a pleine vitesse.
            float stabilityFactor = (stable - StabilityFloorForGrowth) / (1f - StabilityFloorForGrowth);

            float growth = current * MaximumMonthlyGrowth * headroom * stabilityFactor;

            // Arrondi vers le haut : sans cela, un petit systeme dont la croissance calculee
            // vaut 0,4 habitant resterait bloque a jamais.
            return Mathf.Min(capacity, current + Mathf.CeilToInt(growth));
        }
    }
}
