using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Military;
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
    /// Affiche egalement un panneau de diagnostic minimal (<see cref="OnGUI"/>) pour rendre
    /// la selection observable sans attendre l'ecran « Gestion des systemes » de la Phase 11.
    /// Volontairement en IMGUI, pas en TextMeshPro : c'est un outil de mise au point
    /// temporaire, pas une brique de l'interface finale.
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
        private IEventBus _eventBus;
        private StarSystemId? _selectedSystemId;

        // Uniquement pour afficher un nom d'empire plutot qu'un identifiant brut dans le
        // panneau de diagnostic ci-dessous : pas une dependance structurelle a Espace.Gameplay.Empires,
        // resolue paresseusement comme le reste des dependances inter-controleurs de la scene.
        private EmpireRegistry _empireRegistry;

        // Meme raison : afficher la garnison du systeme selectionne sans dependance structurelle a Espace.Gameplay.Military.
        private IMilitaryService _military;

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

        private void OnEnable()
        {
            if (ServiceLocator.TryGet(out _eventBus))
            {
                _eventBus.Subscribe<SystemSelectedEvent>(OnSystemSelected);
                _eventBus.Subscribe<SystemDeselectedEvent>(OnSystemDeselected);
            }
            else
            {
                GameLog.Warning("[GalaxyMapController] IEventBus indisponible : le panneau de diagnostic ne recevra pas la selection.");
            }
        }

        private void OnDisable()
        {
            if (_eventBus == null)
            {
                return;
            }

            _eventBus.Unsubscribe<SystemSelectedEvent>(OnSystemSelected);
            _eventBus.Unsubscribe<SystemDeselectedEvent>(OnSystemDeselected);
            _eventBus = null;
        }

        private void OnSystemSelected(SystemSelectedEvent selectedEvent)
        {
            _selectedSystemId = selectedEvent.SystemId;
        }

        private void OnSystemDeselected(SystemDeselectedEvent deselectedEvent)
        {
            _selectedSystemId = null;
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

        /// <summary>Panneau de diagnostic temporaire affichant le systeme selectionne.</summary>
        private void OnGUI()
        {
            if (_empireRegistry == null)
            {
                ServiceLocator.TryGet(out _empireRegistry);
            }

            if (_military == null)
            {
                ServiceLocator.TryGet(out _military);
            }

            const int width = 260;
            const int padding = 10;
            const int height = 170;

            GUI.Box(new Rect(padding, padding, width, height), string.Empty);

            var layout = new Rect(padding + 8, padding + 6, width - 16, height - 10);
            GUILayout.BeginArea(layout);

            if (_map == null)
            {
                GUILayout.Label("Galaxie non generee.");
            }
            else if (_selectedSystemId.HasValue && _map.TryGetSystem(_selectedSystemId.Value, out StarSystemState system))
            {
                GUILayout.Label($"{system.Name}");
                GUILayout.Label($"Population : {system.Population} M");
                GUILayout.Label($"Richesse : {system.Wealth}/100");
                GUILayout.Label($"Developpement : {system.DevelopmentLevel}/5");
                GUILayout.Label($"Stabilite : {Mathf.RoundToInt(system.Stability * 100f)}%");
                GUILayout.Label($"Proprietaire : {OwnerLabel(system.OwnerId)}");
                GUILayout.Label($"Gisements : {(system.ResourceDeposits.Length == 0 ? "aucun" : string.Join(", ", system.ResourceDeposits))}");
                GUILayout.Label($"Routes : {_map.GetNeighbors(system.Id).Count}");
                GUILayout.Label($"Garnison : {GarrisonLabel(system)}");
            }
            else
            {
                GUILayout.Label($"{_map.Systems.Count} systemes, {_map.Links.Count} routes.");
                GUILayout.Label("Touchez un systeme pour ses details.");
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Nom de l'empire proprietaire, s'il est deja connu (l'IEmpireRegistry n'existe
        /// qu'a partir du Start d'EmpireController) ; repli sur l'identifiant brut sinon.
        /// </summary>
        private string OwnerLabel(int ownerId)
        {
            if (ownerId == StarSystemState.UnownedOwnerId)
            {
                return "Independant";
            }

            if (_empireRegistry != null && _empireRegistry.TryGetEmpire(ownerId, out Empire empire))
            {
                return empire.Name;
            }

            return ownerId.ToString();
        }

        /// <summary>
        /// Resume des flottes stationnees sur ce systeme, tous proprietaires confondus
        /// (repli sur « inconnue » si IMilitaryService n'est pas encore disponible).
        /// </summary>
        private string GarrisonLabel(StarSystemState system)
        {
            if (_military == null)
            {
                return "inconnue";
            }

            IReadOnlyList<Fleet> fleets = _military.GetFleetsAt(system.Id);
            if (fleets.Count == 0)
            {
                return "aucune";
            }

            var parts = new List<string>(fleets.Count);
            foreach (Fleet fleet in fleets)
            {
                parts.Add($"{OwnerLabel(fleet.OwnerId)} : {fleet.Composition.TotalCount}");
            }

            return string.Join(" | ", parts);
        }
    }
}
