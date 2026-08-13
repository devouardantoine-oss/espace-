using System.Collections.Generic;
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

        // La geometrie (echelles des anneaux, du halo, orbite des pastilles) vit dans
        // SystemGlyph, source unique verifiee par des tests d'invariants. Ce composant ne fait
        // que l'appliquer.

        /// <summary>Opacite des anneaux de developpement : lisibles, jamais dominants.</summary>
        private const float DevelopmentRingOpacity = 0.62f;

        /// <summary>Teinte des anneaux de developpement — laiton, distincte de l'anneau decoratif blanc.</summary>
        private static readonly Color DevelopmentRingColor = new Color(0.78f, 0.58f, 0.29f);

        /// <summary>Teinte des pastilles de garnison.</summary>
        private static readonly Color GarrisonPipColor = new Color(0.44f, 0.66f, 0.72f);

        /// <summary>Teinte de la derniere pastille quand la garnison depasse le plafond affichable.</summary>
        private static readonly Color GarrisonOverflowColor = new Color(0.94f, 0.86f, 0.62f);

        private SpriteRenderer _spriteRenderer;
        private CircleCollider2D _collider;
        private Transform _ringTransform;

        /// <summary>Anneau de developpement, cree a la demande et conserve (voir <see cref="ApplyGlyph"/>).</summary>
        private SpriteRenderer _developmentRing;

        /// <summary>Pastilles de garnison, creees a la demande et conservees.</summary>
        private readonly List<SpriteRenderer> _garrisonPips = new List<SpriteRenderer>(SystemGlyph.MaximumGarrisonPips);

        private SpriteRenderer _halo;

        /// <summary>Rayon effectif du marqueur, base des echelles relatives ci-dessus.</summary>
        private float _radius = MarkerRadius;

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

            _radius = MarkerRadius * profile.SizeFactor;
            transform.localScale = Vector3.one * (_radius * 2f);

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
        /// Applique l'etat vivant du systeme : anneaux de developpement, halo de stabilite,
        /// pastilles de garnison (Phase 23, tranche A).
        /// <para>
        /// <b>Ce composant reste un applicateur.</b> Il ne decide de rien et ne lit aucun
        /// service : <see cref="SystemGlyph"/> a deja tranche ce qui est visible et ce qui ne
        /// l'est pas, <see cref="SystemGlyphController"/> fournit les valeurs. C'est la meme
        /// separation que pour <see cref="StarSystemVisualProfile"/> depuis la Phase 12.
        /// </para>
        /// <para>
        /// <b>Les objets sont crees a la demande puis conserves</b>, jamais detruits : un systeme
        /// qui gagne un niveau ne recree pas ses anneaux precedents, et un systeme dont la
        /// garnison retombe se contente de masquer des pastilles. Detruire et recreer a chaque
        /// mois produirait des centaines d'allocations sur une carte de cent systemes.
        /// </para>
        /// <para>
        /// <b>Anneau de developpement contre anneau decoratif.</b> Le marqueur peut deja porter
        /// un anneau issu de <see cref="StarSystemVisualProfile"/> depuis la Phase 12 — il reste,
        /// et les deux ne se confondent pas : le decoratif est une ellipse blanche inclinee qui
        /// tourne, celui du developpement est un cercle laiton fixe, plus large.
        /// </para>
        /// </summary>
        public void ApplyGlyph(SystemGlyph glyph)
        {
            ApplyDevelopmentRing(glyph.DevelopmentRings);
            ApplyHalo(glyph);
            ApplyGarrisonPips(glyph);
        }

        /// <summary>
        /// L'anneau n'apparait qu'au developpement maximal — voir
        /// <see cref="SystemGlyph.FullyDevelopedLevel"/> pour la raison.
        /// </summary>
        private void ApplyDevelopmentRing(int count)
        {
            if (count <= 0)
            {
                if (_developmentRing != null)
                {
                    _developmentRing.enabled = false;
                }

                return;
            }

            if (_developmentRing == null)
            {
                _developmentRing = BuildDevelopmentRing();
            }

            _developmentRing.enabled = true;
        }

        private SpriteRenderer BuildDevelopmentRing()
        {
            var ringObject = new GameObject("DevelopmentRing");
            ringObject.transform.SetParent(transform, worldPositionStays: false);
            ringObject.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            ringObject.transform.localScale =
                new Vector3(SystemGlyph.DevelopmentRingScale, SystemGlyph.DevelopmentRingScale, 1f);

            var renderer = ringObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetThinRingSprite();
            renderer.color = new Color(
                DevelopmentRingColor.r, DevelopmentRingColor.g, DevelopmentRingColor.b, DevelopmentRingOpacity);

            return renderer;
        }

        /// <summary>
        /// Halo de stabilite, place <b>derriere</b> tout le reste. Il n'existe que sur les
        /// systemes possedes : voir la regle de confidentialite dans <see cref="SystemGlyph"/>.
        /// </summary>
        private void ApplyHalo(SystemGlyph glyph)
        {
            if (!glyph.RevealsInternalState || glyph.HaloOpacity <= 0f)
            {
                if (_halo != null)
                {
                    _halo.enabled = false;
                }

                return;
            }

            if (_halo == null)
            {
                var haloObject = new GameObject("StabilityHalo");
                haloObject.transform.SetParent(transform, worldPositionStays: false);
                haloObject.transform.localPosition = new Vector3(0f, 0f, 0.03f);
                haloObject.transform.localScale = new Vector3(SystemGlyph.HaloScale, SystemGlyph.HaloScale, 1f);

                _halo = haloObject.AddComponent<SpriteRenderer>();
                _halo.sprite = RuntimeSpriteFactory.GetAtmosphereSprite();
            }

            _halo.enabled = true;
            _halo.color = new Color(glyph.HaloColor.r, glyph.HaloColor.g, glyph.HaloColor.b, glyph.HaloOpacity);
        }

        /// <summary>
        /// Pastilles de garnison, disposees en eventail sous le systeme — jamais au-dessus, ou
        /// elles se confondraient avec les lunes decoratives de la Phase 12.
        /// </summary>
        private void ApplyGarrisonPips(SystemGlyph glyph)
        {
            int count = glyph.RevealsInternalState ? glyph.GarrisonPips : 0;

            for (int i = _garrisonPips.Count; i < count; i++)
            {
                _garrisonPips.Add(BuildGarrisonPip(i));
            }

            for (int i = 0; i < _garrisonPips.Count; i++)
            {
                bool visible = i < count;
                _garrisonPips[i].enabled = visible;

                if (!visible)
                {
                    continue;
                }

                bool isOverflowMarker = glyph.GarrisonExceedsPips && i == count - 1;
                _garrisonPips[i].color = isOverflowMarker ? GarrisonOverflowColor : GarrisonPipColor;
            }

            LayOutGarrisonPips(count);
        }

        /// <summary>
        /// Repartit les pastilles visibles sur un arc centre vers le bas, recalcule a chaque
        /// changement : trois pastilles doivent rester centrees, pas occuper les trois premieres
        /// places d'un eventail de cinq.
        /// </summary>
        private void LayOutGarrisonPips(int count)
        {
            if (count <= 0)
            {
                return;
            }

            const float downwards = -90f;
            float step = count > 1 ? SystemGlyph.GarrisonPipArc / (count - 1) : 0f;
            float start = downwards - (count > 1 ? SystemGlyph.GarrisonPipArc * 0.5f : 0f);

            for (int i = 0; i < count; i++)
            {
                float radians = (start + step * i) * Mathf.Deg2Rad;
                _garrisonPips[i].transform.localPosition = new Vector3(
                    Mathf.Cos(radians) * SystemGlyph.GarrisonPipOrbit,
                    Mathf.Sin(radians) * SystemGlyph.GarrisonPipOrbit,
                    -0.02f);
            }
        }

        private SpriteRenderer BuildGarrisonPip(int index)
        {
            var pipObject = new GameObject($"GarrisonPip{index}");
            pipObject.transform.SetParent(transform, worldPositionStays: false);
            pipObject.transform.localScale = Vector3.one * SystemGlyph.GarrisonPipScale;

            var renderer = pipObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetCircleSprite();
            renderer.color = GarrisonPipColor;

            return renderer;
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
