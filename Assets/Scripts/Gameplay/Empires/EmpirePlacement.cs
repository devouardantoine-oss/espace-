using System;
using System.Collections.Generic;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Choisit les systèmes d'origine d'un ensemble d'empires : déterministe, sans
    /// dépendance à Unity au-delà de <see cref="Vector2"/> (déjà utilisé par
    /// <see cref="StarSystemState"/>).
    /// <para>
    /// <b>Algorithme :</b> le premier système choisi est le plus proche du centre de la
    /// galaxie (même règle que l'attribution du système du joueur en Phase 4). Chaque
    /// système suivant est celui, parmi les systèmes encore non choisis, dont la distance à
    /// son voisin choisi le plus proche est <b>maximale</b> (« farthest-point sampling ») :
    /// cela disperse les origines des empires au lieu de les agglutiner, sans recourir à
    /// l'aléatoire (donc reproductible pour une galaxie donnée).
    /// </para>
    /// <para>
    /// Complexité O(count² × systèmes) — négligeable pour 6 empires sur 100 systèmes ; une
    /// structure accélératrice n'aurait de sens qu'à une toute autre échelle.
    /// </para>
    /// </summary>
    public static class EmpirePlacement
    {
        /// <summary>Choisit <paramref name="count"/> systèmes d'origine distincts dans <paramref name="map"/>.</summary>
        /// <exception cref="ArgumentNullException">Si <paramref name="map"/> est null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Si <paramref name="count"/> est négatif ou dépasse le nombre de systèmes de la galaxie.
        /// </exception>
        public static StarSystemId[] ChooseHomeSystems(GalaxyMap map, int count)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Ne peut pas etre negatif.");
            }

            if (count > map.Systems.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Depasse le nombre de systemes disponibles dans la galaxie.");
            }

            if (count == 0)
            {
                return Array.Empty<StarSystemId>();
            }

            var chosen = new List<StarSystemState>(count);

            StarSystemState closestToCenter = map.Systems[0];
            float closestSqrDistance = float.MaxValue;
            foreach (StarSystemState system in map.Systems)
            {
                float sqrDistance = system.Position.sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestToCenter = system;
                }
            }
            chosen.Add(closestToCenter);

            while (chosen.Count < count)
            {
                StarSystemState farthest = null;
                float farthestMinDistance = -1f;

                foreach (StarSystemState candidate in map.Systems)
                {
                    if (chosen.Contains(candidate))
                    {
                        continue;
                    }

                    float minDistanceToChosen = float.MaxValue;
                    foreach (StarSystemState alreadyChosen in chosen)
                    {
                        float distance = Vector2.Distance(candidate.Position, alreadyChosen.Position);
                        if (distance < minDistanceToChosen)
                        {
                            minDistanceToChosen = distance;
                        }
                    }

                    if (minDistanceToChosen > farthestMinDistance)
                    {
                        farthestMinDistance = minDistanceToChosen;
                        farthest = candidate;
                    }
                }

                chosen.Add(farthest);
            }

            var result = new StarSystemId[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = chosen[i].Id;
            }

            return result;
        }

        /// <summary>
        /// Reordonne <paramref name="candidateSlots"/> (le resultat de <see cref="ChooseHomeSystems"/>)
        /// pour que l'emplacement d'indice <paramref name="preferredPlayerSlotIndex"/> passe en
        /// premiere position — le joueur est toujours <c>empires[0]</c>
        /// (voir <see cref="EmpireFactory.CreateEmpires"/>), donc c'est ce qu'il faut pour que ce
        /// soit lui qui occupe l'emplacement choisi dans l'ecran de la Phase 13. Le reste
        /// conserve son ordre relatif.
        /// </summary>
        /// <param name="preferredPlayerSlotIndex">
        /// <c>null</c>, ou tout index hors des bornes de <paramref name="candidateSlots"/>,
        /// reproduit exactement le comportement d'avant la Phase 13 (le tableau est retourne
        /// tel quel : l'emplacement 0, le plus proche du centre, reste en tete).
        /// </param>
        /// <exception cref="ArgumentNullException">Si <paramref name="candidateSlots"/> est null.</exception>
        public static StarSystemId[] AssignHomeSystems(StarSystemId[] candidateSlots, int? preferredPlayerSlotIndex)
        {
            if (candidateSlots == null)
            {
                throw new ArgumentNullException(nameof(candidateSlots));
            }

            if (preferredPlayerSlotIndex == null
                || preferredPlayerSlotIndex.Value < 0
                || preferredPlayerSlotIndex.Value >= candidateSlots.Length)
            {
                return candidateSlots;
            }

            int chosenIndex = preferredPlayerSlotIndex.Value;
            var result = new StarSystemId[candidateSlots.Length];
            result[0] = candidateSlots[chosenIndex];

            int writeIndex = 1;
            for (int i = 0; i < candidateSlots.Length; i++)
            {
                if (i == chosenIndex)
                {
                    continue;
                }

                result[writeIndex] = candidateSlots[i];
                writeIndex++;
            }

            return result;
        }
    }
}
