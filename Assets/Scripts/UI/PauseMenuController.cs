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

        /// <summary>Hauteur des cinq boutons d'origine, entete compris.</summary>
        private const int BaseWindowHeight = 220;

        /// <summary>Hauteur du bloc « Musique », ajoutee seulement quand il y a quelque chose a jouer.</summary>
        private const int MusicRowHeight = 60;

        /// <summary>Pas de reglage du volume : dix crans suffisent, et chacun reste perceptible.</summary>
        private const float VolumeStep = 0.1f;

        private bool _visible;
        private bool _pausedByThisMenu;

        private IGameClock _gameClock;
        private ISaveService _save;
        private ISceneLoader _sceneLoader;
        private IMusicService _music;

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
            UITheme.BeginScaledLayout();
            try
            {
                if (!_visible)
                {
                    return;
                }

                if (_save == null) ServiceLocator.TryGet(out _save);
                if (_sceneLoader == null) ServiceLocator.TryGet(out _sceneLoader);
                if (_music == null) ServiceLocator.TryGet(out _music);

                // Le bloc « Musique » n'existe que s'il y a des morceaux : sans cela, le menu
                // reserverait une place vide et proposerait de regler un volume sans effet.
                bool showMusic = _music != null && _music.TrackCount > 0;
                int windowHeight = BaseWindowHeight + (showMusic ? MusicRowHeight : 0);

                var rect = new Rect((UITheme.ScreenWidth - WindowWidth) / 2f, (UITheme.ScreenHeight - windowHeight) / 2f, WindowWidth, windowHeight);
                UiScreenRegions.Occupy(rect, UITheme.Scale);
                GUI.Box(rect, string.Empty, UITheme.Panel);

                GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 8, WindowWidth - 20, windowHeight - 16));

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

                if (showMusic)
                {
                    DrawMusicRow();
                }

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
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        /// <summary>
        /// Reglage de la musique : couper le son, et deux boutons de volume.
        /// <para>
        /// <b>Deux boutons plutot qu'un curseur</b> (<c>GUILayout.HorizontalSlider</c>) : viser
        /// une poignee de quelques pixels au doigt est le geste le plus rate de toute
        /// l'interface, alors qu'un bouton de 34 unites de large ne se manque pas. Dix crans
        /// suffisent a un reglage de fond sonore.
        /// </para>
        /// <para>
        /// <b>Le titre du morceau est affiche</b> : c'est la seule facon, pour le joueur, de
        /// savoir quelle piste retirer du dossier quand l'une d'elles lui deplait.
        /// </para>
        /// </summary>
        private void DrawMusicRow()
        {
            GUILayout.Space(8);

            string trackName = _music.CurrentTrackName;
            GUILayout.Label(
                string.IsNullOrEmpty(trackName) ? "Musique" : $"Musique · {trackName}",
                UITheme.Caption,
                GUILayout.Height(UITheme.CaptionHeight));

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(_music.IsMuted ? "Son coupe" : "Son actif", UITheme.Button, GUILayout.Width(84), GUILayout.Height(30)))
            {
                _music.SetMuted(!_music.IsMuted);
            }

            // Le volume reste reglable son coupe : le joueur prepare son niveau, puis retablit.
            if (GUILayout.Button("-", UITheme.Button, GUILayout.Width(34), GUILayout.Height(30)))
            {
                _music.SetVolume(_music.Volume - VolumeStep);
            }

            // Centre verticalement le pourcentage sur les boutons qui l'encadrent : un texte
            // pose tel quel dans un rectangle de 30 unites se colle a son bord superieur, et
            // UITheme.Value est partage par d'autres ecrans qui n'en veulent pas centre.
            GUILayout.BeginVertical(GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{Mathf.RoundToInt(_music.Volume * 100f)} %", UITheme.Value);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            if (GUILayout.Button("+", UITheme.Button, GUILayout.Width(34), GUILayout.Height(30)))
            {
                _music.SetVolume(_music.Volume + VolumeStep);
            }

            GUILayout.EndHorizontal();
        }
    }
}
