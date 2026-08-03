using System.Collections.Generic;
using UnityEngine;

namespace Espace.Core
{
    /// <summary>
    /// Implementation de <see cref="IMusicService"/> : possede l'<c>AudioSource</c> et traduit
    /// les decisions de <see cref="MusicPlaylistCursor"/> en appels moteur (Phase 21).
    /// <para>
    /// <b>Une seule <c>AudioSource</c>, pas deux.</b> Un vrai fondu enchaine — deux morceaux
    /// qui se superposent — demanderait deux sources et le double de memoire de decodage. Le
    /// joueur a retenu un enchainement « fondu de sortie, court silence, fondu d'entree » : les
    /// deux morceaux ne se chevauchent jamais, donc une source suffit.
    /// </para>
    /// <para>
    /// <b>Le GameObject est cree par le service, pas pose dans la scene.</b> La musique doit
    /// survivre au passage du menu a la carte galactique ; un objet de scene serait detruit au
    /// chargement suivant et la playlist repartirait du debut a chaque ecran.
    /// </para>
    /// <para>
    /// <b>Rythme en temps reel (<c>unscaledDeltaTime</c>), jamais en temps de jeu :</b> la
    /// musique ne doit ni accelerer avec la vitesse x4 ni s'arreter quand le joueur met la
    /// partie en pause.
    /// </para>
    /// <para>
    /// <b>Le volume est une preference d'appareil, pas un etat de partie</b> : il vit dans
    /// <c>PlayerPrefs</c> et non dans la sauvegarde, sinon recharger une partie imposerait au
    /// joueur le reglage sonore d'un autre moment — voire d'un autre telephone.
    /// </para>
    /// </summary>
    public sealed class MusicService : IMusicService
    {
        /// <summary>Cle <c>PlayerPrefs</c> du volume. Prefixee par le jeu : <c>PlayerPrefs</c> est un espace de noms partage par toutes les applications de l'editeur.</summary>
        private const string VolumePreferenceKey = "espace.music.volume";

        /// <summary>Cle <c>PlayerPrefs</c> de la coupure du son (0 ou 1 : <c>PlayerPrefs</c> ne stocke pas de booleen).</summary>
        private const string MutedPreferenceKey = "espace.music.muted";

        private const string HostObjectName = "[Music]";

        /// <summary>
        /// Duree maximale prise en compte pour une frame.
        /// <para>
        /// <c>Time.unscaledDeltaTime</c> n'est pas plafonne par <c>Time.maximumDeltaTime</c>,
        /// contrairement a <c>Time.deltaTime</c> : la premiere frame apres un retour
        /// d'arriere-plan vaut la duree reelle de l'absence, potentiellement des minutes.
        /// Transmise telle quelle, elle ferait traverser au curseur tout le morceau que
        /// l'<c>AudioSource</c>, elle, vient seulement de reprendre au meme endroit.
        /// </para>
        /// </summary>
        private const float MaximumFrameSeconds = 0.25f;

        private readonly AudioClip[] _tracks;
        private readonly MusicSettings _settings;
        private readonly System.Random _random = new System.Random();

        private GameObject _host;
        private AudioSource _source;
        private MusicPlaylistCursor _cursor;

        private float _volume;
        private bool _muted;
        private bool _applicationPaused;

        /// <param name="tracks">Morceaux dans l'ordre de lecture. Les cases vides sont ecartees ; <c>null</c> est accepte et equivaut a une playlist vide.</param>
        /// <param name="settings">Durees de fondu et d'intervalle.</param>
        public MusicService(IReadOnlyList<AudioClip> tracks, MusicSettings settings)
        {
            _settings = settings;
            _tracks = Compact(tracks);
            _volume = settings.Volume;
        }

        /// <inheritdoc />
        public int TrackCount => _tracks.Length;

        /// <inheritdoc />
        public string CurrentTrackName
        {
            get
            {
                if (_cursor == null || _cursor.Phase == MusicPhase.Silent || _tracks.Length == 0)
                {
                    return string.Empty;
                }

                return _tracks[_cursor.TrackIndex].name;
            }
        }

        /// <inheritdoc />
        public float Volume => _volume;

        /// <inheritdoc />
        public bool IsMuted => _muted;

        /// <inheritdoc />
        public void Initialize()
        {
            LoadPreferences();

            if (_tracks.Length == 0)
            {
                // Une information, pas un avertissement : un projet sans fichier musical est un
                // etat parfaitement normal, et le signaler en jaune ferait croire a une panne.
                GameLog.Info("[Music] Aucun morceau charge : la musique d'ambiance reste silencieuse.");
                return;
            }

            _host = new GameObject(HostObjectName);
            Object.DontDestroyOnLoad(_host);

            _source = _host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;

            // Son non spatialise : la musique ne vient d'aucun point de la carte, elle ne doit
            // donc ni s'attenuer ni se deplacer avec la camera.
            _source.spatialBlend = 0f;
            _source.bypassEffects = true;
            _source.bypassListenerEffects = true;
            _source.volume = 0f;

            _cursor = new MusicPlaylistCursor(_tracks.Length, _settings, SampleSilence);

            GameLog.Info($"[Music] {_tracks.Length} morceau(x) charge(s), volume {Mathf.RoundToInt(_volume * 100f)} %{(_muted ? ", son coupe" : string.Empty)}.");
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            SavePreferences();
            PlayerPrefs.Save();

            _cursor = null;
            _source = null;

            if (_host != null)
            {
                Object.Destroy(_host);
                _host = null;
            }
        }

        /// <summary>
        /// Fait avancer l'enchainement. Appele une fois par frame par <c>GameBootstrap</c>,
        /// avec <c>Time.unscaledDeltaTime</c>.
        /// </summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (_cursor == null || _source == null || _applicationPaused)
            {
                return;
            }

            float length = _tracks[_cursor.TrackIndex].length;
            float step = Mathf.Min(unscaledDeltaTime, MaximumFrameSeconds);

            if (_cursor.Tick(step, length))
            {
                _source.clip = _tracks[_cursor.TrackIndex];
                _source.time = 0f;
                _source.Play();
            }
            else if (_cursor.Phase == MusicPhase.Silent && _source.isPlaying)
            {
                // Le fondu de sortie s'acheve un cheveu avant la fin reelle du fichier : sans
                // cet arret, le decodeur continuerait de travailler pendant tout le silence pour
                // un son deja a zero.
                _source.Stop();
            }

            // Le volume est reecrit a chaque frame plutot que seulement pendant les fondus :
            // c'est aussi ce qui applique instantanement un changement de reglage du joueur.
            _source.volume = _muted ? 0f : _volume * _cursor.VolumeMultiplier;
        }

        /// <summary>
        /// Suspend ou reprend la lecture quand l'application passe en arriere-plan.
        /// <para>
        /// Sans cela, le curseur continuerait d'avancer pendant que le systeme a coupe le son :
        /// revenir dans le jeu apres quelques minutes reprendrait la playlist au milieu d'un
        /// morceau muet, voire deux morceaux plus loin.
        /// </para>
        /// </summary>
        public void SetApplicationPaused(bool paused)
        {
            if (_applicationPaused == paused)
            {
                return;
            }

            _applicationPaused = paused;

            if (paused)
            {
                // Le seul instant ou l'on est sur de pouvoir ecrire : Android peut tuer
                // l'application en arriere-plan sans jamais appeler Shutdown, et PlayerPrefs
                // n'est ecrit sur le disque que sur demande explicite.
                PlayerPrefs.Save();
            }

            if (_source == null)
            {
                return;
            }

            if (paused)
            {
                _source.Pause();
            }
            else
            {
                _source.UnPause();
            }
        }

        /// <inheritdoc />
        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
            SavePreferences();
        }

        /// <inheritdoc />
        public void SetMuted(bool muted)
        {
            _muted = muted;
            SavePreferences();
        }

        /// <summary>Duree du prochain silence, tiree uniformement dans l'intervalle configure.</summary>
        private float SampleSilence()
        {
            double span = _settings.MaximumSilenceSeconds - _settings.MinimumSilenceSeconds;
            return (float)(_settings.MinimumSilenceSeconds + _random.NextDouble() * span);
        }

        private void LoadPreferences()
        {
            _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreferenceKey, _settings.Volume));
            _muted = PlayerPrefs.GetInt(MutedPreferenceKey, 0) != 0;
        }

        private void SavePreferences()
        {
            PlayerPrefs.SetFloat(VolumePreferenceKey, _volume);
            PlayerPrefs.SetInt(MutedPreferenceKey, _muted ? 1 : 0);
        }

        /// <summary>
        /// Copie la liste en ecartant les cases vides. Un tableau serialise dans l'editeur en
        /// contient des qu'on l'agrandit sans y deposer de fichier ; laisser passer un
        /// <c>null</c> ferait echouer la lecture au milieu de la playlist, longtemps apres
        /// l'erreur de manipulation.
        /// </summary>
        private static AudioClip[] Compact(IReadOnlyList<AudioClip> tracks)
        {
            if (tracks == null || tracks.Count == 0)
            {
                return new AudioClip[0];
            }

            var compacted = new List<AudioClip>(tracks.Count);
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i] != null)
                {
                    compacted.Add(tracks[i]);
                }
            }

            return compacted.ToArray();
        }
    }
}
