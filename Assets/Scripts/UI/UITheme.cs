using System.Collections.Generic;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Palette de couleurs et styles IMGUI partages par tous les ecrans de
    /// <c>Espace.UI</c> : evite que chaque controleur invente ses propres couleurs, et donne
    /// a l'interface un aspect coherent malgre huit ecrans ecrits independamment.
    /// <para>
    /// <b>Toujours IMGUI (<c>OnGUI</c>), pas UI Toolkit ni uGUI :</b> ce projet est developpe
    /// sans acces a l'editeur Unity (voir le README, § Environnement). UI Toolkit (UXML/USS)
    /// et uGUI (Canvas/RectTransform) exigent tous deux des assets ou des scenes calibres
    /// visuellement — un pixel ou un ancrage faux ne se revele qu'a l'ouverture dans
    /// l'editeur, jamais a la compilation ni dans un test. IMGUI reste la seule approche
    /// entierement exprimable en C# pur, verifiable par la seule lecture du code et les
    /// tests EditMode. Les panneaux de diagnostic des Phases 2 a 10 utilisaient deja IMGUI en
    /// le presentant comme temporaire ; cette phase le garde mais l'eleve au rang d'interface
    /// definitive, avec un theme partage plutot que des <c>GUI.Box</c> par defaut.
    /// </para>
    /// <para>
    /// <b>Styles construits paresseusement, jamais en initialiseur de champ statique :</b>
    /// <c>GUIStyle</c>/<c>GUI.skin</c> ne sont valides qu'a l'interieur d'un appel
    /// <c>OnGUI</c> ; les construire plus tot (chargement du domaine, <c>Awake</c>) leve une
    /// exception. Chaque style est donc mis en cache au premier acces.
    /// </para>
    /// </summary>
    public static class UITheme
    {
        public static readonly Color PanelBackground = new Color(0.047f, 0.063f, 0.11f, 0.94f);
        public static readonly Color HeaderBackground = new Color(0.09f, 0.13f, 0.20f, 1f);
        public static readonly Color AccentBackground = new Color(0.16f, 0.32f, 0.52f, 1f);
        public static readonly Color TextColor = new Color(0.90f, 0.93f, 0.97f, 1f);
        public static readonly Color MutedTextColor = new Color(0.58f, 0.64f, 0.72f, 1f);
        public static readonly Color PositiveColor = new Color(0.45f, 0.80f, 0.55f, 1f);
        public static readonly Color NegativeColor = new Color(0.90f, 0.45f, 0.40f, 1f);

        /// <summary>
        /// Densite de reference : le point ou l'echelle vaut 1 et l'interface s'affiche telle
        /// qu'elle a ete dimensionnee. 160 ppp est la densite de base d'Android (« mdpi »), ce
        /// qui fait de <see cref="Scale"/> l'equivalent exact du facteur densite-independante
        /// du systeme.
        /// </summary>
        private const float ReferenceDpi = 160f;

        /// <summary>
        /// Largeur logique minimale que l'interface doit pouvoir afficher : la fenetre de
        /// gestion (660) plus une marge. L'echelle est plafonnee pour la garantir, sinon le
        /// plus large des panneaux deborderait de l'ecran sur un telephone tenu en portrait.
        /// </summary>
        private const float MinimumLogicalWidth = 700f;

        /// <summary>Garde-fou : au-dela, l'interface deviendrait grotesque sur une tres haute densite.</summary>
        private const float MaximumScale = 4f;

        private static float _cachedScale;
        private static int _cachedForWidth;
        private static int _cachedForHeight;
        private static Matrix4x4 _previousMatrix;

        /// <summary>
        /// Facteur d'agrandissement de toute l'interface, deduit de la densite de l'ecran.
        /// <para>
        /// <b>Sans lui, l'interface est illisible sur telephone.</b> IMGUI dessine en pixels
        /// physiques : un bouton de 30 pixels mesure 8 mm sur un ecran d'ordinateur a 96 ppp,
        /// mais moins de 2 mm sur un telephone a 450 ppp — trois a quatre fois plus petit que
        /// ce qu'un doigt peut viser. Le projet vise le mobile depuis la Phase 1 et n'avait
        /// jamais pu le constater, faute d'appareil.
        /// </para>
        /// <para>
        /// <b>Jamais en dessous de 1 :</b> sur un ecran d'ordinateur (~96 ppp), le rapport
        /// vaudrait 0,6 et retrecirait une interface deja correctement dimensionnee. L'echelle
        /// ne fait donc qu'agrandir, jamais l'inverse.
        /// </para>
        /// <para>
        /// <b>Plafonnee pour que le plus large panneau tienne :</b> agrandir au-dela de
        /// <c>largeurEcran / <see cref="MinimumLogicalWidth"/></c> ferait deborder la fenetre de
        /// gestion. Mieux vaut des boutons un peu plus petits qu'une fenetre dont la moitie est
        /// hors de l'ecran.
        /// </para>
        /// <para>
        /// Recalculee des que la resolution change, ce qui couvre la rotation de l'appareil.
        /// </para>
        /// </summary>
        public static float Scale
        {
            get
            {
                if (_cachedScale > 0f && _cachedForWidth == Screen.width && _cachedForHeight == Screen.height)
                {
                    return _cachedScale;
                }

                _cachedScale = ComputeScale(Screen.dpi, Screen.width);
                _cachedForWidth = Screen.width;
                _cachedForHeight = Screen.height;
                return _cachedScale;
            }
        }

        /// <summary>
        /// La formule de <see cref="Scale"/>, isolee de <c>Screen</c> pour etre verifiable en
        /// EditMode et recoupee independamment — meme separation que <c>HudFormatter</c>
        /// vis-a-vis du reste de l'interface.
        /// </summary>
        /// <param name="dpi">Densite de l'ecran ; <c>0</c> ou negative signifie « inconnue ».</param>
        /// <param name="screenWidthPixels">Largeur de l'ecran en pixels physiques.</param>
        public static float ComputeScale(float dpi, int screenWidthPixels)
        {
            // Une densite inconnue ne se devine pas : on laisse l'interface telle quelle.
            float densityScale = dpi > 1f ? dpi / ReferenceDpi : 1f;
            float widthLimit = screenWidthPixels / MinimumLogicalWidth;

            return Mathf.Clamp(Mathf.Min(densityScale, widthLimit), 1f, MaximumScale);
        }

        /// <summary>Largeur de l'ecran en unites d'interface : c'est elle qu'il faut utiliser pour positionner un panneau, jamais <c>Screen.width</c>.</summary>
        public static float ScreenWidth => Screen.width / Scale;

        /// <summary>Hauteur de l'ecran en unites d'interface : c'est elle qu'il faut utiliser pour positionner un panneau, jamais <c>Screen.height</c>.</summary>
        public static float ScreenHeight => Screen.height / Scale;

        /// <summary>
        /// A appeler en toute premiere ligne d'un <c>OnGUI</c>, avec
        /// <see cref="EndScaledLayout"/> dans un <c>finally</c> — les <c>OnGUI</c> du projet ont
        /// des retours anticipes, et une matrice laissee en place deborderait sur le composant
        /// dessine juste apres.
        /// <para>
        /// IMGUI applique l'inverse de <c>GUI.matrix</c> aux coordonnees des evenements : les
        /// clics et les touchers restent donc alignes sur ce qui est affiche, sans conversion
        /// manuelle.
        /// </para>
        /// </summary>
        public static void BeginScaledLayout()
        {
            _previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Scale, Scale, 1f));
        }

        /// <summary>Restaure la matrice d'avant <see cref="BeginScaledLayout"/>.</summary>
        public static void EndScaledLayout()
        {
            GUI.matrix = _previousMatrix;
        }

        /// <summary>
        /// Tailles de police, en unites d'interface.
        /// <para>
        /// <b>Toujours explicites, jamais heritees de <c>GUI.skin</c> :</b> un style qui laisse
        /// <c>fontSize</c> a zero prend la taille par defaut de la police integree, qui varie
        /// selon la version du moteur. Une disposition calculee en rectangles ne peut pas
        /// reposer la-dessus — c'est ce qui tronquait les libelles de la fiche de systeme, IMGUI
        /// ne signalant jamais qu'un texte deborde de son rectangle.
        /// </para>
        /// </summary>
        public const int CaptionFontSize = 10;

        /// <inheritdoc cref="CaptionFontSize"/>
        public const int LabelFontSize = 12;

        /// <inheritdoc cref="CaptionFontSize"/>
        public const int ValueFontSize = 13;

        /// <inheritdoc cref="CaptionFontSize"/>
        public const int TitleFontSize = 15;

        /// <summary>
        /// Hauteur de rectangle minimale pour qu'un texte de <paramref name="fontSize"/> ne soit
        /// pas rogne. La marge de quatre unites couvre les jambages et l'interligne.
        /// </summary>
        public static int LineHeight(int fontSize) => fontSize + 4;

        /// <summary>Hauteur de ligne d'une legende (<see cref="Caption"/>).</summary>
        public static int CaptionHeight => LineHeight(CaptionFontSize);

        /// <summary>Hauteur de ligne d'un texte courant (<see cref="Label"/>).</summary>
        public static int LabelHeight => LineHeight(LabelFontSize);

        /// <summary>Hauteur de ligne d'une valeur (<see cref="Value"/>).</summary>
        public static int ValueHeight => LineHeight(ValueFontSize);

        /// <summary>Hauteur de ligne d'un titre (<see cref="Title"/>).</summary>
        public static int TitleHeight => LineHeight(TitleFontSize);

        private static GUIStyle _captionStyle;
        private static GUIStyle _valueStyle;

        /// <summary>
        /// Legende en petites capitales grises : les intitules d'une fiche dense.
        /// <para>
        /// <b>Sans retour a la ligne</b>, contrairement a <see cref="MutedLabel"/> : dans un
        /// rectangle d'une seule ligne, un mot renvoye a la ligne suivante disparait purement et
        /// simplement. Mieux vaut un intitule tronque a droite, qui se voit et se corrige, qu'un
        /// intitule a moitie efface, qui ressemble a un bug d'affichage.
        /// </para>
        /// </summary>
        public static GUIStyle Caption
        {
            get
            {
                if (_captionStyle == null)
                {
                    _captionStyle = new GUIStyle(GUI.skin.label);
                    _captionStyle.normal.textColor = MutedTextColor;
                    _captionStyle.fontSize = CaptionFontSize;
                    _captionStyle.wordWrap = false;
                    _captionStyle.clipping = TextClipping.Clip;
                    _captionStyle.padding = new RectOffset(0, 0, 0, 0);
                }

                return _captionStyle;
            }
        }

        /// <summary>Valeur chiffree ou courte, sur une seule ligne. Voir <see cref="Caption"/> pour l'absence de retour a la ligne.</summary>
        public static GUIStyle Value
        {
            get
            {
                if (_valueStyle == null)
                {
                    _valueStyle = new GUIStyle(GUI.skin.label);
                    _valueStyle.normal.textColor = TextColor;
                    _valueStyle.fontSize = ValueFontSize;
                    _valueStyle.wordWrap = false;
                    _valueStyle.clipping = TextClipping.Clip;
                    _valueStyle.padding = new RectOffset(0, 0, 0, 0);
                }

                return _valueStyle;
            }
        }

        private static readonly Dictionary<Color, Texture2D> SolidTextures = new Dictionary<Color, Texture2D>();

        private static GUIStyle _panelStyle;
        private static GUIStyle _headerStyle;
        private static GUIStyle _titleStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _mutedLabelStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _tabButtonStyle;
        private static GUIStyle _activeTabButtonStyle;

        /// <summary>Fond plein d'un panneau (voir <see cref="PanelBackground"/>).</summary>
        public static GUIStyle Panel
        {
            get
            {
                if (_panelStyle == null)
                {
                    _panelStyle = new GUIStyle(GUI.skin.box);
                    _panelStyle.normal.background = SolidTexture(PanelBackground);
                    _panelStyle.normal.textColor = TextColor;
                    _panelStyle.padding = new RectOffset(10, 10, 8, 8);
                }

                return _panelStyle;
            }
        }

        /// <summary>Bandeau d'entete (barre du haut, entete de fenetre) : fond plus clair que <see cref="Panel"/>.</summary>
        public static GUIStyle Header
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle(GUI.skin.box);
                    _headerStyle.normal.background = SolidTexture(HeaderBackground);
                    _headerStyle.normal.textColor = TextColor;
                    _headerStyle.padding = new RectOffset(10, 10, 6, 6);
                }

                return _headerStyle;
            }
        }

        /// <summary>Titre d'ecran ou de section : gras, legerement plus grand.</summary>
        public static GUIStyle Title
        {
            get
            {
                if (_titleStyle == null)
                {
                    _titleStyle = new GUIStyle(GUI.skin.label);
                    _titleStyle.fontStyle = FontStyle.Bold;
                    _titleStyle.fontSize = TitleFontSize;
                    _titleStyle.normal.textColor = TextColor;
                }

                return _titleStyle;
            }
        }

        /// <summary>Texte courant.</summary>
        public static GUIStyle Label
        {
            get
            {
                if (_labelStyle == null)
                {
                    _labelStyle = new GUIStyle(GUI.skin.label);
                    _labelStyle.normal.textColor = TextColor;
                    _labelStyle.fontSize = LabelFontSize;
                    _labelStyle.wordWrap = true;
                }

                return _labelStyle;
            }
        }

        /// <summary>Texte secondaire (legendes, aide) : meme style que <see cref="Label"/> en plus discret.</summary>
        public static GUIStyle MutedLabel
        {
            get
            {
                if (_mutedLabelStyle == null)
                {
                    _mutedLabelStyle = new GUIStyle(GUI.skin.label);
                    _mutedLabelStyle.normal.textColor = MutedTextColor;
                    _mutedLabelStyle.fontSize = LabelFontSize;
                    _mutedLabelStyle.wordWrap = true;
                }

                return _mutedLabelStyle;
            }
        }

        /// <summary>Bouton standard.</summary>
        public static GUIStyle Button
        {
            get
            {
                if (_buttonStyle == null)
                {
                    _buttonStyle = new GUIStyle(GUI.skin.button);
                    _buttonStyle.normal.textColor = TextColor;
                    _buttonStyle.padding = new RectOffset(8, 8, 6, 6);
                }

                return _buttonStyle;
            }
        }

        /// <summary>Onglet inactif de la fenetre de gestion.</summary>
        public static GUIStyle TabButton
        {
            get
            {
                if (_tabButtonStyle == null)
                {
                    _tabButtonStyle = new GUIStyle(GUI.skin.button);
                    _tabButtonStyle.alignment = TextAnchor.MiddleLeft;
                    _tabButtonStyle.normal.textColor = MutedTextColor;
                    _tabButtonStyle.padding = new RectOffset(10, 6, 8, 8);
                }

                return _tabButtonStyle;
            }
        }

        /// <summary>Onglet actif de la fenetre de gestion : fond accentue.</summary>
        public static GUIStyle ActiveTabButton
        {
            get
            {
                if (_activeTabButtonStyle == null)
                {
                    _activeTabButtonStyle = new GUIStyle(GUI.skin.button);
                    _activeTabButtonStyle.alignment = TextAnchor.MiddleLeft;
                    _activeTabButtonStyle.normal.textColor = TextColor;
                    _activeTabButtonStyle.normal.background = SolidTexture(AccentBackground);
                    _activeTabButtonStyle.padding = new RectOffset(10, 6, 8, 8);
                }

                return _activeTabButtonStyle;
            }
        }

        /// <summary>Texture 1x1 plein de <paramref name="color"/>, mise en cache par couleur.</summary>
        public static Texture2D SolidTexture(Color color)
        {
            if (SolidTextures.TryGetValue(color, out Texture2D existing) && existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;

            SolidTextures[color] = texture;
            return texture;
        }
    }
}
