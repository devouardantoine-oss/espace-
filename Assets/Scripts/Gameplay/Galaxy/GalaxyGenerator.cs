using System;
using System.Collections.Generic;
using Espace.Data;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Genere une <see cref="GalaxyMap"/> deterministe : memes parametres, meme graine =
    /// exactement la meme galaxie (systemes, noms, statistiques, routes).
    /// <para>
    /// <b>Pourquoi <see cref="Random"/> (System) et pas <c>UnityEngine.Random</c> ?</b>
    /// <c>UnityEngine.Random</c> repose sur un etat global partage par tout le jeu : deux
    /// appels a <see cref="Generate"/> dans la meme frame (ou entrelaces avec un autre
    /// systeme qui consomme de l'aleatoire) donneraient des resultats differents malgre une
    /// graine identique. Une instance de <see cref="Random"/> dediee isole totalement la
    /// generation, ce qui la rend testable et reproductible.
    /// </para>
    /// <para>
    /// <b>Algorithme, volontairement simple (voir la consigne « version simple d'abord ») :</b>
    /// </para>
    /// <list type="number">
    /// <item>Position des systemes : echantillonnage par rejet dans un disque, avec une
    /// distance minimale imposee entre deux systemes.</item>
    /// <item>Routes hyperspatiales : toutes les paires sont triees par distance, un
    /// algorithme de Kruskal (union-find) construit d'abord un arbre couvrant minimal — donc
    /// une galaxie <b>toujours entierement connectee</b> — puis des routes supplementaires
    /// courtes sont ajoutees jusqu'au degre moyen vise, pour obtenir un reseau plutot qu'un
    /// simple arbre.</item>
    /// </list>
    /// <para>
    /// Complexite O(N^2 log N) dominee par le tri des paires : sans consequence pour les
    /// 100 systemes du MVP. Une triangulation de Delaunay donnerait un reseau plus « propre »
    /// visuellement mais serait notablement plus complexe a implementer et deboguer sans
    /// pouvoir ouvrir l'editeur pour verifier le rendu — a envisager en phase d'equilibrage.
    /// </para>
    /// </summary>
    public static class GalaxyGenerator
    {
        private const int MinPopulation = 0;
        private const int MaxPopulation = 4000;
        private const int MinWealth = 5;
        private const int MaxWealth = 100;
        private const int MinDevelopmentLevel = 0;
        private const int MaxDevelopmentLevel = 5;
        private const float MinStability = 0.2f;
        private const float MaxStability = 1f;

        /// <summary>Genere une galaxie complete a partir de <paramref name="parameters"/>.</summary>
        public static GalaxyMap Generate(GalaxyGenerationParameters parameters)
        {
            var rng = new Random(parameters.Seed);

            StarSystemState[] systems = GenerateSystems(parameters, rng);
            HyperlaneLink[] links = GenerateLinks(systems, parameters, rng);

            return new GalaxyMap(systems, links);
        }

        private static StarSystemState[] GenerateSystems(GalaxyGenerationParameters parameters, Random rng)
        {
            var systems = new StarSystemState[parameters.SystemCount];
            var usedNames = new HashSet<string>();
            var placedPositions = new List<Vector2>(parameters.SystemCount);

            for (int i = 0; i < parameters.SystemCount; i++)
            {
                Vector2 position = PlaceSystem(parameters, rng, placedPositions);
                placedPositions.Add(position);

                var id = new StarSystemId(i);
                string name = StarSystemNameGenerator.GenerateUnique(rng, usedNames);

                int population = rng.Next(MinPopulation, MaxPopulation + 1);
                int wealth = rng.Next(MinWealth, MaxWealth + 1);
                int developmentLevel = rng.Next(MinDevelopmentLevel, MaxDevelopmentLevel + 1);
                float stability = Lerp(MinStability, MaxStability, (float)rng.NextDouble());
                ResourceType[] deposits = GenerateResourceDeposits(rng);

                systems[i] = new StarSystemState(id, name, position, population, wealth, developmentLevel, stability, deposits);
            }

            return systems;
        }

        /// <summary>
        /// Echantillonnage par rejet : essaie jusqu'a <see cref="GalaxyGenerationParameters.MaxPlacementAttempts"/>
        /// positions uniformes dans le disque et retient la premiere qui respecte la distance
        /// minimale. Si aucune n'y parvient (galaxie dense, fin de remplissage), on garde le
        /// meilleur candidat rencontre plutot que d'echouer ou de boucler indefiniment.
        /// </summary>
        private static Vector2 PlaceSystem(GalaxyGenerationParameters parameters, Random rng, List<Vector2> placedPositions)
        {
            Vector2 bestCandidate = Vector2.zero;
            float bestCandidateMinDistance = -1f;

            for (int attempt = 0; attempt < parameters.MaxPlacementAttempts; attempt++)
            {
                Vector2 candidate = SampleUniformInDisc(parameters.GalaxyRadius, rng);
                float nearestDistance = NearestDistance(candidate, placedPositions);

                if (nearestDistance >= parameters.MinSystemDistance)
                {
                    return candidate;
                }

                if (nearestDistance > bestCandidateMinDistance)
                {
                    bestCandidateMinDistance = nearestDistance;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        /// <summary>Point uniforme dans un disque de rayon <paramref name="radius"/> centre en (0,0).</summary>
        private static Vector2 SampleUniformInDisc(float radius, Random rng)
        {
            // sqrt(u) compense la densite radiale : un tirage naif sur (angle, rayon) sans
            // racine carree sur-concentrerait les points pres du centre.
            double angle = rng.NextDouble() * Math.PI * 2.0;
            double distance = Math.Sqrt(rng.NextDouble()) * radius;

            return new Vector2((float)(Math.Cos(angle) * distance), (float)(Math.Sin(angle) * distance));
        }

        private static float NearestDistance(Vector2 point, List<Vector2> others)
        {
            if (others.Count == 0)
            {
                return float.MaxValue;
            }

            float nearest = float.MaxValue;
            for (int i = 0; i < others.Count; i++)
            {
                float distance = Vector2.Distance(point, others[i]);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }

        private static ResourceType[] GenerateResourceDeposits(Random rng)
        {
            // Distribution volontairement simple : 20% aucun gisement, 50% un seul, 30% deux.
            double roll = rng.NextDouble();
            int depositCount = roll < 0.2 ? 0 : roll < 0.7 ? 1 : 2;

            if (depositCount == 0)
            {
                return Array.Empty<ResourceType>();
            }

            if (depositCount == 1)
            {
                return new[] { ResourceTypes.All[rng.Next(ResourceTypes.Count)] };
            }

            // Deux gisements distincts : on tire le premier puis on re-tire jusqu'a en
            // obtenir un different plutot que de batir une structure de melange complete
            // pour seulement 5 valeurs possibles.
            ResourceType first = ResourceTypes.All[rng.Next(ResourceTypes.Count)];
            ResourceType second;
            do
            {
                second = ResourceTypes.All[rng.Next(ResourceTypes.Count)];
            } while (second == first);

            return new[] { first, second };
        }

        private static HyperlaneLink[] GenerateLinks(StarSystemState[] systems, GalaxyGenerationParameters parameters, Random rng)
        {
            int n = systems.Length;
            if (n < 2)
            {
                return Array.Empty<HyperlaneLink>();
            }

            // Toutes les paires, triees par distance croissante : le prefixe de cette liste
            // contient toujours les routes les plus courtes possibles, qu'on utilise pour
            // l'arbre couvrant minimal puis pour les routes supplementaires.
            var candidateEdges = new List<(int a, int b, float distance)>(n * (n - 1) / 2);
            for (int a = 0; a < n; a++)
            {
                for (int b = a + 1; b < n; b++)
                {
                    float distance = Vector2.Distance(systems[a].Position, systems[b].Position);
                    candidateEdges.Add((a, b, distance));
                }
            }
            candidateEdges.Sort((x, y) => x.distance.CompareTo(y.distance));

            var links = new List<HyperlaneLink>();
            var unionFind = new DisjointSet(n);
            int targetEdgeCount = Math.Max(n - 1, (int)Math.Round(n * parameters.TargetAverageDegree / 2f));

            // Passe 1 (Kruskal) : arbre couvrant minimal -> connexite totale garantie.
            foreach ((int a, int b, float _) in candidateEdges)
            {
                if (unionFind.Union(a, b))
                {
                    links.Add(new HyperlaneLink(systems[a].Id, systems[b].Id));
                }
            }

            // Passe 2 : routes supplementaires courtes, jusqu'au degre moyen vise, pour que
            // la carte soit un reseau et pas seulement un arbre (peu d'itineraires alternatifs).
            foreach ((int a, int b, float _) in candidateEdges)
            {
                if (links.Count >= targetEdgeCount)
                {
                    break;
                }

                var link = new HyperlaneLink(systems[a].Id, systems[b].Id);
                if (!links.Contains(link))
                {
                    links.Add(link);
                }
            }

            return links.ToArray();
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>
        /// Structure union-find (disjoint-set) avec compression de chemin et union par rang.
        /// <para>
        /// Reservee a <see cref="GalaxyGenerator"/> : c'est un detail d'implementation de
        /// l'algorithme de Kruskal, pas une brique reutilisee ailleurs pour l'instant. La
        /// sortir dans <c>Espace.Core</c> n'aurait de sens que si un second besoin concret
        /// apparaissait.
        /// </para>
        /// </summary>
        private sealed class DisjointSet
        {
            private readonly int[] _parent;
            private readonly int[] _rank;

            public DisjointSet(int size)
            {
                _parent = new int[size];
                _rank = new int[size];
                for (int i = 0; i < size; i++)
                {
                    _parent[i] = i;
                }
            }

            private int Find(int x)
            {
                if (_parent[x] != x)
                {
                    _parent[x] = Find(_parent[x]); // Compression de chemin.
                }

                return _parent[x];
            }

            /// <returns><c>true</c> si <paramref name="a"/> et <paramref name="b"/> etaient dans des composantes distinctes (et ont donc ete fusionnees).</returns>
            public bool Union(int a, int b)
            {
                int rootA = Find(a);
                int rootB = Find(b);
                if (rootA == rootB)
                {
                    return false;
                }

                if (_rank[rootA] < _rank[rootB])
                {
                    (rootA, rootB) = (rootB, rootA);
                }

                _parent[rootB] = rootA;
                if (_rank[rootA] == _rank[rootB])
                {
                    _rank[rootA]++;
                }

                return true;
            }
        }
    }
}
