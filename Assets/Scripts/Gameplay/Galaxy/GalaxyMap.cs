using System;
using System.Collections.Generic;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Graphe complet de la galaxie : systemes stellaires et routes hyperspatiales qui les relient.
    /// <para>
    /// Structure de donnees passive : elle ne genere rien elle-meme (voir
    /// <see cref="GalaxyGenerator"/>) et ne simule rien (l'economie et l'IA la liront et
    /// la modifieront dans les phases suivantes). Son seul role est d'offrir des acces en
    /// O(1) par identifiant et par voisinage, plutot que de parcourir des listes a chaque
    /// interrogation.
    /// </para>
    /// </summary>
    public sealed class GalaxyMap
    {
        private readonly StarSystemState[] _systems;
        private readonly HyperlaneLink[] _links;
        private readonly Dictionary<StarSystemId, StarSystemState> _systemsById;
        private readonly Dictionary<StarSystemId, List<StarSystemId>> _adjacency;

        /// <summary>Tous les systemes de la galaxie.</summary>
        public IReadOnlyList<StarSystemState> Systems => _systems;

        /// <summary>Toutes les routes hyperspatiales de la galaxie.</summary>
        public IReadOnlyList<HyperlaneLink> Links => _links;

        /// <summary>
        /// Construit une galaxie a partir de systemes et de liens deja generes.
        /// </summary>
        /// <exception cref="ArgumentNullException">Si <paramref name="systems"/> ou <paramref name="links"/> est null.</exception>
        /// <exception cref="ArgumentException">
        /// Si deux systemes partagent le meme identifiant, ou si un lien reference un
        /// identifiant absent de <paramref name="systems"/>. Echouer tot ici evite de
        /// propager une galaxie incoherente jusqu'a l'affichage.
        /// </exception>
        public GalaxyMap(IReadOnlyList<StarSystemState> systems, IReadOnlyList<HyperlaneLink> links)
        {
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (links == null) throw new ArgumentNullException(nameof(links));

            _systems = new StarSystemState[systems.Count];
            _systemsById = new Dictionary<StarSystemId, StarSystemState>(systems.Count);
            _adjacency = new Dictionary<StarSystemId, List<StarSystemId>>(systems.Count);

            for (int i = 0; i < systems.Count; i++)
            {
                StarSystemState system = systems[i];
                if (!_systemsById.TryAdd(system.Id, system))
                {
                    throw new ArgumentException($"Identifiant de systeme duplique : {system.Id}.", nameof(systems));
                }

                _systems[i] = system;
                _adjacency[system.Id] = new List<StarSystemId>();
            }

            _links = new HyperlaneLink[links.Count];
            for (int i = 0; i < links.Count; i++)
            {
                HyperlaneLink link = links[i];
                if (!_systemsById.ContainsKey(link.SystemA) || !_systemsById.ContainsKey(link.SystemB))
                {
                    throw new ArgumentException($"Le lien {link} reference un systeme inexistant.", nameof(links));
                }

                _links[i] = link;
                _adjacency[link.SystemA].Add(link.SystemB);
                _adjacency[link.SystemB].Add(link.SystemA);
            }
        }

        /// <summary>Recupere le systeme <paramref name="id"/>.</summary>
        /// <exception cref="KeyNotFoundException">Si l'identifiant n'existe pas dans cette galaxie.</exception>
        public StarSystemState GetSystem(StarSystemId id)
        {
            if (_systemsById.TryGetValue(id, out StarSystemState system))
            {
                return system;
            }

            throw new KeyNotFoundException($"Aucun systeme avec l'identifiant {id} dans cette galaxie.");
        }

        /// <summary>Variante non levante de <see cref="GetSystem"/>.</summary>
        public bool TryGetSystem(StarSystemId id, out StarSystemState system)
        {
            return _systemsById.TryGetValue(id, out system);
        }

        /// <summary>Identifiants des systemes directement relies a <paramref name="id"/> par une route.</summary>
        public IReadOnlyList<StarSystemId> GetNeighbors(StarSystemId id)
        {
            return _adjacency.TryGetValue(id, out List<StarSystemId> neighbors)
                ? neighbors
                : Array.Empty<StarSystemId>();
        }

        /// <summary>Indique si une route hyperspatiale relie directement les deux systemes.</summary>
        public bool AreLinked(StarSystemId a, StarSystemId b)
        {
            return _adjacency.TryGetValue(a, out List<StarSystemId> neighbors) && neighbors.Contains(b);
        }
    }
}
