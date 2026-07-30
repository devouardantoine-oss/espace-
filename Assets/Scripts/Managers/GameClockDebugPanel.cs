using Espace.Core;
using UnityEngine;

namespace Espace.Managers
{
    /// <summary>
    /// Panneau de diagnostic temporaire affichant la date et la vitesse de l'horloge, avec
    /// des boutons tactiles pour la piloter.
    /// <para>
    /// Meme statut que le panneau de <c>GalaxyMapController</c> en Phase 2 : outil de mise
    /// au point en IMGUI, pas l'ecran de parametres final (Phase 11). Les boutons appellent
    /// directement le vrai <see cref="IGameClock"/> resolu via le <see cref="ServiceLocator"/> :
    /// ce n'est pas une maquette, c'est le controle reel de l'horloge.
    /// </para>
    /// </summary>
    public sealed class GameClockDebugPanel : MonoBehaviour
    {
        private const int PanelWidth = 220;
        private const int PanelHeight = 100;

        private IGameClock _gameClock;

        private void OnEnable()
        {
            if (!ServiceLocator.TryGet(out _gameClock))
            {
                GameLog.Warning("[GameClockDebugPanel] IGameClock indisponible : le panneau restera vide.");
            }
        }

        private void OnDisable()
        {
            _gameClock = null;
        }

        private void OnGUI()
        {
            if (_gameClock == null)
            {
                return;
            }

            var rect = new Rect(Screen.width - PanelWidth - 10, 10, PanelWidth, PanelHeight);
            GUI.Box(rect, string.Empty);

            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, PanelWidth - 16, PanelHeight - 12));

            GUILayout.Label($"Date : {_gameClock.CurrentDate}");
            GUILayout.Label(_gameClock.IsPaused
                ? "Vitesse : Pause"
                : $"Vitesse : {_gameClock.CurrentSpeed} (x{_gameClock.CurrentMultiplier:0.#})");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_gameClock.IsPaused ? "Lecture" : "Pause"))
            {
                _gameClock.TogglePause();
            }
            if (GUILayout.Button("Normal")) _gameClock.SetSpeed(GameSpeed.Normal);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rapide")) _gameClock.SetSpeed(GameSpeed.Fast);
            if (GUILayout.Button("Tres rapide")) _gameClock.SetSpeed(GameSpeed.Faster);
            if (GUILayout.Button("Maximum")) _gameClock.SetSpeed(GameSpeed.Fastest);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
