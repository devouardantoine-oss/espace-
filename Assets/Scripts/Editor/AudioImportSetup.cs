using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Espace.Editor
{
    /// <summary>
    /// Regle en un clic l'import des musiques d'ambiance (Phase 21).
    /// <para>
    /// <b>Pourquoi ce n'est pas un detail :</b> Unity importe par defaut un fichier audio en
    /// <c>Decompress On Load</c>. Un morceau de trois minutes se retrouve alors decompresse en
    /// memoire — environ 30 Mo de PCM pour 3 Mo de fichier — et quatre morceaux suffisent a
    /// faire tuer l'application par Android. En <c>Streaming</c>, le fichier reste sur le
    /// disque et ne coute qu'un tampon de quelques centaines de kilo-octets, quelle que soit sa
    /// duree. C'est exactement le bon reglage pour une musique de fond : longue, jouee une
    /// seule fois a la fois, et jamais declenchee dans l'urgence.
    /// </para>
    /// <para>
    /// <b>Separe de <c>ProjectSetup</c></b> : celui-ci configure le moteur et ne touche a aucun
    /// asset importe. Melanger les deux obligerait a reimporter tous les fichiers audio a
    /// chaque configuration du projet.
    /// </para>
    /// <para>
    /// L'operation est idempotente : les fichiers deja correctement regles ne sont pas
    /// reimportes.
    /// </para>
    /// </summary>
    public static class AudioImportSetup
    {
        /// <summary>Dossiers fouilles. Le premier est celui que <c>MusicLibrary</c> charge automatiquement.</summary>
        private static readonly string[] MusicFolders = { "Assets/Resources/Music", "Assets/Audio" };

        /// <summary>Qualite Vorbis. 0,7 est le palier ou l'oreille cesse d'entendre la difference sur un fond sonore, pour environ la moitie du poids de 1,0.</summary>
        private const float VorbisQuality = 0.7f;

        [MenuItem("Tools/Espace/Configure Audio Import", priority = 10)]
        public static void ConfigureAudioImport()
        {
            List<string> folders = ExistingFolders();
            if (folders.Count == 0)
            {
                Debug.LogWarning(
                    "[Audio] Aucun dossier de musique trouve. Deposez vos fichiers .ogg dans "
                    + "Assets/Resources/Music, puis relancez Tools > Espace > Configure Audio Import.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", folders.ToArray());
            int changed = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is AudioImporter importer && ApplyStreamingSettings(importer))
                {
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            Debug.Log(
                $"[Audio] {guids.Length} fichier(s) audio inspecte(s) dans {string.Join(", ", folders)} — "
                + $"{changed} reimporte(s) en Streaming/Vorbis.");
        }

        private static List<string> ExistingFolders()
        {
            var folders = new List<string>(MusicFolders.Length);
            foreach (string folder in MusicFolders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                {
                    folders.Add(folder);
                }
            }

            return folders;
        }

        /// <summary>
        /// Applique les reglages par defaut, qui valent pour toutes les plateformes tant
        /// qu'aucune surcharge n'est definie.
        /// </summary>
        /// <returns><c>true</c> si au moins un reglage a change, donc si un reimport est justifie.</returns>
        private static bool ApplyStreamingSettings(AudioImporter importer)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            bool changed = settings.loadType != AudioClipLoadType.Streaming
                || settings.compressionFormat != AudioCompressionFormat.Vorbis
                || !Mathf.Approximately(settings.quality, VorbisQuality)
                || settings.preloadAudioData
                || !importer.loadInBackground;

            if (!changed)
            {
                return false;
            }

            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = VorbisQuality;

            // Precharger irait a l'encontre du streaming : ce serait remettre le fichier entier
            // en memoire au chargement de la scene.
            settings.preloadAudioData = false;

            importer.defaultSampleSettings = settings;

            // Sans cela, le premier morceau bloque le thread principal a l'ouverture du jeu,
            // le temps que le decodeur soit pret.
            importer.loadInBackground = true;

            return true;
        }
    }
}
