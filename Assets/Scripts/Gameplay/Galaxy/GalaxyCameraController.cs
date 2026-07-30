using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Deplacement et zoom de la camera orthographique sur la carte galactique.
    /// <para>
    /// <b>Pourquoi l'API bas niveau du nouveau Input System plutot qu'un asset d'Input
    /// Actions ?</b> Cette carte n'a que deux gestes (glisser, pincer). Un asset
    /// <c>.inputactions</c> est un fichier JSON supplementaire a authorer et a assigner dans
    /// l'inspecteur — un point de defaillance de plus alors que je ne peux pas ouvrir
    /// l'editeur pour verifier son cablage. L'API <c>EnhancedTouch</c> couvre exactement ce
    /// besoin. Si l'interface (Phase 11) demande des gestes plus riches, une bascule vers un
    /// asset d'actions restera possible sans toucher au reste du jeu.
    /// </para>
    /// <para>
    /// La souris (glisser-clic + molette) reste geree en plus du tactile : c'est ce qui
    /// permet de tester la carte dans la fenetre Game de l'editeur sans peripherique tactile.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class GalaxyCameraController : MonoBehaviour
    {
        [Tooltip("Taille orthographique minimale (zoom maximal).")]
        [SerializeField]
        private float minOrthographicSize = 4f;

        [Tooltip("Marge ajoutee au rayon de la galaxie pour definir le zoom minimal (zoom arriere maximal) et les limites de deplacement.")]
        [SerializeField]
        private float boundsMargin = 8f;

        [Tooltip("Sensibilite du zoom a la molette (test editeur).")]
        [SerializeField]
        private float scrollZoomSpeed = 0.1f;

        private Camera _camera;
        private float _maxOrthographicSize = 30f;
        private Vector2 _panBoundsMin;
        private Vector2 _panBoundsMax;

        private bool _isDragging;
        private Vector3 _dragAnchorWorld;
        private float _lastPinchDistance = -1f;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        /// <summary>
        /// Configure les limites de zoom et de deplacement d'apres le rayon de la galaxie
        /// generee. Doit etre appele avant toute interaction utilisateur.
        /// </summary>
        public void Initialize(float galaxyRadius)
        {
            _maxOrthographicSize = galaxyRadius + boundsMargin;
            _camera.orthographicSize = Mathf.Clamp(_maxOrthographicSize * 0.6f, minOrthographicSize, _maxOrthographicSize);

            float bound = galaxyRadius + boundsMargin * 0.5f;
            _panBoundsMin = new Vector2(-bound, -bound);
            _panBoundsMax = new Vector2(bound, bound);

            ClampPosition();
        }

        private void Update()
        {
            if (TryHandlePinchZoom())
            {
                // Un pincement est en cours : on ignore le glisser cette frame pour eviter
                // un panoramique parasite pendant le zoom a deux doigts.
                ClampPosition();
                return;
            }

            HandleScrollZoom();
            HandleDragPan();
            ClampPosition();
        }

        private bool TryHandlePinchZoom()
        {
            var activeTouches = Touch.activeTouches;
            if (activeTouches.Count < 2)
            {
                _lastPinchDistance = -1f;
                return false;
            }

            float currentDistance = Vector2.Distance(activeTouches[0].screenPosition, activeTouches[1].screenPosition);

            if (_lastPinchDistance > 0f && currentDistance > 1f)
            {
                float ratio = _lastPinchDistance / currentDistance;
                ApplyZoom(_camera.orthographicSize * ratio);
            }

            _lastPinchDistance = currentDistance;
            return true;
        }

        private void HandleScrollZoom()
        {
            if (Mouse.current == null)
            {
                return;
            }

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            ApplyZoom(_camera.orthographicSize - scroll * scrollZoomSpeed);
        }

        private void HandleDragPan()
        {
            if (Pointer.current == null)
            {
                return;
            }

            bool isPressed = Pointer.current.press.isPressed;
            Vector2 screenPosition = Pointer.current.position.ReadValue();

            if (isPressed && !_isDragging)
            {
                _isDragging = true;
                _dragAnchorWorld = ScreenToWorld(screenPosition);
            }
            else if (isPressed)
            {
                // Deplace la camera pour que le point du monde saisi au depart reste sous le
                // pointeur : l'ecart courant compense a la fois le mouvement du doigt et tout
                // residu de la frame precedente en une seule correction.
                Vector3 currentWorld = ScreenToWorld(screenPosition);
                transform.position += _dragAnchorWorld - currentWorld;
            }
            else
            {
                _isDragging = false;
            }
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            float distanceToPlane = -transform.position.z;
            return _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distanceToPlane));
        }

        private void ApplyZoom(float desiredSize)
        {
            _camera.orthographicSize = Mathf.Clamp(desiredSize, minOrthographicSize, _maxOrthographicSize);
        }

        private void ClampPosition()
        {
            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, _panBoundsMin.x, _panBoundsMax.x);
            position.y = Mathf.Clamp(position.y, _panBoundsMin.y, _panBoundsMax.y);
            transform.position = position;
        }
    }
}
