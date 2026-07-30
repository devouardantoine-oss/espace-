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

        private Camera _camera;
        private IEventBus _eventBus;

        private Vector2 _pressScreenPosition;
        private float _pressTime;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
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

            // Resolu paresseusement plutot qu'en Awake : garantit que GameBootstrap
            // (execution order -1000) a deja enregistre les services au premier appui.
            if (_eventBus == null && !ServiceLocator.TryGet(out _eventBus))
            {
                GameLog.Warning("[GalaxySelectionController] IEventBus indisponible : la selection ne sera pas publiee.");
                return;
            }

            Vector3 worldPoint = _camera.ScreenToWorldPoint(new Vector3(releaseScreenPosition.x, releaseScreenPosition.y, -_camera.transform.position.z));
            Collider2D hit = Physics2D.OverlapPoint(worldPoint);

            if (hit != null && hit.TryGetComponent(out StarSystemMarker marker))
            {
                _eventBus.Publish(new SystemSelectedEvent(marker.Id));
            }
            else
            {
                _eventBus.Publish(new SystemDeselectedEvent());
            }
        }
    }
}
