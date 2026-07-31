using System.Collections.Generic;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Ou une flotte peut passer, et a combien de sauts se trouve chaque destination
    /// atteignable depuis le territoire d'un empire (Phase 18).
    /// <para>
    /// Fonctions statiques pures, meme esprit que <see cref="HyperlanePathfinder"/> : aucune
    /// dependance a <c>ServiceLocator</c> ni a un <c>MonoBehaviour</c>, testables sans scene.
    /// </para>
    /// </summary>
    public static class FleetRouting
    {
        /// <summary>
        /// Un systeme peut-il servir de <b>point de passage</b> a une flotte de
        /// <paramref name="empireId"/> ?
        /// <para>
        /// <b>Source unique de la regle</b> (Phase 18) : utilisee a la fois par
        /// <c>MilitaryService.TryPlanRoute</c>, qui calcule l'itineraire reel, et par
        /// <see cref="HopDistances"/>, qui sert a l'IA a choisir ses cibles. Les laisser diverger
        /// ferait choisir a l'IA des destinations que le service refuserait ensuite — elle
        /// gaspillerait son mois, et pire, elle aurait deja detache une flotte pour rien.
        /// </para>
        /// <para>
        /// Ne s'applique <b>jamais aux extremites</b> d'un trajet : un systeme ennemi est une
        /// destination legale mais pas un couloir. Voir <see cref="HyperlanePathfinder.TryFindPath"/>.
        /// </para>
        /// </summary>
        public static bool IsTraversableWaypoint(StarSystemState waypoint, int empireId)
        {
            return waypoint != null
                && (waypoint.OwnerId == StarSystemState.UnownedOwnerId || waypoint.OwnerId == empireId);
        }

        /// <summary>Distance en sauts d'une destination, et systeme possede d'ou l'atteindre au plus court.</summary>
        public readonly struct Reach
        {
            /// <summary>Nombre de sauts hyperspatiaux depuis <see cref="Origin"/> (au moins 1).</summary>
            public readonly int Hops;

            /// <summary>Le systeme possede par l'empire d'ou partir : c'est de la que la flotte sera detachee.</summary>
            public readonly StarSystemId Origin;

            public Reach(int hops, StarSystemId origin)
            {
                Hops = hops;
                Origin = origin;
            }
        }

        /// <summary>
        /// Parcours en largeur <b>multi-source</b> depuis tous les systemes possedes par
        /// <paramref name="empireId"/> : renvoie, pour chaque systeme atteignable en au plus
        /// <paramref name="maxHops"/> sauts, sa distance et le systeme possede le plus proche.
        /// Les systemes de depart eux-memes ne figurent pas dans le resultat.
        /// <para>
        /// <b>Un seul parcours par empire et par mois, en O(V+E).</b> Interroger le pathfinder
        /// pour chaque cible possible ferait, sur 100 systemes et 5 IA, cinq cents Dijkstra
        /// O(V²) par mois de jeu. Ici l'IA a besoin de comparer des candidats, pas de connaitre
        /// l'itineraire exact : la distance en sauts suffit a classer, et
        /// <c>MilitaryService.TryMoveFleet</c> calculera de toute facon le vrai chemin pondere
        /// par la distance au moment du depart.
        /// </para>
        /// <para>
        /// <b>Un systeme non traversable est atteint mais jamais depasse :</b> il entre dans le
        /// resultat (on peut vouloir l'attaquer) mais ses propres voisins ne sont pas explores a
        /// travers lui. C'est exactement la regle « le predicat ne s'applique pas aux
        /// extremites » de la Phase 17, transposee au parcours en largeur — sans elle, l'IA
        /// planifierait des trajets passant par le territoire d'un tiers, que
        /// <c>TryPlanRoute</c> refuse.
        /// </para>
        /// <para>
        /// Deterministe : les systemes de depart sont visites dans l'ordre de
        /// <see cref="GalaxyMap.Systems"/> et la file preserve cet ordre, donc a egalite de
        /// distance c'est toujours la meme origine qui gagne.
        /// </para>
        /// </summary>
        private readonly struct Step
        {
            public readonly StarSystemId SystemId;
            public readonly int Hops;
            public readonly StarSystemId Origin;

            public Step(StarSystemId systemId, int hops, StarSystemId origin)
            {
                SystemId = systemId;
                Hops = hops;
                Origin = origin;
            }
        }

        public static Dictionary<StarSystemId, Reach> HopDistances(int empireId, GalaxyMap map, int maxHops)
        {
            var reached = new Dictionary<StarSystemId, Reach>();
            if (map == null || maxHops <= 0)
            {
                return reached;
            }

            var visited = new HashSet<StarSystemId>();
            var frontier = new Queue<Step>();

            // Amorcage : chaque systeme possede est une source a distance 0, sa propre origine.
            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId != empireId)
                {
                    continue;
                }

                visited.Add(system.Id);
                frontier.Enqueue(new Step(system.Id, 0, system.Id));
            }

            while (frontier.Count > 0)
            {
                Step step = frontier.Dequeue();
                if (step.Hops >= maxHops)
                {
                    continue;
                }

                foreach (StarSystemId neighborId in map.GetNeighbors(step.SystemId))
                {
                    if (visited.Contains(neighborId) || !map.TryGetSystem(neighborId, out StarSystemState neighbor))
                    {
                        continue;
                    }

                    visited.Add(neighborId);
                    int hops = step.Hops + 1;
                    reached[neighborId] = new Reach(hops, step.Origin);

                    // Atteint mais pas depasse : on ne traverse pas le territoire d'un tiers.
                    if (IsTraversableWaypoint(neighbor, empireId))
                    {
                        frontier.Enqueue(new Step(neighborId, hops, step.Origin));
                    }
                }
            }

            return reached;
        }
    }
}
