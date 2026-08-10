using UnityEngine;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Accumulation de richesse d'un systeme (Phase 22, P1).
    /// <para>
    /// <b>Le defaut corrige.</b> <c>Wealth</c> etait figee comme <c>Population</c>, alors qu'elle
    /// est la seule entree des credits. Le revenu d'un empire ne pouvait donc ni monter ni
    /// descendre autrement qu'en changeant de frontieres.
    /// </para>
    /// <para>
    /// <b>La richesse est ce que l'impot n'a pas pris.</b> C'est la contrepartie qui manquait a
    /// la fiscalite : taxer fort rapporte tout de suite et appauvrit la base imposable, taxer
    /// peu ne rapporte rien aujourd'hui et construit le revenu de demain. Le joueur arbitre
    /// entre son present et son futur, ce qui est une vraie decision — contrairement a
    /// « mettre le curseur au maximum ».
    /// </para>
    /// <para>
    /// <b>Plafonnee par la population et le developpement</b>, pas par un nombre fixe : un
    /// systeme ne s'enrichit pas au-dela de ce que ses habitants et ses infrastructures peuvent
    /// produire. Deuxieme frein a l'emballement, apres la capacite demographique.
    /// </para>
    /// </summary>
    public static class WealthModel
    {
        /// <summary>Richesse plafond apportee par point de population.</summary>
        public const float CapacityPerPopulation = 0.25f;

        /// <summary>Richesse plafond ajoutee par niveau de developpement.</summary>
        public const float CapacityPerDevelopmentLevel = 120f;

        /// <summary>Croissance mensuelle maximale, a capacite vide, impot nul et stabilite parfaite.</summary>
        public const float MaximumMonthlyGrowth = 0.05f;

        /// <summary>
        /// Erosion mensuelle appliquee quoi qu'il arrive. C'est elle qui empeche une richesse
        /// acquise de rester acquise : un systeme neglige — instable, surtaxe — retombe, ce qui
        /// ouvre la porte au rattrapage sans qu'aucun malus n'ait ete inflige a personne.
        /// </summary>
        public const float MonthlyDecay = 0.008f;

        /// <summary>Richesse maximale qu'un systeme peut soutenir.</summary>
        public static float CapacityFor(int population, int developmentLevel)
        {
            return Mathf.Max(0, population) * CapacityPerPopulation
                + Mathf.Max(0, developmentLevel) * CapacityPerDevelopmentLevel;
        }

        /// <summary>
        /// Richesse du mois suivant.
        /// </summary>
        /// <param name="wealth">Richesse actuelle.</param>
        /// <param name="population">Population du systeme.</param>
        /// <param name="developmentLevel">Niveau de developpement.</param>
        /// <param name="nominalTaxRate">Taux <b>affiche</b>, et non percu : c'est bien ce qui est preleve qui n'est pas reinvesti.</param>
        /// <param name="stability">Stabilite du systeme, entre 0 et 1.</param>
        public static int Next(int wealth, int population, int developmentLevel, float nominalTaxRate, float stability)
        {
            int current = Mathf.Max(0, wealth);
            float capacity = CapacityFor(population, developmentLevel);

            float decay = current * MonthlyDecay;

            if (capacity <= 0f)
            {
                return Mathf.Max(0, current - Mathf.CeilToInt(decay));
            }

            float headroom = Mathf.Max(0f, 1f - current / capacity);
            float growth = current * MaximumMonthlyGrowth
                * headroom
                * TaxationModel.ReinvestmentFactor(nominalTaxRate)
                * Mathf.Clamp01(stability);

            // Amorce : un systeme a richesse nulle ne pourrait jamais demarrer, la croissance
            // etant proportionnelle a l'existant. Un point de population en apporte le minimum.
            if (current == 0 && population > 0)
            {
                growth = Mathf.Max(growth, 1f);
            }

            float next = current + growth - decay;
            return Mathf.Clamp(Mathf.RoundToInt(next), 0, Mathf.CeilToInt(capacity));
        }
    }
}
