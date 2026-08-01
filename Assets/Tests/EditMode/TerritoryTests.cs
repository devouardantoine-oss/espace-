using System.Collections.Generic;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Couverture du decoupage en cellules de controle, de la construction de leurs maillages
    /// et de l'echelle de lecture (Phase 19).
    /// <para>
    /// Ces trois briques sont volontairement pures : elles ne touchent ni a la scene, ni au
    /// rendu, ni au temps. C'est ce qui permet de verifier ici la propriete qui fonde tout le
    /// systeme — <i>chaque point d'une cellule est plus proche de son systeme que de tout
    /// autre</i> — sans lancer le jeu.
    /// </para>
    /// </summary>
    [TestFixture]
    public class TerritoryTests
    {
        private const float BoundaryRadius = 50f;

        /// <summary>Jeu de sites reproductible, reparti comme une vraie galaxie (disque, distance minimale).</summary>
        private static Vector2[] SampleSites(int count = 40, int seed = 20260801)
        {
            var random = new System.Random(seed);
            var sites = new List<Vector2>(count);

            int guard = 0;
            while (sites.Count < count && guard++ < 10000)
            {
                double angle = random.NextDouble() * System.Math.PI * 2d;
                double radius = System.Math.Sqrt(random.NextDouble()) * 40d;
                var candidate = new Vector2(
                    (float)(System.Math.Cos(angle) * radius),
                    (float)(System.Math.Sin(angle) * radius));

                bool tooClose = false;
                foreach (Vector2 existing in sites)
                {
                    if ((existing - candidate).sqrMagnitude < 9f)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    sites.Add(candidate);
                }
            }

            return sites.ToArray();
        }

        private static Dictionary<int, Color> Palette() => new Dictionary<int, Color>
        {
            { 0, new Color(0.25f, 0.55f, 0.95f) },
            { 1, new Color(0.85f, 0.20f, 0.20f) },
        };

        // ------------------------------------------------------------ decoupage

        [Test]
        public void Compute_SingleSite_TakesTheWholeBoundary()
        {
            TerritoryCell[] cells = TerritoryPartition.Compute(new[] { Vector2.zero }, BoundaryRadius);

            Assert.AreEqual(1, cells.Length);
            Assert.GreaterOrEqual(cells[0].Vertices.Count, 3);

            foreach (TerritoryCellVertex vertex in cells[0].Vertices)
            {
                Assert.AreEqual(TerritoryPartition.RimNeighbour, vertex.NeighbourIndex,
                    "Sans voisin, toutes les aretes bordent le vide.");
            }
        }

        [Test]
        public void Compute_TwoSites_EachCellBordersTheOther()
        {
            var sites = new[] { new Vector2(-10f, 0f), new Vector2(10f, 0f) };

            TerritoryCell[] cells = TerritoryPartition.Compute(sites, BoundaryRadius);

            Assert.AreEqual(1, CountEdgesTowards(cells[0], 1), "La cellule 0 doit border la cellule 1 par exactement une arete.");
            Assert.AreEqual(1, CountEdgesTowards(cells[1], 0), "La cellule 1 doit border la cellule 0 par exactement une arete.");
        }

        [Test]
        public void Compute_EverySiteLiesInsideItsOwnCell()
        {
            Vector2[] sites = SampleSites();
            TerritoryCell[] cells = TerritoryPartition.Compute(sites, BoundaryRadius);

            for (int i = 0; i < cells.Length; i++)
            {
                Assert.IsTrue(IsInsideConvex(cells[i], sites[i]),
                    $"Le systeme {i} devrait se trouver a l'interieur de sa propre cellule.");
            }
        }

        [Test]
        public void Compute_NoInteriorPointIsCloserToAnotherSite()
        {
            Vector2[] sites = SampleSites();
            TerritoryCell[] cells = TerritoryPartition.Compute(sites, BoundaryRadius);

            for (int i = 0; i < cells.Length; i++)
            {
                // Points echantillonnes entre le centre de gravite et chaque sommet, en restant
                // a l'interieur : c'est la propriete de Voronoi elle-meme qui est verifiee.
                foreach (TerritoryCellVertex vertex in cells[i].Vertices)
                {
                    Vector2 sample = Vector2.Lerp(cells[i].Centroid, vertex.Position, 0.8f);

                    float ownDistance = (sample - sites[i]).sqrMagnitude;
                    for (int j = 0; j < sites.Length; j++)
                    {
                        if (j == i)
                        {
                            continue;
                        }

                        Assert.LessOrEqual(ownDistance, (sample - sites[j]).sqrMagnitude + 1e-3f,
                            $"Un point de la cellule {i} est plus proche du systeme {j}.");
                    }
                }
            }
        }

        [Test]
        public void Compute_SharedEdges_AreTaggedWithTheCorrectNeighbour()
        {
            Vector2[] sites = SampleSites(25);
            TerritoryCell[] cells = TerritoryPartition.Compute(sites, BoundaryRadius);

            // Une arete taguee « voisin j » doit etre equidistante des sites i et j : c'est la
            // definition meme d'une bissectrice. Un tag errone ferait dessiner une frontiere au
            // mauvais endroit, ou de la mauvaise couleur.
            for (int i = 0; i < cells.Length; i++)
            {
                IReadOnlyList<TerritoryCellVertex> vertices = cells[i].Vertices;
                for (int v = 0; v < vertices.Count; v++)
                {
                    int neighbour = vertices[v].NeighbourIndex;
                    if (neighbour == TerritoryPartition.RimNeighbour)
                    {
                        continue;
                    }

                    Vector2 middle = Vector2.Lerp(vertices[v].Position, vertices[(v + 1) % vertices.Count].Position, 0.5f);
                    float toOwn = (middle - sites[i]).magnitude;
                    float toNeighbour = (middle - sites[neighbour]).magnitude;

                    Assert.AreEqual(toOwn, toNeighbour, 1e-2f,
                        $"L'arete {v} de la cellule {i} est taguee {neighbour} sans en etre equidistante.");
                }
            }
        }

        [Test]
        public void Compute_CellsHaveAPositiveArea()
        {
            TerritoryCell[] cells = TerritoryPartition.Compute(SampleSites(), BoundaryRadius);

            foreach (TerritoryCell cell in cells)
            {
                Assert.Greater(cell.Area, 0f, "Une cellule degeneree produirait des triangles invisibles ou inverses.");
            }
        }

        [Test]
        public void Compute_IsDeterministic()
        {
            Vector2[] sites = SampleSites(20);

            TerritoryCell[] first = TerritoryPartition.Compute(sites, BoundaryRadius);
            TerritoryCell[] second = TerritoryPartition.Compute(sites, BoundaryRadius);

            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].Vertices.Count, second[i].Vertices.Count);
                for (int v = 0; v < first[i].Vertices.Count; v++)
                {
                    Assert.AreEqual(first[i].Vertices[v].Position, second[i].Vertices[v].Position);
                    Assert.AreEqual(first[i].Vertices[v].NeighbourIndex, second[i].Vertices[v].NeighbourIndex);
                }
            }
        }

        [Test]
        public void Compute_InvalidArguments_Throw()
        {
            Assert.Throws<System.ArgumentNullException>(() => TerritoryPartition.Compute(null, BoundaryRadius));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => TerritoryPartition.Compute(new[] { Vector2.zero }, 0f));
        }

        // ------------------------------------------------------------- maillage

        [Test]
        public void BuildFill_UnownedCells_ProduceNothing()
        {
            TerritoryCell[] cells = TerritoryPartition.Compute(SampleSites(10), BoundaryRadius);
            int[] owners = Filled(cells.Length, StarSystemState.UnownedOwnerId);
            var data = new TerritoryMeshData();

            TerritoryMeshBuilder.BuildFill(cells, owners, Palette(), 0.5f, data);

            Assert.AreEqual(0, data.Vertices.Count);
            Assert.AreEqual(0, data.Triangles.Count);
        }

        [Test]
        public void BuildFill_OwnedCell_IsAFanAroundItsSystem()
        {
            TerritoryCell[] cells = TerritoryPartition.Compute(new[] { Vector2.zero }, BoundaryRadius);
            var data = new TerritoryMeshData();

            TerritoryMeshBuilder.BuildFill(cells, new[] { 0 }, Palette(), 0.5f, data);

            int edges = cells[0].Vertices.Count;
            Assert.AreEqual(edges + 1, data.Vertices.Count, "Un eventail = un sommet central + un sommet par coin.");
            Assert.AreEqual(edges * 3, data.Triangles.Count, "Un eventail = un triangle par arete.");
            Assert.AreEqual(new Vector3(0f, 0f, 0.5f), data.Vertices[0], "Le sommet central est le systeme lui-meme.");
        }

        [Test]
        public void BuildFill_FadesFromTheSystemTowardsTheBorder()
        {
            TerritoryCell[] cells = TerritoryPartition.Compute(new[] { Vector2.zero }, BoundaryRadius);
            var data = new TerritoryMeshData();

            TerritoryMeshBuilder.BuildFill(cells, new[] { 0 }, Palette(), 0.5f, data);

            // C'est tout le concept « frontieres stellaires » : la couleur ne s'affirme qu'au
            // bord du territoire, jamais en son cœur.
            Assert.Less(data.Colors[0].a, data.Colors[1].a);
        }

        [Test]
        public void BuildBorders_EdgeBetweenTwoSystemsOfTheSameEmpire_IsNotDrawn()
        {
            var sites = new[] { new Vector2(-6f, 0f), new Vector2(6f, 0f) };
            TerritoryCell[] cells = TerritoryPartition.Compute(sites, BoundaryRadius);
            var data = new TerritoryMeshData();

            TerritoryMeshBuilder.BuildBorders(cells, new[] { 0, 0 }, Palette(), 0.2f, 0.45f, data);
            int sameEmpire = data.Vertices.Count;

            TerritoryMeshBuilder.BuildBorders(cells, new[] { 0, 1 }, Palette(), 0.2f, 0.45f, data);
            int rivalEmpires = data.Vertices.Count;

            Assert.Less(sameEmpire, rivalEmpires,
                "Deux systemes du meme empire doivent former un bloc continu, sans trait de separation.");
        }

        [Test]
        public void BuildBorders_ContestedFrontier_IsStrongerThanAFacadeOnTheVoid()
        {
            var sites = new[] { new Vector2(-6f, 0f), new Vector2(6f, 0f) };
            TerritoryCell[] cells = TerritoryPartition.Compute(sites, BoundaryRadius);

            var lonely = new TerritoryMeshData();
            TerritoryMeshBuilder.BuildBorders(cells, new[] { 0, StarSystemState.UnownedOwnerId }, Palette(), 0.2f, 0.45f, lonely);

            var contested = new TerritoryMeshData();
            TerritoryMeshBuilder.BuildBorders(cells, new[] { 0, 1 }, Palette(), 0.2f, 0.45f, contested);

            Assert.Greater(MaxAlpha(contested), MaxAlpha(lonely),
                "Une frontiere contestee doit briller plus qu'une facade donnant sur le vide.");
        }

        [Test]
        public void BuildBorders_StripsAreOffsetTowardsTheInsideOfTheirCell()
        {
            TerritoryCell[] cells = TerritoryPartition.Compute(SampleSites(15), BoundaryRadius);
            int[] owners = Filled(cells.Length, 0);
            owners[0] = 1;

            var data = new TerritoryMeshData();
            TerritoryMeshBuilder.BuildBorders(cells, owners, Palette(), 0.3f, 0.45f, data);

            // Chaque ruban est decale vers l'interieur : ses sommets ne doivent jamais sortir du
            // disque galactique, sans quoi une frontiere deborderait de la carte.
            float limit = BoundaryRadius + 1f;
            foreach (Vector3 vertex in data.Vertices)
            {
                Assert.LessOrEqual(new Vector2(vertex.x, vertex.y).magnitude, limit);
            }
        }

        [Test]
        public void BuildBorders_MismatchedOwnerCount_Throws()
        {
            TerritoryCell[] cells = TerritoryPartition.Compute(SampleSites(5), BoundaryRadius);

            Assert.Throws<System.ArgumentException>(
                () => TerritoryMeshBuilder.BuildBorders(cells, new[] { 0 }, Palette(), 0.2f, 0.45f, new TerritoryMeshData()));
        }

        [Test]
        public void ApplyTo_TransfersThenClearsTheMesh()
        {
            TerritoryCell[] cells = TerritoryPartition.Compute(new[] { Vector2.zero }, BoundaryRadius);
            var data = new TerritoryMeshData();
            var mesh = new Mesh();

            try
            {
                TerritoryMeshBuilder.BuildFill(cells, new[] { 0 }, Palette(), 0.5f, data);
                data.ApplyTo(mesh);
                Assert.AreEqual(data.Vertices.Count, mesh.vertexCount);

                data.Clear();
                data.ApplyTo(mesh);
                Assert.AreEqual(0, mesh.vertexCount, "Un territoire entierement perdu doit vider le maillage.");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        // ------------------------------------------------------ echelle de lecture

        [Test]
        public void TierFor_FollowsTheReadingScale()
        {
            Assert.AreEqual(TerritoryDetailTier.Galactic, TerritoryLevelOfDetail.TierFor(0f));
            Assert.AreEqual(TerritoryDetailTier.Galactic, TerritoryLevelOfDetail.TierFor(0.29f));
            Assert.AreEqual(TerritoryDetailTier.Sector, TerritoryLevelOfDetail.TierFor(0.30f));
            Assert.AreEqual(TerritoryDetailTier.System, TerritoryLevelOfDetail.TierFor(0.50f));
            Assert.AreEqual(TerritoryDetailTier.Orbital, TerritoryLevelOfDetail.TierFor(0.75f));
            Assert.AreEqual(TerritoryDetailTier.Orbital, TerritoryLevelOfDetail.TierFor(1f));
        }

        [Test]
        public void FillAlphaFor_FadesAsTheCameraClosesIn()
        {
            float previous = TerritoryLevelOfDetail.FillAlphaFor(0f);

            for (float zoom = 0.1f; zoom <= 1f; zoom += 0.1f)
            {
                float current = TerritoryLevelOfDetail.FillAlphaFor(zoom);
                Assert.Less(current, previous, "Les territoires doivent s'effacer a mesure qu'on s'approche.");
                previous = current;
            }
        }

        [Test]
        public void BorderAlphaFor_StaysStrongerThanTheFill()
        {
            for (float zoom = 0f; zoom <= 1f; zoom += 0.25f)
            {
                Assert.Greater(
                    TerritoryLevelOfDetail.BorderAlphaFor(zoom),
                    TerritoryLevelOfDetail.FillAlphaFor(zoom),
                    "Une frontiere reste lisible meme quand la zone qu'elle borde s'efface.");
            }
        }

        [Test]
        public void FactionLabelAlphaFor_HandsOverToTheSystemNames()
        {
            Assert.AreEqual(1f, TerritoryLevelOfDetail.FactionLabelAlphaFor(0f), 1e-4f);
            Assert.AreEqual(0f, TerritoryLevelOfDetail.FactionLabelAlphaFor(0.5f), 1e-4f);
            Assert.AreEqual(0f, TerritoryLevelOfDetail.FactionLabelAlphaFor(1f), 1e-4f);
        }

        [Test]
        public void BorderWidthFor_ShrinksWithEachTier()
        {
            Assert.Greater(
                TerritoryLevelOfDetail.BorderWidthFor(TerritoryDetailTier.Galactic),
                TerritoryLevelOfDetail.BorderWidthFor(TerritoryDetailTier.Sector));
            Assert.Greater(
                TerritoryLevelOfDetail.BorderWidthFor(TerritoryDetailTier.Sector),
                TerritoryLevelOfDetail.BorderWidthFor(TerritoryDetailTier.System));
            Assert.Greater(
                TerritoryLevelOfDetail.BorderWidthFor(TerritoryDetailTier.System),
                TerritoryLevelOfDetail.BorderWidthFor(TerritoryDetailTier.Orbital));
        }

        [Test]
        public void ZoomInFraction_IsZeroWhenFarAndOneWhenClose()
        {
            Assert.AreEqual(0f, TerritoryLevelOfDetail.ZoomInFraction(53f, 4f, 53f), 1e-4f);
            Assert.AreEqual(1f, TerritoryLevelOfDetail.ZoomInFraction(4f, 4f, 53f), 1e-4f);
            Assert.AreEqual(0.5f, TerritoryLevelOfDetail.ZoomInFraction(28.5f, 4f, 53f), 1e-3f);
        }

        // ------------------------------------------------------------- outillage

        private static int[] Filled(int count, int value)
        {
            var owners = new int[count];
            for (int i = 0; i < count; i++)
            {
                owners[i] = value;
            }

            return owners;
        }

        private static int CountEdgesTowards(TerritoryCell cell, int neighbourIndex)
        {
            int count = 0;
            foreach (TerritoryCellVertex vertex in cell.Vertices)
            {
                if (vertex.NeighbourIndex == neighbourIndex)
                {
                    count++;
                }
            }

            return count;
        }

        private static float MaxAlpha(TerritoryMeshData data)
        {
            float max = 0f;
            foreach (Color color in data.Colors)
            {
                if (color.a > max)
                {
                    max = color.a;
                }
            }

            return max;
        }

        /// <summary>Appartenance a un polygone convexe : le point est du meme cote de toutes les aretes.</summary>
        private static bool IsInsideConvex(TerritoryCell cell, Vector2 point)
        {
            IReadOnlyList<TerritoryCellVertex> vertices = cell.Vertices;
            int sign = 0;

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 a = vertices[i].Position;
                Vector2 b = vertices[(i + 1) % vertices.Count].Position;

                float cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
                if (Mathf.Abs(cross) < 1e-4f)
                {
                    continue;
                }

                int currentSign = cross > 0f ? 1 : -1;
                if (sign == 0)
                {
                    sign = currentSign;
                }
                else if (sign != currentSign)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
