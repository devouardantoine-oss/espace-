namespace Espace.Gameplay.Save
{
    /// <summary>Sauvegarde et chargement de l'etat complet d'une partie, au format JSON.</summary>
    public interface ISaveService
    {
        /// <summary>Vrai si un fichier de sauvegarde existe deja sur le disque.</summary>
        bool SaveFileExists { get; }

        /// <summary>Capture l'etat courant de tous les services et l'ecrit sur le disque, en remplacant toute sauvegarde precedente.</summary>
        void SaveNow();

        /// <summary>
        /// Lit le fichier de sauvegarde s'il existe et applique son contenu a tous les
        /// services (ecrasant leur etat courant). Echoue proprement (fichier absent,
        /// illisible, ou JSON corrompu) sans jamais lever d'exception.
        /// </summary>
        bool TryLoadAndApply(out string error);
    }
}
