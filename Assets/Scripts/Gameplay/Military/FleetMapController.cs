using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Dessine un vaisseau sur la carte galactique pour chaque flotte en voyage, et son
    /// itineraire restant (Phase 20).
    /// <para>
    /// <b>Sondage de <c>IMilitaryService.GetFleetsInTransit</c>, pas d'abonnement :</b> une
    /// flotte apparait sur <c>FleetDepartedEvent</c>, mais disparait de quatre facons
    /// differentes (arrivee, colonisation, bataille perdue, dissolution) dont toutes ne
    /// publient pas d'evenement portant l'identifiant de la flotte. Relire la liste chaque
    /// frame reste correct quelle que soit la cause, pour une poignee d'elements — il y a au
    /// plus quelques flottes en vol dans toute la galaxie, la ou les territoires en
    /// comparaient cent.
    /// </para>
    /// <para>
    /// <b>Marqueurs mis en commun (<see cref="ObjectPool{T}"/>) :</b> les departs et arrivees
    /// sont frequents une fois l'horloge lancee a vitesse maximale ; instancier et detruire un
    /// GameObject a chaque fois produirait exactement le genre de pic de ramasse-miettes que
    /// les conventions du projet interdisent.
    /// </para>
    /// <para>
    /// <b>Seules les flottes en voyage sont dessinees.</b> Une flotte stationnee <i>est</i> la
    /// garnison de son systeme : lui donner un vaisseau ajouterait une centaine d'icones
    /// immobiles sur une carte qui affiche deja ses systemes et ses territoires. Le panneau de
    /// systeme reste l'endroit ou l'on consulte une garnison.
    /// </para>
    /// </summary>
    public sealed class FleetMapController : MonoBehaviour
    {
        /// <summary>Nombre de marqueurs pre-alloues : couvre le cas courant sans jamais instancier en cours de partie.</summary>
        private const int PrewarmedMarkers = 8;

        private readonly Dictionary<int, FleetMarker> _activeMarkers = new Dictionary<int, FleetMarker>();
        private readonly List<int> _staleFleetIds = new List<int>();

        private ObjectPool<FleetMarker> _markerPool;
        private Transform _markerRoot;

        private GalaxyMap _map;
        private Camera _camera;
        private IEventBus _eventBus;
        private IMilitaryService _military;
        private IGameClock _clock;
        private EmpireRegistry _empireRegistry;

        private FleetTrailRenderer _trails;
        private int _selectedFleetId = -1;

        /// <summary>
        /// Cable l'affichage. Appele une seule fois par <c>GalaxyMapController</c>, avec la
        /// galaxie et la camera deja construites.
        /// </summary>
        public void Initialize(GalaxyMap map, Camera camera)
        {
            _map = map;
            _camera = camera;

            _markerRoot = new GameObject("FleetMarkers").transform;
            _markerRoot.SetParent(transform, worldPositionStays: false);

            Sprite shipSprite = RuntimeSpriteFactory.GetShipSprite();
            Sprite ringSprite = RuntimeSpriteFactory.GetRingSprite();

            _markerPool = new ObjectPool<FleetMarker>(
                factory: () =>
                {
                    var markerObject = new GameObject("Fleet");
                    markerObject.transform.SetParent(_markerRoot, worldPositionStays: false);
                    var marker = markerObject.AddComponent<FleetMarker>();
                    marker.Initialize(-1, shipSprite, ringSprite);
                    return marker;
                },
                onGet: marker => marker.gameObject.SetActive(true),
                onRelease: marker => marker.gameObject.SetActive(false),
                prewarmCount: PrewarmedMarkers);

            var trailObject = new GameObject("FleetTrails");
            trailObject.transform.SetParent(transform, worldPositionStays: false);
            _trails = trailObject.AddComponent<FleetTrailRenderer>();
            _trails.Initialize(map);
        }

        private void OnEnable()
        {
            if (ServiceLocator.TryGet(out _eventBus))
            {
                _eventBus.Subscribe<FleetSelectedEvent>(OnFleetSelected);
                _eventBus.Subscribe<FleetDeselectedEvent>(OnFleetDeselected);
                _eventBus.Subscribe<SystemSelectedEvent>(OnSystemSelected);
            }
        }

        private void OnDisable()
        {
            if (_eventBus == null)
            {
                return;
            }

            _eventBus.Unsubscribe<FleetSelectedEvent>(OnFleetSelected);
            _eventBus.Unsubscribe<FleetDeselectedEvent>(OnFleetDeselected);
            _eventBus.Unsubscribe<SystemSelectedEvent>(OnSystemSelected);
            _eventBus = null;
        }

        private void OnFleetSelected(FleetSelectedEvent selected) => _selectedFleetId = selected.FleetId;

        private void OnFleetDeselected(FleetDeselectedEvent _) => _selectedFleetId = -1;

        /// <summary>Selectionner un systeme abandonne la selection de flotte : les deux sont exclusives.</summary>
        private void OnSystemSelected(SystemSelectedEvent _) => _selectedFleetId = -1;

        private void Update()
        {
            if (_map == null || _camera == null)
            {
                return;
            }

            if (_military == null && !ServiceLocator.TryGet(out _military))
            {
                return;
            }

            if (_clock == null && !ServiceLocator.TryGet(out _clock))
            {
                return;
            }

            if (_empireRegistry == null)
            {
                ServiceLocator.TryGet(out _empireRegistry);
            }

            IReadOnlyList<Fleet> inTransit = _military.GetFleetsInTransit();
            SyncMarkers(inTransit);
            _trails.Sync(inTransit, _selectedFleetId, OwnerColor, _clock.CurrentDate);
        }

        private void SyncMarkers(IReadOnlyList<Fleet> inTransit)
        {
            GameDate today = _clock.CurrentDate;
            float orthographicSize = _camera.orthographicSize;
            float deltaTime = Time.deltaTime;

            // Marque tout comme perime, puis ne conserve que ce qui a ete revu : evite un
            // second parcours pour detecter les disparitions.
            _staleFleetIds.Clear();
            foreach (int fleetId in _activeMarkers.Keys)
            {
                _staleFleetIds.Add(fleetId);
            }

            foreach (Fleet fleet in inTransit)
            {
                if (!FleetPresentation.TryComputePose(fleet, _map, today, out FleetPose pose))
                {
                    continue;
                }

                if (!_activeMarkers.TryGetValue(fleet.Id, out FleetMarker marker))
                {
                    marker = _markerPool.Get();
                    marker.Initialize(fleet.Id, RuntimeSpriteFactory.GetShipSprite(), RuntimeSpriteFactory.GetRingSprite());
                    _activeMarkers[fleet.Id] = marker;
                }
                else
                {
                    _staleFleetIds.Remove(fleet.Id);
                }

                marker.Apply(
                    pose.Position,
                    pose.HeadingDegrees,
                    OwnerColor(fleet.OwnerId),
                    fleet.Composition.TotalCount,
                    fleet.Id == _selectedFleetId,
                    orthographicSize,
                    deltaTime);
            }

            foreach (int fleetId in _staleFleetIds)
            {
                if (!_activeMarkers.TryGetValue(fleetId, out FleetMarker marker))
                {
                    continue;
                }

                _markerPool.Release(marker);
                _activeMarkers.Remove(fleetId);

                if (fleetId == _selectedFleetId)
                {
                    // La flotte selectionnee est arrivee, a fusionne ou a ete detruite : le
                    // panneau qui l'affichait doit se refermer plutot que montrer un fantome.
                    _selectedFleetId = -1;
                    _eventBus?.Publish(new FleetDeselectedEvent());
                }
            }
        }

        private Color OwnerColor(int ownerId)
        {
            return _empireRegistry != null && _empireRegistry.TryGetEmpire(ownerId, out Empire empire)
                ? empire.Color
                : Color.gray;
        }
    }
}
