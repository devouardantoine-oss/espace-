using Espace.Core;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Save;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Menu principal, seul contenu de la scene <c>Bootstrap</c> (refondu en Phase 21.2,
    /// concept « galaxie vivante »).
    /// <para>
    /// <b>Le fond est la carte du jeu</b>, survolee lentement par la camera : voir
    /// <see cref="MenuBackdropController"/>. Le menu lui-meme est un panneau holographique pose
    /// sur le tiers gauche, la ou la galaxie est la moins dense. Ce partage — la scene rend le
    /// decor, IMGUI dessine les panneaux par-dessus — est celui deja en place sur la carte
    /// galactique depuis la Phase 12 ; les ecrans d'ouverture n'inventent rien.
    /// </para>
    /// <para>
    /// <b>Le decor est monte au runtime, pas pose dans la scene.</b> Le projet est developpe
    /// sans acces a l'editeur (voir le README) : un decor compose d'objets de scene ne serait
    /// verifiable qu'en ouvrant Unity, alors qu'un decor monte par code se lit et se teste.
    /// </para>
    /// <para>
    /// <b>« Continuer » charge directement la galaxie</b> : la sauvegarde est reprise telle
    /// quelle par <c>SaveController</c>. « Nouvelle partie » ouvre l'ecran de choix de faction
    /// (<see cref="FactionPickerController"/>, meme GameObject <c>[UI]</c>), qui se charge de
    /// supprimer la sauvegarde et de remettre l'horloge a zero — ces effets n'ont de sens
    /// qu'une fois les choix faits.
    /// </para>
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Tooltip("Meme asset que FactionPickerController : le fond du menu doit montrer la galaxie ou la partie se deroulera.")]
        [SerializeField]
        private GalaxyConfig galaxyConfig;

        [Tooltip("Meme roster que FactionPickerController : sert a marquer les capitales de leur couleur sur le fond.")]
        [SerializeField]
        private EmpireDefinition[] empireDefinitions = System.Array.Empty<EmpireDefinition>();

        private const string GalaxyMapSceneName = "GalaxyMap";

        /// <summary>Largeur du panneau, en unites d'interface. Un tiers des 700 unites garanties, plus la marge.</summary>
        private const int PanelWidth = 252;

        /// <summary>Marge du panneau aux bords de l'ecran.</summary>
        private const int PanelMargin = 26;

        /// <summary>Hauteur confortable d'une commande, quand la place le permet.</summary>
        private const int ButtonHeight = 36;

        /// <summary>
        /// Hauteur minimale d'une commande. En dessous, la cible tactile devient trop petite ;
        /// c'est donc l'en-tete qui se compacte, jamais les boutons qui disparaissent.
        /// </summary>
        private const int MinimumButtonHeight = 28;

        private const int ButtonGap = 6;
        private const int InnerPadding = 20;

        /// <summary>Hauteur du titre « ESPACE » en version confortable.</summary>
        private const int TitleHeight = 38;

        /// <summary>Hauteur du titre en version compacte, sans sous-titre.</summary>
        private const int CompactTitleHeight = 28;

        /// <summary>Espace entre le filet de separation et la premiere commande.</summary>
        private const int TitleToCommandsGap = 12;

        /// <summary>Duree d'apparition d'un element au chargement du menu, en secondes.</summary>
        private const float RevealSeconds = 0.45f;

        /// <summary>Retard entre deux elements de la cascade d'apparition, en secondes.</summary>
        private const float RevealStagger = 0.07f;

        /// <summary>Deplacement horizontal d'un element pendant son apparition, en unites d'interface.</summary>
        private const float RevealSlide = 22f;

        private FactionPickerController _factionPicker;
        private MenuBackdropController _backdrop;
        private float _openedAt;

        private void Awake()
        {
            _factionPicker = GetComponent<FactionPickerController>();
            _openedAt = Time.unscaledTime;

            var backdropHost = new GameObject("[MenuBackdrop]");
            _backdrop = backdropHost.AddComponent<MenuBackdropController>();
            _backdrop.Build(galaxyConfig, empireDefinitions);
        }

        private void OnDestroy()
        {
            if (_backdrop != null)
            {
                Destroy(_backdrop.gameObject);
            }
        }

        private void OnGUI()
        {
            UITheme.BeginScaledLayout();
            try
            {
                if (_factionPicker != null && _factionPicker.IsOpen)
                {
                    return;
                }

                DrawPanel();
                DrawRegionLabel();
            }
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        private void DrawPanel()
        {
            FactionPalette palette = FactionPalette.Neutral;
            float elapsed = Time.unscaledTime - _openedAt;

            float height = UITheme.ScreenHeight - PanelMargin * 2f;
            var panel = new Rect(PanelMargin, PanelMargin, PanelWidth, height);

            UiScreenRegions.Occupy(panel, UITheme.Scale);
            UITheme.DrawPanel(panel, palette);

            float x = panel.x + InnerPadding;
            float width = panel.width - InnerPadding * 2f;

            float contentTop = panel.y + InnerPadding;
            float contentBottom = panel.yMax - InnerPadding;

            int commandCount = ContinueEnabled ? 4 : 3;
            float gaps = (commandCount - 1) * ButtonGap;

            // Sur un telephone tres dense, l'echelle est bridee par la largeur et l'ecran ne
            // fait plus que 286 unites de haut : l'en-tete confortable et quatre commandes n'y
            // tiennent pas. On sacrifie alors le sous-titre, jamais une commande — meme regle
            // que la barre d'etat depuis la Phase 20. Un debordement de mise en page ne se
            // signale jamais dans IMGUI : il se lit a l'ecran, ou pas du tout.
            float fullHeader = TitleHeight + UITheme.CaptionHeight + 10f + 1f + TitleToCommandsGap;
            float compactHeader = CompactTitleHeight + 8f + 1f + TitleToCommandsGap;
            bool compact = contentBottom - contentTop - fullHeader < commandCount * MinimumButtonHeight + gaps;

            float commandsTop = contentTop + (compact ? compactHeader : fullHeader);

            float buttonHeight = Mathf.Clamp(
                (contentBottom - commandsTop - gaps) / commandCount,
                MinimumButtonHeight,
                ButtonHeight);

            // Ancrees au bas du panneau tant qu'il reste de la place : le pouce atteint
            // naturellement le bas de l'ecran, et la galaxie reste degagee en haut.
            float commandsHeight = commandCount * buttonHeight + gaps;
            float y = Mathf.Max(contentBottom - commandsHeight, commandsTop);

            DrawTitle(x, width, contentTop, palette, elapsed, compact);
            DrawCommands(x, width, y, buttonHeight, palette, elapsed);
        }

        /// <param name="compact">Sur un ecran trop court, le sous-titre est abandonne et le titre reduit (voir <see cref="DrawPanel"/>).</param>
        private void DrawTitle(float x, float width, float y, FactionPalette palette, float elapsed, bool compact)
        {
            float height = compact ? CompactTitleHeight : TitleHeight;

            var title = new GUIStyle(UITheme.Title)
            {
                fontSize = compact ? 22 : 30,
                normal = { textColor = palette.Text },
            };

            float slide = RevealSlide * (1f - UiEasing.StaggeredReveal(elapsed, 0, RevealStagger, RevealSeconds));
            GUI.Label(new Rect(x - slide, y, width, height), "ESPACE", title);

            float hairline = y + height + 8f;

            if (!compact)
            {
                var subtitle = new GUIStyle(UITheme.Caption)
                {
                    normal = { textColor = palette.AccentSoft },
                };

                GUI.Label(new Rect(x - slide, y + height, width, UITheme.CaptionHeight), "GRANDE STRATEGIE GALACTIQUE", subtitle);
                hairline = y + height + UITheme.CaptionHeight + 10f;
            }

            UITheme.DrawHairline(new Rect(x, hairline, width, 1f), palette.AccentAt(0.35f));
        }

        private void DrawCommands(float x, float width, float y, float buttonHeight, FactionPalette palette, float elapsed)
        {
            // L'index de cascade demarre a 1 : le titre occupe le rang 0.
            int rank = 1;

            if (DrawCommand(x, width, ref y, buttonHeight, palette, elapsed, rank++, "NOUVELLE PARTIE", primary: true, enabled: true))
            {
                StartNewGame();
            }

            if (ContinueEnabled && DrawCommand(x, width, ref y, buttonHeight, palette, elapsed, rank++, "CONTINUER", primary: false, enabled: true))
            {
                LoadGalaxyMap();
            }

            // Grise plutot qu'absent : une commande qui apparait et disparait d'une session a
            // l'autre deplace toutes les autres, et le joueur perd ses reperes.
            if (DrawCommand(x, width, ref y, buttonHeight, palette, elapsed, rank++, "PARAMETRES", primary: false, enabled: false))
            {
                // Ouvert a l'etape suivante de la refonte.
            }

            if (DrawCommand(x, width, ref y, buttonHeight, palette, elapsed, rank, "QUITTER", primary: false, enabled: true))
            {
                Application.Quit();
            }
        }

        /// <summary>
        /// Dessine une commande et avance <paramref name="y"/>. Renvoie <c>true</c> si elle a
        /// ete pressee.
        /// </summary>
        private bool DrawCommand(float x, float width, ref float y, float buttonHeight, FactionPalette palette, float elapsed, int rank, string label, bool primary, bool enabled)
        {
            float reveal = UiEasing.StaggeredReveal(elapsed, rank, RevealStagger, RevealSeconds);
            var rect = new Rect(x - RevealSlide * (1f - reveal), y, width, buttonHeight);

            var style = new GUIStyle(UITheme.Button)
            {
                fontSize = UITheme.ValueFontSize,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(16, 10, 0, 0),
                normal =
                {
                    background = UITheme.SolidTexture(primary ? palette.AccentAt(0.18f) : new Color(1f, 1f, 1f, 0.035f)),
                    textColor = enabled ? (primary ? palette.Text : palette.TextMuted) : palette.AccentAt(0.28f),
                },
            };

            bool wasEnabled = GUI.enabled;
            GUI.enabled = enabled;
            bool pressed = GUI.Button(rect, label, style);
            GUI.enabled = wasEnabled;

            // Un liseré vertical a gauche plutot qu'un cadre complet : il marque la commande
            // principale sans l'enfermer dans une boite, ce qui ferait retomber le panneau dans
            // l'esthetique de boite de dialogue que la refonte cherche a quitter.
            if (primary && enabled)
            {
                GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), UITheme.SolidTexture(palette.Accent));
            }

            y += buttonHeight + ButtonGap;
            return pressed;
        }

        /// <summary>
        /// Nom de la region survolee, aligne a droite. C'est le seul element qui signale que le
        /// fond est vivant plutot qu'une image fixe.
        /// </summary>
        private void DrawRegionLabel()
        {
            if (_backdrop == null || !_backdrop.IsBuilt || string.IsNullOrEmpty(_backdrop.FocusedRegionName))
            {
                return;
            }

            FactionPalette palette = FactionPalette.Neutral;
            const float width = 200f;
            float x = UITheme.ScreenWidth - PanelMargin - width;

            var caption = new GUIStyle(UITheme.Caption)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = palette.AccentSoft },
            };

            var value = new GUIStyle(UITheme.Value)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = palette.Text },
            };

            GUI.Label(new Rect(x, PanelMargin, width, UITheme.CaptionHeight), "SECTEUR OBSERVE", caption);
            GUI.Label(new Rect(x, PanelMargin + UITheme.CaptionHeight, width, UITheme.ValueHeight), _backdrop.FocusedRegionName.ToUpperInvariant(), value);
        }

        private static bool ContinueEnabled => SaveFileLocator.Exists();

        private void StartNewGame()
        {
            if (_factionPicker == null)
            {
                GameLog.Error("[MainMenu] FactionPickerController indisponible : impossible d'ouvrir le choix de faction.");
                return;
            }

            _factionPicker.Open();
        }

        private void LoadGalaxyMap()
        {
            if (!ServiceLocator.TryGet(out ISceneLoader sceneLoader))
            {
                GameLog.Error("[MainMenu] ISceneLoader indisponible : impossible de charger la galaxie.");
                return;
            }

            sceneLoader.LoadScene(GalaxyMapSceneName);
        }
    }
}
