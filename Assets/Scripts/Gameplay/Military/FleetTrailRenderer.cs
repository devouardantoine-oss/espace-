using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Trace l'itineraire restant des flottes du joueur en vol, en un seul maillage combine
    /// (Phase 20).
    /// <para>
    /// <b>Seules les flottes du joueur laissent une trajectoire.</b> Les vaisseaux ennemis sont
    /// visibles — une galaxie ou rien ne bouge est morte — mais leur <i>destination</i> est un
    /// renseignement, pas une donnee offerte : le projet a un systeme d'espionnage precisement
    /// pour que l'information se merite, et la Phase 18 refuse deja a l'IA toute omniscience
    /// que le joueur n'a pas. La reciproque doit tenir.
    /// </para>
    /// <para>
    /// <b>Reconstruit a chaque frame, sans detection de changement :</b> contrairement aux
    /// territoires (une centaine de cellules, plusieurs milliers de sommets), il y a au plus une
    /// poignee de flottes en vol et quelques dizaines de quads. Ajouter une signature a comparer
    /// couterait plus de code que le calcul lui-meme, et l'avancement du vaisseau change de
    /// toute facon en continu.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class FleetTrailRenderer : MonoBehaviour
    {
        /// <summary>Profondeur Z : derriere les vaisseaux et les systemes, devant les routes hyperspatiales.</summary>
        private const float TrailDepth = 0.05f;

        /// <summary>Largeur du trace, en fraction de la demi-hauteur visible : epaisseur constante a l'ecran.</summary>
        private const float WidthFactor = 0.006f;

        /// <summary>Opacite du tronçon deja parcouru de l'etape en cours.</summary>
        private const float TraveledAlpha = 0.18f;

        /// <summary>Opacite du chemin restant.</summary>
        private const float RemainingAlpha = 0.55f;

        /// <summary>Opacite du chemin restant de la flotte selectionnee.</summary>
        private const float SelectedAlpha = 0.95f;

        private static readonly string[] ShaderCandidates =
        {
            "Sprites/Default",
            "Universal Render Pipeline/2D/Sprite-Unlit-Default",
        };

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _triangles = new List<int>();

        private GalaxyMap _map;
        private Camera _camera;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _material;

        public void Initialize(GalaxyMap map)
        {
            _map = map;
            _camera = Camera.main;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            _mesh = new Mesh { name = "FleetTrails" };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;

            _material = CreateMaterial();
            _meshRenderer.sharedMaterial = _material;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private void OnDestroy()
        {
            DestroyGenerated(_mesh);
            DestroyGenerated(_material);
        }

        /// <summary>Reconstruit le trace pour <paramref name="fleets"/>.</summary>
        /// <param name="currentDate">
        /// Date du jour, fournie par l'appelant plutot que resolue ici : <c>FleetMapController</c>
        /// detient deja l'horloge, et la relire une seconde fois par frame n'apporterait rien.
        /// </param>
        public void Sync(IReadOnlyList<Fleet> fleets, int selectedFleetId, Func<int, Color> ownerColor, GameDate currentDate)
        {
            if (_map == null || _mesh == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();

            float width = (_camera != null ? _camera.orthographicSize : 30f) * WidthFactor;

            foreach (Fleet fleet in fleets)
            {
                if (fleet.OwnerId != EconomyService.PlayerOwnerId || fleet.Route == null)
                {
                    continue;
                }

                Color color = ownerColor(fleet.OwnerId);
                float remainingAlpha = fleet.Id == selectedFleetId ? SelectedAlpha : RemainingAlpha;

                AppendJourney(fleet, color, remainingAlpha, width, currentDate);
            }

            _mesh.Clear();

            if (_vertices.Count > 0)
            {
                _mesh.SetVertices(_vertices);
                _mesh.SetColors(_colors);
                _mesh.SetTriangles(_triangles, 0);
                _mesh.RecalculateBounds();
            }
        }

        /// <summary>
        /// Ajoute l'etape en cours (coupee en deux a la position du vaisseau, pour que le
        /// parcouru s'efface derriere lui) puis toutes les etapes suivantes.
        /// </summary>
        private void AppendJourney(Fleet fleet, Color color, float remainingAlpha, float width, GameDate currentDate)
        {
            IReadOnlyList<StarSystemId> route = fleet.Route;

            for (int leg = fleet.RouteIndex; leg + 1 < route.Count; leg++)
            {
                if (!_map.TryGetSystem(route[leg], out StarSystemState from)
                    || !_map.TryGetSystem(route[leg + 1], out StarSystemState to))
                {
                    continue;
                }

                if (leg != fleet.RouteIndex)
                {
                    AppendSegment(from.Position, to.Position, width, WithAlpha(color, remainingAlpha));
                    continue;
                }

                float progress = fleet.DepartureDate.HasValue && fleet.ArrivalDate.HasValue
                    ? FleetPresentation.ComputeLegProgress(fleet.DepartureDate.Value, fleet.ArrivalDate.Value, currentDate)
                    : 0f;

                Vector2 ship = Vector2.Lerp(from.Position, to.Position, progress);
                AppendSegment(from.Position, ship, width, WithAlpha(color, TraveledAlpha));
                AppendSegment(ship, to.Position, width, WithAlpha(color, remainingAlpha));
            }
        }

        private void AppendSegment(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);

            int baseIndex = _vertices.Count;
            _vertices.Add(new Vector3(from.x - normal.x, from.y - normal.y, TrailDepth));
            _vertices.Add(new Vector3(from.x + normal.x, from.y + normal.y, TrailDepth));
            _vertices.Add(new Vector3(to.x + normal.x, to.y + normal.y, TrailDepth));
            _vertices.Add(new Vector3(to.x - normal.x, to.y - normal.y, TrailDepth));

            for (int i = 0; i < 4; i++)
            {
                _colors.Add(color);
            }

            _triangles.Add(baseIndex);
            _triangles.Add(baseIndex + 1);
            _triangles.Add(baseIndex + 2);
            _triangles.Add(baseIndex);
            _triangles.Add(baseIndex + 2);
            _triangles.Add(baseIndex + 3);
        }

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private static Material CreateMaterial()
        {
            foreach (string name in ShaderCandidates)
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    continue;
                }

                var material = new Material(shader) { name = "FleetTrailMaterial" };
                if (material.HasProperty("_RendererColor"))
                {
                    material.SetColor("_RendererColor", Color.white);
                }
                if (material.HasProperty("_Flip"))
                {
                    material.SetVector("_Flip", Vector4.one);
                }
                return material;
            }

            Debug.LogError("[FleetTrails] Aucun nuancier transparent disponible : les trajectoires resteront invisibles.");
            return null;
        }

        private static void DestroyGenerated(UnityEngine.Object generated)
        {
            if (generated == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generated);
            }
            else
            {
                DestroyImmediate(generated);
            }
        }
    }
}
