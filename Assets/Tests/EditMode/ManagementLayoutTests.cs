using Espace.UI;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie la disposition du rail de gestion et de son panneau (Phase 24, etape 2).
    /// <para>
    /// La promesse du concept tient en une phrase : <b>on peut regarder et gerer en meme
    /// temps</b>. Si le panneau finit par couvrir la carte, on a simplement reconstruit la
    /// fenetre modale sous une autre forme — sans que rien ne plante, et sans qu'aucun autre
    /// test ne s'en apercoive.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ManagementLayoutTests
    {
        /// <summary>700 est le minimum <i>garanti</i> sur un ecran dense, pas un plancher : voir <see cref="HudLayoutTests"/>.</summary>
        private static readonly float[] Widths = { 420f, 520f, 640f, 700f, 800f, 1024f, 1600f };

        private static readonly float[] Heights = { 286f, 320f, 400f, 600f };

        [Test]
        public void Panel_NeverLeavesTheScreen()
        {
            foreach (float width in Widths)
            {
                ManagementLayout layout = ManagementLayout.For(width, 286f);

                Assert.LessOrEqual(
                    layout.Panel.xMax, width,
                    $"A {width} unites, le panneau sort de l'ecran.");
            }
        }

        [Test]
        public void Rail_AndPanel_NeverOverlap()
        {
            foreach (float width in Widths)
            {
                ManagementLayout layout = ManagementLayout.For(width, 286f);

                Assert.LessOrEqual(layout.Rail.xMax, layout.Panel.x, $"A {width} unites, le rail passe sous le panneau.");
            }
        }

        [Test]
        public void SomeMapStaysVisible_OnEveryNormalWidth()
        {
            // La promesse du concept. Elle ne peut plus etre tenue en dessous d'une certaine
            // largeur — d'ou le seuil a 640, qui couvre tous les formats realistes.
            foreach (float width in Widths)
            {
                if (width < 640f)
                {
                    continue;
                }

                ManagementLayout layout = ManagementLayout.For(width, 286f);

                Assert.GreaterOrEqual(
                    width - layout.Panel.xMax, ManagementLayout.MinimumVisibleMap,
                    $"A {width} unites, il ne reste plus assez de carte a droite du panneau.");
            }
        }

        [Test]
        public void Panel_StaysUsableEvenWhenSqueezed()
        {
            foreach (float width in Widths)
            {
                Assert.GreaterOrEqual(
                    ManagementLayout.For(width, 286f).Panel.width, ManagementLayout.MinimumPanelWidth * 0.9f,
                    $"A {width} unites, le panneau est trop etroit pour afficher quoi que ce soit.");
            }
        }

        [Test]
        public void Rail_FitsUnderTheHudOnTheShortestScreen()
        {
            // 286 unites est la hauteur du pire cas realiste. Le rail doit y tenir entier, sinon
            // une entree serait inatteignable.
            ManagementLayout layout = ManagementLayout.For(700f, 286f);

            Assert.AreEqual(ManagementLayout.RailHeight(), layout.Rail.height, "Le rail doit tenir en entier.");
            Assert.LessOrEqual(layout.Rail.yMax, 286f - HudLayout.Padding);
        }

        [Test]
        public void Rail_StartsBelowTheHudClusters()
        {
            // Le rail ne doit jamais chevaucher la grappe des credits, qui vit dans le meme coin.
            ManagementLayout layout = ManagementLayout.For(700f, 286f);

            Assert.GreaterOrEqual(layout.Rail.y, HudLayout.BandHeight + HudLayout.ClusterHeight);
        }

        [Test]
        public void Panel_UsesTheHeightItIsGiven()
        {
            foreach (float height in Heights)
            {
                ManagementLayout layout = ManagementLayout.For(700f, height);

                Assert.LessOrEqual(layout.Panel.yMax, height, $"A {height} unites de haut, le panneau deborde.");
                Assert.Greater(layout.Panel.height, 0f);
            }
        }

        [Test]
        public void Panel_IsSmallerThanTheWindowItReplaces()
        {
            // L'ancienne fenetre faisait 660 x 460 — plus haute que l'ecran garanti, donc coupee.
            // Si le panneau repassait au-dessus, la refonte n'aurait servi a rien.
            ManagementLayout layout = ManagementLayout.For(700f, 286f);

            Assert.Less(layout.Panel.width, 660f);
            Assert.Less(layout.Panel.height, 460f);
        }
    }
}
