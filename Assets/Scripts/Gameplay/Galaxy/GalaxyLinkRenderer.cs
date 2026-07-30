using System.Collections.Generic;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Dessine toutes les routes hyperspatiales de la galaxie en un seul <i>draw call</i>.
    /// <para>
    /// <b>Choix technique :</b> une centaine de systemes produit facilement 120 a 150 routes.
    /// Un <c>LineRenderer</c> par route couterait autant de draw calls. Ici, chaque route est
    /// un quad (2 triangles) ajoute a un unique maillage combine ; le cout de rendu de toute
    /// la carte reste constant quel que soit le nombre de routes.
    /// </para>
    /// <para>
    /// Les routes sont placees a une profondeur legerement superieure a celle des marqueurs
    /// de systeme (voir <see cref="LinkDepth"/>) : avec une camera orthographique et un
    /// materiau opaque, le depth buffer garantit que les systemes restent visuellement
    /// au-dessus des routes sans avoir a coordonner un ordre de tri entre un maillage et des
    /// <c>SpriteRenderer</c>.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class GalaxyLinkRenderer : MonoBehaviour
    {
        /// <summary>Profondeur (Z) des routes, derriere les systemes qui restent a Z = 0.</summary>
        private const float LinkDepth = 0.1f;

        /// <summary>Largeur des routes affichees, en unites monde.</summary>
        private const float LineWidth = 0.08f;

        private static readonly Color LinkColor = new Color(0.3f, 0.55f, 0.6f, 1f);

        private static readonly string[] FallbackShaderNames =
        {
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "Legacy Shaders/Diffuse"
        };

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        /// <summary>
        /// Construit le maillage combine representant <paramref name="map"/>.Links.
        /// </summary>
        /// <param name="map">Galaxie dont on dessine les routes.</param>
        /// <param name="worldPositions">Position monde de chaque systeme (Z ignore, remplace par <see cref="LinkDepth"/>).</param>
        public void Build(GalaxyMap map, IReadOnlyDictionary<StarSystemId, Vector3> worldPositions)
        {
            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
                _meshRenderer = GetComponent<MeshRenderer>();
            }

            var vertices = new List<Vector3>(map.Links.Count * 4);
            var triangles = new List<int>(map.Links.Count * 6);

            foreach (HyperlaneLink link in map.Links)
            {
                if (!worldPositions.TryGetValue(link.SystemA, out Vector3 from) ||
                    !worldPositions.TryGetValue(link.SystemB, out Vector3 to))
                {
                    continue;
                }

                AppendSegmentQuad(from, to, vertices, triangles);
            }

            var mesh = new Mesh { name = "HyperlaneLinks" };
            if (vertices.Count > 0)
            {
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
            }

            _meshFilter.sharedMesh = mesh;
            _meshRenderer.sharedMaterial = CreateLinkMaterial();
        }

        /// <summary>Ajoute au maillage un quad fin representant le segment entre deux systemes.</summary>
        private static void AppendSegmentQuad(Vector3 from, Vector3 to, List<Vector3> vertices, List<int> triangles)
        {
            from.z = LinkDepth;
            to.z = LinkDepth;

            Vector3 direction = (to - from);
            direction.z = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 normal = new Vector3(-direction.y, direction.x, 0f).normalized * (LineWidth * 0.5f);

            int baseIndex = vertices.Count;
            vertices.Add(from - normal);
            vertices.Add(from + normal);
            vertices.Add(to + normal);
            vertices.Add(to - normal);

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }

        private static Material CreateLinkMaterial()
        {
            foreach (string shaderName in FallbackShaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    var material = new Material(shader) { name = "HyperlaneLinkMaterial" };
                    if (material.HasProperty("_Color"))
                    {
                        material.SetColor("_Color", LinkColor);
                    }
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", LinkColor);
                    }
                    return material;
                }
            }

            Debug.LogError("[GalaxyLinkRenderer] Aucun shader unlit disponible : les routes hyperspatiales resteront invisibles.");
            return null;
        }
    }
}
