using System.Collections.Generic;
using Espace.Core;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Nom (puis details) des systemes, affiches selon le niveau de zoom.
    /// <para>
    /// <b>Taille constante a l'ecran (Phase 20).</b> Les libelles etaient dimensionnes en unites
    /// monde : ils grossissaient donc avec le zoom, et au zoom rapproche un seul nom barrait le
    /// tiers de l'ecran par-dessus ses voisins. L'echelle est desormais recalculee d'apres la
    /// taille orthographique de la camera — un nom de systeme est une annotation de carte, pas un
    /// objet de la scene.
    /// </para>
    /// <para>
    /// <b>Anti-chevauchement (Phase 20).</b> Cent noms affiches sans arbitrage se recouvraient
    /// dans les amas, au point de rendre les deux illisibles. Les libelles sont desormais places
    /// du plus important au moins important, et celui qui empieterait sur un deja place est
    /// simplement omis. Perdre un nom vaut mieux qu'en rendre deux illisibles.
    /// </para>
    /// <para>
    /// <b>Le texte n'est reecrit que s'il a change.</b> L'ancienne version reconstruisait cent
    /// chaines par frame des que les details etaient visibles — environ 6 Ko jetes au
    /// ramasse-miettes a chaque image, soit des micro-saccades sur telephone.
    /// </para>
    /// <para>
    /// <b>Sondage du zoom courant, pas de nouvel evenement :</b> <see cref="GalaxyCameraController"/>
    /// n'expose aucun evenement de changement de zoom ; lui en ajouter un pour ce seul besoin
    /// coderait un couplage pour un autre. Les seuils sont relatifs a l'intervalle de zoom, donc
    /// corrects quelle que soit la taille de galaxie configuree.
    /// </para>
    /// <para>
    /// <b><c>TextMesh</c> (integre au moteur), pas TextMeshPro :</b> TMP exige d'importer ses
    /// « Essential Resources », une etape d'editeur risquee a effectuer sans acces a Unity. La
    /// police integree ne demande aucun asset (voir <see cref="BuiltinFontLoader"/>).
    /// </para>
    /// </summary>
    public sealed class SystemLabelController : MonoBehaviour
    {
        /// <summary>Fraction de zoom (0 = le plus eloigne, 1 = le plus proche) au-dela de laquelle les noms apparaissent.</summary>
        private const float NameVisibilityThreshold = 0.30f;

        /// <summary>Fraction au-dela de laquelle les systemes secondaires recoivent aussi leur nom.</summary>
        private const float MinorSystemThreshold = 0.48f;

        /// <summary>Fraction au-dela de laquelle la ligne de statistiques s'ajoute au nom.</summary>
        private const float DetailVisibilityThreshold = 0.72f;

        /// <summary>Hauteur visee d'une ligne de texte, en fraction de la hauteur de l'ecran.</summary>
        private const float ScreenHeightFraction = 0.021f;

        private const int FontSize = 48;
        private const float CharacterSize = 1f;

        /// <summary>Hauteur monde d'une ligne a l'echelle 1 : <c>TextMesh</c> rend un caractere sur <c>fontSize * characterSize / 10</c> unites.</summary>
        private const float LineHeightAtUnitScale = FontSize * CharacterSize / 10f;

        /// <summary>Decalage du libelle au-dessus du marqueur, en fraction de la demi-hauteur visible.</summary>
        private const float VerticalOffsetFraction = 0.028f;

        /// <summary>Largeur moyenne d'un caractere rapportee a sa hauteur, pour estimer l'encombrement d'un libelle.</summary>
        private const float CharacterWidthRatio = 0.55f;

        private const float LabelDepth = -0.02f;

        private readonly Dictionary<StarSystemId, TextMesh> _labels = new Dictionary<StarSystemId, TextMesh>();
        private readonly Dictionary<StarSystemId, MeshRenderer> _labelRenderers = new Dictionary<StarSystemId, MeshRenderer>();

        /// <summary>Systemes tries par importance decroissante. Calcule une fois : ni la position ni le developpement initial ne changent l'ordre en cours de partie.</summary>
        private StarSystemState[] _byImportance;

        /// <summary>Rectangles ecran deja occupes cette frame, pour l'anti-chevauchement.</summary>
        private readonly List<Rect> _occupied = new List<Rect>(128);

        private GalaxyMap _map;
        private GalaxyCameraController _cameraController;
        private Camera _camera;

        /// <summary>Cree un libelle (initialement masque) par systeme. Appele une seule fois a la generation de la carte.</summary>
        public void Initialize(GalaxyMap map, IReadOnlyDictionary<StarSystemId, Vector3> worldPositions, GalaxyCameraController cameraController, Camera camera)
        {
            _map = map;
            _cameraController = cameraController;
            _camera = camera;

            Font builtinFont = BuiltinFontLoader.Load();
            if (builtinFont == null)
            {
                GameLog.Warning("[Labels] Aucune police integree disponible : les noms de systemes ne seront pas affiches.");
                return;
            }

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
                labelObject.transform.position = new Vector3(position.x, position.y, LabelDepth);

                var textMesh = labelObject.AddComponent<TextMesh>();
                textMesh.font = builtinFont;
                textMesh.fontSize = FontSize;
                textMesh.characterSize = CharacterSize;
                textMesh.anchor = TextAnchor.LowerCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.color = new Color(0.88f, 0.92f, 0.98f, 0.92f);
                textMesh.text = system.Name;

                var renderer = labelObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = builtinFont.material;
                renderer.enabled = false;

                _labels[system.Id] = textMesh;
                _labelRenderers[system.Id] = renderer;
            }

            BuildImportanceOrder(map);
        }

        /// <summary>
        /// Ordonne les systemes du plus important au moins important. C'est cet ordre qui decide
        /// qui garde son nom quand deux libelles se disputent la meme place : une capitale
        /// developpee prime sur un caillou vide.
        /// </summary>
        private void BuildImportanceOrder(GalaxyMap map)
        {
            _byImportance = new StarSystemState[map.Systems.Count];
            for (int i = 0; i < map.Systems.Count; i++)
            {
                _byImportance[i] = map.Systems[i];
            }

            System.Array.Sort(_byImportance, (a, b) =>
            {
                int byImportance = Importance(b).CompareTo(Importance(a));
                // Depart sur l'identifiant : l'ordre reste total, donc le meme nom disparait
                // d'une frame a l'autre plutot que de clignoter entre deux voisins a egalite.
                return byImportance != 0 ? byImportance : a.Id.Value.CompareTo(b.Id.Value);
            });
        }

        private static int Importance(StarSystemState system) =>
            (system.OwnerId != StarSystemState.UnownedOwnerId ? 100 : 0) + system.DevelopmentLevel * 10 + system.Population / 500;

        private void Update()
        {
            if (_map == null || _cameraController == null || _camera == null || _byImportance == null)
            {
                return;
            }

            float zoomInFraction = Mathf.InverseLerp(
                _cameraController.MaxOrthographicSize,
                _cameraController.MinOrthographicSize,
                _camera.orthographicSize);

            if (zoomInFraction < NameVisibilityThreshold)
            {
                HideAll();
                return;
            }

            bool showMinorSystems = zoomInFraction >= MinorSystemThreshold;
            bool showDetails = zoomInFraction >= DetailVisibilityThreshold;

            float orthographicSize = _camera.orthographicSize;
            float worldLineHeight = 2f * orthographicSize * ScreenHeightFraction;
            float scale = worldLineHeight / LineHeightAtUnitScale;
            float verticalOffset = orthographicSize * VerticalOffsetFraction;

            _occupied.Clear();

            foreach (StarSystemState system in _byImportance)
            {
                if (!_labels.TryGetValue(system.Id, out TextMesh label)
                    || !_labelRenderers.TryGetValue(system.Id, out MeshRenderer renderer))
                {
                    continue;
                }

                bool minor = system.OwnerId == StarSystemState.UnownedOwnerId && system.DevelopmentLevel < 3;
                if (minor && !showMinorSystems)
                {
                    renderer.enabled = false;
                    continue;
                }

                string text = showDetails
                    ? $"{system.Name}\n{system.Population} M · {system.DevelopmentLevel}/5"
                    : system.Name;

                // Reecrit seulement si necessaire : TextMesh reconstruit son maillage a chaque
                // affectation, meme pour une chaine identique.
                if (!string.Equals(label.text, text))
                {
                    label.text = text;
                }

                Vector2 position = system.Position;
                if (!TryReserve(position, text, worldLineHeight, showDetails ? 2 : 1))
                {
                    renderer.enabled = false;
                    continue;
                }

                label.transform.position = new Vector3(position.x, position.y + verticalOffset, LabelDepth);
                label.transform.localScale = Vector3.one * scale;
                renderer.enabled = true;
            }
        }

        /// <summary>
        /// Reserve la place du libelle si elle est libre. Le rectangle est estime en unites
        /// monde a partir du nombre de caracteres — une mesure exacte demanderait de sonder le
        /// maillage de <c>TextMesh</c>, bien plus couteux pour un gain invisible a l'œil.
        /// </summary>
        private bool TryReserve(Vector2 position, string text, float lineHeight, int lineCount)
        {
            int longestLine = 0;
            int current = 0;
            foreach (char c in text)
            {
                if (c == '\n')
                {
                    current = 0;
                    continue;
                }

                current++;
                if (current > longestLine)
                {
                    longestLine = current;
                }
            }

            float width = longestLine * lineHeight * CharacterWidthRatio;
            float height = lineCount * lineHeight;
            var box = new Rect(position.x - width * 0.5f, position.y, width, height);

            for (int i = 0; i < _occupied.Count; i++)
            {
                if (_occupied[i].Overlaps(box))
                {
                    return false;
                }
            }

            _occupied.Add(box);
            return true;
        }

        private void HideAll()
        {
            foreach (MeshRenderer renderer in _labelRenderers.Values)
            {
                if (renderer.enabled)
                {
                    renderer.enabled = false;
                }
            }
        }
    }
}
