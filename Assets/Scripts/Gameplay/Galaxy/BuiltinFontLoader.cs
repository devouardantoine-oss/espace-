using System;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Acces a la police integree au moteur, utilisee par tous les textes dessines dans le
    /// monde de la carte galactique.
    /// <para>
    /// <b>Pourquoi une brique partagee ?</b> Deux controleurs affichent aujourd'hui du texte
    /// dans la scene (<see cref="SystemLabelController"/> pour les systemes,
    /// <see cref="FactionLabelController"/> pour les factions) et la resolution de la police
    /// comporte un piege de version (voir <see cref="Load"/>) qu'il serait absurde de
    /// reecrire — et de corriger deux fois — dans chacun.
    /// </para>
    /// <para>
    /// <b><c>TextMesh</c> (integre au moteur), pas TextMeshPro :</b> TMP exige d'importer ses
    /// « Essential Resources », une etape d'editeur risquee a effectuer a l'aveugle dans cet
    /// environnement sans acces a Unity. La police integree ne demande aucun asset.
    /// </para>
    /// </summary>
    public static class BuiltinFontLoader
    {
        /// <summary>
        /// Noms essayes dans l'ordre, du plus recent au plus ancien : <b>Unity 6 a retire
        /// <c>Arial.ttf</c></b> au profit de <c>LegacyRuntime.ttf</c>. Garder l'ancien nom en
        /// second permet au projet de rester ouvrable sur une version anterieure de l'editeur.
        /// </summary>
        private static readonly string[] FontNames = { "LegacyRuntime.ttf", "Arial.ttf" };

        private static Font _cached;

        /// <summary>
        /// La police integree, ou <c>null</c> si aucune n'est disponible.
        /// <para>
        /// <see cref="Resources.GetBuiltinResource{T}"/> ne renvoie pas <c>null</c> pour un nom
        /// inconnu : elle leve une <see cref="ArgumentException"/>. Demander un nom en dur
        /// suffisait donc a faire echouer l'initialisation de la carte au lancement.
        /// </para>
        /// <para>
        /// L'appelant doit traiter <c>null</c> comme une degradation cosmetique — l'absence de
        /// texte sur la carte n'est pas une raison d'empecher la partie de demarrer.
        /// </para>
        /// </summary>
        public static Font Load()
        {
            if (_cached != null)
            {
                return _cached;
            }

            foreach (string fontName in FontNames)
            {
                try
                {
                    Font font = Resources.GetBuiltinResource<Font>(fontName);
                    if (font != null)
                    {
                        _cached = font;
                        return _cached;
                    }
                }
                catch (ArgumentException)
                {
                    // Nom inconnu de cette version du moteur : on essaie le suivant.
                }
            }

            return null;
        }
    }
}
