using System;
using System.Collections.Generic;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Sommet d'une cellule de controle, porteur de l'identite du voisin situe de l'autre cote
    /// de l'arete qui <b>part</b> de ce sommet (Phase 19).
    /// <para>
    /// <b>Pourquoi taguer l'arete plutot que recalculer l'adjacence apres coup ?</b> Le rendu a
    /// besoin de savoir, pour chaque arete, qui se trouve en face : c'est ce qui distingue une
    /// frontiere contestee (deux empires differents) d'une facade sur le vide, et c'est toute
    /// l'idee du concept « frontieres stellaires ». Cette information est connue gratuitement au
    /// moment du decoupage — le demi-plan qui cree l'arete <i>est</i> celui du voisin. La
    /// retrouver ensuite couterait une recherche geometrique par arete, avec les problemes de
    /// tolerance numerique que cela suppose.
    /// </para>
    /// </summary>
    public readonly struct TerritoryCellVertex
    {
        /// <summary>Position du sommet, en unites monde.</summary>
        public readonly Vector2 Position;

        /// <summary>
        /// Index du site voisin dont la bissectrice a produit l'arete partant de ce sommet, ou
        /// <see cref="TerritoryPartition.RimNeighbour"/> si cette arete appartient au bord du
        /// disque galactique.
        /// </summary>
        public readonly int NeighbourIndex;

        public TerritoryCellVertex(Vector2 position, int neighbourIndex)
        {
            Position = position;
            NeighbourIndex = neighbourIndex;
        }
    }

    /// <summary>
    /// Cellule de controle d'un systeme : le polygone convexe des points de la galaxie plus
    /// proches de ce systeme que de tout autre.
    /// </summary>
    public sealed class TerritoryCell
    {
        private readonly TerritoryCellVertex[] _vertices;

        /// <summary>Sommets du polygone, dans l'ordre trigonometrique.</summary>
        public IReadOnlyList<TerritoryCellVertex> Vertices => _vertices;

        /// <summary>Position du systeme proprietaire de la cellule. Toujours a l'interieur du polygone.</summary>
        public Vector2 Site { get; }

        /// <summary>
        /// Centre de gravite du polygone (pondere par l'aire, pas une simple moyenne des
        /// sommets). Sert d'ancrage au nom de faction : la moyenne des sommets derive vers les
        /// zones ou ils sont les plus denses et sortirait visuellement du territoire.
        /// </summary>
        public Vector2 Centroid { get; }

        /// <summary>Aire du polygone, en unites monde carrees. Pondere le centre de gravite d'un empire.</summary>
        public float Area { get; }

        internal TerritoryCell(Vector2 site, TerritoryCellVertex[] vertices)
        {
            _vertices = vertices;
            Site = site;

            ComputeAreaAndCentroid(vertices, site, out float area, out Vector2 centroid);
            Area = area;
            Centroid = centroid;
        }

        /// <summary>
        /// Aire et centre de gravite par la formule du lacet. Si le polygone est degenere
        /// (aire nulle, cas theoriquement impossible mais pas structurellement interdit), le
        /// site lui-meme sert de repli plutot que de propager une division par zero.
        /// </summary>
        private static void ComputeAreaAndCentroid(TerritoryCellVertex[] vertices, Vector2 fallback, out float area, out Vector2 centroid)
        {
            double doubleArea = 0d;
            double cx = 0d;
            double cy = 0d;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 a = vertices[i].Position;
                Vector2 b = vertices[(i + 1) % vertices.Length].Position;

                double cross = (double)a.x * b.y - (double)b.x * a.y;
                doubleArea += cross;
                cx += (a.x + b.x) * cross;
                cy += (a.y + b.y) * cross;
            }

            area = (float)Math.Abs(doubleArea) * 0.5f;

            if (Math.Abs(doubleArea) < 1e-9d)
            {
                centroid = fallback;
                return;
            }

            centroid = new Vector2((float)(cx / (3d * doubleArea)), (float)(cy / (3d * doubleArea)));
        }
    }

    /// <summary>
    /// Decoupe la galaxie en cellules de controle, une par systeme (Phase 19) : chaque point de
    /// l'espace appartient au systeme le plus proche. Les cellules d'un meme empire se touchent
    /// donc exactement, ce qui produit un territoire continu la ou les halos de la Phase 12 ne
    /// produisaient que des taches disjointes.
    /// <para>
    /// <b>Decoupage par demi-plans (Sutherland-Hodgman) plutot qu'un algorithme de Voronoi
    /// dedie (Fortune, Delaunay) :</b> le resultat est identique, mais l'implementation tient
    /// en une boucle de rognage triviale a relire et a tester, la ou Fortune demande une
    /// structure de front de mer et une file de priorite dont la moindre erreur de tolerance
    /// numerique produit une cellule silencieusement fausse. Le cout n'est paye qu'une fois, a
    /// la generation de la carte : les cellules ne dependent que des positions des systemes,
    /// qui ne bougent jamais. Seules les <i>couleurs</i> changent quand un systeme change de
    /// maitre, et cela ne retouche pas la geometrie.
    /// </para>
    /// <para>
    /// <b>Classe statique pure, sans dependance a Unity au-dela de <see cref="Vector2"/> :</b>
    /// meme raison que <see cref="GalaxyGenerator"/> ou <see cref="StarSystemVisualProfile"/> —
    /// tout est verifiable en EditMode sans instancier de scene.
    /// </para>
    /// </summary>
    public static class TerritoryPartition
    {
        /// <summary>Valeur de <see cref="TerritoryCellVertex.NeighbourIndex"/> pour une arete du bord du disque galactique.</summary>
        public const int RimNeighbour = -1;

        /// <summary>Nombre de cotes du polygone approximant le disque galactique.</summary>
        private const int DefaultBoundarySegments = 64;

        /// <summary>
        /// Tolerance de classification d'un sommet par rapport a une bissectrice. Un sommet a
        /// moins de cette distance du plan de coupe est considere « dedans » : cela evite de
        /// produire des aretes de longueur quasi nulle qui deviendraient des triangles degeneres
        /// au maillage.
        /// </summary>
        private const float Epsilon = 1e-5f;

        /// <summary>
        /// Calcule la cellule de controle de chaque site.
        /// </summary>
        /// <param name="sites">Positions des systemes, dans l'ordre ou les cellules seront retournees.</param>
        /// <param name="boundaryRadius">
        /// Rayon du disque au-dela duquel les cellules sont coupees. Prendre le rayon de la
        /// galaxie plus une marge : sans bord, les cellules peripheriques seraient infinies.
        /// </param>
        /// <param name="boundarySegments">Nombre de cotes du disque. Plus il est eleve, plus le contour exterieur est rond.</param>
        /// <returns>Une cellule par site, dans le meme ordre. Jamais <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException">Si <paramref name="sites"/> est null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="boundaryRadius"/> n'est pas strictement positif.</exception>
        public static TerritoryCell[] Compute(
            IReadOnlyList<Vector2> sites,
            float boundaryRadius,
            int boundarySegments = DefaultBoundarySegments)
        {
            if (sites == null)
            {
                throw new ArgumentNullException(nameof(sites));
            }

            if (boundaryRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(boundaryRadius), "Le rayon du bord doit etre strictement positif.");
            }

            int segments = Mathf.Max(3, boundarySegments);
            TerritoryCellVertex[] boundary = BuildBoundary(boundaryRadius, segments);

            var cells = new TerritoryCell[sites.Count];

            // Tampons reutilises d'une cellule a l'autre : le decoupage en alloue sinon deux
            // listes par site et par voisin.
            var polygon = new List<TerritoryCellVertex>(segments + 16);
            var scratch = new List<TerritoryCellVertex>(segments + 16);
            var order = new NeighbourDistance[Math.Max(0, sites.Count - 1)];

            for (int i = 0; i < sites.Count; i++)
            {
                cells[i] = ComputeCell(i, sites, boundary, polygon, scratch, order);
            }

            return cells;
        }

        /// <summary>Disque approxime par un polygone regulier, oriente dans le sens trigonometrique.</summary>
        private static TerritoryCellVertex[] BuildBoundary(float radius, int segments)
        {
            var boundary = new TerritoryCellVertex[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                boundary[i] = new TerritoryCellVertex(
                    new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius),
                    RimNeighbour);
            }

            return boundary;
        }

        private readonly struct NeighbourDistance
        {
            public readonly int Index;
            public readonly float SqrDistance;

            public NeighbourDistance(int index, float sqrDistance)
            {
                Index = index;
                SqrDistance = sqrDistance;
            }
        }

        private static TerritoryCell ComputeCell(
            int siteIndex,
            IReadOnlyList<Vector2> sites,
            TerritoryCellVertex[] boundary,
            List<TerritoryCellVertex> polygon,
            List<TerritoryCellVertex> scratch,
            NeighbourDistance[] order)
        {
            Vector2 site = sites[siteIndex];

            polygon.Clear();
            polygon.AddRange(boundary);

            // Les voisins sont traites du plus proche au plus lointain : c'est ce qui rend la
            // sortie anticipee ci-dessous efficace, les premiers voisins retrecissant vite la
            // cellule.
            int count = 0;
            for (int j = 0; j < sites.Count; j++)
            {
                if (j == siteIndex)
                {
                    continue;
                }

                order[count++] = new NeighbourDistance(j, (sites[j] - site).sqrMagnitude);
            }

            Array.Sort(order, 0, count, NeighbourDistanceComparer.Instance);

            float maxRadiusSqr = MaxVertexRadiusSqr(polygon, site);

            for (int k = 0; k < count; k++)
            {
                NeighbourDistance neighbour = order[k];

                // Sortie anticipee <b>exacte</b>, pas une heuristique : la bissectrice entre le
                // site et un voisin passe a une distance |voisin - site| / 2 du site. Si cette
                // distance depasse le rayon du sommet le plus eloigne de la cellule courante,
                // la bissectrice ne peut plus la couper — et comme les voisins suivants sont
                // encore plus lointains, aucun ne le pourra non plus.
                if (neighbour.SqrDistance > 4f * maxRadiusSqr)
                {
                    break;
                }

                Vector2 other = sites[neighbour.Index];
                Vector2 normal = other - site;
                if (normal.sqrMagnitude < Epsilon)
                {
                    // Deux systemes confondus : la bissectrice n'est pas definie. Le generateur
                    // impose une distance minimale, mais un decoupage ne doit pas dependre de
                    // cette garantie pour rester correct.
                    continue;
                }

                ClipHalfPlane(polygon, scratch, (site + other) * 0.5f, normal, neighbour.Index);

                if (polygon.Count < 3)
                {
                    break;
                }

                maxRadiusSqr = MaxVertexRadiusSqr(polygon, site);
            }

            return new TerritoryCell(site, polygon.ToArray());
        }

        private sealed class NeighbourDistanceComparer : IComparer<NeighbourDistance>
        {
            public static readonly NeighbourDistanceComparer Instance = new NeighbourDistanceComparer();

            public int Compare(NeighbourDistance a, NeighbourDistance b) => a.SqrDistance.CompareTo(b.SqrDistance);
        }

        private static float MaxVertexRadiusSqr(List<TerritoryCellVertex> polygon, Vector2 site)
        {
            float max = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                float sqr = (polygon[i].Position - site).sqrMagnitude;
                if (sqr > max)
                {
                    max = sqr;
                }
            }

            return max;
        }

        /// <summary>
        /// Rogne <paramref name="polygon"/> (modifie sur place) en ne gardant que les points p
        /// verifiant <c>dot(p - mid, normal) &lt;= 0</c>, c'est-a-dire le cote du site.
        /// <para>
        /// Les sommets creees sur la ligne de coupe recoivent <paramref name="tag"/> comme
        /// voisin ; ceux issus d'une arete preexistante conservent le leur. C'est ce qui permet
        /// au rendu de savoir plus tard qui borde chaque arete.
        /// </para>
        /// </summary>
        private static void ClipHalfPlane(
            List<TerritoryCellVertex> polygon,
            List<TerritoryCellVertex> scratch,
            Vector2 mid,
            Vector2 normal,
            int tag)
        {
            scratch.Clear();

            int count = polygon.Count;
            for (int i = 0; i < count; i++)
            {
                TerritoryCellVertex current = polygon[i];
                TerritoryCellVertex next = polygon[(i + 1) % count];

                float distanceCurrent = Vector2.Dot(current.Position - mid, normal);
                float distanceNext = Vector2.Dot(next.Position - mid, normal);

                bool currentInside = distanceCurrent <= Epsilon;
                bool nextInside = distanceNext <= Epsilon;

                if (currentInside)
                {
                    scratch.Add(current);

                    if (!nextInside)
                    {
                        // On sort : l'arete qui part du point d'intersection longe la coupe,
                        // donc elle appartient au voisin.
                        scratch.Add(new TerritoryCellVertex(
                            Intersect(current.Position, next.Position, distanceCurrent, distanceNext),
                            tag));
                    }
                }
                else if (nextInside)
                {
                    // On rentre : l'arete qui part du point d'intersection est le reste de
                    // l'arete d'origine, elle garde donc le voisin de celle-ci.
                    scratch.Add(new TerritoryCellVertex(
                        Intersect(current.Position, next.Position, distanceCurrent, distanceNext),
                        current.NeighbourIndex));
                }
            }

            polygon.Clear();
            polygon.AddRange(scratch);
        }

        private static Vector2 Intersect(Vector2 from, Vector2 to, float distanceFrom, float distanceTo)
        {
            float denominator = distanceFrom - distanceTo;
            if (Mathf.Abs(denominator) < float.Epsilon)
            {
                return from;
            }

            return Vector2.LerpUnclamped(from, to, distanceFrom / denominator);
        }
    }
}
