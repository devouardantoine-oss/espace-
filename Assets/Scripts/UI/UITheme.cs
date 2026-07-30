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
                    _titleStyle.fontSize = 14;
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
