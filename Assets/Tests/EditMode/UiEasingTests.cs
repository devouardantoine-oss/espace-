using Espace.UI;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie l'amortissement des animations d'interface (Phase 21.1).
    /// <para>
    /// Le test qui compte est <see cref="Approach_ReachesTheSameStateWhateverTheFrameRate"/> :
    /// c'est exactement le defaut qu'un <c>Lerp</c> par frame introduirait, et il est
    /// indetectable a l'œil sur la machine de developpement — il ne se voit que sur un
    /// telephone qui rame.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class UiEasingTests
    {
        [Test]
        public void Blend_IsImmediateWhenSmoothingIsZero()
        {
            Assert.AreEqual(1f, UiEasing.Blend(0f, 0.016f));
            Assert.AreEqual(1f, UiEasing.Blend(-1f, 0.016f));
        }

        [Test]
        public void Blend_IsZeroWhenNoTimePassed()
        {
            Assert.AreEqual(1f, UiEasing.Blend(0.2f, 0f), "Une frame de duree nulle ne doit pas figer l'animation a mi-chemin.");
        }

        [Test]
        public void Blend_CoversAboutSixtyThreePercentAfterOneTimeConstant()
        {
            // Propriete definissant la constante de temps d'un amortissement exponentiel.
            float blend = UiEasing.Blend(0.2f, 0.2f);

            Assert.AreEqual(0.632f, blend, 0.002f);
        }

        [Test]
        public void Approach_ReachesTheSameStateWhateverTheFrameRate()
        {
            const float target = 100f;
            const float smoothing = 0.15f;

            float at60 = 0f;
            for (int i = 0; i < 60; i++)
            {
                at60 = UiEasing.Approach(at60, target, smoothing, 1f / 60f);
            }

            float at30 = 0f;
            for (int i = 0; i < 30; i++)
            {
                at30 = UiEasing.Approach(at30, target, smoothing, 1f / 30f);
            }

            Assert.AreEqual(at60, at30, 0.01f, "Apres une seconde, les deux cadences doivent avoir converge au meme point.");
        }

        [Test]
        public void Approach_ConvergesTowardsTheTarget()
        {
            float value = 0f;
            for (int i = 0; i < 240; i++)
            {
                value = UiEasing.Approach(value, 1f, 0.12f, 1f / 60f);
            }

            Assert.AreEqual(1f, value, 0.001f);
        }

        [Test]
        public void Approach_OnColorsConvergesOnEveryChannel()
        {
            Color color = Color.black;
            for (int i = 0; i < 240; i++)
            {
                color = UiEasing.Approach(color, Color.white, 0.12f, 1f / 60f);
            }

            Assert.AreEqual(1f, color.r, 0.001f);
            Assert.AreEqual(1f, color.g, 0.001f);
            Assert.AreEqual(1f, color.b, 0.001f);
            Assert.AreEqual(1f, color.a, 0.001f);
        }

        [Test]
        public void EaseOutCubic_StaysWithinItsBoundsAndStartsFast()
        {
            Assert.AreEqual(0f, UiEasing.EaseOutCubic(0f));
            Assert.AreEqual(1f, UiEasing.EaseOutCubic(1f));
            Assert.AreEqual(1f, UiEasing.EaseOutCubic(5f), "Une progression au-dela de 1 doit rester bornee.");
            Assert.AreEqual(0f, UiEasing.EaseOutCubic(-3f));

            // Le propre d'un depart franc : a mi-parcours, plus de la moitie du chemin est fait.
            Assert.Greater(UiEasing.EaseOutCubic(0.5f), 0.5f);
        }

        [Test]
        public void SmoothStep01_IsSymmetricAroundItsMiddle()
        {
            Assert.AreEqual(0.5f, UiEasing.SmoothStep01(0.5f), 0.0001f);
            Assert.AreEqual(1f - UiEasing.SmoothStep01(0.25f), UiEasing.SmoothStep01(0.75f), 0.0001f);
        }

        [Test]
        public void StaggeredReveal_DelaysEachElementInTurn()
        {
            const float stagger = 0.06f;
            const float duration = 0.25f;

            // A 0,05 s, le deuxieme element n'a pas encore commence.
            Assert.Greater(UiEasing.StaggeredReveal(0.05f, 0, stagger, duration), 0f);
            Assert.AreEqual(0f, UiEasing.StaggeredReveal(0.05f, 1, stagger, duration));

            // Une fois la cascade finie, tout le monde est en place.
            Assert.AreEqual(1f, UiEasing.StaggeredReveal(2f, 5, stagger, duration));
        }

        [Test]
        public void StaggeredReveal_WithoutDurationIsInstant()
        {
            Assert.AreEqual(1f, UiEasing.StaggeredReveal(0f, 3, 0.1f, 0f));
        }

        [Test]
        public void Pulse_OscillatesBetweenItsBounds()
        {
            Assert.AreEqual(0f, UiEasing.Pulse(0f, 4f), 0.0001f);
            Assert.AreEqual(1f, UiEasing.Pulse(2f, 4f), 0.0001f);
            Assert.AreEqual(0f, UiEasing.Pulse(4f, 4f), 0.0001f, "Un cycle complet doit revenir au point de depart.");
        }

        [Test]
        public void Pulse_WithoutPeriodStaysFlat()
        {
            Assert.AreEqual(0f, UiEasing.Pulse(3f, 0f));
        }
    }
}
