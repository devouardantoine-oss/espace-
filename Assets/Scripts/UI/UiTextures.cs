using System.Collections.Generic;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Fabrique les textures d'habillage de l'interface — degrades, cadres d'angle, halos —
    /// entierement par code (Phase 21.1).
    /// <para>
    /// <b>Meme parti pris que <c>GalaxyBackgroundFactory</c> et <c>RuntimeSpriteFactory</c> :</b>
    /// aucune image importee. Ce n'est pas seulement une contrainte d'environnement, c'est ce
    /// qui garantit qu'une couleur de faction inedite produira un habillage coherent — une
    /// texture peinte devrait etre redessinee pour chaque nouvelle civilisation.
    /// </para>
    /// <para>
    /// <b>Tout est mis en cache par parametres.</b> IMGUI redessine chaque frame ; fabriquer une
    /// texture dans un <c>OnGUI</c> sans cache creerait soixante textures par seconde, que le
    /// ramasse-miettes ne libererait jamais (les <c>Texture2D</c> sont des objets natifs). Le
    /// cache est donc obligatoire, pas une optimisation.
    /// </para>
    /// <para>
    /// <b>Les cadres exploitent le decoupage en neuf zones d'IMGUI.</b> Un <c>GUIStyle</c> dont
    /// <c>border</c> est renseigne etire les bords et laisse les coins intacts : une texture de
    /// 48 pixels habille donc un panneau de n'importe quelle taille sans que les equerres se
    /// deforment.
    /// </para>
    /// </summary>
    public static class UiTextures
    {
        /// <summary>Cote des textures de cadre. Assez grand pour loger une equerre nette, assez petit pour etre negligeable en memoire.</summary>
        private const int FrameSize = 48;

        /// <summary>Longueur d'une equerre, en pixels de la texture. C'est aussi la valeur de <c>GUIStyle.border</c>.</summary>
        public const int FrameCorner = 14;

        /// <summary>Epaisseur du trait d'une equerre.</summary>
        private const int FrameThickness = 2;

        /// <summary>Hauteur des degrades verticaux. Une colonne d'un pixel de large suffit : IMGUI etire horizontalement.</summary>
        private const int GradientHeight = 64;

        /// <summary>Cote des halos radiaux.</summary>
        private const int GlowSize = 64;

        private readonly struct GradientKey
        {
            public readonly Color Top;
            public readonly Color Bottom;

            public GradientKey(Color top, Color bottom)
            {
                Top = top;
                Bottom = bottom;
            }
        }

        private static readonly Dictionary<GradientKey, Texture2D> Gradients = new Dictionary<GradientKey, Texture2D>();
        private static readonly Dictionary<Color, Texture2D> Frames = new Dictionary<Color, Texture2D>();
        private static readonly Dictionary<Color, Texture2D> Glows = new Dictionary<Color, Texture2D>();

        /// <summary>
        /// Degrade vertical, du haut vers le bas. Le fond des panneaux holographiques : un aplat
        /// donne un rectangle mort, un degrade suggere une surface qui capte la lumiere.
        /// </summary>
        public static Texture2D Gradient(Color top, Color bottom)
        {
            var key = new GradientKey(top, bottom);
            if (Gradients.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(1, GradientHeight, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            for (int y = 0; y < GradientHeight; y++)
            {
                // La ligne 0 d'une texture est en bas, alors que « top » designe le haut du
                // panneau a l'ecran : l'interpolation est donc inversee.
                float t = 1f - y / (float)(GradientHeight - 1);
                texture.SetPixel(0, y, Color.Lerp(top, bottom, t));
            }

            texture.Apply();
            Gradients[key] = texture;
            return texture;
        }

        /// <summary>
        /// Cadre a equerres : quatre angles traces, aucun bord continu. C'est la signature
        /// visuelle d'un affichage tete haute — un rectangle ferme ressemble a une boite de
        /// dialogue, quatre equerres ressemblent a une visee.
        /// <para>A utiliser avec <see cref="FrameCorner"/> comme <c>GUIStyle.border</c>.</para>
        /// </summary>
        public static Texture2D CornerFrame(Color accent)
        {
            if (Frames.TryGetValue(accent, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(FrameSize, FrameSize, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var transparent = new Color(accent.r, accent.g, accent.b, 0f);
            for (int y = 0; y < FrameSize; y++)
            {
                for (int x = 0; x < FrameSize; x++)
                {
                    texture.SetPixel(x, y, IsOnCorner(x, y) ? accent : transparent);
                }
            }

            texture.Apply();
            Frames[accent] = texture;
            return texture;
        }

        /// <summary>
        /// Vrai si le pixel appartient a l'une des quatre equerres.
        /// <para>
        /// Le test se fait sur la distance aux bords plutot que sur les coordonnees brutes :
        /// les quatre angles se decrivent alors d'un seul jeu de conditions, au lieu de quatre
        /// blocs symetriques a maintenir en accord.
        /// </para>
        /// </summary>
        private static bool IsOnCorner(int x, int y)
        {
            int fromLeftOrRight = Mathf.Min(x, FrameSize - 1 - x);
            int fromTopOrBottom = Mathf.Min(y, FrameSize - 1 - y);

            bool onVerticalStroke = fromLeftOrRight < FrameThickness && fromTopOrBottom < FrameCorner;
            bool onHorizontalStroke = fromTopOrBottom < FrameThickness && fromLeftOrRight < FrameCorner;

            return onVerticalStroke || onHorizontalStroke;
        }

        /// <summary>
        /// Halo radial : opaque au centre, transparent au bord. Sert d'aura derriere un element
        /// selectionne et de lueur atmospherique autour d'une planete.
        /// </summary>
        public static Texture2D RadialGlow(Color center)
        {
            if (Glows.TryGetValue(center, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(GlowSize, GlowSize, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            const float radius = GlowSize * 0.5f;
            for (int y = 0; y < GlowSize; y++)
            {
                for (int x = 0; x < GlowSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    float falloff = Mathf.Clamp01(1f - distance / radius);

                    // Au cube plutot que lineaire : une decroissance lineaire dessine un disque
                    // aux bords visibles, alors qu'un halo doit s'eteindre sans qu'on situe sa fin.
                    float alpha = center.a * falloff * falloff * falloff;
                    texture.SetPixel(x, y, new Color(center.r, center.g, center.b, alpha));
                }
            }

            texture.Apply();
            Glows[center] = texture;
            return texture;
        }

        /// <summary>
        /// Vide le cache. Appele lors d'un rechargement de domaine en editeur, ou les textures
        /// natives survivent aux variables statiques qui les referencaient.
        /// </summary>
        public static void Clear()
        {
            Gradients.Clear();
            Frames.Clear();
            Glows.Clear();
        }
    }
}
