using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Vaisseau representant une flotte en vol sur la carte galactique (Phase 20).
    /// <para>
    /// <b>Vue passive, volontairement ignorante de <c>Fleet</c> :</b> ce composant ne connait
    /// qu'un identifiant entier et les valeurs a afficher, poussees par
    /// <c>Espace.Gameplay.Military.FleetMapController</c>. C'est ce qui permet a
    /// <see cref="GalaxySelectionController"/> — qui vit dans <c>Galaxy</c> — de le reconnaitre
    /// au toucher sans que <c>Galaxy</c> ait a dependre de <c>Military</c>. La dependance ne va
    /// que dans un sens : Military connait Galaxy, jamais l'inverse.
    /// </para>
    /// <para>
    /// <b>Taille constante a l'ecran</b> (voir <see cref="Apply"/>) : un vaisseau dimensionne en
    /// unites monde serait un point invisible au zoom arriere et un objet geant au zoom avant.
    /// Le rayon du collisionneur suit la meme echelle, donc la cible tactile ne change jamais
    /// de taille sous le doigt.
    /// </para>
    /// <para>
    /// <b>Position lissee, pas appliquee brutalement :</b> le calendrier du jeu avance par jours
    /// entiers, donc la position calculee saute d'un cran par jour. Un amortissement
    /// exponentiel vers la position cible transforme ces sauts en glissement continu, sans
    /// toucher a l'horloge ni au modele.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class FleetMarker : MonoBehaviour
    {
        /// <summary>Profondeur Z : devant les systemes (0) et les routes (0,1), derriere les libelles.</summary>
        private const float ShipDepth = -0.01f;

        /// <summary>Rayon tactile, en fraction de la demi-hauteur visible : garantit une cible confortable a tout zoom.</summary>
        private const float TouchRadiusFactor = 0.055f;

        /// <summary>Echelle du vaisseau a une unite embarquee, et a dix (le plafond par flotte).</summary>
        private const float ScaleAtOneUnit = 0.75f;
        private const float ScaleAtFullFleet = 1.25f;

        /// <summary>Echelle de l'anneau de selection, relative au vaisseau.</summary>
        private const float SelectionRingScale = 2.4f;

        /// <summary>
        /// Constante de temps de l'amortissement, en secondes. Au-dela, le vaisseau trainerait
        /// visiblement derriere sa position reelle a vitesse maximale.
        /// </summary>
        private const float SmoothingTime = 0.35f;

        /// <summary>Au-dela de cette distance, la position est appliquee sans lissage : la flotte a change d'etape ou vient d'apparaitre.</summary>
        private const float TeleportThreshold = 6f;

        private SpriteRenderer _renderer;
        private CircleCollider2D _collider;
        private Transform _ringTransform;
        private SpriteRenderer _ringRenderer;

        private Vector2 _smoothedPosition;
        private bool _hasPosition;

        /// <summary>Identifiant de la flotte representee. Valide uniquement apres <see cref="Initialize"/>.</summary>
        public int FleetId { get; private set; }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<CircleCollider2D>();
            _collider.isTrigger = true;
        }

        /// <summary>
        /// Prepare le marqueur pour une flotte. Appele a chaque fois qu'un marqueur est repris
        /// dans le pool : l'identifiant change, et la position doit repartir sans lissage.
        /// </summary>
        public void Initialize(int fleetId, Sprite shipSprite, Sprite ringSprite)
        {
            FleetId = fleetId;
            _hasPosition = false;

            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
                _collider = GetComponent<CircleCollider2D>();
                _collider.isTrigger = true;
            }

            _renderer.sprite = shipSprite;

            if (_ringTransform == null)
            {
                var ring = new GameObject("Selection");
                ring.transform.SetParent(transform, worldPositionStays: false);
                _ringRenderer = ring.AddComponent<SpriteRenderer>();
                _ringRenderer.sprite = ringSprite;
                _ringTransform = ring.transform;
                _ringTransform.localScale = Vector3.one * SelectionRingScale;
            }

            _ringRenderer.enabled = false;
        }

        /// <summary>
        /// Applique l'etat courant de la flotte.
        /// </summary>
        /// <param name="targetPosition">Position calculee par <c>FleetPresentation</c>.</param>
        /// <param name="headingDegrees">Cap, pour orienter le vaisseau dans le sens du trajet.</param>
        /// <param name="color">Couleur de l'empire proprietaire.</param>
        /// <param name="unitCount">Nombre d'unites embarquees : dimensionne le vaisseau, la force se lisant sans texte.</param>
        /// <param name="selected">Vrai pour afficher l'anneau de selection.</param>
        /// <param name="orthographicSize">Demi-hauteur visible de la camera, pour garder une taille d'ecran constante.</param>
        /// <param name="deltaTime">Temps ecoule, pour l'amortissement. Passer 0 applique la position immediatement.</param>
        public void Apply(
            Vector2 targetPosition,
            float headingDegrees,
            Color color,
            int unitCount,
            bool selected,
            float orthographicSize,
            float deltaTime)
        {
            if (!_hasPosition || deltaTime <= 0f
                || (targetPosition - _smoothedPosition).sqrMagnitude > TeleportThreshold * TeleportThreshold)
            {
                _smoothedPosition = targetPosition;
                _hasPosition = true;
            }
            else
            {
                // Amortissement exponentiel independant de la frequence d'affichage : le meme
                // rendu a 30 et a 60 images par seconde.
                float blend = 1f - Mathf.Exp(-deltaTime / SmoothingTime);
                _smoothedPosition = Vector2.Lerp(_smoothedPosition, targetPosition, blend);
            }

            transform.position = new Vector3(_smoothedPosition.x, _smoothedPosition.y, ShipDepth);
            transform.rotation = Quaternion.Euler(0f, 0f, headingDegrees);

            float screenScale = orthographicSize * TouchRadiusFactor;
            float fleetScale = Mathf.Lerp(ScaleAtOneUnit, ScaleAtFullFleet, Mathf.Clamp01((unitCount - 1) / 9f));
            transform.localScale = Vector3.one * (screenScale * fleetScale);

            // Le collisionneur vit dans l'echelle locale du marqueur : un rayon de 0,5 couvre
            // donc exactement le sprite, quelle que soit l'echelle appliquee ci-dessus.
            _collider.radius = 0.6f;

            _renderer.color = color;

            if (_ringRenderer != null && _ringRenderer.enabled != selected)
            {
                _ringRenderer.enabled = selected;
            }

            if (selected && _ringRenderer != null)
            {
                _ringRenderer.color = new Color(1f, 1f, 1f, 0.85f);
            }
        }
    }
}
