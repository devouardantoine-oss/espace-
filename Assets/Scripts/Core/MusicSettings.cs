using UnityEngine;

namespace Espace.Core
{
    /// <summary>
    /// Reglages immuables de la musique d'ambiance (Phase 21).
    /// <para>
    /// Meme separation que <see cref="GameClockSettings"/> : une structure C# pure, sans
    /// dependance a un asset, pour que <see cref="MusicPlaylistCursor"/> reste verifiable en
    /// EditMode. La version editable vit dans <c>Espace.Data.MusicPlaylist</c>.
    /// </para>
    /// <para>
    /// <b>Les valeurs sont assainies dans le constructeur</b> plutot que supposees correctes :
    /// elles viennent d'un asset que n'importe qui peut regler de travers, et un fondu de duree
    /// negative ou un silence dont le minimum depasse le maximum produirait un enchainement
    /// incoherent au lieu d'une erreur visible.
    /// </para>
    /// </summary>
    public readonly struct MusicSettings
    {
        /// <summary>Duree du fondu d'entree et de sortie, en secondes reelles.</summary>
        public readonly float FadeSeconds;

        /// <summary>Duree minimale du silence entre deux morceaux, en secondes reelles.</summary>
        public readonly float MinimumSilenceSeconds;

        /// <summary>Duree maximale du silence entre deux morceaux, en secondes reelles.</summary>
        public readonly float MaximumSilenceSeconds;

        /// <summary>Volume par defaut, avant la preference enregistree par le joueur.</summary>
        public readonly float Volume;

        public MusicSettings(float fadeSeconds, float minimumSilenceSeconds, float maximumSilenceSeconds, float volume)
        {
            FadeSeconds = Mathf.Max(0f, fadeSeconds);
            MinimumSilenceSeconds = Mathf.Max(0f, minimumSilenceSeconds);
            MaximumSilenceSeconds = Mathf.Max(MinimumSilenceSeconds, maximumSilenceSeconds);
            Volume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// Reglages retenus avec le joueur : fondu de deux secondes, puis trois a huit secondes
        /// de silence. Le silence evite l'effet « radio » d'un morceau qui repart aussitot, et
        /// sa duree variable empeche l'oreille d'anticiper la reprise.
        /// </summary>
        public static MusicSettings Default => new MusicSettings(2f, 3f, 8f, 0.6f);
    }
}
