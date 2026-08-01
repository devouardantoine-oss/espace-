using System;
using System.Collections.Generic;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Tampons de construction d'un maillage de territoire, reutilises d'une reconstruction a
    /// l'autre (Phase 19).
    /// <para>
    /// <b>Pourquoi un objet plutot que des listes locales ?</b> Une reconstruction touche
    /// plusieurs milliers de sommets. Les allouer a chaque changement de proprietaire
    /// produirait un pic de ramasse-miettes exactement au moment ou le jeu joue deja une
    /// bataille ou une colonisation — c'est-a-dire au pire moment possible. Les listes sont
    /// donc creees une fois et videes, jamais remplacees.
    /// </para>
    /// </summary>
    public sealed class TerritoryMeshData
    {
        /// <summary>Sommets, en unites monde.</summary>
        public readonly List<Vector3> Vertices = new List<Vector3>();

        /// <summary>
        /// Couleur par sommet. Le canal alpha porte l'intensite <b>relative</b> : l'opacite
        /// globale est appliquee par la teinte du materiau, ce qui permet de la faire varier
        /// avec le zoom sans reconstruire quoi que ce soit.
        /// </summary>
        public readonly List<Color> Colors = new List<Color>();

        /// <summary>Indices des triangles.</summary>
        public readonly List<int> Triangles = new List<int>();

        /// <summary>Vide les trois listes sans liberer leur capacite.</summary>
        public void Clear()
        {
            Vertices.Clear();
            Colors.Clear();
            Triangles.Clear();
        }

        /// <summary>
        /// Transfere le contenu dans <paramref name="mesh"/>, qui est vide au prealable. Le
        /// maillage est reutilise plutot que recree : un <see cref="Mesh"/> est une ressource
        /// native, en abandonner un par reconstruction fuirait de la memoire graphique.
        /// </summary>
        public void ApplyTo(Mesh mesh)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            mesh.Clear();

            if (Vertices.Count == 0)
            {
                return;
            }

            // Au-dela de 65 535 sommets il faudrait un format d'index 32 bits. Une galaxie de
            // 200 systemes en produit environ 12 000 : la marge est large, mais le format est
            // choisi explicitement plutot que laisse au hasard d'une configuration future.
            mesh.indexFormat = Vertices.Count > ushort.MaxValue
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(Vertices);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
        }
    }

    /// <summary>
    /// Transforme un decoupage en cellules (<see cref="TerritoryPartition"/>) et une carte des
    /// proprietaires en deux maillages combines : le remplissage des territoires et leurs
    /// frontieres (Phase 19).
    /// <para>
    /// <b>Deux maillages, deux <i>draw calls</i>, quel que soit le nombre de systemes</b> —
    /// meme raisonnement que <see cref="GalaxyLinkRenderer"/> pour les routes. Les cent halos
    /// de la Phase 12 coutaient cent <see cref="SpriteRenderer"/> mis a jour a chaque frame ;
    /// ici rien n'est reconstruit tant qu'aucun systeme ne change de maitre.
    /// </para>
    /// <para>
    /// <b>Le concept « frontieres stellaires » tient entierement dans les couleurs de
    /// sommets</b>, sans shader dedie : le remplissage s'estompe vers le centre de chaque
    /// cellule et s'affirme au bord, et seules les aretes bordant un <i>autre</i> empire
    /// recoivent un halo et un trait vif. Une frontiere contestee brille donc, une facade sur
    /// le vide s'evanouit — la carte designe d'elle-meme ou se joue la tension, sans que le
    /// joueur ait a ouvrir un ecran.
    /// </para>
    /// <para>
    /// <b>Chaque cellule dessine sa propre moitie de frontiere</b>, decalee vers l'interieur.
    /// Une frontiere entre deux empires porte ainsi les deux couleurs, une de chaque cote :
    /// chacun lit la sienne. C'est aussi ce qui evite d'avoir a dedupliquer les aretes
    /// partagees, et donc a comparer des positions en virgule flottante.
    /// </para>
    /// </summary>
    public static class TerritoryMeshBuilder
    {
        /// <summary>Intensite relative du remplissage au centre d'une cellule (le bord vaut 1).</summary>
        private const float FillCenterIntensity = 0.45f;

        /// <summary>Intensite relative du trait d'une frontiere contestee.</summary>
        private const float ContestedLineIntensity = 1f;

        /// <summary>Intensite relative du halo, au contact de la frontiere (elle tombe a zero vers l'interieur).</summary>
        private const float ContestedGlowIntensity = 0.30f;

        /// <summary>Intensite relative du trait d'une facade donnant sur le vide ou sur un systeme libre.</summary>
        private const float RimLineIntensity = 0.28f;

        /// <summary>Melange vers le blanc applique au trait d'une frontiere contestee, pour qu'elle se detache du remplissage.</summary>
        private const float ContestedLineBrightening = 0.35f;

        /// <summary>
        /// Construit le remplissage : un eventail de triangles par cellule possedee, du systeme
        /// vers ses sommets.
        /// <para>
        /// L'eventail est toujours valide sans triangulation generale : une cellule de controle
        /// est convexe par construction et contient toujours son propre systeme.
        /// </para>
        /// </summary>
        /// <param name="cells">Cellules, dans le meme ordre que <paramref name="ownerIds"/>.</param>
        /// <param name="ownerIds">Proprietaire de chaque cellule, ou <see cref="StarSystemState.UnownedOwnerId"/>.</param>
        /// <param name="ownerColors">Couleur par identifiant d'empire. Un identifiant absent fait ignorer la cellule.</param>
        /// <param name="depth">Profondeur Z du maillage.</param>
        /// <param name="target">Tampons a remplir. Vides au prealable.</param>
        public static void BuildFill(
            IReadOnlyList<TerritoryCell> cells,
            IReadOnlyList<int> ownerIds,
            IReadOnlyDictionary<int, Color> ownerColors,
            float depth,
            TerritoryMeshData target)
        {
            ValidateArguments(cells, ownerIds, ownerColors, target);
            target.Clear();

            for (int i = 0; i < cells.Count; i++)
            {
                if (!TryGetOwnerColor(cells[i], ownerIds[i], ownerColors, out Color color))
                {
                    continue;
                }

                IReadOnlyList<TerritoryCellVertex> vertices = cells[i].Vertices;

                int centerIndex = target.Vertices.Count;
                target.Vertices.Add(new Vector3(cells[i].Site.x, cells[i].Site.y, depth));
                target.Colors.Add(WithIntensity(color, FillCenterIntensity));

                for (int v = 0; v < vertices.Count; v++)
                {
                    Vector2 position = vertices[v].Position;
                    target.Vertices.Add(new Vector3(position.x, position.y, depth));
                    target.Colors.Add(WithIntensity(color, 1f));
                }

                for (int v = 0; v < vertices.Count; v++)
                {
                    target.Triangles.Add(centerIndex);
                    target.Triangles.Add(centerIndex + 1 + v);
                    target.Triangles.Add(centerIndex + 1 + (v + 1) % vertices.Count);
                }
            }
        }

        /// <summary>
        /// Construit les frontieres : pour chaque cellule possedee, les aretes dont le voisin
        /// n'a pas le meme maitre. Une arete bordant un autre empire recoit un halo puis un
        /// trait vif ; une arete donnant sur le vide ou sur un systeme libre ne recoit qu'un
        /// trait discret.
        /// </summary>
        /// <param name="cells">Cellules, dans le meme ordre que <paramref name="ownerIds"/>.</param>
        /// <param name="ownerIds">Proprietaire de chaque cellule, ou <see cref="StarSystemState.UnownedOwnerId"/>.</param>
        /// <param name="ownerColors">Couleur par identifiant d'empire. Un identifiant absent fait ignorer la cellule.</param>
        /// <param name="lineWidth">Largeur du trait, en unites monde (voir <see cref="TerritoryLevelOfDetail.BorderWidthFor"/>).</param>
        /// <param name="depth">Profondeur Z du maillage.</param>
        /// <param name="target">Tampons a remplir. Vides au prealable.</param>
        public static void BuildBorders(
            IReadOnlyList<TerritoryCell> cells,
            IReadOnlyList<int> ownerIds,
            IReadOnlyDictionary<int, Color> ownerColors,
            float lineWidth,
            float depth,
            TerritoryMeshData target)
        {
            ValidateArguments(cells, ownerIds, ownerColors, target);

            if (lineWidth <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(lineWidth), "La largeur de trait doit etre strictement positive.");
            }

            target.Clear();

            float glowWidth = lineWidth * TerritoryLevelOfDetail.ContestedGlowWidthFactor;
            Color white = Color.white;

            for (int i = 0; i < cells.Count; i++)
            {
                int owner = ownerIds[i];
                if (!TryGetOwnerColor(cells[i], owner, ownerColors, out Color color))
                {
                    continue;
                }

                Color lineColor = Color.Lerp(color, white, ContestedLineBrightening);
                IReadOnlyList<TerritoryCellVertex> vertices = cells[i].Vertices;

                for (int v = 0; v < vertices.Count; v++)
                {
                    TerritoryCellVertex vertex = vertices[v];
                    Vector2 from = vertex.Position;
                    Vector2 to = vertices[(v + 1) % vertices.Count].Position;

                    int neighbourOwner = NeighbourOwner(vertex.NeighbourIndex, ownerIds);
                    if (neighbourOwner == owner)
                    {
                        // Meme maitre de part et d'autre : arete interieure au territoire, elle
                        // ne doit rien dessiner du tout — c'est ce qui fait la difference entre
                        // un bloc continu et une mosaique de cellules.
                        continue;
                    }

                    Vector2 inward = InwardNormal(from, to, cells[i].Site);
                    bool contested = neighbourOwner != StarSystemState.UnownedOwnerId;

                    if (contested)
                    {
                        AppendStrip(target, from, to, inward, glowWidth, depth,
                            WithIntensity(color, ContestedGlowIntensity), WithIntensity(color, 0f));

                        AppendStrip(target, from, to, inward, lineWidth, depth,
                            WithIntensity(lineColor, ContestedLineIntensity), WithIntensity(lineColor, ContestedLineIntensity));
                    }
                    else
                    {
                        AppendStrip(target, from, to, inward, lineWidth, depth,
                            WithIntensity(color, RimLineIntensity), WithIntensity(color, RimLineIntensity));
                    }
                }
            }
        }

        /// <summary>
        /// Ajoute un ruban le long de l'arete <paramref name="from"/>-<paramref name="to"/>,
        /// entierement decale vers l'interieur de la cellule.
        /// <para>
        /// Le decalage est unilateral (et non centre sur l'arete) pour que la moitie dessinee
        /// par la cellule d'en face ne recouvre pas celle-ci : les deux couleurs restent
        /// visibles cote a cote.
        /// </para>
        /// </summary>
        private static void AppendStrip(
            TerritoryMeshData target,
            Vector2 from,
            Vector2 to,
            Vector2 inward,
            float width,
            float depth,
            Color edgeColor,
            Color innerColor)
        {
            Vector2 offset = inward * width;

            int baseIndex = target.Vertices.Count;

            target.Vertices.Add(new Vector3(from.x, from.y, depth));
            target.Colors.Add(edgeColor);
            target.Vertices.Add(new Vector3(to.x, to.y, depth));
            target.Colors.Add(edgeColor);
            target.Vertices.Add(new Vector3(to.x + offset.x, to.y + offset.y, depth));
            target.Colors.Add(innerColor);
            target.Vertices.Add(new Vector3(from.x + offset.x, from.y + offset.y, depth));
            target.Colors.Add(innerColor);

            target.Triangles.Add(baseIndex);
            target.Triangles.Add(baseIndex + 1);
            target.Triangles.Add(baseIndex + 2);
            target.Triangles.Add(baseIndex);
            target.Triangles.Add(baseIndex + 2);
            target.Triangles.Add(baseIndex + 3);
        }

        /// <summary>
        /// Normale unitaire a l'arete, orientee vers l'interieur de la cellule.
        /// <para>
        /// L'orientation est deduite de la position du systeme plutot que du sens de parcours
        /// du polygone : le resultat reste juste meme si un decoupage futur inversait ce sens.
        /// </para>
        /// </summary>
        private static Vector2 InwardNormal(Vector2 from, Vector2 to, Vector2 site)
        {
            Vector2 edge = to - from;
            var normal = new Vector2(-edge.y, edge.x);

            float lengthSqr = normal.sqrMagnitude;
            if (lengthSqr < float.Epsilon)
            {
                return Vector2.zero;
            }

            normal /= Mathf.Sqrt(lengthSqr);
            return Vector2.Dot(site - from, normal) < 0f ? -normal : normal;
        }

        private static int NeighbourOwner(int neighbourIndex, IReadOnlyList<int> ownerIds)
        {
            return neighbourIndex == TerritoryPartition.RimNeighbour || neighbourIndex >= ownerIds.Count
                ? StarSystemState.UnownedOwnerId
                : ownerIds[neighbourIndex];
        }

        private static bool TryGetOwnerColor(
            TerritoryCell cell,
            int ownerId,
            IReadOnlyDictionary<int, Color> ownerColors,
            out Color color)
        {
            color = default;

            if (ownerId == StarSystemState.UnownedOwnerId || cell == null || cell.Vertices.Count < 3)
            {
                return false;
            }

            return ownerColors.TryGetValue(ownerId, out color);
        }

        private static Color WithIntensity(Color color, float intensity) =>
            new Color(color.r, color.g, color.b, intensity);

        private static void ValidateArguments(
            IReadOnlyList<TerritoryCell> cells,
            IReadOnlyList<int> ownerIds,
            IReadOnlyDictionary<int, Color> ownerColors,
            TerritoryMeshData target)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (ownerIds == null) throw new ArgumentNullException(nameof(ownerIds));
            if (ownerColors == null) throw new ArgumentNullException(nameof(ownerColors));
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (ownerIds.Count != cells.Count)
            {
                throw new ArgumentException(
                    $"Il faut exactement un proprietaire par cellule ({cells.Count} cellules, {ownerIds.Count} proprietaires).",
                    nameof(ownerIds));
            }
        }
    }
}
