using System.IO;
using UnityEngine;

namespace Espace.Gameplay.Save
{
    /// <summary>
    /// Chemin du fichier de sauvegarde unique du jeu, et verification de son existence.
    /// <para>
    /// <b>Extrait de <see cref="SaveService"/>/<see cref="SaveController"/> en Phase 11 :</b>
    /// le nouveau menu principal doit savoir si une sauvegarde existe (pour activer ou non
    /// « Continuer ») et, pour « Nouvelle partie », pouvoir la supprimer — deux besoins qui
    /// naissent avant qu'aucun service de la scene <c>GalaxyMap</c> (donc <see cref="ISaveService"/>
    /// lui-meme) n'existe. Une classe statique sans dependance, utilisable depuis n'importe
    /// quelle scene, evite de dupliquer le chemin du fichier a deux endroits.
    /// </para>
    /// </summary>
    public static class SaveFileLocator
    {
        private const string SaveFileName = "savegame.json";

        /// <summary>Chemin absolu du fichier de sauvegarde unique du jeu.</summary>
        public static string FilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>Une sauvegarde existe-t-elle deja sur cet appareil ?</summary>
        public static bool Exists() => File.Exists(FilePath);

        /// <summary>
        /// Supprime la sauvegarde existante, si presente. Utilise par « Nouvelle partie » pour
        /// qu'une partie fraiche ne soit pas immediatement ecrasee par <see cref="SaveController"/>
        /// au chargement de la scene <c>GalaxyMap</c>.
        /// </summary>
        public static void DeleteIfExists()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }
}
