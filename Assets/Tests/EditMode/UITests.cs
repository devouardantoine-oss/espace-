using System.IO;
using Espace.Core;
using Espace.Gameplay.Save;
using Espace.UI;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Tests de la seule logique pure de la Phase 11 (interface) : <see cref="HudFormatter"/>
    /// et <see cref="SaveFileLocator"/>. Le reste de la phase (agencement IMGUI, boutons) n'est
    /// verifiable qu'en Play Mode par l'utilisateur — voir la remarque de <see cref="UITheme"/>.
    /// </summary>
    [TestFixture]
    public class HudFormatterTests
    {
        [Test]
        public void FormatDate_UsesReadableFrenchLayout()
        {
            var date = new GameDate(3, 7, 12);
            Assert.AreEqual("An 3, Mois 7, Jour 12", HudFormatter.FormatDate(date));
        }

        [TestCase(0f, "0")]
        [TestCase(750f, "750")]
        [TestCase(999f, "999")]
        [TestCase(1000f, "1.0k")]
        [TestCase(1234f, "1.2k")]
        [TestCase(999_000f, "999.0k")]
        [TestCase(1_000_000f, "1.0M")]
        [TestCase(4_500_000f, "4.5M")]
        public void FormatResource_AbbreviatesAboveThresholds(float value, string expected)
        {
            Assert.AreEqual(expected, HudFormatter.FormatResource(value));
        }

        [Test]
        public void FormatResource_NegativeValue_KeepsSign()
        {
            Assert.AreEqual("-1.5k", HudFormatter.FormatResource(-1500f));
        }

        [Test]
        public void FormatSigned_PositiveValue_HasPlusPrefix()
        {
            Assert.AreEqual("+250", HudFormatter.FormatSigned(250f));
        }

        [Test]
        public void FormatSigned_NegativeValue_HasMinusPrefix()
        {
            Assert.AreEqual("-250", HudFormatter.FormatSigned(-250f));
        }

        [Test]
        public void FormatSigned_ZeroValue_HasNoPrefix()
        {
            Assert.AreEqual("0", HudFormatter.FormatSigned(0f));
        }

        [TestCase(0f, "0%")]
        [TestCase(0.25f, "25%")]
        [TestCase(0.5f, "50%")]
        [TestCase(1f, "100%")]
        public void FormatPercent_ConvertsFractionToRoundedPercent(float fraction, string expected)
        {
            Assert.AreEqual(expected, HudFormatter.FormatPercent(fraction));
        }
    }

    [TestFixture]
    public class SaveFileLocatorTests
    {
        private string _backupContent;
        private bool _fileExistedBeforeTest;

        [SetUp]
        public void SetUp()
        {
            _fileExistedBeforeTest = File.Exists(SaveFileLocator.FilePath);
            if (_fileExistedBeforeTest)
            {
                _backupContent = File.ReadAllText(SaveFileLocator.FilePath);
                File.Delete(SaveFileLocator.FilePath);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(SaveFileLocator.FilePath))
            {
                File.Delete(SaveFileLocator.FilePath);
            }

            if (_fileExistedBeforeTest)
            {
                File.WriteAllText(SaveFileLocator.FilePath, _backupContent);
            }
        }

        [Test]
        public void FilePath_EndsWithSaveFileName()
        {
            StringAssert.EndsWith("savegame.json", SaveFileLocator.FilePath);
        }

        [Test]
        public void Exists_NoFile_ReturnsFalse()
        {
            Assert.IsFalse(SaveFileLocator.Exists());
        }

        [Test]
        public void Exists_FilePresent_ReturnsTrue()
        {
            File.WriteAllText(SaveFileLocator.FilePath, "{}");
            Assert.IsTrue(SaveFileLocator.Exists());
        }

        [Test]
        public void DeleteIfExists_FilePresent_RemovesIt()
        {
            File.WriteAllText(SaveFileLocator.FilePath, "{}");
            SaveFileLocator.DeleteIfExists();
            Assert.IsFalse(File.Exists(SaveFileLocator.FilePath));
        }

        [Test]
        public void DeleteIfExists_NoFile_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => SaveFileLocator.DeleteIfExists());
        }
    }

    /// <summary>
    /// Verifie <see cref="UITheme.ComputeScale"/> : l'agrandissement de l'interface deduit de
    /// la densite de l'ecran (correction mobile). La formule est isolee de <c>Screen</c>
    /// justement pour etre verifiable ici, comme <see cref="HudFormatter"/>.
    /// </summary>
    [TestFixture]
    public class UIThemeScaleTests
    {
        private const float FloatTolerance = 0.001f;

        [Test]
        public void ComputeScale_DesktopDensity_LeavesInterfaceUntouched()
        {
            // ~96 ppp donnerait un rapport de 0,6 : l'echelle ne doit jamais retrecir une
            // interface deja correctement dimensionnee pour un ecran d'ordinateur.
            Assert.AreEqual(1f, UITheme.ComputeScale(96f, 1920), FloatTolerance);
        }

        [Test]
        public void ComputeScale_UnknownDensity_LeavesInterfaceUntouched()
        {
            // Screen.dpi vaut 0 quand le systeme ne la connait pas : on ne devine pas.
            Assert.AreEqual(1f, UITheme.ComputeScale(0f, 1920), FloatTolerance);
        }

        [Test]
        public void ComputeScale_PhoneDensity_EnlargesInterface()
        {
            // Redmi Note 13 Pro en paysage : 446 ppp / 160 = 2,79.
            Assert.AreEqual(2.7875f, UITheme.ComputeScale(446f, 2712), FloatTolerance);
        }

        [Test]
        public void ComputeScale_NarrowScreen_IsCappedSoTheWidestPanelStillFits()
        {
            // Le meme telephone en portrait : la densite reclamerait 2,79, mais la fenetre de
            // gestion (660 unites) deborderait. L'echelle se bride a 1220/700 = 1,74.
            float scale = UITheme.ComputeScale(446f, 1220);

            Assert.Less(scale, 2.7875f, "L'echelle doit se brider sur un ecran etroit.");
            Assert.GreaterOrEqual(1220 / scale, 660f, "Le plus large panneau doit toujours tenir.");
        }

        [TestCase(96f, 1920)]
        [TestCase(446f, 1220)]
        [TestCase(446f, 2712)]
        [TestCase(264f, 2360)]
        [TestCase(640f, 1440)]
        public void ComputeScale_WidestPanelAlwaysFits(float dpi, int screenWidth)
        {
            // Propriete structurante : quelle que soit la machine, la fenetre de gestion doit
            // rester entierement visible.
            Assert.GreaterOrEqual(screenWidth / UITheme.ComputeScale(dpi, screenWidth), 660f);
        }

        [Test]
        public void ComputeScale_ExtremeDensity_IsCappedToKeepTheInterfaceSane()
        {
            Assert.LessOrEqual(UITheme.ComputeScale(2000f, 8000), 4f);
        }
    }
}
