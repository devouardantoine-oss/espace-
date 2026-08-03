using System.Collections.Generic;
using UnityEngine;

namespace Espace.Core
{
    /// <summary>
    /// Zones de l'ecran occupees par l'interface, pour que le jeu ne reagisse pas a un appui
    /// qui visait un panneau (Phase 20).
    /// <para>
    /// <b>Le probleme :</b> IMGUI dessine par-dessus la scene mais ne consomme pas les entrees
    /// du nouvel Input System. <c>GalaxySelectionController</c> lit <c>Pointer.current</c>
    /// directement ; un appui sur un onglet de la fiche de systeme ne touchait donc aucun
    /// collisionneur, et il en concluait « le joueur a touche le vide » — refermant le panneau
    /// que le joueur etait justement en train d'utiliser.
    /// </para>
    /// <para>
    /// <b>Pourquoi ici, dans <c>Espace.Core</c> ?</b> Le producteur (<c>Espace.UI</c>) et le
    /// consommateur (<c>Espace.Gameplay</c>) ne se connaissent pas et ne doivent pas se
    /// connaitre : la dependance ne va que vers <c>Core</c>. Une simple liste de rectangles
    /// partagee suffit, sans coupler les deux couches.
    /// </para>
    /// <para>
    /// <b>Coordonnees en pixels physiques, origine en haut a gauche</b> — celles d'IMGUI, pas
    /// celles du pointeur. La conversion est faite une seule fois, dans
    /// <see cref="ContainsPointer"/>, plutot que par chaque appelant.
    /// </para>
    /// <para>
    /// <b>Tolerance d'une frame :</b> les panneaux s'enregistrent dans <c>OnGUI</c>, qui
    /// s'execute apres <c>Update</c>. Une zone reste donc valable quelques frames, ce qui est
    /// sans consequence — un panneau ne se deplace pas d'une frame a l'autre — et evite un
    /// ordre d'execution a imposer entre composants.
    /// </para>
    /// </summary>
    public static class UiScreenRegions
    {
        /// <summary>Nombre de frames pendant lesquelles une zone enregistree reste prise en compte.</summary>
        private const int FramesOfTolerance = 2;

        private readonly struct Region
        {
            public readonly Rect Rect;
            public readonly int Frame;

            public Region(Rect rect, int frame)
            {
                Rect = rect;
                Frame = frame;
            }
        }

        private static readonly List<Region> Regions = new List<Region>(8);

        /// <summary>
        /// Declare qu'un panneau occupe <paramref name="rect"/>, exprime en <b>unites
        /// d'interface</b> (celles de <c>UITheme.ScreenWidth</c>/<c>ScreenHeight</c>), avec
        /// <paramref name="scale"/> le facteur d'agrandissement applique.
        /// <para>A appeler depuis <c>OnGUI</c>, a chaque frame ou le panneau est visible.</para>
        /// </summary>
        public static void Occupy(Rect rect, float scale)
        {
            if (scale <= 0f)
            {
                scale = 1f;
            }

            var physical = new Rect(rect.x * scale, rect.y * scale, rect.width * scale, rect.height * scale);

            int frame = Time.frameCount;
            PruneExpired(frame);

            // Une zone identique reenregistree la meme frame (deux passes IMGUI, mise en page
            // puis dessin) ne doit pas s'accumuler.
            for (int i = 0; i < Regions.Count; i++)
            {
                if (Regions[i].Frame == frame && Regions[i].Rect == physical)
                {
                    return;
                }
            }

            Regions.Add(new Region(physical, frame));
        }

        /// <summary>
        /// Vrai si <paramref name="pointerScreenPosition"/> — en pixels, origine en bas a gauche,
        /// telle que la renvoie l'Input System — tombe sur un panneau d'interface.
        /// </summary>
        public static bool ContainsPointer(Vector2 pointerScreenPosition)
        {
            int frame = Time.frameCount;
            var guiPoint = new Vector2(pointerScreenPosition.x, Screen.height - pointerScreenPosition.y);

            for (int i = 0; i < Regions.Count; i++)
            {
                if (frame - Regions[i].Frame <= FramesOfTolerance && Regions[i].Rect.Contains(guiPoint))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Vide toutes les zones. A appeler au changement de scene, ou le contenu precedent n'a plus de sens.</summary>
        public static void Clear() => Regions.Clear();

        private static void PruneExpired(int frame)
        {
            for (int i = Regions.Count - 1; i >= 0; i--)
            {
                if (frame - Regions[i].Frame > FramesOfTolerance)
                {
                    Regions.RemoveAt(i);
                }
            }
        }
    }
}
