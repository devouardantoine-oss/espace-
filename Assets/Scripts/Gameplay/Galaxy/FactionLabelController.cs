using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Empires;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Nom de chaque empire pose au cœur de son territoire, visible uniquement au zoom eloigne
    /// (Phase 19).
    /// <para>
    /// <b>Le pendant des zones, pas une decoration :</b> une carte politique se lit d'abord par
    /// ses noms de regions. Sans eux, six couleurs restent six couleurs — le joueur doit ouvrir
    /// un ecran pour savoir a qui il a affaire. Leur opacite suit exactement la courbe inverse
    /// de celle des noms de systeme (voir <see cref="TerritoryLevelOfDetail"/>) : les deux
    /// familles de libelles se relaient au lieu de se superposer.
    /// </para>
    /// <para>
    /// <b>Ancrage au centre de gravite des cellules, pondere par leur aire</b>, et non a la
    /// moyenne des positions des systemes : un empire tenant une grappe dense et un systeme
    /// lointain verrait sinon son nom derive vers le vide. L'ancre est recalculee quand un
    /// systeme change de maitre — c'est-a-dire quelques fois par partie.
    /// </para>
    /// <para>
    /// <b>Taille constante a l'ecran :</b> l'echelle du libelle est recalculee d'apres la taille
    /// orthographique de la camera. Un texte pose dans le monde grandirait avec le zoom et
    /// finirait par barrer l'ecran — un nom de region est une annotation de carte, pas un objet
    /// de la scene.
    /// </para>
    /// </summary>
    public sealed class FactionLabelController : MonoBehaviour
    {
        /// <summary>Profondeur Z des libelles : devant tout le reste de la carte.</summary>
        private const float LabelDepth = -0.03f;

        /// <summary>Resolution de rendu de la police. Sans effet sur la taille affichee (voir <see cref="ScreenHeightFraction"/>).</summary>
        private const int FontSize = 48;

        private const float CharacterSize = 1f;

        /// <summary>
        /// Hauteur monde d'une ligne a l'echelle 1, pour la combinaison
        /// <see cref="FontSize"/>/<see cref="CharacterSize"/> ci-dessus : <c>TextMesh</c> rend
        /// un caractere sur <c>fontSize * characterSize / 10</c> unites monde.
        /// </summary>
        private const float LineHeightAtUnitScale = FontSize * CharacterSize / 10f;

        /// <summary>Hauteur visee du nom, en fraction de la hauteur de l'ecran.</summary>
        private const float ScreenHeightFraction = 0.026f;

        /// <summary>En deca de cette opacite, les libelles sont eteints plutot que dessines transparents.</summary>
        private const float VisibilityEpsilon = 0.01f;

        private readonly List<FactionLabel> _labels = new List<FactionLabel>();

        private GalaxyMap _map;
        private IReadOnlyList<TerritoryCell> _cells;
        private GalaxyCameraController _cameraController;
        private Camera _camera;
        private EmpireRegistry _empireRegistry;

        private int[] _lastKnownOwnerIds;
        private bool _anchorsDirty = true;

        private sealed class FactionLabel
        {
            public int EmpireId;
            public TextMesh Text;
            public MeshRenderer Renderer;
            public Color Color;
        }

        /// <summary>
        /// Cable l'affichage. Appele une seule fois par <see cref="GalaxyMapController"/>.
        /// </summary>
        /// <param name="cells">Cellules de controle, <b>dans le meme ordre que <c>map.Systems</c></b>.</param>
        public void Initialize(
            GalaxyMap map,
            IReadOnlyList<TerritoryCell> cells,
            GalaxyCameraController cameraController,
            Camera camera)
        {
            _map = map;
            _cells = cells;
            _cameraController = cameraController;
            _camera = camera;

            if (_map == null || _cells == null || _cells.Count != _map.Systems.Count)
            {
                _cells = null;
                return;
            }

            _lastKnownOwnerIds = new int[_map.Systems.Count];
            for (int i = 0; i < _lastKnownOwnerIds.Length; i++)
            {
                _lastKnownOwnerIds[i] = StarSystemState.UnownedOwnerId;
            }
        }

        private void Update()
        {
            if (_cells == null || _cameraController == null || _camera == null)
            {
                return;
            }

            if (!EnsureLabelsBuilt())
            {
                return;
            }

            DetectOwnershipChanges();

            float alpha = TerritoryLevelOfDetail.FactionLabelAlphaFor(
                TerritoryLevelOfDetail.ZoomInFraction(
                    _camera.orthographicSize,
                    _cameraController.MinOrthographicSize,
                    _cameraController.MaxOrthographicSize));

            bool visible = alpha > VisibilityEpsilon;

            if (visible && _anchorsDirty)
            {
                UpdateAnchors();
                _anchorsDirty = false;
            }

            // Une seule echelle pour tous les libelles : ils partagent la meme taille a
            // l'ecran, donc la meme echelle monde.
            float scale = 2f * _camera.orthographicSize * ScreenHeightFraction / LineHeightAtUnitScale;

            foreach (FactionLabel label in _labels)
            {
                if (label.Renderer.enabled != visible)
                {
                    label.Renderer.enabled = visible;
                }

                if (!visible)
                {
                    continue;
                }

                label.Text.transform.localScale = Vector3.one * scale;
                label.Text.color = new Color(label.Color.r, label.Color.g, label.Color.b, alpha);
            }
        }

        /// <summary>
        /// Cree un libelle par empire, une fois le registre disponible (il n'existe qu'a partir
        /// du <c>Start</c> d'<c>EmpireController</c>).
        /// </summary>
        /// <returns>Vrai si les libelles sont prets a etre mis a jour.</returns>
        private bool EnsureLabelsBuilt()
        {
            if (_labels.Count > 0)
            {
                return true;
            }

            if (_empireRegistry == null && !ServiceLocator.TryGet(out _empireRegistry))
            {
                return false;
            }

            Font font = BuiltinFontLoader.Load();
            if (font == null)
            {
                GameLog.Warning("[Factions] Aucune police integree disponible : les noms de factions ne seront pas affiches.");
                _cells = null;
                return false;
            }

            var root = new GameObject("FactionLabels").transform;
            root.SetParent(transform, worldPositionStays: false);

            foreach (Empire empire in _empireRegistry.Empires)
            {
                var labelObject = new GameObject($"Faction_{empire.Name}");
                labelObject.transform.SetParent(root, worldPositionStays: false);

                var textMesh = labelObject.AddComponent<TextMesh>();
                textMesh.font = font;
                textMesh.fontSize = FontSize;
                textMesh.characterSize = CharacterSize;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.text = empire.Name.ToUpperInvariant();

                var renderer = labelObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = font.material;
                renderer.enabled = false;

                // Le nom porte la couleur de l'empire, eclaircie : la teinte pleine d'un empire
                // sombre serait illisible sur le fond spatial, la teinte blanche pure ferait
                // perdre l'association couleur/faction que la carte vient d'etablir.
                _labels.Add(new FactionLabel
                {
                    EmpireId = empire.Id,
                    Text = textMesh,
                    Renderer = renderer,
                    Color = Color.Lerp(empire.Color, Color.white, 0.55f),
                });
            }

            _anchorsDirty = true;
            return _labels.Count > 0;
        }

        private void DetectOwnershipChanges()
        {
            IReadOnlyList<StarSystemState> systems = _map.Systems;
            for (int i = 0; i < systems.Count; i++)
            {
                if (_lastKnownOwnerIds[i] == systems[i].OwnerId)
                {
                    continue;
                }

                _lastKnownOwnerIds[i] = systems[i].OwnerId;
                _anchorsDirty = true;
            }
        }

        /// <summary>
        /// Repositionne chaque nom au centre de gravite des cellules de son empire, pondere par
        /// leur aire. Un empire sans aucun systeme voit son libelle vide : il disparait de la
        /// carte sans que l'objet ait besoin d'etre detruit et recree si l'empire renait.
        /// </summary>
        private void UpdateAnchors()
        {
            IReadOnlyList<StarSystemState> systems = _map.Systems;

            foreach (FactionLabel label in _labels)
            {
                Vector2 weighted = Vector2.zero;
                float totalArea = 0f;

                for (int i = 0; i < systems.Count; i++)
                {
                    if (systems[i].OwnerId != label.EmpireId)
                    {
                        continue;
                    }

                    float area = _cells[i].Area;
                    weighted += _cells[i].Centroid * area;
                    totalArea += area;
                }

                if (totalArea <= 0f)
                {
                    label.Text.text = string.Empty;
                    continue;
                }

                Vector2 anchor = weighted / totalArea;
                label.Text.transform.position = new Vector3(anchor.x, anchor.y, LabelDepth);

                if (label.Text.text.Length == 0 &&
                    _empireRegistry.TryGetEmpire(label.EmpireId, out Empire empire))
                {
                    label.Text.text = empire.Name.ToUpperInvariant();
                }
            }
        }
    }
}
