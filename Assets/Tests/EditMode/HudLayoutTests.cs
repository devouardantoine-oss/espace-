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
            // Sur un ecran confortable la date est la ; en se resserrant elle part avant les
            // credits, parce qu'elle est rappelee dans la fenetre de gestion alors qu'un tresor
            // invisible obligerait a l'ouvrir a chaque decision d'achat.
            //
            // La propriete est ecrite comme un balayage plutot que sur deux largeurs choisies :
            // la version precedente affirmait « a 420 unites la date est sacrifiee », ce qui
            // etait vrai des cinq boutons de vitesse et a cesse de l'etre des que la liste
            // deroulante a rendu 92 unites. Un test d'ordre ne se perime pas quand une largeur
            // bouge — il ne casse que si l'ordre des sacrifices change vraiment.
            Assert.IsTrue(HudLayout.For(700f, hasClock: true).ShowsDate, "Un ecran confortable garde la date.");

            for (float width = 200f; width <= 1600f; width += 2f)
            {
                HudLayout layout = HudLayout.For(width, hasClock: true);

                if (!layout.ShowsCredits)
                {
                    Assert.IsFalse(
                        layout.ShowsDate,
                        $"A {width} unites, les credits sont sacrifies avant la date : l'ordre est inverse.");
                }
            }
        }

        [Test]
        public void NarrowScreen_KeepsCreditsOnceTheDateIsGone()
        {
            // Le repli doit servir a quelque chose : il existe une plage de largeurs ou la date
            // est partie et ou les credits, eux, tiennent encore. Sans cette plage, sacrifier la
            // date ne rachete rien et l'ordre des replis serait a revoir.
            bool foundTheWindow = false;

            for (float width = 200f; width <= 1600f; width += 2f)
            {
                HudLayout layout = HudLayout.For(width, hasClock: true);
                if (!layout.ShowsDate && layout.ShowsCredits)
                {
                    foundTheWindow = true;
                    break;
                }
            }

            Assert.IsTrue(foundTheWindow, "Abandonner la date doit ramener les credits sur au moins une largeur.");
        }

        [Test]
        public void SpeedList_ReservesExactlyOneRowPerChoice()
        {
            // La hauteur de la liste depliee est calculee a partir de SpeedOptionCount, mais les
            // lignes sont ecrites une par une dans HudController. Les deux doivent s'accorder :
            // une valeur ajoutee a GameSpeed sans toucher a la constante donnerait une derniere
            // ligne dessinee hors du cadre, donc invisible et pourtant cliquable.
            Assert.AreEqual(
                System.Enum.GetValues(typeof(Espace.Core.GameSpeed)).Length,
                HudLayout.SpeedOptionCount,
                "Une ligne par valeur de GameSpeed — la pause comprise, puisqu'elle en fait partie.");
        }

        [Test]
        public void SpeedList_FitsUnderTheClusterOnTheShortestScreen()
        {
            // Pire cas de hauteur logique : environ 286 unites, quand UITheme.Scale sature a 4.
            // La liste depliee tombe sous la grappe ; si elle depassait, la derniere vitesse
            // sortirait de l'ecran.
            const float ShortestScreenHeight = 286f;

            float listBottom = HudLayout.For(700f, hasClock: true).RightCluster.yMax
                               + 2f
                               + HudLayout.SpeedOptionCount * HudLayout.SpeedOptionHeight;

            Assert.Less(listBottom, ShortestScreenHeight, "La liste des vitesses sort par le bas.");
        }

        [Test]
        public void SpeedSelector_CostsLessThanTheRowOfButtonsItReplaced()
        {
            // Cinq boutons de 26 unites separes de 4 : 146 unites reservees en permanence pour
            // une commande utilisee par a-coups. Tout l'interet de la liste deroulante est la ;
            // si quelqu'un elargit le selecteur au point de perdre ce gain, autant revenir aux
            // boutons, qui eux ne demandaient qu'un appui.
            const int OldRowOfButtonsWidth = 5 * 26 + 4 * 4;

            Assert.Less(HudLayout.SpeedSelectorWidth, OldRowOfButtonsWidth);
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
