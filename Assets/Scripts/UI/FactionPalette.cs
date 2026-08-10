using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Palette complete d'une faction, deduite d'une seule couleur (Phase 21.1).
    /// <para>
    /// <b>Une couleur en entree, six en sortie.</b> C'est la contrainte de conception qui rend
    /// la promesse « ajouter une faction plus tard sans refaire l'interface » tenable : une
    /// nouvelle civilisation demande de renseigner <c>EmpireDefinition.Color</c>, rien d'autre.
    /// Si chaque faction exigeait six couleurs saisies a la main, la septieme serait fatalement
    /// incoherente avec les six premieres — et personne ne s'en apercevrait avant de la voir a
    /// l'ecran.
    /// </para>
    /// <para>
    /// <b>Derivation en teinte-saturation-valeur, pas en RVB.</b> Assombrir une couleur en
    /// multipliant ses trois canaux la desature aussi, et un bleu sombre obtenu ainsi vire au
    /// gris. En travaillant en TSV, la teinte de la faction survit jusque dans le fond d'ecran
    /// le plus sombre — c'est precisement ce qui doit donner l'impression de changer de monde.
    /// </para>
    /// <para>
    /// <b>Saturation et valeur sont bornees par le bas.</b> Une couleur de faction reglee trop
    /// pale ou trop sombre dans l'editeur produirait une palette morte. Le plancher garantit
    /// qu'une palette reste lisible meme mal parametree.
    /// </para>
    /// </summary>
    public readonly struct FactionPalette
    {
        /// <summary>Saturation minimale de l'accent : en dessous, la faction n'a plus d'identite chromatique.</summary>
        private const float MinimumAccentSaturation = 0.45f;

        /// <summary>Valeur minimale de l'accent : en dessous, il disparait sur le fond sombre.</summary>
        private const float MinimumAccentValue = 0.60f;

        /// <summary>Couleur pleine de la faction : bordures actives, barres, titres accentues.</summary>
        public readonly Color Accent;

        /// <summary>Version attenuee de l'accent, pour les traits et les separateurs.</summary>
        public readonly Color AccentSoft;

        /// <summary>Fond des panneaux : tres sombre, mais teinte de la faction.</summary>
        public readonly Color Panel;

        /// <summary>Fond d'ecran, plus sombre encore que <see cref="Panel"/>.</summary>
        public readonly Color Background;

        /// <summary>Texte principal : presque blanc, avec juste assez de teinte pour ne pas jurer.</summary>
        public readonly Color Text;

        /// <summary>Texte secondaire : intitules, legendes, valeurs desactivees.</summary>
        public readonly Color TextMuted;

        private FactionPalette(Color accent, Color accentSoft, Color panel, Color background, Color text, Color textMuted)
        {
            Accent = accent;
            AccentSoft = accentSoft;
            Panel = panel;
            Background = background;
            Text = text;
            TextMuted = textMuted;
        }

        /// <summary>Construit la palette complete a partir de la couleur d'une faction.</summary>
        public static FactionPalette FromAccent(Color source)
        {
            Color.RGBToHSV(source, out float hue, out float saturation, out float value);

            float accentSaturation = Mathf.Max(saturation, MinimumAccentSaturation);
            float accentValue = Mathf.Max(value, MinimumAccentValue);

            return new FactionPalette(
                accent: Opaque(hue, accentSaturation, accentValue),
                accentSoft: Opaque(hue, accentSaturation * 0.72f, accentValue * 0.62f),
                panel: Opaque(hue, accentSaturation * 0.38f, 0.115f),
                background: Opaque(hue, accentSaturation * 0.55f, 0.055f),
                text: Opaque(hue, accentSaturation * 0.10f, 0.95f),
                textMuted: Opaque(hue, accentSaturation * 0.22f, 0.63f));
        }

        /// <summary>
        /// Palette de repli, sans faction : le bleu-nuit du theme general. Utilisee au menu
        /// principal, avant tout choix de civilisation.
        /// </summary>
        public static FactionPalette Neutral => FromAccent(new Color(0.25f, 0.55f, 0.95f, 1f));

        /// <summary>
        /// Melange deux palettes. C'est la brique du basculement d'identite : l'ecran passe
        /// progressivement d'une civilisation a l'autre au lieu de changer d'un coup.
        /// </summary>
        public static FactionPalette Lerp(FactionPalette from, FactionPalette to, float t)
        {
            float clamped = Mathf.Clamp01(t);
            return new FactionPalette(
                Color.Lerp(from.Accent, to.Accent, clamped),
                Color.Lerp(from.AccentSoft, to.AccentSoft, clamped),
                Color.Lerp(from.Panel, to.Panel, clamped),
                Color.Lerp(from.Background, to.Background, clamped),
                Color.Lerp(from.Text, to.Text, clamped),
                Color.Lerp(from.TextMuted, to.TextMuted, clamped));
        }

        /// <summary>Rapproche cette palette de <paramref name="target"/> d'une frame (voir <see cref="UiEasing"/>).</summary>
        public FactionPalette Approach(FactionPalette target, float smoothingSeconds, float deltaTime)
        {
            return Lerp(this, target, UiEasing.Blend(smoothingSeconds, deltaTime));
        }

        /// <summary>L'accent a une opacite donnee : fonds de bouton actif, halos, remplissages discrets.</summary>
        public Color AccentAt(float alpha)
        {
            Color tinted = Accent;
            tinted.a = Mathf.Clamp01(alpha);
            return tinted;
        }

        private static Color Opaque(float hue, float saturation, float value)
        {
            Color color = Color.HSVToRGB(hue, Mathf.Clamp01(saturation), Mathf.Clamp01(value));
            color.a = 1f;
            return color;
        }
    }
}
