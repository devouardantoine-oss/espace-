using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Disposition des deux grappes d'angle de <see cref="HudController"/> (Phase 23, tranche B).
    /// <para>
    /// <b>Pourquoi cette disposition vit hors du composant.</b> Trois débordements d'interface
    /// ont déjà échappé aux tests dans ce projet — le panneau du menu, la colonne de doctrine,
    /// le dossier de monde — parce qu'une largeur calculée dans un <c>MonoBehaviour</c> ne se
    /// vérifie pas sans éditeur, et qu'<c>OnGUI</c> déborde en silence. Une fonction pure se
    /// vérifie sur tous les formats d'écran d'un coup.
    /// </para>
    /// <para>
    /// <b>La règle appliquée, la même depuis la Phase 11 :</b> ce qui disparaît en premier sur un
    /// écran étroit est de l'<b>information</b>, jamais une <b>commande</b>. On peut relire une
    /// date ou un montant dans la fenêtre de gestion ; on ne peut pas ouvrir un menu dont le
    /// bouton n'existe plus.
    /// </para>
    /// <para>
    /// <b>La largeur logique n'est pas toujours de 700 unités.</b> C'est le minimum <em>garanti</em>
    /// sur un écran dense, pas un plancher absolu : <c>UITheme.Scale</c> étant borné à 1 par le
    /// bas, un petit écran peu dense descend en dessous. C'est le cas que cette disposition doit
    /// tenir.
    /// </para>
    /// </summary>
    public readonly struct HudLayout
    {
        /// <summary>Hauteur du liseré d'état.</summary>
        public const int BandHeight = 5;

        /// <summary>Hauteur des grappes d'angle.</summary>
        public const int ClusterHeight = 24;

        public const int Padding = 8;
        public const int Gap = 4;

        /// <summary>Largeur du bloc date.</summary>
        public const int DateWidth = 76;

        /// <summary>
        /// Largeur du selecteur de vitesse.
        /// <para>
        /// <b>Une liste deroulante depuis la Phase 24</b>, a la place des cinq boutons alignes
        /// qui coutaient 146 unites en permanence. Le selecteur en demande 54 : les 92 unites
        /// recuperees reviennent a la carte, et le repli de la date devient beaucoup plus rare.
        /// </para>
        /// </summary>
        public const int SpeedSelectorWidth = 54;

        /// <summary>Hauteur d'une ligne de la liste deroulante ouverte.</summary>
        public const int SpeedOptionHeight = 22;

        /// <summary>
        /// Largeur du bouton « Menu ».
        /// <para>
        /// Le bouton « Gestion » a disparu en Phase 24 : le rail de gestion est permanent, donc
        /// il n'y avait plus rien à ouvrir. Ses 74 unités sont rendues à la carte.
        /// </para>
        /// </summary>
        public const int MenuButtonWidth = 52;

        /// <summary>
        /// Largeur du compteur d'alertes.
        /// <para>
        /// <b>Toujours reservee, meme sans alerte a afficher.</b> Une largeur variable ferait
        /// glisser les boutons voisins a chaque nouvel avis — et deplacer une cible sous le doigt
        /// du joueur est la pire chose qu'une interface puisse faire. Trente-deux unites perdues
        /// valent mieux qu'un bandeau qui bouge.
        /// </para>
        /// </summary>
        public const int AlertBadgeWidth = 32;

        /// <summary>
        /// Largeur du bloc crédits de l'angle gauche.
        /// <para>
        /// Ramenée de 84 à 76 quand le compteur d'alertes est arrivé (Phase 24) : le compteur est
        /// une commande, il ne peut pas être sacrifié, donc c'est l'information qui se resserre.
        /// Le contenu tient sans peine — « CREDITS » et un montant abrégé.
        /// </para>
        /// </summary>
        public const int CreditsWidth = 76;

        /// <summary>Pause plus quatre vitesses.</summary>
        public const int SpeedOptionCount = 5;

        /// <summary>Vrai si le bloc date tient.</summary>
        public readonly bool ShowsDate;

        /// <summary>Vrai si la grappe des crédits tient sans chevaucher les commandes.</summary>
        public readonly bool ShowsCredits;

        /// <summary>Grappe gauche. Sans objet si <see cref="ShowsCredits"/> est faux.</summary>
        public readonly Rect LeftCluster;

        /// <summary>Grappe droite, toujours présente : elle ne porte que des commandes.</summary>
        public readonly Rect RightCluster;

        private HudLayout(bool showsDate, bool showsCredits, Rect leftCluster, Rect rightCluster)
        {
            ShowsDate = showsDate;
            ShowsCredits = showsCredits;
            LeftCluster = leftCluster;
            RightCluster = rightCluster;
        }

        /// <summary>Largeur de la grappe droite, avec ou sans le bloc date.</summary>
        public static float RightClusterWidth(bool hasClock, bool withDate)
        {
            float speeds = hasClock ? SpeedSelectorWidth : 0f;
            float date = hasClock && withDate ? DateWidth + Gap : 0f;
            float windows = AlertBadgeWidth + Gap + MenuButtonWidth;

            return date + speeds + Gap + windows + 2 * Gap;
        }

        /// <summary>
        /// Dispose les deux grappes sur un écran de <paramref name="screenWidth"/> unités.
        /// <para>
        /// L'ordre des replis est délibéré : la date part avant les crédits, parce qu'elle est
        /// rappelée sous la vitesse active, alors qu'un trésor invisible oblige à ouvrir une
        /// fenêtre à chaque décision d'achat.
        /// </para>
        /// </summary>
        public static HudLayout For(float screenWidth, bool hasClock)
        {
            float top = BandHeight + Gap;
            float reservedOnTheLeft = Padding + CreditsWidth + Gap;

            bool showsDate = true;
            float width = RightClusterWidth(hasClock, withDate: true);

            if (screenWidth - Padding - width < reservedOnTheLeft)
            {
                showsDate = false;
                width = RightClusterWidth(hasClock, withDate: false);
            }

            var right = new Rect(screenWidth - Padding - width, top, width, ClusterHeight);
            bool showsCredits = right.x >= reservedOnTheLeft;

            return new HudLayout(
                showsDate,
                showsCredits,
                new Rect(Padding, top, CreditsWidth, ClusterHeight),
                right);
        }
    }
}
