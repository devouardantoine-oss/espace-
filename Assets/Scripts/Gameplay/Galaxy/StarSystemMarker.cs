using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Representation visuelle et tactile d'un systeme stellaire sur la carte galactique.
    /// <para>
    /// Porte un <see cref="CircleCollider2D"/> qui sert uniquement de cible de raycast pour
    /// <see cref="GalaxySelectionController"/> — la carte n'a pas de physique simulee.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class StarSystemMarker : MonoBehaviour
    {
        /// <summary>Rayon visuel et tactile du marqueur, en unites monde.</summary>
        private const float MarkerRadius = 0.6f;

        /// <summary>Couleur d'un systeme au developpement minimal (0).</summary>
        private static readonly Color LowDevelopmentColor = new Color(0.35f, 0.45f, 0.55f);

        /// <summary>Couleur d'un systeme au developpement maximal (5).</summary>
        private static readonly Color HighDevelopmentColor = new Color(0.4f, 0.85f, 1f);

        private SpriteRenderer _spriteRenderer;
        private CircleCollider2D _collider;

        /// <summary>Identifiant du systeme represente. Valide uniquement apres <see cref="Initialize"/>.</summary>
        public StarSystemId Id { get; private set; }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<CircleCollider2D>();
        }

        /// <summary>
        /// Configure le marqueur pour representer <paramref name="system"/>.
        /// Appele une seule fois par <see cref="GalaxyMapController"/> a la generation de la carte.
        /// </summary>
        public void Initialize(StarSystemState system, Sprite sprite)
        {
            Id = system.Id;

            _spriteRenderer.sprite = sprite;
            _spriteRenderer.color = ColorForDevelopmentLevel(system.DevelopmentLevel);

            _collider.radius = MarkerRadius;
            _collider.isTrigger = true;

            transform.localScale = Vector3.one * (MarkerRadius * 2f);
        }

        /// <summary>
        /// Couleur indicative du niveau de developpement (0 a 5) : plus le systeme est
        /// developpe, plus il est lumineux. Confirme visuellement, sans UI, que les
        /// statistiques generees atteignent bien l'affichage.
        /// </summary>
        private static Color ColorForDevelopmentLevel(int developmentLevel)
        {
            float t = Mathf.Clamp01(developmentLevel / 5f);
            return Color.Lerp(LowDevelopmentColor, HighDevelopmentColor, t);
        }
    }
}
