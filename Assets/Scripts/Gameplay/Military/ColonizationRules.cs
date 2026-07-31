using System;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Regles de colonisation d'un systeme libre (Phase 16) : combien d'Infanterie il faut
    /// embarquer, et combien s'y perd a l'installation.
    /// <para>
    /// <b>Fonctions statiques pures</b>, meme esprit que <see cref="CombatResolver"/> et
    /// <see cref="Espace.Gameplay.Empires.EmpirePlacement"/> : aucune dependance a Unity au-dela
    /// de <c>Mathf</c>, aucun aleatoire, testables sans scene et recoupables par un script
    /// independant.
    /// </para>
    /// <para>
    /// <b>« Defenses » d'un systeme libre = son developpement.</b> Aucun champ de defense
    /// n'existe sur <see cref="StarSystemState"/>, et un systeme sans proprietaire n'a par
    /// construction aucune garnison. Le niveau de developpement sert donc de mesure de ce
    /// qu'il faut surmonter pour s'y implanter — exactement le role qu'il joue deja comme
    /// bonus de terrain defensif dans la formule de combat (voir
    /// <c>MilitaryService.TerrainBonusPerDevelopmentLevel</c>).
    /// </para>
    /// <para>
    /// <b>Echelle des pertes.</b> Le brief raisonne en soldats (« 5 perdus sur un petit
    /// systeme, 150 sur un grand systeme developpe ») ; le jeu compte en unites, avec un
    /// plafond de 10 par flotte depuis la Phase 14. L'exigence est donc ramenee a 1..6 unites
    /// d'Infanterie — toujours realisable en une seule flotte — et les pertes a 1..exigence.
    /// </para>
    /// </summary>
    public static class ColonizationRules
    {
        /// <summary>Population (en millions) couverte par une unite d'Infanterie supplementaire : 0-1499 -> +0, 1500-2999 -> +1, 3000+ -> +2 sur la plage generee (0-4000).</summary>
        private const int PopulationPerExtraInfantry = 1500;

        /// <summary>Pertes a stabilite maximale : <c>1.25 - 1.0 = 0.25</c>, soit un quart des colons (au moins un).</summary>
        private const float LossFactorAtFullStability = 1.25f;

        /// <summary>
        /// Nombre d'unites d'Infanterie qu'une flotte doit transporter pour pouvoir coloniser
        /// <paramref name="target"/>. Toujours entre 1 et 6, quelles que soient les
        /// statistiques du systeme.
        /// <para>
        /// <c>1 + population/1500 + (developpement + 1)/2</c> : une unite de base toujours
        /// requise, puis une par tranche de population, puis une par tranche de deux niveaux
        /// de developpement (0,1,1,2,2,3 pour les niveaux 0 a 5) — « plus le systeme est
        /// developpe, plus il faut d'infanterie », comme demande.
        /// </para>
        /// </summary>
        public static int RequiredInfantry(StarSystemState target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            int populationTier = Mathf.Max(0, target.Population) / PopulationPerExtraInfantry;
            int developmentTier = (Mathf.Max(0, target.DevelopmentLevel) + 1) / 2;
            return 1 + populationTier + developmentTier;
        }

        /// <summary>
        /// Nombre d'unites d'Infanterie perdues en colonisant <paramref name="target"/> :
        /// toujours au moins une (des colons restent sur place) et jamais plus que
        /// <see cref="RequiredInfantry"/>.
        /// <para>
        /// Les pertes suivent la taille du systeme (via l'exigence) et sa stabilite : un
        /// systeme stable (1.0) coute un quart des colons, un systeme au bord de la revolte
        /// (0.2) les consomme tous. C'est la traduction en unites du « petit systeme = peu de
        /// pertes, grand systeme developpe = beaucoup » du brief.
        /// </para>
        /// </summary>
        public static int InfantryLost(StarSystemState target)
        {
            int required = RequiredInfantry(target);
            float lossFraction = LossFactorAtFullStability - Mathf.Clamp01(target.Stability);
            return Mathf.Clamp(Mathf.CeilToInt(required * lossFraction), 1, required);
        }
    }
}
