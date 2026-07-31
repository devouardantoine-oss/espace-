using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Representation visuelle et tactile d'un systeme stellaire sur la carte galactique.
    /// <para>
    /// Porte un <see cref="CircleCollider2D"/> qui sert uniquement de cible de raycast pour
    /// <see cref="GalaxySelectionController"/> — la carte n'a pas de physique simulee.
    /// </para>
    /// <para>
    /// <b>Style unique par systeme depuis la Phase 12 :</b> couleur, anneau et lunes viennent
    /// de <see cref="StarSystemVisualProfile"/>, calcule une seule fois par
    /// <see cref="GalaxyMapController"/> (qui connait la graine de generation) et passe tel
    /// quel a <see cref="Initialize"/> — ce composant reste un simple applicateur, il ne
    /// calcule jamais lui-meme de valeur deterministe.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class StarSystemMarker : MonoBehaviour
    {
        /// <summary>Rayon visuel et tactile de base du marqueur, en unites monde (avant <see cref="StarSystemVisualProfile.SizeFactor"/>).</summary>
        private const float MarkerRadius = 0.6f;

        /// <summary>Assombrissement du corps au developpement minimal (0), multiplicatif.</summary>
        private const float LowDevelopmentShade = 0.55f;

        /// <summary>Eclaircissement du corps au developpement maximal (5), multiplicatif.</summary>
        private const float HighDevelopmentShade = 1.2f;

        /// <summary>Echelle de l'anneau par rapport au marqueur (rayon exterieur).</summary>
        private const float RingScale = 1.9f;

        /// <summary>Aplatissement vertical de l'anneau, pour l'effet d'ellipse vue en perspective.</summary>
        private const float RingFlattening = 0.4f;

        /// <summary>Echelle d'une lune par rapport au marqueur.</summary>
        private const float MoonScale = 0.28f;

        /// <summary>Distance d'orbite d'une lune, en multiple du rayon du marqueur.</summary>
        private const float MoonOrbitRadius = 1.5f;

        /// <summary>Vitesse de rotation de l'anneau, en degres/seconde — purement cosmetique.</summary>
        private const float RingRotationSpeed = 6f;

        private SpriteRenderer _spriteRenderer;
        private CircleCollider2D _collider;
        private Transform _ringTransform;

        /// <summary>Identifiant du systeme represente. Valide uniquement apres <see cref="Initialize"/>.</summary>
        public StarSystemId Id { get; private set; }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<CircleCollider2D>();
        }

        /// <summary>
        /// Configure le marqueur pour representer <paramref name="system"/> avec le style
        /// <paramref name="profile"/>. Appele une seule fois par <see cref="GalaxyMapController"/>
        /// a la generation de la carte.
        /// </summary>
        public void Initialize(StarSystemState system, Sprite sprite, StarSystemVisualProfile profile)
        {
            Id = system.Id;

            _spriteRenderer.sprite = sprite;
            _spriteRenderer.color = ColorForDevelopmentLevel(profile.BodyColor, system.DevelopmentLevel);

            _collider.radius = MarkerRadius;
            _collider.isTrigger = true;

            float radius = MarkerRadius * profile.SizeFactor;
            transform.localScale = Vector3.one * (radius * 2f);

            if (profile.HasRing)
            {
                _ringTransform = BuildRing();
            }

            for (int i = 0; i < profile.MoonCount; i++)
            {
                BuildMoon(i, profile.MoonCount);
            }
        }

        private void Update()
        {
            if (_ringTransform != null)
            {
                _ringTransform.Rotate(0f, 0f, RingRotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Anneau : sprite aplati (echelle X/Y differente) pour evoquer une ellipse vue en
        /// perspective, place derriere le corps principal (Z plus eloigne de la camera).
        /// </summary>
        private Transform BuildRing()
        {
            var ringObject = new GameObject("Ring");
            ringObject.transform.SetParent(transform, worldPositionStays: false);
            ringObject.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            ringObject.transform.localScale = new Vector3(RingScale, RingScale * RingFlattening, 1f);

            var renderer = ringObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetRingSprite();
            renderer.color = new Color(1f, 1f, 1f, 0.55f);

            return ringObject.transform;
        }

        /// <summary>
        /// Lune : petit disque en orbite fixe autour du marqueur, a un angle deterministe selon
        /// son index (pas de mouvement en jeu pour ce MVP visuel — une orbite figee suffit a
        /// distinguer les systemes).
        /// </summary>
        private void BuildMoon(int index, int totalMoons)
        {
            float angle = (360f / Mathf.Max(totalMoons, 1)) * index + 40f;
            float radians = angle * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * MoonOrbitRadius;

            var moonObject = new GameObject($"Moon{index}");
            moonObject.transform.SetParent(transform, worldPositionStays: false);
            moonObject.transform.localPosition = offset + new Vector3(0f, 0f, -0.01f);
            moonObject.transform.localScale = Vector3.one * MoonScale;

            var renderer = moonObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetMoonSprite();
            renderer.color = new Color(0.75f, 0.78f, 0.82f);
        }

        /// <summary>
        /// Teinte du systeme (<see cref="StarSystemVisualProfile.BodyColor"/>) modulee par le
        /// niveau de developpement (0 a 5) : confirme visuellement, sans UI, que les
        /// statistiques generees atteignent bien l'affichage — comportement conserve depuis la
        /// Phase 2, seulement rebase sur la couleur propre au systeme plutot qu'un degrade fixe.
        /// </summary>
        private static Color ColorForDevelopmentLevel(Color bodyColor, int developmentLevel)
        {
            float t = Mathf.Clamp01(developmentLevel / 5f);
            float shade = Mathf.Lerp(LowDevelopmentShade, HighDevelopmentShade, t);
            return new Color(bodyColor.r * shade, bodyColor.g * shade, bodyColor.b * shade, bodyColor.a);
        }
    }
}
