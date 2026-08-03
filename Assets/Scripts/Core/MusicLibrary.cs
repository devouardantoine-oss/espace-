using System;
using UnityEngine;

namespace Espace.Core
{
    /// <summary>
    /// Decouverte automatique des morceaux deposes dans <c>Assets/Resources/Music</c>
    /// (Phase 21).
    /// <para>
    /// <b>Pourquoi une decouverte automatique plutot qu'un asset a remplir ?</b> Ajouter une
    /// musique doit se resumer a deposer un fichier dans un dossier. Passer par un
    /// <c>ScriptableObject</c> imposerait, pour chaque morceau, d'ouvrir l'editeur, d'agrandir
    /// un tableau et d'y glisser le fichier — trois manipulations qui n'apportent rien tant que
    /// l'ordre alphabetique suffit. L'asset reste disponible
    /// (<c>Espace.Data.MusicPlaylist</c>) pour qui veut un ordre explicite ou des reglages
    /// differents ; il a simplement cesse d'etre obligatoire.
    /// </para>
    /// <para>
    /// <b>Ordre alphabetique ordinal, pas culturel :</b> <c>string.Compare</c> classe
    /// differemment selon la langue du systeme, ce qui donnerait une playlist dans un ordre sur
    /// le poste de developpement et dans un autre sur le telephone du joueur. Nommer les
    /// fichiers <c>01_…</c>, <c>02_…</c> suffit alors a fixer l'ordre de lecture.
    /// </para>
    /// <para>
    /// <b><c>Resources</c> plutot qu'<c>Addressables</c> :</b> quelques morceaux charges au
    /// demarrage et jamais decharges ne justifient pas un systeme de chargement asynchrone.
    /// Regles en <c>Streaming</c> (voir <c>Tools/Espace/Configure Audio Import</c>), les
    /// fichiers restent sur le disque et ne coutent que quelques centaines de kilo-octets de
    /// tampon, quelle que soit leur duree.
    /// </para>
    /// </summary>
    public static class MusicLibrary
    {
        /// <summary>Dossier fouille, relatif a n'importe quel <c>Resources</c> du projet.</summary>
        public const string DefaultResourceFolder = "Music";

        /// <summary>
        /// Charge tous les <c>AudioClip</c> de <paramref name="resourceFolder"/>, classes par
        /// nom de fichier. Renvoie un tableau vide si le dossier n'existe pas.
        /// </summary>
        public static AudioClip[] LoadFromResources(string resourceFolder = DefaultResourceFolder)
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>(resourceFolder);
            if (clips == null || clips.Length == 0)
            {
                return new AudioClip[0];
            }

            Array.Sort(clips, CompareByName);
            return clips;
        }

        private static int CompareByName(AudioClip a, AudioClip b)
        {
            return string.CompareOrdinal(a.name, b.name);
        }
    }
}
