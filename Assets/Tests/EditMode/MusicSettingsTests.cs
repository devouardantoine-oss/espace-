using Espace.Core;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie que <see cref="MusicSettings"/> assainit les valeurs saisies dans l'asset
    /// (Phase 21) : elles viennent d'un champ d'editeur, donc de l'exterieur du code.
    /// </summary>
    [TestFixture]
    public sealed class MusicSettingsTests
    {
        [Test]
        public void NegativeDurations_AreBroughtBackToZero()
        {
            var settings = new MusicSettings(-1f, -5f, -2f, 0.5f);

            Assert.AreEqual(0f, settings.FadeSeconds);
            Assert.AreEqual(0f, settings.MinimumSilenceSeconds);
            Assert.AreEqual(0f, settings.MaximumSilenceSeconds);
        }

        [Test]
        public void InvertedSilenceRange_IsCollapsedOntoItsMinimum()
        {
            // Sans cela, le tirage du silence porterait sur un intervalle vide.
            var settings = new MusicSettings(2f, 10f, 3f, 0.5f);

            Assert.AreEqual(10f, settings.MinimumSilenceSeconds);
            Assert.AreEqual(10f, settings.MaximumSilenceSeconds);
        }

        [Test]
        public void Volume_IsClampedToTheAudibleRange()
        {
            Assert.AreEqual(1f, new MusicSettings(2f, 3f, 8f, 4f).Volume);
            Assert.AreEqual(0f, new MusicSettings(2f, 3f, 8f, -1f).Volume);
        }

        [Test]
        public void Default_MatchesTheAgreedPacing()
        {
            MusicSettings settings = MusicSettings.Default;

            Assert.AreEqual(2f, settings.FadeSeconds);
            Assert.AreEqual(3f, settings.MinimumSilenceSeconds);
            Assert.AreEqual(8f, settings.MaximumSilenceSeconds);
        }
    }
}
