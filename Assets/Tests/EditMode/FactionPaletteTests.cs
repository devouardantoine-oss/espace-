using Espace.UI;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie la derivation d'une palette complete a partir d'une seule couleur (Phase 21.1).
    /// <para>
    /// C'est la brique qui tient la promesse « ajouter une faction sans refaire l'interface » :
    /// si elle produit une palette illisible pour une couleur mal reglee, le defaut n'apparait
    /// qu'a l'ecran, le jour ou la septieme civilisation est ajoutee.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class FactionPaletteTests
    {
        /// <summary>Les six couleurs reellement utilisees par les <c>EmpireDefinition</c> du projet.</summary>
        private static readonly Color[] RosterColors =
        {
            new Color(0.25f, 0.55f, 0.95f), // Federation de l'Aube
            new Color(0.95f, 0.60f, 0.15f), // Essaim de Kethra
            new Color(0.85f, 0.20f, 0.20f), // Bastion de Drathmoor
            new Color(0.90f, 0.80f, 0.20f), // Ligue Marchande d'Oskar
            new Color(0.35f, 0.75f, 0.45f), // Sanctuaire de Vharin
            new Color(0.60f, 0.30f, 0.75f), // Cartel des Confins
        };

        private static float Brightness(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// <summary>
        /// Compare canal par canal avec une tolerance.
        /// <para>
        /// <b>Une egalite stricte serait fausse</b> : <c>Color.Lerp</c> calcule
        /// <c>a + (b - a) * t</c>, et a <c>t = 1</c> le resultat n'est pas exactement <c>b</c>
        /// des que <c>b - a</c> n'est pas representable exactement. Exiger l'egalite au bit pres
        /// ferait echouer un test qui decrit pourtant le bon comportement.
        /// </para>
        /// </summary>
        private static void AssertSameColor(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-5f, message);
            Assert.AreEqual(expected.g, actual.g, 1e-5f, message);
            Assert.AreEqual(expected.b, actual.b, 1e-5f, message);
            Assert.AreEqual(expected.a, actual.a, 1e-5f, message);
        }

        [Test]
        public void EveryRosterColor_ProducesAReadablePalette()
        {
            foreach (Color source in RosterColors)
            {
                FactionPalette palette = FactionPalette.FromAccent(source);

                Assert.Greater(
                    Brightness(palette.Text) - Brightness(palette.Panel), 0.6f,
                    "Le texte doit trancher nettement sur le fond de panneau.");

                Assert.Greater(
                    Brightness(palette.Accent) - Brightness(palette.Background), 0.25f,
                    "L'accent doit rester visible sur le fond d'ecran.");

                Assert.Greater(
                    Brightness(palette.TextMuted), Brightness(palette.Panel),
                    "Meme le texte secondaire doit etre plus clair que son fond.");
            }
        }

        [Test]
        public void EveryRosterColor_KeepsItsHueThroughoutThePalette()
        {
            // C'est ce qui doit donner l'impression de changer de monde : le fond lui-meme est
            // teinte. Un assombrissement en RVB aurait vire au gris.
            foreach (Color source in RosterColors)
            {
                FactionPalette palette = FactionPalette.FromAccent(source);

                Color.RGBToHSV(source, out float sourceHue, out _, out _);
                Color.RGBToHSV(palette.Background, out float backgroundHue, out float backgroundSaturation, out _);

                Assert.Greater(backgroundSaturation, 0.1f, "Le fond doit rester teinte, pas gris.");
                Assert.AreEqual(sourceHue, backgroundHue, 0.01f, "La teinte doit survivre a l'assombrissement.");
            }
        }

        [Test]
        public void PalePaletteSource_IsBroughtBackToAUsableAccent()
        {
            // Une couleur presque blanche reglee par erreur dans l'editeur.
            FactionPalette palette = FactionPalette.FromAccent(new Color(0.95f, 0.95f, 0.92f));

            Color.RGBToHSV(palette.Accent, out _, out float saturation, out _);
            Assert.GreaterOrEqual(saturation, 0.44f, "Un accent delave doit etre resature pour rester identifiable.");
        }

        [Test]
        public void DarkPaletteSource_IsBroughtBackToAUsableAccent()
        {
            FactionPalette palette = FactionPalette.FromAccent(new Color(0.04f, 0.02f, 0.09f));

            Color.RGBToHSV(palette.Accent, out _, out _, out float value);
            Assert.GreaterOrEqual(value, 0.59f, "Un accent trop sombre doit etre eclairci pour rester visible.");
        }

        [Test]
        public void BackgroundIsAlwaysDarkerThanPanel()
        {
            foreach (Color source in RosterColors)
            {
                FactionPalette palette = FactionPalette.FromAccent(source);
                Assert.Less(
                    Brightness(palette.Background), Brightness(palette.Panel),
                    "Un panneau doit se detacher du fond, sinon il disparait.");
            }
        }

        [Test]
        public void EveryChannelIsOpaque()
        {
            FactionPalette palette = FactionPalette.FromAccent(RosterColors[0]);

            Assert.AreEqual(1f, palette.Accent.a);
            Assert.AreEqual(1f, palette.Panel.a);
            Assert.AreEqual(1f, palette.Background.a);
            Assert.AreEqual(1f, palette.Text.a);
        }

        [Test]
        public void Lerp_ReturnsEachEndAtItsExtremes()
        {
            FactionPalette blue = FactionPalette.FromAccent(RosterColors[0]);
            FactionPalette orange = FactionPalette.FromAccent(RosterColors[1]);

            AssertSameColor(blue.Accent, FactionPalette.Lerp(blue, orange, 0f).Accent, "A t = 0, la palette de depart doit etre rendue telle quelle.");
            AssertSameColor(orange.Accent, FactionPalette.Lerp(blue, orange, 1f).Accent, "A t = 1, la palette d'arrivee doit etre atteinte.");
        }

        [Test]
        public void Lerp_ClampsOutOfRangeProgress()
        {
            FactionPalette blue = FactionPalette.FromAccent(RosterColors[0]);
            FactionPalette orange = FactionPalette.FromAccent(RosterColors[1]);

            AssertSameColor(blue.Accent, FactionPalette.Lerp(blue, orange, -2f).Accent, "Une progression negative doit etre ramenee a 0.");
            AssertSameColor(orange.Accent, FactionPalette.Lerp(blue, orange, 4f).Accent, "Une progression superieure a 1 doit etre ramenee a 1.");
        }

        [Test]
        public void Approach_ConvergesOnTheTargetPalette()
        {
            FactionPalette current = FactionPalette.FromAccent(RosterColors[0]);
            FactionPalette target = FactionPalette.FromAccent(RosterColors[1]);

            for (int i = 0; i < 240; i++)
            {
                current = current.Approach(target, UiEasing.PaletteSmoothing, 1f / 60f);
            }

            Assert.AreEqual(target.Accent.r, current.Accent.r, 0.002f);
            Assert.AreEqual(target.Background.b, current.Background.b, 0.002f);
        }

        [Test]
        public void AccentAt_OnlyChangesOpacity()
        {
            FactionPalette palette = FactionPalette.FromAccent(RosterColors[2]);
            Color tinted = palette.AccentAt(0.2f);

            Assert.AreEqual(palette.Accent.r, tinted.r);
            Assert.AreEqual(palette.Accent.g, tinted.g);
            Assert.AreEqual(palette.Accent.b, tinted.b);
            Assert.AreEqual(0.2f, tinted.a);
        }

        [Test]
        public void AccentAt_ClampsOpacity()
        {
            FactionPalette palette = FactionPalette.FromAccent(RosterColors[2]);

            Assert.AreEqual(1f, palette.AccentAt(3f).a);
            Assert.AreEqual(0f, palette.AccentAt(-1f).a);
        }

        [Test]
        public void Neutral_IsReadableToo()
        {
            FactionPalette palette = FactionPalette.Neutral;
            Assert.Greater(Brightness(palette.Text) - Brightness(palette.Panel), 0.6f);
        }
    }
}
