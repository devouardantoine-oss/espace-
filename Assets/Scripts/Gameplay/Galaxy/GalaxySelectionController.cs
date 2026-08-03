using Espace.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Detecte les appuis brefs (« tap ») sur la carte galactique et publie la selection
    /// correspondante sur l'<see cref="IEventBus"/>.
    /// <para>
    /// Un tap se distingue d'un glisser (geree independamment par
    /// <see cref="GalaxyCameraController"/>) par un mouvement et une duree faibles entre
    /// l'appui et le relachement. Les deux controleurs lisent le meme <see cref="Pointer"/>
    /// sans se coordonner explicitement : un tap deplace la camera de façon negligeable, et
    /// un glisser volontaire echoue simplement le test de tap — aucun etat partage requis.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class GalaxySelectionController : MonoBehaviour
    {
        [Tooltip("Duree maximale (secondes) entre l'appui et le relachement pour etre considere comme un tap.")]
        [SerializeField]
        private float tapMaxDuration = 0.35f;

        [Tooltip("Deplacement maximal (pixels ecran) tolere entre l'appui et le relachement pour etre considere comme un tap.")]
        [SerializeField]
        private float tapMaxMovementPixels = 12f;

        /// <summary>
        /// Nombre maximal de collisionneurs recuperes par appui. Trois suffit largement (un
        /// systeme, un vaisseau, une marge) et le tampon est reutilise d'un appui a l'autre :
        /// la surcharge de <c>Physics2D.OverlapPoint</c> a tableau n'alloue rien, contrairement
        /// a <c>OverlapPointAll</c>.
        /// </summary>
        private readonly Collider2D[] _hits = new Collider2D[4];

        private ContactFilter2D _hitFilter;

        private Camera _camera;
        private IEventBus _eventBus;

        private Vector2 _pressScreenPosition;
        private float _pressTime;

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            // Les marqueurs de flotte sont des declencheurs (isTrigger) : sans cela, le filtre
            // par defaut les ecarterait et un vaisseau ne serait jamais selectionnable.
            _hitFilter = new ContactFilter2D { useTriggers = true };
            _hitFilter.NoFilter();
            _hitFilter.useTriggers = true;
        }

        private void Update()
        {
            if (Pointer.current == null)
            {
                return;
            }

            if (Pointer.current.press.wasPressedThisFrame)
            {
                _pressScreenPosition = Pointer.current.position.ReadValue();
                _pressTime = Time.unscaledTime;
            }
            else if (Pointer.current.press.wasReleasedThisFrame)
            {
                HandleRelease(Pointer.current.position.ReadValue());
            }
        }

        private void HandleRelease(Vector2 releaseScreenPosition)
        {
            float duration = Time.unscaledTime - _pressTime;
            float movement = Vector2.Distance(_pressScreenPosition, releaseScreenPosition);

            if (duration > tapMaxDuration || movement > tapMaxMovementPixels)
            {
                // Glisser (panoramique) plutot qu'un appui bref : pas de selection.
                return;
            }

            // IMGUI dessine par-dessus la scene mais ne consomme pas les entrees du nouvel Input
            // System. Sans ce test, toucher un onglet de la fiche de systeme ne rencontrait aucun
            // collisionneur, et cette methode en concluait « le joueur a touche le vide » —
            // refermant le panneau que le joueur etait en train d'utiliser.
            if (UiScreenRegions.ContainsPointer(releaseScreenPosition))
            {
                return;
            }

            // Resolu paresseusement plutot qu'en Awake : garantit que GameBootstrap
            // (execution order -1000) a deja enregistre les services au premier appui.
            if (_eventBus == null && !ServiceLocator.TryGet(out _eventBus))
            {
                GameLog.Warning("[GalaxySelectionController] IEventBus indisponible : la selection ne sera pas publiee.");
                return;
            }

            Vector3 worldPoint = _camera.ScreenToWorldPoint(new Vector3(releaseScreenPosition.x, releaseScreenPosition.y, -_camera.transform.position.z));

            // Un vaisseau passant au-dessus d'un systeme couvre les deux collisionneurs.
            // OverlapPoint n'en renvoie qu'un, choisi arbitrairement : il faut donc tous les
            // recuperer et arbitrer explicitement, sinon selectionner une flotte au-dessus d'un
            // systeme donnerait un resultat different d'une frame a l'autre.
            int hitCount = Physics2D.OverlapPoint(worldPoint, _hitFilter, _hits);

            if (TryPublishFleetSelection(hitCount))
            {
                return;
            }

            for (int i = 0; i < hitCount; i++)
            {
                if (_hits[i] != null && _hits[i].TryGetComponent(out StarSystemMarker marker))
                {
                    _eventBus.Publish(new SystemSelectedEvent(marker.Id));
                    return;
                }
            }

            // Le fond de la carte : les deux selections retombent, celle de systeme comme celle
            // de flotte. Publier les deux evite qu'un panneau reste ouvert sur une entite que
            // le joueur vient visiblement d'abandonner.
            _eventBus.Publish(new FleetDeselectedEvent());
            _eventBus.Publish(new SystemDeselectedEvent());
        }

        /// <summary>
        /// Les vaisseaux ont la priorite sur les systemes (Phase 20) : ils sont dessines
        /// par-dessus et sont bien plus petits, donc viser un vaisseau est un geste deliberé,
        /// alors qu'atteindre un systeme sous un vaisseau ne l'est jamais.
        /// </summary>
        private bool TryPublishFleetSelection(int hitCount)
        {
            for (int i = 0; i < hitCount; i++)
            {
                if (_hits[i] == null || !_hits[i].TryGetComponent(out FleetMarker fleetMarker))
                {
                    continue;
                }

                _eventBus.Publish(new SystemDeselectedEvent());
                _eventBus.Publish(new FleetSelectedEvent(fleetMarker.FleetId));
                return true;
            }

            return false;
        }
    }
}
