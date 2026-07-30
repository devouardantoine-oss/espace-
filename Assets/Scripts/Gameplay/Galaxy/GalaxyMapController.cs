using System.Collections.Generic;
using Espace.Core;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Orchestrateur unique de la scene <c>GalaxyMap</c> : genere la galaxie, construit sa
    /// representation visuelle et cable la camera et la selection.
    /// <para>
    /// <b>Choix technique :</b> toutes les liaisons entre composants (camera, marqueurs de
    /// systeme, rendu des routes) sont creees <b>par code</b> plutot que par references
    /// serialisees dans l'inspecteur. Une scene Unity ecrite a la main est du YAML sujet a
    /// erreur ; en minimisant le nombre de references croisees a authorer manuellement (il
    /// n'en reste qu'une : ce composant vers l'asset <see cref="GalaxyConfig"/>), le risque
    /// de scene mal cablee est fortement reduit. Cette classe fait donc plus qu'un
    /// orchestrateur habituel le ferait dans un projet edite normalement dans l'editeur.
    /// </para>
    /// <para>
    /// La selection de systeme (evenements <see cref="SystemSelectedEvent"/>/<see cref="SystemDeselectedEvent"/>)
    /// est affichee par <c>Espace.UI.SystemInfoPanelController</c> depuis la Phase 11 : ce
    /// composant ne fait plus que la publier, il ne la rend plus lui-meme (l'ancien panneau de
    /// diagnostic IMGUI qui vivait ici jusqu'a la Phase 10 a ete retire).
    /// </para>
    /// </summary>
    public sealed class GalaxyMapController : MonoBehaviour
    {
        [Tooltip("Parametres de generation de la galaxie.")]
        [SerializeField]
        private GalaxyConfig config;

        [Tooltip("Taille de zoom minimale de la camera (zoom maximal).")]
        [SerializeField]
        private float minOrthographicSize = 4f;

        private GalaxyMap _map;

        private void Awake()
        {
            if (config == null)
            {
                GameLog.Error("[GalaxyMapController] Aucun GalaxyConfig assigne : la carte ne peut pas etre generee.");
                return;
            }

            _map = GalaxyGenerator.Generate(config.ToGenerationParameters());
            GameLog.Info($"[GalaxyMap] Galaxie generee : {_map.Systems.Count} systemes, {_map.Links.Count} routes hyperspatiales.");

            // Publiee sous son type concret (comme GameManager en Phase 1) : d'autres
            // systemes de la meme scene (l'economie, Phase 4) doivent pouvoir la lire sans
            // detenir de reference directe vers ce composant.
            if (!ServiceLocator.IsRegistered<GalaxyMap>())
            {
                ServiceLocator.Register(_map);
            }

            Dictionary<StarSystemId, Vector3> worldPositions = BuildMarkers(_map);
            BuildLinkRenderer(_map, worldPositions);
            SetupCamera();
        }

        /// <summary>
        /// Desenregistre la galaxie a la destruction de ce composant : sans cela, un retour
        /// au menu principal suivi d'une nouvelle partie (Phase 11) trouverait
        /// <see cref="ServiceLocator.IsRegistered{T}"/> deja vrai dans <see cref="Awake"/> et
        /// laisserait tous les autres services de la nouvelle scene pointer vers l'ancienne
        /// galaxie plutot que celle fraichement generee.
        /// </summary>
        private void OnDestroy()
        {
            if (_map != null)
            {
                ServiceLocator.Unregister<GalaxyMap>();
                _map = null;
            }
        }

        /// <summary>Instancie un marqueur par systeme et retourne leurs positions monde, indexees par identifiant.</summary>
        private Dictionary<StarSystemId, Vector3> BuildMarkers(GalaxyMap map)
        {
            var systemsRoot = new GameObject("Systems").transform;
            systemsRoot.SetParent(transform, worldPositionStays: false);

            Sprite circleSprite = RuntimeSpriteFactory.GetCircleSprite();
            var worldPositions = new Dictionary<StarSystemId, Vector3>(map.Systems.Count);

            foreach (StarSystemState system in map.Systems)
            {
                var worldPosition = new Vector3(system.Position.x, system.Position.y, 0f);
                worldPositions[system.Id] = worldPosition;

                var markerObject = new GameObject($"System_{system.Name}");
                markerObject.transform.SetParent(systemsRoot, worldPositionStays: false);
                markerObject.transform.position = worldPosition;

                var marker = markerObject.AddComponent<StarSystemMarker>();
                marker.Initialize(system, circleSprite);
            }

            return worldPositions;
        }

        /// <summary>Cree le maillage combine des routes hyperspatiales, en un seul draw call.</summary>
        private void BuildLinkRenderer(GalaxyMap map, Dictionary<StarSystemId, Vector3> worldPositions)
        {
            var linksObject = new GameObject("Hyperlanes");
            linksObject.transform.SetParent(transform, worldPositionStays: false);

            var linkRenderer = linksObject.AddComponent<GalaxyLinkRenderer>();
            linkRenderer.Build(map, worldPositions);
        }

        /// <summary>Attache camera et selection tactile a la camera principale de la scene.</summary>
        private void SetupCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameLog.Error("[GalaxyMapController] Aucune camera principale (tag MainCamera) dans la scene.");
                return;
            }

            mainCamera.orthographic = true;

            var cameraController = mainCamera.gameObject.AddComponent<GalaxyCameraController>();
            cameraController.Initialize(config.GalaxyRadius);

            mainCamera.gameObject.AddComponent<GalaxySelectionController>();
        }
    }
}
