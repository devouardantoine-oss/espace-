using Espace.Core;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Panneau de diagnostic temporaire listant les 6 empires de la partie : seul moyen
    /// d'observer que l'IA agit (production, constructions) sans derouler la console.
    /// <para>
    /// Meme statut que les autres panneaux depuis la Phase 2 : IMGUI, pas l'ecran final
    /// (Phase 11). Place en haut de l'ecran, seule zone encore libre entre l'encart de
    /// selection de systeme (haut-gauche) et celui de l'horloge (haut-droite).
    /// </para>
    /// <para>
    /// <b>Resolution paresseuse dans <see cref="OnGUI"/> :</b> meme raison que
    /// <c>EconomyDebugPanel</c> — <see cref="EmpireRegistry"/>/<see cref="IEconomyService"/>
    /// peuvent n'etre enregistres qu'apres le <c>Start</c> de ce composant ; <c>OnGUI</c>
    /// s'execute systematiquement apres tous les <c>Start</c> de la frame.
    /// </para>
    /// </summary>
    public sealed class EmpireDebugPanel : MonoBehaviour
    {
        private const int PanelWidth = 420;
        private const int RowHeight = 20;
        private const int HeaderHeight = 14;

        private EmpireRegistry _empireRegistry;
        private IEconomyService _economy;
        private GalaxyMap _map;

        private void OnGUI()
        {
            if (_empireRegistry == null && !ServiceLocator.TryGet(out _empireRegistry))
            {
                return;
            }

            if (_economy == null)
            {
                ServiceLocator.TryGet(out _economy);
            }

            if (_map == null)
            {
                ServiceLocator.TryGet(out _map);
            }

            int height = HeaderHeight + RowHeight * _empireRegistry.Empires.Count + 10;
            var rect = new Rect((Screen.width - PanelWidth) / 2f, 10, PanelWidth, height);
            GUI.Box(rect, string.Empty);

            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 4, PanelWidth - 16, height - 8));
            GUILayout.Label("Empires");

            foreach (Empire empire in _empireRegistry.Empires)
            {
                int systemCount = CountOwnedSystems(empire.Id);
                float credits = _economy?.GetTreasury(empire.Id).Credits ?? 0f;
                string role = empire.IsPlayerControlled ? "Vous" : empire.Personality.ToString();

                GUILayout.Label($"{empire.Name}  —  {role}  —  {systemCount} systeme(s)  —  {credits:0} Cr");
            }

            GUILayout.EndArea();
        }

        private int CountOwnedSystems(int empireId)
        {
            if (_map == null)
            {
                return 0;
            }

            int count = 0;
            foreach (StarSystemState system in _map.Systems)
            {
                if (system.OwnerId == empireId)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
