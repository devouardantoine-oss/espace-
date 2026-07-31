using System.Collections.Generic;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Nom (puis details) des systemes affiches automatiquement selon le niveau de zoom, sans
    /// avoir a toucher un systeme (Phase 12) : zoom eloigne = points seuls, zoom moyen = noms,
    /// zoom proche = noms + une ligne de statistiques.
    /// <para>
    /// <b>Sondage du zoom courant (<see cref="Update"/>), pas de nouvel evenement :</b>
    /// <see cref="GalaxyCameraController"/> n'expose aujourd'hui aucun evenement de changement
    /// de zoom ; lui en ajouter un pour ce seul besoin coderait un couplage pour un autre. Le
    /// zoom est lu chaque frame et compare a des seuils relatifs (fraction de l'intervalle
    /// [zoom le plus proche, zoom le plus eloigne] exposee par
    /// <see cref="GalaxyCameraController.MinOrthographicSize"/>/<see cref="GalaxyCameraController.MaxOrthographicSize"/>),
    /// pour rester correct quelle que soit la taille de la galaxie configuree.
    /// </para>
    /// <para>
    /// <b><c>TextMesh</c> (legacy, integre au moteur), pas TextMeshPro :</b> aucun composant de
    /// texte n'existait dans le projet avant cette phase. TMP Pro exige d'importer ses
    /// « Essential Resources », une etape d'editeur risquee a effectuer a l'aveugle dans cet
    /// environnement sans acces a Unity. <c>TextMesh</c> utilise la police integree du moteur
    /// (<c>Resources.GetBuiltinResource&lt;Font&gt;</c>) sans aucun asset a importer.
    /// </para>
    /// </summary>
    public sealed class SystemLabelController : MonoBehaviour
    {
        /// <summary>
        /// Fraction (0 = zoom le plus eloigne, 1 = zoom le plus proche) au-dela de laquelle les
        /// noms apparaissent.
        /// </summary>
        private const float NameVisibilityThreshold = 0.35f;

        /// <summary>Fraction au-dela de laquelle la ligne de statistiques s'ajoute au nom.</summary>
        private const float DetailVisibilityThreshold = 0.7f;

        private const float LabelYOffset = 0.9f;
        private const float LabelDepth = -0.02f;
        private const float CharacterSize = 0.28f;
        private const int FontSize = 48;

        private readonly Dictionary<StarSystemId, TextMesh> _labels = new Dictionary<StarSystemId, TextMesh>();
        private readonly Dictionary<StarSystemId, MeshRenderer> _labelRenderers = new Dictionary<StarSystemId, MeshRenderer>();

        private GalaxyMap _map;
        private GalaxyCameraController _cameraController;
        private Camera _camera;

        /// <summary>
        /// Cree un label (initialement masque) par systeme. Appele une seule fois par
        /// <see cref="GalaxyMapController"/> a la generation de la carte.
        /// </summary>
        public void Initialize(GalaxyMap map, IReadOnlyDictionary<StarSystemId, Vector3> worldPositions, GalaxyCameraController cameraController, Camera camera)
        {
            _map = map;
            _cameraController = cameraController;
            _camera = camera;

            Font builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var labelRoot = new GameObject("SystemLabels").transform;
            labelRoot.SetParent(transform, worldPositionStays: false);

            foreach (StarSystemState system in map.Systems)
            {
                if (!worldPositions.TryGetValue(system.Id, out Vector3 position))
                {
                    continue;
                }

                var labelObject = new GameObject($"Label_{system.Name}");
                labelObject.transform.SetParent(labelRoot, worldPositionStays: false);
                labelObject.transform.position = new Vector3(position.x, position.y + LabelYOffset, LabelDepth);

                var textMesh = labelObject.AddComponent<TextMesh>();
                textMesh.font = builtinFont;
                textMesh.fontSize = FontSize;
                textMesh.characterSize = CharacterSize;
                textMesh.anchor = TextAnchor.LowerCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.color = new Color(0.88f, 0.92f, 0.98f, 0.9f);
                textMesh.text = string.Empty;

                var renderer = labelObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = builtinFont.material;
                renderer.enabled = false;

                _labels[system.Id] = textMesh;
                _labelRenderers[system.Id] = renderer;
            }
        }

        private void Update()
        {
            if (_map == null || _cameraController == null || _camera == null)
            {
                return;
            }

            float zoomInFraction = Mathf.InverseLerp(
                _cameraController.MaxOrthographicSize,
                _cameraController.MinOrthographicSize,
                _camera.orthographicSize);

            bool showNames = zoomInFraction >= NameVisibilityThreshold;
            bool showDetails = zoomInFraction >= DetailVisibilityThreshold;

            foreach (StarSystemState system in _map.Systems)
            {
                if (!_labels.TryGetValue(system.Id, out TextMesh label) || !_labelRenderers.TryGetValue(system.Id, out MeshRenderer renderer))
                {
                    continue;
                }

                renderer.enabled = showNames;
                if (!showNames)
                {
                    continue;
                }

                label.text = showDetails
                    ? $"{system.Name}\nPop {system.Population}  Dev {system.DevelopmentLevel}/5"
                    : system.Name;
            }
        }
    }
}
