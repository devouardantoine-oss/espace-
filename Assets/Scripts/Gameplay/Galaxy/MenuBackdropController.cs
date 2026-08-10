using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Empires;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Fond contemplatif du menu principal : la galaxie du jeu, survolee lentement par la
    /// camera (Phase 21.2, concept « galaxie vivante »).
    /// <para>
    /// <b>La vraie carte, pas une image d'accroche.</b> Le fond est engendre par le meme
    /// <see cref="GalaxyGenerator"/> et la meme graine que la partie qui commencera : le joueur
    /// regarde donc la galaxie ou il va reellement jouer. C'est la reponse la plus economique a
    /// « montrer l'univers » — tout ce qui s'affiche ici existe deja et est deja teste.
    /// </para>
    /// <para>
    /// <b>Aucun service, aucune simulation.</b> La galaxie d'apercu est un objet jetable,
    /// exactement comme celui que <c>FactionPickerController</c> regenere depuis la Phase 13.
    /// Le menu n'enregistre rien, ne fait avancer aucune horloge et ne pilote aucun empire :
    /// il dessine.
    /// </para>
    /// <para>
    /// <b>La camera est reconfiguree au runtime plutot que dans la scene.</b> La scene
    /// <c>Bootstrap</c> porte une camera en perspective heritee de la Phase 1 ; la retailler
    /// dans le fichier de scene rendrait le menu dependant d'un reglage invisible depuis le
    /// code. La configuration est donc posee ici, ou elle se lit — et l'ancien etat est restaure
    /// a la destruction, pour ne rien casser si la scene sert a autre chose plus tard.
    /// </para>
    /// </summary>
    public sealed class MenuBackdropController : MonoBehaviour
    {
        /// <summary>Fraction du rayon galactique visible en hauteur. En dessous de 1, la camera est « dans » la galaxie plutot qu'au-dessus.</summary>
        private const float VisibleRadiusFraction = 0.62f;

        /// <summary>Duree d'un trajet de camera d'un point d'interet au suivant, en secondes.</summary>
        private const float DriftSeconds = 14f;

        /// <summary>Temps d'arret sur un point d'interet avant de repartir, en secondes.</summary>
        private const float DwellSeconds = 3f;

        /// <summary>Distance maximale du centre, en fraction du rayon, ou la camera peut se poser. Au-dela elle cadrerait du vide.</summary>
        private const float DriftRadiusFraction = 0.42f;

        /// <summary>Profondeur de la camera. Plus loin que le fond stellaire, qui est a 2.</summary>
        private const float CameraDepth = -12f;

        /// <summary>Rayon d'un marqueur de systeme, en unites monde.</summary>
        private const float SystemRadius = 0.34f;

        /// <summary>Grossissement applique au marqueur d'une capitale de faction, pour qu'elle se distingue du fond.</summary>
        private const float HomeSystemScale = 2.1f;

        /// <summary>Periode de pulsation des capitales, en secondes.</summary>
        private const float HomePulseSeconds = 4.5f;

        private Camera _camera;
        private Transform _root;

        /// <summary>Etat de la camera avant reconfiguration, restaure a la destruction.</summary>
        private bool _cameraWasOrthographic;
        private float _cameraOriginalSize;
        private Vector3 _cameraOriginalPosition;
        private CameraClearFlags _cameraOriginalClearFlags;
        private Color _cameraOriginalBackground;
        private bool _cameraCaptured;

        private readonly List<SpriteRenderer> _homeMarkers = new List<SpriteRenderer>(8);
        private readonly List<Color> _homeColors = new List<Color>(8);

        private Vector2 _driftFrom;
        private Vector2 _driftTo;
        private float _driftElapsed;
        private float _galaxyRadius = 1f;
        private System.Random _random;

        /// <summary>Nom de la region actuellement survolee, affiche par le menu. Vide tant que rien n'est construit.</summary>
        public string FocusedRegionName { get; private set; } = string.Empty;

        /// <summary>Vrai si le fond a pu etre construit. Faux si la configuration manquait.</summary>
        public bool IsBuilt { get; private set; }

        /// <summary>
        /// Engendre la galaxie d'apercu et met en place le decor.
        /// </summary>
        /// <param name="config">Memes parametres que la partie a venir : sans lui, le fond montrerait une autre galaxie que celle du jeu.</param>
        /// <param name="empires">Roster, pour marquer les capitales de leur couleur. Peut etre vide.</param>
        public void Build(GalaxyConfig config, IReadOnlyList<EmpireDefinition> empires)
        {
            if (config == null)
            {
                GameLog.Warning("[MenuBackdrop] GalaxyConfig absent : le menu s'affichera sur un fond uni.");
                return;
            }

            GalaxyGenerationParameters parameters = config.ToGenerationParameters();
            GalaxyMap map = GalaxyGenerator.Generate(parameters);

            _galaxyRadius = Mathf.Max(1f, parameters.GalaxyRadius);
            _random = new System.Random(parameters.Seed);

            _root = new GameObject("MenuBackdrop").transform;
            _root.SetParent(transform, worldPositionStays: false);

            GalaxyBackgroundFactory.Build(_root.gameObject, parameters);

            Dictionary<StarSystemId, Vector3> worldPositions = BuildSystemMarkers(map, parameters.Seed);
            BuildLinks(map, worldPositions);
            MarkHomeSystems(map, empires, worldPositions);

            ConfigureCamera();
            StartNewDrift(immediate: true);

            IsBuilt = true;
        }

        private Dictionary<StarSystemId, Vector3> BuildSystemMarkers(GalaxyMap map, int seed)
        {
            var systemsRoot = new GameObject("Systems").transform;
            systemsRoot.SetParent(_root, worldPositionStays: false);

            var worldPositions = new Dictionary<StarSystemId, Vector3>(map.Systems.Count);
            Sprite disc = RuntimeSpriteFactory.GetCircleSprite();

            foreach (StarSystemState system in map.Systems)
            {
                var position = new Vector3(system.Position.x, system.Position.y, 0f);
                worldPositions[system.Id] = position;

                // Le meme profil visuel que la carte de jeu : un systeme garde donc son
                // apparence entre le menu et la partie, ce qui n'est pas un detail — c'est ce
                // qui fait que le fond du menu « est » la galaxie et non une illustration.
                StarSystemVisualProfile profile = StarSystemVisualProfile.Compute(system, seed);

                var marker = new GameObject($"System_{system.Name}");
                marker.transform.SetParent(systemsRoot, worldPositionStays: false);
                marker.transform.position = position;
                marker.transform.localScale = Vector3.one * SystemRadius * 2f * profile.SizeFactor;

                var renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = disc;
                renderer.color = profile.BodyColor;
            }

            return worldPositions;
        }

        private void BuildLinks(GalaxyMap map, IReadOnlyDictionary<StarSystemId, Vector3> worldPositions)
        {
            var linksObject = new GameObject("Hyperlanes");
            linksObject.transform.SetParent(_root, worldPositionStays: false);
            linksObject.AddComponent<GalaxyLinkRenderer>().Build(map, worldPositions);
        }

        /// <summary>
        /// Pose un halo de la couleur de chaque faction sur sa capitale.
        /// <para>
        /// <b>Des halos, pas des territoires.</b> Une galaxie qui vient d'etre engendree n'a
        /// aucun proprietaire : dessiner des zones d'influence reviendrait a inventer un etat de
        /// partie qui n'existe pas. Les emplacements de depart, eux, sont bien reels — ils
        /// sortent du meme <see cref="EmpirePlacement"/> que la partie utilisera. Les six
        /// couleurs apparaissent donc a l'ecran sans qu'on ait rien fabrique.
        /// </para>
        /// </summary>
        private void MarkHomeSystems(GalaxyMap map, IReadOnlyList<EmpireDefinition> empires, IReadOnlyDictionary<StarSystemId, Vector3> worldPositions)
        {
            if (empires == null || empires.Count == 0)
            {
                return;
            }

            StarSystemId[] homes = EmpirePlacement.ChooseHomeSystems(map, empires.Count);
            var haloRoot = new GameObject("Capitals").transform;
            haloRoot.SetParent(_root, worldPositionStays: false);

            Sprite halo = RuntimeSpriteFactory.GetAtmosphereSprite();

            for (int i = 0; i < homes.Length && i < empires.Count; i++)
            {
                if (empires[i] == null || !worldPositions.TryGetValue(homes[i], out Vector3 position))
                {
                    continue;
                }

                var marker = new GameObject($"Capital_{empires[i].DisplayName}");
                marker.transform.SetParent(haloRoot, worldPositionStays: false);
                marker.transform.position = new Vector3(position.x, position.y, 0.05f);
                marker.transform.localScale = Vector3.one * SystemRadius * 2f * HomeSystemScale;

                var renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = halo;
                renderer.color = empires[i].Color;

                _homeMarkers.Add(renderer);
                _homeColors.Add(empires[i].Color);
            }
        }

        private void ConfigureCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                GameLog.Warning("[MenuBackdrop] Aucune camera principale : le fond ne sera pas cadre.");
                return;
            }

            _cameraWasOrthographic = _camera.orthographic;
            _cameraOriginalSize = _camera.orthographicSize;
            _cameraOriginalPosition = _camera.transform.position;
            _cameraOriginalClearFlags = _camera.clearFlags;
            _cameraOriginalBackground = _camera.backgroundColor;
            _cameraCaptured = true;

            _camera.orthographic = true;
            _camera.orthographicSize = _galaxyRadius * VisibleRadiusFraction;
            _camera.transform.rotation = Quaternion.identity;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.015f, 0.018f, 0.035f, 1f);
        }

        /// <summary>
        /// Choisit un nouveau point d'interet et amorce la derive vers lui.
        /// </summary>
        /// <param name="immediate">Au premier appel, la camera se pose directement sur sa cible plutot que de deriver depuis le centre.</param>
        private void StartNewDrift(bool immediate)
        {
            Vector2 target = RandomPointOfInterest();

            _driftFrom = immediate ? target : _driftTo;
            _driftTo = target;
            _driftElapsed = immediate ? DriftSeconds : 0f;

            FocusedRegionName = RegionNameFor(target);
        }

        private Vector2 RandomPointOfInterest()
        {
            double angle = _random.NextDouble() * 2.0 * Mathf.PI;

            // Racine carree du tirage : sans elle, les points s'agglutineraient au centre, la
            // surface d'une couronne croissant avec son rayon.
            double distance = System.Math.Sqrt(_random.NextDouble()) * _galaxyRadius * DriftRadiusFraction;

            return new Vector2(
                (float)(System.Math.Cos(angle) * distance),
                (float)(System.Math.Sin(angle) * distance));
        }

        /// <summary>
        /// Nomme la region observee d'apres l'angle du point vise. Purement decoratif : le jeu
        /// ne modelise aucune region, et en inventer une couche de donnees pour un libelle de
        /// menu serait payer cher un detail d'ambiance.
        /// </summary>
        private static string RegionNameFor(Vector2 point)
        {
            string[] arms = { "Bras de Persee", "Bras d'Orion", "Bras du Sagittaire", "Bordure exterieure", "Noyau galactique", "Bras de Norma" };

            if (point.sqrMagnitude < 1f)
            {
                return arms[4];
            }

            float angle = Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            int index = Mathf.Clamp(Mathf.FloorToInt(angle / 60f), 0, arms.Length - 1);
            return arms[index];
        }

        private void Update()
        {
            if (!IsBuilt)
            {
                return;
            }

            // Temps reel : le menu n'a pas d'horloge de jeu, et il ne doit pas se figer si une
            // partie precedente a laisse la simulation en pause.
            float deltaTime = Time.unscaledDeltaTime;

            DriftCamera(deltaTime);
            PulseCapitals();
        }

        private void DriftCamera(float deltaTime)
        {
            if (_camera == null)
            {
                return;
            }

            _driftElapsed += deltaTime;

            float progress = Mathf.Clamp01(_driftElapsed / DriftSeconds);
            float eased = progress * progress * (3f - 2f * progress);
            Vector2 position = Vector2.Lerp(_driftFrom, _driftTo, eased);

            _camera.transform.position = new Vector3(position.x, position.y, CameraDepth);

            if (_driftElapsed >= DriftSeconds + DwellSeconds)
            {
                StartNewDrift(immediate: false);
            }
        }

        private void PulseCapitals()
        {
            float pulse = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * Time.unscaledTime / HomePulseSeconds);

            for (int i = 0; i < _homeMarkers.Count; i++)
            {
                if (_homeMarkers[i] == null)
                {
                    continue;
                }

                Color color = _homeColors[i];
                color.a = Mathf.Lerp(0.45f, 0.95f, pulse);
                _homeMarkers[i].color = color;
            }
        }

        private void OnDestroy()
        {
            if (!_cameraCaptured || _camera == null)
            {
                return;
            }

            _camera.orthographic = _cameraWasOrthographic;
            _camera.orthographicSize = _cameraOriginalSize;
            _camera.transform.position = _cameraOriginalPosition;
            _camera.clearFlags = _cameraOriginalClearFlags;
            _camera.backgroundColor = _cameraOriginalBackground;
        }
    }
}
