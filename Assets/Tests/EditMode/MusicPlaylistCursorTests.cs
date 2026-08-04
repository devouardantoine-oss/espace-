using System;
using Espace.Core;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie l'enchainement des morceaux (Phase 21) : ordre fixe, boucle, fondus et silences.
    /// <para>
    /// <b>Tout est verifiable sans moteur audio</b> parce que <see cref="MusicPlaylistCursor"/>
    /// ne lit aucun son : il decide seulement quoi jouer et a quel volume. Sans cette
    /// separation, il faudrait un mode Play et une oreille pour constater qu'un fondu dure la
    /// bonne duree.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class MusicPlaylistCursorTests
    {
        private const float Frame = 1f / 60f;
        private const float TrackLength = 60f;

        /// <summary>Reglages nets a lire dans les assertions : deux secondes de fondu, cinq de silence, exactement.</summary>
        private static MusicSettings Settings => new MusicSettings(2f, 5f, 5f, 1f);

        private static MusicPlaylistCursor NewCursor(int trackCount = 3, MusicSettings? settings = null, float silence = 5f)
        {
            return new MusicPlaylistCursor(trackCount, settings ?? Settings, () => silence);
        }

        /// <summary>Avance le curseur de <paramref name="seconds"/> et renvoie le nombre de demarrages demandes.</summary>
        private static int Advance(MusicPlaylistCursor cursor, float seconds, float trackLength = TrackLength)
        {
            int starts = 0;
            int frames = (int)Math.Round(seconds / Frame);
            for (int i = 0; i < frames; i++)
            {
                if (cursor.Tick(Frame, trackLength))
                {
                    starts++;
                }
            }

            return starts;
        }

        [Test]
        public void Constructor_RejectsNullSilenceSampler()
        {
            Assert.Throws<ArgumentNullException>(() => new MusicPlaylistCursor(2, Settings, null));
        }

        [Test]
        public void FirstTick_StartsTheFirstTrackImmediately()
        {
            MusicPlaylistCursor cursor = NewCursor();

            Assert.IsTrue(cursor.Tick(Frame, TrackLength), "Le premier morceau doit demarrer sans attendre.");
            Assert.AreEqual(0, cursor.TrackIndex);
            Assert.AreEqual(MusicPhase.FadingIn, cursor.Phase);
        }

        [Test]
        public void EmptyPlaylist_NeverStartsAnything()
        {
            MusicPlaylistCursor cursor = NewCursor(trackCount: 0);

            Assert.AreEqual(0, Advance(cursor, 120f));
            Assert.AreEqual(MusicPhase.Silent, cursor.Phase);
            Assert.AreEqual(0f, cursor.VolumeMultiplier);
        }

        [Test]
        public void FadeIn_RisesFromSilenceToFullVolume()
        {
            MusicPlaylistCursor cursor = NewCursor();
            cursor.Tick(Frame, TrackLength);

            Advance(cursor, 1f);
            float midway = cursor.VolumeMultiplier;

            Assert.Greater(midway, 0.3f, "A mi-fondu, le volume doit avoir sensiblement monte.");
            Assert.Less(midway, 0.7f, "A mi-fondu, le volume ne doit pas etre deja au maximum.");
        }

        [Test]
        public void FadeIn_ReachesPlayingAfterTheFadeDuration()
        {
            MusicPlaylistCursor cursor = NewCursor();
            cursor.Tick(Frame, TrackLength);

            Advance(cursor, 2.5f);

            Assert.AreEqual(MusicPhase.Playing, cursor.Phase);
            Assert.AreEqual(1f, cursor.VolumeMultiplier);
        }

        [Test]
        public void FadeOut_StartsOneFadeBeforeTheEndOfTheTrack()
        {
            MusicPlaylistCursor cursor = NewCursor();
            cursor.Tick(Frame, TrackLength);

            // A une seconde de la fin du fondu de sortie theorique (60 - 2 = 58 s).
            Advance(cursor, 57f);
            Assert.AreEqual(MusicPhase.Playing, cursor.Phase, "Le fondu de sortie ne doit pas avoir commence.");

            Advance(cursor, 2f);
            Assert.AreEqual(MusicPhase.FadingOut, cursor.Phase);
        }

        [Test]
        public void ShortTrack_KeepsAtLeastHalfOfItselfAtFullVolume()
        {
            // Un morceau plus court que le fondu commencerait a s'effacer avant d'etre monte.
            MusicPlaylistCursor cursor = NewCursor();
            cursor.Tick(Frame, trackLengthSeconds: 2f);

            Advance(cursor, 0.5f, trackLength: 2f);
            Assert.AreNotEqual(MusicPhase.FadingOut, cursor.Phase, "Le fondu de sortie ne doit pas commencer avant la moitie du morceau.");
        }

        [Test]
        public void Silence_LastsTheSampledDuration()
        {
            MusicPlaylistCursor cursor = NewCursor(silence: 5f);
            cursor.Tick(Frame, TrackLength);

            // Fondu d'entree (2 s), lecture, fondu de sortie (2 s) : le silence commence a 60 s.
            Advance(cursor, 61f);
            Assert.AreEqual(MusicPhase.Silent, cursor.Phase);

            Assert.AreEqual(0, Advance(cursor, 4f), "Le morceau suivant ne doit pas demarrer avant la fin du silence.");
            Assert.AreEqual(1, Advance(cursor, 2f), "Le morceau suivant doit demarrer a la fin du silence.");
        }

        [Test]
        public void Silence_IsClampedToTheConfiguredRange()
        {
            // Un tirage aberrant ne doit pas produire un silence de dix minutes.
            var settings = new MusicSettings(2f, 3f, 8f, 1f);
            var cursor = new MusicPlaylistCursor(2, settings, () => 600f);

            cursor.Tick(Frame, TrackLength);
            Advance(cursor, 61f);
            Assert.AreEqual(MusicPhase.Silent, cursor.Phase);

            Assert.AreEqual(1, Advance(cursor, 9f), "Le silence doit etre ramene au maximum configure (8 s).");
        }

        [Test]
        public void Playlist_AdvancesInFixedOrderThenLoops()
        {
            MusicPlaylistCursor cursor = NewCursor(trackCount: 3);
            var order = new System.Collections.Generic.List<int>();

            // Un morceau occupe 60 s de lecture plus 5 s de silence : quatre cycles suffisent
            // a observer la boucle complete.
            for (int i = 0; i < 4 * 65 * 60; i++)
            {
                if (cursor.Tick(Frame, TrackLength))
                {
                    order.Add(cursor.TrackIndex);
                }
            }

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 0 }, order);
        }

        [Test]
        public void SingleTrack_RepeatsItselfWithTheUsualSilence()
        {
            MusicPlaylistCursor cursor = NewCursor(trackCount: 1);
            cursor.Tick(Frame, TrackLength);

            Advance(cursor, 66f);

            Assert.AreEqual(0, cursor.TrackIndex);
            Assert.AreEqual(MusicPhase.FadingIn, cursor.Phase);
        }

        [Test]
        public void TrackIndex_StaysOnTheFinishedTrackDuringTheSilence()
        {
            // Le titre affiche dans le menu pause ne doit pas annoncer le morceau suivant
            // pendant que rien ne joue.
            MusicPlaylistCursor cursor = NewCursor(trackCount: 3);
            cursor.Tick(Frame, TrackLength);

            Advance(cursor, 61f);

            Assert.AreEqual(MusicPhase.Silent, cursor.Phase);
            Assert.AreEqual(0, cursor.TrackIndex);
        }

        [Test]
        public void SkipToNextTrack_InterruptsAndMovesOn()
        {
            MusicPlaylistCursor cursor = NewCursor(trackCount: 3);
            cursor.Tick(Frame, TrackLength);
            Advance(cursor, 10f);

            cursor.SkipToNextTrack();
            Assert.AreEqual(MusicPhase.Silent, cursor.Phase);
            Assert.AreEqual(0f, cursor.VolumeMultiplier);

            Assert.AreEqual(1, Advance(cursor, 6f));
            Assert.AreEqual(1, cursor.TrackIndex);
        }

        [Test]
        public void SkipToNextTrack_OnEmptyPlaylistDoesNothing()
        {
            MusicPlaylistCursor cursor = NewCursor(trackCount: 0);

            Assert.DoesNotThrow(() => cursor.SkipToNextTrack());
            Assert.AreEqual(MusicPhase.Silent, cursor.Phase);
        }

        [Test]
        public void ZeroFade_PlaysAtFullVolumeWithoutTransition()
        {
            var settings = new MusicSettings(0f, 1f, 1f, 1f);
            var cursor = new MusicPlaylistCursor(2, settings, () => 1f);

            cursor.Tick(Frame, TrackLength);

            Assert.AreEqual(1f, cursor.VolumeMultiplier, "Sans fondu, le morceau doit demarrer a plein volume.");
        }

        [Test]
        public void NonPositiveDeltaTime_LeavesTheCursorUntouched()
        {
            // Une frame de duree nulle survient a la reprise apres une mise en arriere-plan.
            MusicPlaylistCursor cursor = NewCursor();

            Assert.IsFalse(cursor.Tick(0f, TrackLength));
            Assert.AreEqual(MusicPhase.Silent, cursor.Phase);
        }

        [Test]
        public void NonPositiveTrackLength_LetsTheTrackPlayOn()
        {
            // Duree inconnue (un flux, ou un clip non encore charge) : mieux vaut laisser jouer
            // que couper toutes les deux secondes.
            MusicPlaylistCursor cursor = NewCursor();
            cursor.Tick(Frame, trackLengthSeconds: 0f);

            Advance(cursor, 300f, trackLength: 0f);

            Assert.AreEqual(MusicPhase.Playing, cursor.Phase);
            Assert.AreEqual(1f, cursor.VolumeMultiplier);
        }
    }
}
