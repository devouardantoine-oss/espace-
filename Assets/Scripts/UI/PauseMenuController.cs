using Espace.Core;
using Espace.Gameplay.Save;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Menu pause de la scene <c>GalaxyMap</c>, ouvert/ferme par le bouton « Menu » de
    /// <see cref="HudController"/> : reprendre, sauvegarder, recharger, retourner au menu
    /// principal, quitter le jeu.
    /// <para>
    /// <b>Met l'horloge en pause a l'ouverture, la reprend a la fermeture — seulement si
    /// c'est ce composant qui l'a mise en pause :</b> si le joueur avait deja mis le jeu en
    /// pause avant d'ouvrir ce menu, le fermer ne doit pas relancer le temps a sa place.
    /// </para>
    /// </summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        private const int WindowWidth = 260;
        private const int WindowHeight = 220;

        private bool _visible;
        private bool _pausedByThisMenu;

        private IGameClock _gameClock;
        private ISaveService _save;
        private ISceneLoader _sceneLoader;

        /// <summary>Ouvre ou ferme le menu. Appele par <see cref="HudController"/>.</summary>
        public void ToggleVisible()
        {
            _visible = !_visible;

            if (_gameClock == null)
            {
                ServiceLocator.TryGet(out _gameClock);
            }

            if (_gameClock == null)
            {
                return;
            }

            if (_visible && !_gameClock.IsPaused)
            {
                _gameClock.Pause();
                _pausedByThisMenu = true;
            }
            else if (!_visible && _pausedByThisMenu)
            {
                _gameClock.Resume();
                _pausedByThisMenu = false;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            if (_save == null) ServiceLocator.TryGet(out _save);
            if (_sceneLoader == null) ServiceLocator.TryGet(out _sceneLoader);

            var rect = new Rect((Screen.width - WindowWidth) / 2f, (Screen.height - WindowHeight) / 2f, WindowWidth, WindowHeight);
            GUI.Box(rect, string.Empty, UITheme.Panel);

            GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 8, WindowWidth - 20, WindowHeight - 16));

            GUILayout.Label("Pause", UITheme.Title);
            GUILayout.Space(8);

            if (GUILayout.Button("Reprendre", UITheme.Button, GUILayout.Height(32)))
            {
                ToggleVisible();
            }

            GUI.enabled = _save != null;
            if (GUILayout.Button("Sauvegarder maintenant", UITheme.Button, GUILayout.Height(32)))
            {
                _save.SaveNow();
            }

            if (GUILayout.Button("Recharger la sauvegarde", UITheme.Button, GUILayout.Height(32)))
            {
                _save.TryLoadAndApply(out string error);
                if (error != null)
                {
                    GameLog.Warning($"[Save] {error}");
                }
            }
            GUI.enabled = true;

            GUILayout.Space(8);

            if (GUILayout.Button("Menu principal", UITheme.Button, GUILayout.Height(32)))
            {
                _save?.SaveNow();
                if (_sceneLoader != null)
                {
                    _sceneLoader.LoadScene("Bootstrap");
                }
                else
                {
                    GameLog.Error("[PauseMenu] ISceneLoader indisponible : impossible de retourner au menu principal.");
                }
            }

            if (GUILayout.Button("Quitter le jeu", UITheme.Button, GUILayout.Height(32)))
            {
                _save?.SaveNow();
                Application.Quit();
            }

            GUILayout.EndArea();
        }
    }
}
