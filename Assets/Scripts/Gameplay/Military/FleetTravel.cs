using System;
using System.Collections.Generic;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Arithmetique de duree d'un voyage de flotte (Phase 20).
    /// <para>
    /// <b>Extraite de <see cref="MilitaryService"/> plutot que recopiee :</b> la planification
    /// d'offensive doit annoncer au joueur la duree exacte que le service appliquera. Deux
    /// implementations de la meme formule divergeraient a la premiere retouche d'equilibrage, et
    /// l'interface se mettrait alors a promettre des dates que le jeu ne tiendrait pas. Le
    /// service delegue desormais ici ; il n'y a qu'une formule.
    /// </para>
    /// <para>
    /// Classe statique pure : aucune dependance au <c>ServiceLocator</c>, les multiplicateurs de
    /// recherche et d'Amiral sont passes par l'appelant, qui seul sait les resoudre.
    /// </para>
    /// </summary>
    public static class FleetTravel
    {
        /// <summary>Vitesse de repli quand aucune unite du catalogue n'est embarquee.</summary>
        private const float FallbackSpeed = 1f;

        /// <summary>
        /// La vitesse d'une flotte mixte est celle de son unite la plus lente : un Cuirasse
        /// escortant de l'Infanterie n'ira pas plus vite qu'elle.
        /// </summary>
        public static float SlowestSpeed(UnitBundle composition, IReadOnlyList<UnitTypeDefinition> catalog)
        {
            if (catalog == null)
            {
                return FallbackSpeed;
            }

            float slowest = float.MaxValue;
            bool any = false;

            foreach (UnitTypeDefinition unitType in catalog)
            {
                if (unitType == null || composition.Get(unitType.UnitType) <= 0)
                {
                    continue;
                }

                any = true;
                if (unitType.Speed < slowest)
                {
                    slowest = unitType.Speed;
                }
            }

            return any ? slowest : FallbackSpeed;
        }

        /// <summary>
        /// Vitesse effective d'une flotte : sa plus lente unite, acceleree par la recherche en
        /// Logistique et par le bonus de vitesse de son Amiral.
        /// </summary>
        public static float EffectiveSpeed(
            UnitBundle composition,
            IReadOnlyList<UnitTypeDefinition> catalog,
            float logisticsMultiplier,
            float admiralSpeedBonus)
        {
            float speed = SlowestSpeed(composition, catalog) * logisticsMultiplier * (1f + admiralSpeedBonus);
            return speed > 0.0001f ? speed : FallbackSpeed;
        }

        /// <summary>
        /// Distance cumulee du depart jusqu'a la fin de l'etape <paramref name="throughLegIndex"/>
        /// incluse, le long de <paramref name="route"/>.
        /// </summary>
        public static float CumulativeDistance(GalaxyMap map, IReadOnlyList<StarSystemId> route, int throughLegIndex)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (route == null) throw new ArgumentNullException(nameof(route));

            float distance = 0f;
            for (int leg = 0; leg <= throughLegIndex && leg + 1 < route.Count; leg++)
            {
                distance += HyperlanePathfinder.LegDistance(map, route[leg], route[leg + 1]);
            }

            return distance;
        }

        /// <summary>
        /// Jours ecoules depuis le depart du voyage a la fin de l'etape <paramref name="legIndex"/>.
        /// <para>
        /// <b>L'arrondi n'a lieu qu'une fois, sur la distance cumulee :</b> arrondir chaque etape
        /// separement ferait payer a un trajet de quinze sauts quinze arrondis et quinze
        /// planchers d'un jour, alors que la duree doit dependre de la distance. Le plancher
        /// <c>legIndex + 1</c> garantit malgre tout au moins un jour par etape, donc une suite de
        /// dates strictement croissante.
        /// </para>
        /// </summary>
        public static int DaysToLeg(float cumulativeDistance, float speed, int legIndex)
        {
            float effectiveSpeed = speed > 0.0001f ? speed : FallbackSpeed;
            return Mathf.Max(legIndex + 1, Mathf.CeilToInt(cumulativeDistance / effectiveSpeed));
        }

        /// <summary>
        /// Duree totale, en jours, d'un voyage parcourant <paramref name="route"/> entiere. C'est
        /// la valeur que la planification d'offensive annonce au joueur.
        /// </summary>
        /// <returns>0 si l'itineraire est vide ou reduit au systeme de depart.</returns>
        public static int JourneyDays(GalaxyMap map, IReadOnlyList<StarSystemId> route, float speed)
        {
            if (route == null || route.Count < 2)
            {
                return 0;
            }

            int lastLeg = route.Count - 2;
            return DaysToLeg(CumulativeDistance(map, route, lastLeg), speed, lastLeg);
        }
    }
}
