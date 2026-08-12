using Espace.UI;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie la disposition des deux grappes d'angle (Phase 23, tranche B).
    /// <para>
    /// Trois debordements d'interface ont deja echappe aux tests dans ce projet — le panneau du
    /// menu, la colonne de doctrine, le dossier de monde — parce qu'<c>OnGUI</c> deborde en
    /// silence : rien ne plante, un bouton sort simplement de l'ecran. Ces tests balaient les
    /// formats plutot que d'esperer s'en apercevoir dans l'editeur.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class HudLayoutTests
    {
        /// <summary>
        /// Largeurs logiques a couvrir. 700 est le minimum <i>garanti</i> sur un ecran dense,
        /// pas un plancher : <c>UITheme.Scale</c> etant borne a 1 par le bas, un petit ecran peu
        /// dense descend en dessous. D'ou les trois premieres valeurs.
        /// </summary>
        private static readonly float[] Widths = { 420f, 520f, 640f, 700f, 800f, 1024f, 1600f };

        [Test]
        public void Clusters_NeverOverlap()
        {
            foreach (float width in Widths)
            {
                HudLayout layout = HudLayout.For(width, hasClock: true);

                if (!layout.ShowsCredits)
                {
                    continue;
                }

                Assert.LessOrEqual(
                    layout.LeftCluster.xMax, layout.RightCluster.x,
                    $"A {width} unites, les deux grappes se chevauchent.");
            }
        }

        [Test]
        public void RightCluster_NeverLeavesTheScreen()
        {
            foreach (float width in Widths)
            {
                HudLayout layout = HudLayout.For(width, hasClock: true);

                Assert.GreaterOrEqual(layout.RightCluster.x, 0f, $"A {width} unites, la grappe droite sort a gauche.");
                Assert.LessOrEqual(layout.RightCluster.xMax, width, $"A {width} unites, la grappe droite sort a droite.");
            }
        }

        [Test]
        public void Commands_SurviveEveryWidth()
        {
            // La regle du projet depuis la Phase 11 : ce qui disparait en premier est de
            // l'information, jamais une commande. La grappe droite ne porte que des commandes —
            // elle doit donc toujours etre dessinee, et assez large pour les contenir.
            float minimum = HudLayout.RightClusterWidth(hasClock: true, withDate: false);

            foreach (float width in Widths)
            {
                Assert.GreaterOrEqual(
                    HudLayout.For(width, hasClock: true).RightCluster.width, minimum,
                    $"A {width} unites, une commande a ete sacrifiee.");
            }
        }

        [Test]
        public void Date_IsTheFirstThingSacrificed()
        {
            // Sur un ecran confortable la date est la ; sur un ecran etroit elle part avant les
            // credits, parce qu'elle est rappelee sous la vitesse active alors qu'un tresor
            // invisible obligerait a ouvrir une fenetre a chaque decision d'achat.
            Assert.IsTrue(HudLayout.For(700f, hasClock: true).ShowsDate);
            Assert.IsFalse(HudLayout.For(420f, hasClock: true).ShowsDate);
        }

        [Test]
        public void NarrowScreen_KeepsCreditsOnceTheDateIsGone()
        {
            // Le repli doit suffire : abandonner la date libere 80 unites, ce qui doit ramener
            // les credits. Si ce test echoue, l'ordre des sacrifices est a revoir.
            HudLayout layout = HudLayout.For(420f, hasClock: true);

            Assert.IsFalse(layout.ShowsDate);
            Assert.IsTrue(layout.ShowsCredits, "Une fois la date sacrifiee, les credits doivent tenir.");
        }

        [Test]
        public void WithoutAClock_TheClusterShrinks()
        {
            // L'horloge peut ne pas encore etre enregistree au premier rendu de la scene : la
            // grappe ne doit pas reserver la place de boutons qu'elle ne dessinera pas.
            Assert.Less(
                HudLayout.For(700f, hasClock: false).RightCluster.width,
                HudLayout.For(700f, hasClock: true).RightCluster.width);
        }

        [Test]
        public void ChromeStaysWellBelowTheOldBar()
        {
            // L'ancienne barre faisait 44 unites sur toute la largeur. Le lisere et les grappes
            // doivent tenir dans moins, sinon la tranche n'a servi a rien.
            const int OldBarHeight = 44;

            HudLayout layout = HudLayout.For(700f, hasClock: true);
            float chromeHeight = layout.RightCluster.yMax;

            Assert.Less(chromeHeight, OldBarHeight, "La hauteur occupee doit avoir baisse.");
        }

        [Test]
        public void MapStaysReachableBetweenTheClusters()
        {
            // Le gain principal de la tranche : le haut de la carte redevient cliquable. Si les
            // deux grappes se rejoignaient, on retrouverait la barre pleine largeur d'avant sous
            // une autre forme.
            HudLayout layout = HudLayout.For(700f, hasClock: true);

            Assert.Greater(
                layout.RightCluster.x - layout.LeftCluster.xMax, 100f,
                "Il doit rester une large ouverture vers la carte entre les deux grappes.");
        }

        [Test]
        public void BandLeavesRoomForTheClusters()
        {
            Assert.Greater(HudLayout.For(700f, hasClock: true).RightCluster.y, HudLayout.BandHeight);
        }
    }
}
