using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using UnityEngine;

namespace Espace.Gameplay.Espionage
{
    /// <summary>
    /// Panneau de diagnostic temporaire pour l'espionnage du joueur : sa puissance
    /// d'espionnage, un bouton par mission pour chaque empire IA, et le dernier resultat de
    /// decouverte d'armees.
    /// <para>
    /// Meme statut IMGUI temporaire que les autres panneaux depuis la Phase 2 ; empile en haut
    /// a droite, juste sous <c>DiplomacyDebugPanel</c>.
    /// </para>
    /// </summary>
    public sealed class EspionageDebugPanel : MonoBehaviour
    {
        private const int PanelWidth = 320;
        private const int PanelHeight = 280;
        private const int Gap = 10;

        /// <summary>Hauteur de l'encart de diplomatie, ancre en haut a droite : ce panneau se place juste en dessous.</summary>
        private const int DiplomacyPanelHeight = 320;

        private IEspionageService _espionage;
        private EmpireRegistry _empireRegistry;
        private GalaxyMap _map;
        private readonly Dictionary<int, UnitBundle> _lastDiscovered = new Dictionary<int, UnitBundle>();

        private void OnGUI()
        {
            if (_espionage == null && !ServiceLocator.TryGet(out _espionage))
            {
                return;
            }

            if (_empireRegistry == null && !ServiceLocator.TryGet(out _empireRegistry))
            {
                return;
            }

            if (_map == null && !ServiceLocator.TryGet(out _map))
            {
                return;
            }

            var rect = new Rect(Screen.width - PanelWidth - Gap, Gap + DiplomacyPanelHeight + Gap, PanelWidth, PanelHeight);
            GUI.Box(rect, string.Empty);

            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, PanelWidth - 16, PanelHeight - 12));

            int playerId = EconomyService.PlayerOwnerId;
            GUILayout.Label($"Espionnage (puissance {_espionage.GetEspionagePower(playerId):0})");

            foreach (Empire empire in _empireRegistry.Empires)
            {
                if (empire.IsPlayerControlled)
                {
                    continue;
                }

                DrawEmpireRow(playerId, empire);
            }

            GUILayout.EndArea();
        }

        private void DrawEmpireRow(int playerId, Empire target)
        {
            StarSystemId? targetSystemId = FindPrimarySystemId(target.Id);
            if (targetSystemId == null)
            {
                return;
            }

            float counterPower = _espionage.GetCounterEspionagePower(target.Id, targetSystemId.Value);
            GUILayout.Label($"{target.Name} (contre-espionnage {counterPower:0})");

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Vol tech"))
            {
                _espionage.TryStealTechnology(playerId, target.Id, out string error);
                LogIfFailed(error);
            }

            if (GUILayout.Button("Sabotage"))
            {
                _espionage.TrySabotage(playerId, targetSystemId.Value, out string error);
                LogIfFailed(error);
            }

            if (GUILayout.Button("Revolte"))
            {
                _espionage.TryInciteRevolt(playerId, targetSystemId.Value, out string error);
                LogIfFailed(error);
            }

            if (GUILayout.Button("Influence"))
            {
                _espionage.TryInfluenceGovernment(playerId, target.Id, out string error);
                LogIfFailed(error);
            }

            if (GUILayout.Button("Decouvrir"))
            {
                if (_espionage.TryDiscoverArmies(playerId, target.Id, targetSystemId.Value, out UnitBundle discovered, out string error))
                {
                    _lastDiscovered[target.Id] = discovered;
                }

                LogIfFailed(error);
            }

            GUILayout.EndHorizontal();

            if (_lastDiscovered.TryGetValue(target.Id, out UnitBundle garrison))
            {
                GUILayout.Label($"Derniere decouverte : {garrison}");
            }
        }

        private StarSystemId? FindPrimarySystemId(int empireId)
        {
            foreach (StarSystemState system in _map.Systems)
            {
                if (system.OwnerId == empireId)
                {
                    return system.Id;
                }
            }

            return null;
        }

        private static void LogIfFailed(string error)
        {
            if (error != null)
            {
                GameLog.Warning($"[Espionage] {error}");
            }
        }
    }
}
