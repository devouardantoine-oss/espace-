using System;
using UnityEngine;

namespace Espace.Core
{
    /// <summary>Etape courante de l'enchainement des morceaux.</summary>
    public enum MusicPhase
    {
        /// <summary>Aucun son : l'intervalle entre deux morceaux s'ecoule.</summary>
        Silent,

        /// <summary>Le morceau vient de demarrer et monte progressivement.</summary>
        FadingIn,

        /// <summary>Le morceau joue a plein volume.</summary>
        Playing,

        /// <summary>Le morceau s'efface avant l'intervalle suivant.</summary>
        FadingOut
    }

    /// <summary>
    /// Enchainement des morceaux de la playlist : ordre fixe, en boucle, avec un fondu de
    /// sortie, un silence de duree variable, puis un fondu d'entree (Phase 21).
    /// <para>
    /// <b>Objet C# pur, sans <c>AudioSource</c> :</b> c'est ce qui rend l'enchainement
    /// verifiable en EditMode. Le curseur ne sait pas lire un son — il dit seulement quel
    /// morceau doit jouer, a quel volume, et a quel instant en demarrer un nouveau.
    /// <see cref="MusicService"/> se charge de traduire ces decisions en appels moteur.
    /// </para>
    /// <para>
    /// <b>Le silence est tire une seule fois par intervalle</b>, a l'entree de la phase, et non
    /// releve a chaque frame : une duree retiree du generateur a chaque image ne convergerait
    /// jamais vers une fin d'intervalle.
    /// </para>
    /// <para>
    /// <b>Le premier morceau demarre sans attendre :</b> l'intervalle initial vaut zero, pour
    /// que la musique accompagne le lancement du jeu plutot que de le faire patienter.
    /// </para>
    /// </summary>
    public sealed class MusicPlaylistCursor
    {
        /// <summary>
        /// Part maximale d'un morceau consacree au fondu de sortie. Sans ce garde-fou, un
        /// morceau plus court que le fondu commencerait a s'effacer avant meme d'etre monte.
        /// </summary>
        private const float MaximumFadeOutShare = 0.5f;

        private readonly MusicSettings _settings;
        private readonly Func<float> _silenceSampler;
        private readonly int _trackCount;

        private MusicPhase _phase = MusicPhase.Silent;

        /// <summary>Temps ecoule depuis l'entree dans <see cref="_phase"/>.</summary>
        private float _phaseElapsed;

        /// <summary>Temps ecoule depuis le debut du morceau courant.</summary>
        private float _trackElapsed;

        /// <summary>Duree du silence en cours, tiree a l'entree de la phase <see cref="MusicPhase.Silent"/>.</summary>
        private float _silenceSeconds;

        private int _trackIndex;

        /// <summary>Vrai des qu'un premier morceau a ete lance : c'est ce qui distingue le demarrage du jeu d'un enchainement.</summary>
        private bool _hasPlayed;

        /// <param name="trackCount">Nombre de morceaux disponibles. Zero est accepte : le curseur reste alors muet.</param>
        /// <param name="settings">Durees de fondu et d'intervalle.</param>
        /// <param name="silenceSampler">
        /// Tire la duree du prochain silence. Injecte plutot que tire en interne pour que les
        /// tests puissent imposer une valeur exacte — un <c>Random</c> cache rendrait
        /// l'enchainement invérifiable.
        /// </param>
        public MusicPlaylistCursor(int trackCount, MusicSettings settings, Func<float> silenceSampler)
        {
            if (silenceSampler == null)
            {
                throw new ArgumentNullException(nameof(silenceSampler));
            }

            _trackCount = Mathf.Max(0, trackCount);
            _settings = settings;
            _silenceSampler = silenceSampler;

            // Intervalle initial nul : le premier Tick demande aussitot la lecture du morceau 0.
            _silenceSeconds = 0f;
        }

        /// <summary>Index du morceau courant (ou du prochain, pendant un silence).</summary>
        public int TrackIndex => _trackIndex;

        /// <summary>Etape courante de l'enchainement.</summary>
        public MusicPhase Phase => _phase;

        /// <summary>
        /// Volume a appliquer au morceau, entre 0 et 1. C'est un <b>multiplicateur</b> du volume
        /// choisi par le joueur, pas un volume absolu.
        /// </summary>
        public float VolumeMultiplier
        {
            get
            {
                if (_settings.FadeSeconds <= 0f)
                {
                    return _phase == MusicPhase.Silent ? 0f : 1f;
                }

                switch (_phase)
                {
                    case MusicPhase.FadingIn:
                        return Mathf.Clamp01(_phaseElapsed / _settings.FadeSeconds);
                    case MusicPhase.Playing:
                        return 1f;
                    case MusicPhase.FadingOut:
                        return Mathf.Clamp01(1f - _phaseElapsed / _settings.FadeSeconds);
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>
        /// Fait avancer l'enchainement d'une frame.
        /// </summary>
        /// <param name="deltaTime">Temps ecoule, en secondes <b>reelles</b> : la musique ne suit pas la vitesse de jeu et continue en pause.</param>
        /// <param name="trackLengthSeconds">Duree du morceau courant. Une valeur nulle ou negative laisse le morceau jouer indefiniment.</param>
        /// <returns><c>true</c> si l'appelant doit demarrer la lecture de <see cref="TrackIndex"/> maintenant.</returns>
        public bool Tick(float deltaTime, float trackLengthSeconds)
        {
            if (_trackCount == 0 || deltaTime <= 0f)
            {
                return false;
            }

            _phaseElapsed += deltaTime;

            switch (_phase)
            {
                case MusicPhase.Silent:
                    return TickSilent();

                case MusicPhase.FadingIn:
                    _trackElapsed += deltaTime;
                    if (_phaseElapsed >= _settings.FadeSeconds)
                    {
                        EnterPhase(MusicPhase.Playing);
                    }
                    return false;

                case MusicPhase.Playing:
                    _trackElapsed += deltaTime;
                    if (trackLengthSeconds > 0f && _trackElapsed >= FadeOutStart(trackLengthSeconds))
                    {
                        EnterPhase(MusicPhase.FadingOut);
                    }
                    return false;

                case MusicPhase.FadingOut:
                    _trackElapsed += deltaTime;
                    if (_phaseElapsed >= _settings.FadeSeconds)
                    {
                        // L'index n'avance pas ici : pendant tout le silence, TrackIndex designe
                        // encore le morceau qui vient de finir — c'est lui que le menu pause doit
                        // continuer d'afficher. Il passe au suivant au moment ou la lecture est
                        // reellement demandee (voir TickSilent).
                        EnterPhase(MusicPhase.Silent);
                        _silenceSeconds = SampleSilence();
                    }
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Interrompt le morceau courant et enchaine sur le suivant apres l'intervalle habituel.
        /// Utilise quand la playlist est rechargee ou qu'un morceau s'avere illisible.
        /// </summary>
        public void SkipToNextTrack()
        {
            if (_trackCount == 0)
            {
                return;
            }

            EnterPhase(MusicPhase.Silent);
            _silenceSeconds = SampleSilence();
        }

        private bool TickSilent()
        {
            if (_phaseElapsed < _silenceSeconds)
            {
                return false;
            }

            // Le tout premier demarrage doit jouer le morceau 0, pas le morceau 1 : l'index
            // n'avance donc que si un morceau a deja ete joue.
            _trackIndex = _hasPlayed ? (_trackIndex + 1) % _trackCount : 0;
            _hasPlayed = true;

            EnterPhase(MusicPhase.FadingIn);
            _trackElapsed = 0f;
            return true;
        }

        private void EnterPhase(MusicPhase phase)
        {
            _phase = phase;
            _phaseElapsed = 0f;
        }

        /// <summary>
        /// Instant, dans le morceau, ou le fondu de sortie commence. Il demarre assez tot pour
        /// que le silence tombe pile a la fin du morceau, sans jamais empieter sur plus de la
        /// moitie de sa duree.
        /// </summary>
        private float FadeOutStart(float trackLengthSeconds)
        {
            return Mathf.Max(trackLengthSeconds - _settings.FadeSeconds, trackLengthSeconds * MaximumFadeOutShare);
        }

        private float SampleSilence()
        {
            return Mathf.Clamp(_silenceSampler(), _settings.MinimumSilenceSeconds, _settings.MaximumSilenceSeconds);
        }
    }
}
