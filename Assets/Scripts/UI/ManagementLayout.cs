using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Disposition du rail de gestion et de son panneau (Phase 24, étape 2).
    /// <para>
    /// <b>Ce qui est remplacé.</b> Une fenêtre modale de <b>660 × 460</b>, centrée. Elle est plus
    /// haute que les 286 unités garanties sur un téléphone dense — donc <b>coupée</b> — et elle
    /// recouvre la carte entièrement : impossible de comparer deux systèmes, ou de suivre une
    /// flotte en vol pendant qu'on donne un ordre à une autre. Le jeu avait deux modes, regarder
    /// ou gérer.
    /// </para>
    /// <para>
    /// <b>Ce qui remplace.</b> Un rail permanent contre le bord gauche — sous le pouce en
    /// paysage — et un panneau qui se pose <i>à côté</i>, jamais par-dessus. Il reste toujours de
    /// la carte manipulable à droite.
    /// </para>
    /// <para>
    /// <b>Fonction pure</b>, pour la même raison que <see cref="HudLayout"/> : trois débordements
    /// d'interface ont déjà échappé aux tests dans ce projet parce qu'<c>OnGUI</c> déborde en
    /// silence. Une largeur calculée ici se vérifie sur tous les formats d'un coup.
    /// </para>
    /// </summary>
    public readonly struct ManagementLayout
    {
        /// <summary>Largeur du rail. Un glyphe plus un libellé court.</summary>
        public const int RailWidth = 108;

        /// <summary>
        /// Hauteur d'une entrée du rail. Environ 9 mm au doigt à l'échelle 1 — au-dessus du
        /// minimum tactile confortable, et assez serré pour que six entrées tiennent dans les
        /// deux tiers de la hauteur.
        /// </summary>
        public const int RailItemHeight = 30;

        /// <summary>Empire, Flottes, Diplomatie, Recherche, Espionnage, Journal.</summary>
        public const int RailItemCount = 6;

        /// <summary>Largeur souhaitée du panneau. Réduite si l'écran ne la donne pas.</summary>
        public const int PreferredPanelWidth = 322;

        /// <summary>
        /// Largeur de carte qu'il faut préserver à droite du panneau.
        /// <para>
        /// C'est la promesse du concept : on peut regarder <i>et</i> gérer. Un panneau qui
        /// mangerait tout l'écran ramènerait la fenêtre modale sous une autre forme.
        /// </para>
        /// </summary>
        public const int MinimumVisibleMap = 90;

        /// <summary>En deçà, le panneau ne peut plus rien afficher d'utile.</summary>
        public const int MinimumPanelWidth = 180;

        /// <summary>Rectangle du rail, toujours dessiné.</summary>
        public readonly Rect Rail;

        /// <summary>Rectangle du panneau. Valide seulement si une entrée est ouverte.</summary>
        public readonly Rect Panel;

        private ManagementLayout(Rect rail, Rect panel)
        {
            Rail = rail;
            Panel = panel;
        }

        /// <summary>Hauteur totale du rail.</summary>
        public static int RailHeight()
        {
            return RailItemCount * RailItemHeight;
        }

        /// <summary>Ordonnée à laquelle rail et panneau commencent, sous les grappes du bandeau.</summary>
        public static float TopOffset()
        {
            return HudLayout.BandHeight + HudLayout.Gap + HudLayout.ClusterHeight + HudLayout.Gap;
        }

        /// <summary>
        /// Dispose le rail et le panneau.
        /// <para>
        /// <b>Le panneau cède avant la carte.</b> Sur un écran étroit il se resserre jusqu'à
        /// <see cref="MinimumPanelWidth"/> pour laisser <see cref="MinimumVisibleMap"/> de carte
        /// visible — parce qu'un panneau un peu serré reste utilisable, alors qu'une carte
        /// entièrement couverte annule tout l'intérêt du concept.
        /// </para>
        /// </summary>
        public static ManagementLayout For(float screenWidth, float screenHeight)
        {
            float top = TopOffset();

            float railHeight = Mathf.Min(RailHeight(), Mathf.Max(RailItemHeight, screenHeight - top - HudLayout.Padding));
            var rail = new Rect(HudLayout.Padding, top, RailWidth, railHeight);

            float panelX = rail.xMax + HudLayout.Gap;
            float available = screenWidth - panelX - HudLayout.Padding - MinimumVisibleMap;
            float panelWidth = Mathf.Max(MinimumPanelWidth, Mathf.Min(PreferredPanelWidth, available));

            // Dernier garde-fou : sur un ecran vraiment etroit, mieux vaut un panneau qui tient
            // a l'ecran qu'un panneau qui en sort.
            panelWidth = Mathf.Min(panelWidth, screenWidth - panelX - HudLayout.Padding);

            float panelHeight = Mathf.Max(RailItemHeight, screenHeight - top - HudLayout.Padding);

            return new ManagementLayout(rail, new Rect(panelX, top, panelWidth, panelHeight));
        }
    }
}
