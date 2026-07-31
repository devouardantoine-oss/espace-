using System;
using System.Collections.Generic;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Calcul d'itineraire le long des routes hyperspatiales (Phase 17).
    /// <para>
    /// <b>Fonctions statiques pures</b>, meme esprit que <see cref="Espace.Gameplay.Military.CombatResolver"/>
    /// et <see cref="Espace.Gameplay.Empires.EmpirePlacement"/> : aucune dependance a une scene,
    /// aucun aleatoire, testables en EditMode et recoupables par un script independant.
    /// </para>
    /// <para>
    /// <b>Dijkstra pondere par la distance, pas par le nombre de sauts :</b> le brief demande une
    /// duree fonction de la distance, donc le bon itineraire est le plus court en distance
    /// parcourue — un detour de trois sauts courts peut etre plus rapide qu'un saut long. Les
    /// aretes n'ont pas de poids stocke (<see cref="HyperlaneLink"/> n'en porte pas) : il est
    /// recalcule par <c>Vector2.Distance</c> entre les positions des deux systemes.
    /// </para>
    /// <para>
    /// <b>Implementation en O(V²), sans tas binaire :</b> a une centaine de systemes le cout est
    /// negligeable (l'appel a lieu au lancement d'un ordre de deplacement, pas par frame), et le
    /// balayage lineaire permet un <b>departage strictement deterministe</b> sur
    /// <see cref="StarSystemId"/> — l'ordre d'egalite d'un tas serait, lui, une source de
    /// resultats variables d'une execution a l'autre, ce que ce projet s'interdit partout.
    /// </para>
    /// <para>
    /// <b>Ne jamais appeler depuis <c>OnGUI</c> :</b> cette methode alloue et <c>OnGUI</c>
    /// s'execute au moins deux fois par frame (Layout puis Repaint). Un apercu d'itineraire doit
    /// etre mis en cache par l'appelant.
    /// </para>
    /// </summary>
    public static class HyperlanePathfinder
    {
        /// <summary>
        /// Cherche le trajet le plus court (en distance) de <paramref name="from"/> a
        /// <paramref name="to"/>. Le chemin renvoye <b>inclut les deux extremites</b> ; il vaut
        /// donc un seul element si origine et destination sont identiques.
        /// <para>
        /// <b><paramref name="isIntermediateTraversable"/> ne s'applique qu'aux systemes
        /// intermediaires</b>, jamais a l'origine ni a la destination : un systeme ennemi est une
        /// destination parfaitement legale (on vient l'attaquer) sans etre un point de passage
        /// legal. Appliquer le filtre aux extremites rendrait toute attaque impossible.
        /// <c>null</c> signifie « tout est traversable ».
        /// </para>
        /// </summary>
        public static bool TryFindPath(
            GalaxyMap map, StarSystemId from, StarSystemId to,
            Func<StarSystemState, bool> isIntermediateTraversable, out IReadOnlyList<StarSystemId> path)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            path = null;

            if (!map.TryGetSystem(from, out _) || !map.TryGetSystem(to, out _))
            {
                return false;
            }

            if (from.Equals(to))
            {
                path = new[] { from };
                return true;
            }

            var distanceTo = new Dictionary<StarSystemId, float>();
            var cameFrom = new Dictionary<StarSystemId, StarSystemId>();
            var settled = new HashSet<StarSystemId>();

            distanceTo[from] = 0f;

            while (true)
            {
                if (!TryExtractNearest(distanceTo, settled, out StarSystemId current))
                {
                    return false; // Plus rien d'atteignable : aucune route ne mene a destination.
                }

                if (current.Equals(to))
                {
                    path = BuildPath(cameFrom, from, to);
                    return true;
                }

                settled.Add(current);
                RelaxNeighbors(map, current, to, isIntermediateTraversable, distanceTo, cameFrom, settled);
            }
        }

        /// <summary>
        /// Distance totale parcourue le long de <paramref name="path"/>, en additionnant chaque
        /// tronçon. Zero pour un chemin vide ou reduit a un seul systeme.
        /// </summary>
        public static float TotalDistance(GalaxyMap map, IReadOnlyList<StarSystemId> path)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (path == null || path.Count < 2)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                total += LegDistance(map, path[i], path[i + 1]);
            }

            return total;
        }

        /// <summary>Distance d'un tronçon entre deux systemes adjacents.</summary>
        public static float LegDistance(GalaxyMap map, StarSystemId from, StarSystemId to)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            return map.TryGetSystem(from, out StarSystemState a) && map.TryGetSystem(to, out StarSystemState b)
                ? Vector2.Distance(a.Position, b.Position)
                : 0f;
        }

        /// <summary>
        /// Systeme non encore traite le plus proche de l'origine. A egalite de distance, le plus
        /// petit identifiant l'emporte — c'est ce qui garantit un itineraire identique d'une
        /// execution a l'autre.
        /// </summary>
        private static bool TryExtractNearest(
            Dictionary<StarSystemId, float> distanceTo, HashSet<StarSystemId> settled, out StarSystemId nearest)
        {
            nearest = default;
            bool found = false;
            float bestDistance = float.MaxValue;

            foreach (KeyValuePair<StarSystemId, float> entry in distanceTo)
            {
                if (settled.Contains(entry.Key))
                {
                    continue;
                }

                bool better = !found
                    || entry.Value < bestDistance
                    || (Mathf.Approximately(entry.Value, bestDistance) && entry.Key.Value < nearest.Value);

                if (better)
                {
                    nearest = entry.Key;
                    bestDistance = entry.Value;
                    found = true;
                }
            }

            return found;
        }

        private static void RelaxNeighbors(
            GalaxyMap map, StarSystemId current, StarSystemId destination,
            Func<StarSystemState, bool> isIntermediateTraversable,
            Dictionary<StarSystemId, float> distanceTo, Dictionary<StarSystemId, StarSystemId> cameFrom,
            HashSet<StarSystemId> settled)
        {
            float currentDistance = distanceTo[current];

            foreach (StarSystemId neighborId in map.GetNeighbors(current))
            {
                if (settled.Contains(neighborId) || !map.TryGetSystem(neighborId, out StarSystemState neighbor))
                {
                    continue;
                }

                // Le filtre ne concerne que les points de passage : la destination finale y echappe
                // toujours (voir la remarque de TryFindPath).
                if (!neighborId.Equals(destination) && isIntermediateTraversable != null && !isIntermediateTraversable(neighbor))
                {
                    continue;
                }

                float candidate = currentDistance + LegDistance(map, current, neighborId);

                if (distanceTo.TryGetValue(neighborId, out float known))
                {
                    bool strictlyShorter = candidate < known && !Mathf.Approximately(candidate, known);
                    // A distance egale, le predecesseur de plus petit identifiant l'emporte :
                    // meme role que le departage de TryExtractNearest, pour que deux trajets de
                    // meme longueur donnent toujours le meme itineraire.
                    bool sameLengthButLowerId = Mathf.Approximately(candidate, known)
                        && cameFrom.TryGetValue(neighborId, out StarSystemId knownPredecessor)
                        && current.Value < knownPredecessor.Value;

                    if (!strictlyShorter && !sameLengthButLowerId)
                    {
                        continue;
                    }
                }

                distanceTo[neighborId] = candidate;
                cameFrom[neighborId] = current;
            }
        }

        private static IReadOnlyList<StarSystemId> BuildPath(
            Dictionary<StarSystemId, StarSystemId> cameFrom, StarSystemId from, StarSystemId to)
        {
            var reversed = new List<StarSystemId> { to };
            StarSystemId step = to;

            while (!step.Equals(from))
            {
                step = cameFrom[step];
                reversed.Add(step);
            }

            reversed.Reverse();
            return reversed;
        }
    }
}
