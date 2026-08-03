namespace Espace.Core
{
    /// <summary>
    /// Musique d'ambiance : une playlist unique, jouee en boucle dans un ordre fixe, du
    /// lancement du jeu jusqu'a sa fermeture (Phase 21).
    /// <para>
    /// <b>Reduite a ce que l'interface pilote reellement.</b> Le service expose le volume, la
    /// coupure du son et le titre courant — rien de plus. Ni « morceau precedent », ni « mettre
    /// en pause », ni « jouer tel morceau » : aucun ecran n'en a besoin, et chaque membre
    /// ajoute ici devrait etre double dans les tests de tous les composants qui dependent de
    /// cette interface.
    /// </para>
    /// </summary>
    public interface IMusicService : IGameService
    {
        /// <summary>Nombre de morceaux chargés. Zero signifie qu'aucun fichier audio n'a ete trouve.</summary>
        int TrackCount { get; }

        /// <summary>Nom du morceau en cours, ou une chaine vide pendant un silence ou faute de playlist.</summary>
        string CurrentTrackName { get; }

        /// <summary>Volume choisi par le joueur, entre 0 et 1. Independant des fondus, qui le multiplient.</summary>
        float Volume { get; }

        /// <summary>Vrai si le joueur a coupe la musique. La lecture continue en sourdine, pour que reactiver le son ne relance pas la playlist du debut.</summary>
        bool IsMuted { get; }

        /// <summary>Regle le volume et enregistre la preference sur l'appareil.</summary>
        void SetVolume(float volume);

        /// <summary>Coupe ou retablit le son et enregistre la preference sur l'appareil.</summary>
        void SetMuted(bool muted);
    }
}
