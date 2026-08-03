using Espace.Core;
using UnityEngine;

namespace Espace.Data
{
    /// <summary>
    /// Playlist explicite : ordre de lecture choisi a la main et durees de fondu ajustables
    /// sans recompiler (Phase 21).
    /// <para>
    /// Meme pattern que <see cref="GameClockConfig"/> : l'asset ne sert qu'a saisir des valeurs,
    /// qu'il convertit en structures pures (<see cref="MusicSettings"/>) avant de les remettre
    /// au service. <c>Espace.Core</c> n'a ainsi jamais besoin de connaitre <c>Espace.Data</c>.
    /// </para>
    /// <para>
    /// <b>Entierement optionnel.</b> Sans asset assigne, <c>GameBootstrap</c> se rabat sur les
    /// fichiers de <c>Assets/Resources/Music</c> (voir <see cref="MusicLibrary"/>). Cet asset ne
    /// devient utile que pour imposer un ordre que le nom des fichiers ne donne pas, ou pour
    /// s'ecarter des durees de fondu par defaut.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "MusicPlaylist", menuName = "Espace/Audio/Music Playlist", order = 0)]
    public sealed class MusicPlaylist : ScriptableObject
    {
        [Header("Morceaux")]
        [Tooltip("Joues dans cet ordre, en boucle. Les cases vides sont ignorees.")]
        [SerializeField]
        private AudioClip[] tracks;

        [Header("Enchainement")]
        [Tooltip("Duree du fondu d'entree et de sortie, en secondes.")]
        [SerializeField]
        [Range(0f, 8f)]
        private float fadeSeconds = 2f;

        [Tooltip("Silence minimal entre deux morceaux, en secondes.")]
        [SerializeField]
        [Range(0f, 30f)]
        private float minimumSilenceSeconds = 3f;

        [Tooltip("Silence maximal entre deux morceaux, en secondes.")]
        [SerializeField]
        [Range(0f, 60f)]
        private float maximumSilenceSeconds = 8f;

        [Tooltip("Volume au premier lancement. Le joueur peut ensuite le regler dans le menu pause.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float volume = 0.6f;

        /// <summary>Morceaux dans l'ordre de lecture. Jamais <c>null</c>.</summary>
        public AudioClip[] Tracks => tracks ?? new AudioClip[0];

        /// <summary>Construit les parametres immuables consommes par <see cref="MusicService"/>.</summary>
        public MusicSettings ToSettings()
        {
            return new MusicSettings(fadeSeconds, minimumSilenceSeconds, maximumSilenceSeconds, volume);
        }
    }
}
