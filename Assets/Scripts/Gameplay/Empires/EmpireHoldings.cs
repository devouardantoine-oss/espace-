using System.Collections.Generic;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Ce qu'un empire possede sur la carte : ses systemes, sa capitale, sa puissance totale,
    /// ses voisins (Phase 18).
    /// <para>
    /// Fonctions statiques pures, meme esprit que <see cref="EmpirePlacement"/> ou
    /// <see cref="Espace.Gameplay.Military.ColonizationRules"/> : aucune dependance a
    /// <c>ServiceLocator</c> ni a un <c>MonoBehaviour</c>, testables sans scene.
    /// </para>
    /// <para>
    /// <b>Pourquoi cette classe existe :</b> jusqu'a la Phase 17, chacun des cinq decision
    /// makers de l'IA embarquait sa propre copie privee d'un <c>FindPrimarySystem</c> qui
    /// renvoyait le <i>premier</i> systeme de <see cref="GalaxyMap.Systems"/> appartenant a
    /// l'empire, et raisonnait uniquement dessus. C'etait sans consequence tant qu'un empire ne
    /// possedait qu'un systeme ; depuis que la colonisation fonctionne reellement (Phase 16),
    /// cela veut dire que les colonies ne sont jamais developpees, jamais garnisonnees, et que
    /// l'IA cesse de s'etendre des que les voisins directs de sa capitale sont pris.
    /// </para>
    /// </summary>
    public static class EmpireHoldings
    {
        /// <summary>
        /// Tous les systemes possedes par <paramref name="empireId"/>, dans l'ordre de
        /// <see cref="GalaxyMap.Systems"/> — donc stable d'un appel a l'autre et d'une partie a
        /// l'autre (la generation est deterministe depuis la Phase 10).
        /// </summary>
        public static List<StarSystemState> OwnedSystems(int empireId, GalaxyMap map)
        {
            var owned = new List<StarSystemState>();
            if (map == null)
            {
                return owned;
            }

            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId == empireId)
                {
                    owned.Add(system);
                }
            }

            return owned;
        }

        /// <summary>
        /// Le systeme le plus developpe de l'empire, ou <c>null</c> s'il n'en possede aucun.
        /// A developpement egal, celui dont l'identifiant est le plus petit.
        /// <para>
        /// <b>Le developpement plutot que l'ordre de la carte :</b> une colonie nait au
        /// developpement 0, elle ne peut donc jamais deloger la capitale par accident — ce que
        /// « le premier systeme trouve » faisait des qu'une colonie tombait a un indice plus
        /// faible que la vraie capitale, l'IA se mettant alors a gerer un caillou vide en
        /// laissant son centre a l'abandon. Le critere suit malgre tout l'empire si sa capitale
        /// historique est conquise.
        /// </para>
        /// </summary>
        public static StarSystemState Capital(int empireId, GalaxyMap map)
        {
            if (map == null)
            {
                return null;
            }

            StarSystemState capital = null;
            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId != empireId)
                {
                    continue;
                }

                if (capital == null
                    || system.DevelopmentLevel > capital.DevelopmentLevel
                    || (system.DevelopmentLevel == capital.DevelopmentLevel && system.Id.Value < capital.Id.Value))
                {
                    capital = system;
                }
            }

            return capital;
        }

        /// <summary>
        /// Puissance de combat cumulee de toutes les garnisons de l'empire.
        /// <para>
        /// <b>Remplace la comparaison de deux garnisons isolees</b> dans
        /// <see cref="Espace.Gameplay.Diplomacy.DiplomacyDecisionMaker"/> (Phase 18) : des que
        /// les deux camps possedent plusieurs systemes, comparer la garnison de l'un a celle de
        /// l'autre ne mesure plus rien. Sur une partie ou chaque empire n'a qu'un systeme, la
        /// valeur est exactement l'ancienne.
        /// </para>
        /// <para>
        /// Ne compte que les garnisons stationnees, pas les flottes en vol : c'est ce que
        /// <see cref="IMilitaryService.GetGarrison"/> expose systeme par systeme, et une flotte
        /// en route n'est de toute facon defensivement nulle part.
        /// </para>
        /// </summary>
        public static float TotalPower(int empireId, GalaxyMap map, IMilitaryService military)
        {
            if (map == null || military == null)
            {
                return 0f;
            }

            float total = 0f;
            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId == empireId)
                {
                    total += military.EstimatePower(military.GetGarrison(system.Id, empireId));
                }
            }

            return total;
        }

        /// <summary>
        /// Identifiants des empires bordant <b>au moins un</b> systeme de
        /// <paramref name="empireId"/>, tries par ordre croissant (deterministe, sans doublon).
        /// <para>
        /// Avant la Phase 18 la diplomatie ne connaissait que les voisins de la capitale : un
        /// rival ne bordant que les colonies n'existait tout simplement pas a ses yeux — ni
        /// pacte, ni guerre, ni paix possible avec lui.
        /// </para>
        /// </summary>
        public static List<int> NeighboringEmpires(int empireId, GalaxyMap map)
        {
            var neighbors = new List<int>();
            if (map == null)
            {
                return neighbors;
            }

            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId != empireId)
                {
                    continue;
                }

                foreach (StarSystemId neighborId in map.GetNeighbors(system.Id))
                {
                    if (!map.TryGetSystem(neighborId, out StarSystemState neighbor)
                        || neighbor.OwnerId == StarSystemState.UnownedOwnerId
                        || neighbor.OwnerId == empireId
                        || neighbors.Contains(neighbor.OwnerId))
                    {
                        continue;
                    }

                    neighbors.Add(neighbor.OwnerId);
                }
            }

            neighbors.Sort();
            return neighbors;
        }

        /// <summary>
        /// Le premier systeme de <paramref name="ownerId"/> borde par un systeme de
        /// <paramref name="empireId"/>, ou <c>null</c> si les deux empires ne se touchent pas.
        /// Deterministe (ordre de <see cref="GalaxyMap.Systems"/>).
        /// <para>
        /// Un systeme adjacent est toujours joignable : l'itineraire ne compte aucune etape
        /// intermediaire, donc le filtre de traversabilite de
        /// <see cref="Espace.Gameplay.Military.FleetRouting"/> ne s'y applique jamais. C'est
        /// pourquoi la declaration de guerre reste fondee sur l'adjacence — elle garantit une
        /// guerre reellement exploitable, sans verification d'itineraire supplementaire.
        /// </para>
        /// </summary>
        public static StarSystemState FirstBorderSystemOf(int empireId, int ownerId, GalaxyMap map)
        {
            if (map == null)
            {
                return null;
            }

            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId != empireId)
                {
                    continue;
                }

                foreach (StarSystemId neighborId in map.GetNeighbors(system.Id))
                {
                    if (map.TryGetSystem(neighborId, out StarSystemState neighbor) && neighbor.OwnerId == ownerId)
                    {
                        return neighbor;
                    }
                }
            }

            return null;
        }
    }
}
