using Espace.Core;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Une planete en rotation, avec atmosphere et terminateur, montee entierement au runtime
    /// (Phase 21.1).
    /// <para>
    /// <b>Trois elements, trois appels de rendu :</b> la sphere texturee, un halo derriere elle,
    /// une ombre devant. Aucune lumiere, aucun nuanceur maison — voir
    /// <see cref="RuntimeSpriteFactory.GetTerminatorSprite"/> pour la raison. Le budget est
    /// tenable sur telephone y compris si trois mondes s'affichent en meme temps.
    /// </para>
    /// <para>
    /// <b>Seul le globe tourne.</b> Le halo et l'ombre sont des enfants non tournants : ils
    /// suivent la position et l'echelle, jamais l'orientation. C'est ce qui donne l'illusion
    /// d'une etoile fixe eclairant une surface en mouvement.
    /// </para>
    /// <para>
    /// <b>Le materiau est choisi par degradation successive</b>, comme
    /// <c>TerritoryOverlayController</c> (Phase 19) : selon la configuration du pipeline de
    /// rendu, le nuanceur non eclaire d'URP peut ne pas etre present. Echouer sur un ecran de
    /// menu parce qu'un nuanceur manque serait un defaut invisible en editeur et fatal sur
    /// l'appareil.
    /// </para>
    /// </summary>
    public sealed class PlanetVisual : MonoBehaviour
    {
        /// <summary>Vitesse de rotation. Un tour en six minutes : perceptible si l'on regarde, jamais distrayant.</summary>
        private const float DefaultDegreesPerSecond = 1f;

        /// <summary>Taille du halo par rapport au globe. Au-dela, l'atmosphere se detache en anneau.</summary>
        private const float AtmosphereScale = 1.42f;

        /// <summary>Duree d'un cycle de respiration du halo, en secondes.</summary>
        private const float AtmospherePeriod = 7f;

        /// <summary>Amplitude de la respiration, en fraction de l'opacite de base.</summary>
        private const float AtmosphereBreath = 0.18f;

        /// <summary>Recul du halo et avancee de l'ombre, en unites monde, pour garantir l'ordre de rendu.</summary>
        private const float DepthOffset = 0.01f;

        /// <summary>Le sprite d'atmosphere occupe une unite monde a l'echelle 1 ; le globe fait 1 unite de diametre.</summary>
        private const float SpriteToSphereScale = 1f;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private Transform _globe;
        private SpriteRenderer _atmosphere;
        private SpriteRenderer _terminator;
        private Texture2D _surface;
        private Color _atmosphereColor = Color.white;
        private float _degreesPerSecond = DefaultDegreesPerSecond;

        /// <summary>Type du monde actuellement affiche. <c>null</c> tant que <see cref="Show"/> n'a pas ete appele.</summary>
        public PlanetKind? Kind { get; private set; }

        /// <summary>
        /// Affiche (ou remplace) le monde montre par ce composant.
        /// </summary>
        /// <param name="kind">Type de monde : determine la palette de surface et la teinte de l'atmosphere.</param>
        /// <param name="seed">Meme graine, meme monde. Utiliser l'identifiant du systeme pour qu'un systeme garde toujours le meme visage.</param>
        /// <param name="radius">Rayon en unites monde.</param>
        public void Show(PlanetKind kind, int seed, float radius)
        {
            EnsureBuilt();

            // La texture precedente est un objet natif : la laisser derriere soi la garderait en
            // memoire jusqu'a la fermeture du jeu, et cet ecran change de monde a chaque appui.
            if (_surface != null)
            {
                Destroy(_surface);
            }

            _surface = PlanetTextureFactory.CreateSurface(seed, kind);
            ApplySurfaceTexture(_surface);

            Kind = kind;
            _atmosphereColor = AtmosphereColorOf(kind);
            _atmosphere.color = _atmosphereColor;
            _atmosphere.enabled = kind != PlanetKind.Barren;

            SetRadius(radius);
        }

        /// <summary>Change le rayon sans regenerer la texture.</summary>
        public void SetRadius(float radius)
        {
            EnsureBuilt();

            float diameter = Mathf.Max(0.01f, radius) * 2f;
            _globe.localScale = Vector3.one * diameter;

            float spriteScale = diameter * SpriteToSphereScale;
            _atmosphere.transform.localScale = Vector3.one * spriteScale * AtmosphereScale;
            _terminator.transform.localScale = Vector3.one * spriteScale;
        }

        /// <summary>Vitesse de rotation, en degres par seconde. Negatif inverse le sens.</summary>
        public void SetRotationSpeed(float degreesPerSecond) => _degreesPerSecond = degreesPerSecond;

        private void Update()
        {
            if (_globe == null)
            {
                return;
            }

            // Temps reel : cette rotation est un element d'ambiance, elle n'a aucune raison de
            // suivre la vitesse de la simulation ni de s'arreter en pause.
            _globe.Rotate(Vector3.up, _degreesPerSecond * Time.unscaledDeltaTime, Space.Self);

            if (_atmosphere != null && _atmosphere.enabled)
            {
                float breath = 1f - AtmosphereBreath * UiPulse(Time.unscaledTime, AtmospherePeriod);
                Color color = _atmosphereColor;
                color.a *= breath;
                _atmosphere.color = color;
            }
        }

        private void OnDestroy()
        {
            if (_surface != null)
            {
                Destroy(_surface);
                _surface = null;
            }
        }

        private void EnsureBuilt()
        {
            if (_globe != null)
            {
                return;
            }

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Globe";

            // La primitive arrive avec un collisionneur dont personne n'a besoin, et qui
            // intercepterait les appuis destines a la selection.
            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            sphere.transform.SetParent(transform, worldPositionStays: false);

            // L'axe de rotation est incline : une planete dont l'equateur coincide exactement
            // avec l'horizontale de l'ecran a l'air d'un objet pose, pas d'un corps celeste.
            sphere.transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            _globe = sphere.transform;

            var renderer = sphere.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(FindSurfaceShader())
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _atmosphere = CreateOverlay("Atmosphere", RuntimeSpriteFactory.GetAtmosphereSprite(), -DepthOffset, sortingOrder: -1);
            _terminator = CreateOverlay("Terminator", RuntimeSpriteFactory.GetTerminatorSprite(), DepthOffset, sortingOrder: 1);
        }

        private SpriteRenderer CreateOverlay(string overlayName, Sprite sprite, float depth, int sortingOrder)
        {
            var host = new GameObject(overlayName);
            host.transform.SetParent(transform, worldPositionStays: false);
            host.transform.localPosition = new Vector3(0f, 0f, depth);

            var renderer = host.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void ApplySurfaceTexture(Texture2D texture)
        {
            Material material = _globe.GetComponent<MeshRenderer>().sharedMaterial;

            // Les deux noms coexistent : URP lit _BaseMap, les nuanceurs integres historiques
            // lisent _MainTex. Renseigner celui qui existe evite une planete uniformement blanche
            // selon le nuanceur qui a ete retenu.
            if (material.HasProperty(BaseMapId))
            {
                material.SetTexture(BaseMapId, texture);
            }

            if (material.HasProperty(MainTexId))
            {
                material.SetTexture(MainTexId, texture);
            }
        }

        private static Shader FindSurfaceShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                return shader;
            }

            shader = Shader.Find("Unlit/Texture");
            if (shader != null)
            {
                return shader;
            }

            GameLog.Warning("[Planet] Aucun nuanceur non eclaire trouve : repli sur Sprites/Default.");
            return Shader.Find("Sprites/Default");
        }

        /// <summary>
        /// Oscillation douce entre 0 et 1. Dupliquee ici plutot que prise dans <c>UiEasing</c> :
        /// celui-ci vit dans <c>Espace.UI</c>, et <c>Espace.Gameplay</c> ne doit pas dependre de
        /// l'interface — la fleche va dans l'autre sens.
        /// </summary>
        private static float UiPulse(float timeSeconds, float periodSeconds)
        {
            return 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * timeSeconds / periodSeconds);
        }

        private static Color AtmosphereColorOf(PlanetKind kind)
        {
            switch (kind)
            {
                case PlanetKind.Ocean: return new Color(0.42f, 0.68f, 1f, 0.85f);
                case PlanetKind.Arid: return new Color(0.95f, 0.72f, 0.45f, 0.55f);
                case PlanetKind.Ice: return new Color(0.70f, 0.88f, 1f, 0.60f);
                case PlanetKind.Toxic: return new Color(0.66f, 0.85f, 0.35f, 0.70f);
                case PlanetKind.Barren: return new Color(0.5f, 0.5f, 0.5f, 0f);
                default: return new Color(0.45f, 0.72f, 1f, 0.75f);
            }
        }
    }
}
