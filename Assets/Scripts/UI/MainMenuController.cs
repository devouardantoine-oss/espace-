using Espace.Core;
using Espace.Gameplay.Save;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Menu principal, seul contenu visible de la scene <c>Bootstrap</c> : Nouvelle partie,
    /// Continuer (si une sauvegarde existe), Quitter.
    /// <para>
    /// <b>Donne enfin un contenu reel a <c>MainMenuState</c> :</b> depuis la Phase 1, l'etat
    /// « menu principal » de la machine a etats se contentait de journaliser son entree — la
    /// scene <c>Bootstrap</c> restait a l'ecran noir. Ce composant n'a besoin d'aucun lien
    /// avec cette machine a etats : il agit directement sur <see cref="ISceneLoader"/>, deja
    /// enregistre a ce stade par <c>GameBootstrap</c> (voir sa remarque sur
    /// <c>DontDestroyOnLoad</c>) — pas de nouvel etat a inventer pour deux boutons.
    /// </para>
    /// <para>
    /// <b>« Nouvelle partie » supprime la sauvegarde existante et remet l'horloge a zero :</b>
    /// sans cela, <c>SaveController</c> rechargerait automatiquement l'ancienne partie des
    /// l'arrivee dans <c>GalaxyMap</c>, et l'horloge (persistante entre les scenes) resterait
    /// a la date ou le joueur s'etait arrete. « Continuer » ne fait ni l'un ni l'autre : la
    /// sauvegarde existante est reprise telle quelle par <c>SaveController</c>.
    /// </para>
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private const int WindowWidth = 300;
        private const int WindowHeight = 220;
        private const string GalaxyMapSceneName = "GalaxyMap";

        private void OnGUI()
        {
            var rect = new Rect((Screen.width - WindowWidth) / 2f, (Screen.height - WindowHeight) / 2f, WindowWidth, WindowHeight);
            GUI.Box(rect, string.Empty, UITheme.Panel);

            GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 10, WindowWidth - 24, WindowHeight - 20));

            GUILayout.Label("ESPACE", UITheme.Title);
            GUILayout.Label("Grande strategie galactique", UITheme.MutedLabel);
            GUILayout.Space(16);

            if (GUILayout.Button("Nouvelle partie", UITheme.Button, GUILayout.Height(36)))
            {
                StartNewGame();
            }

            GUI.enabled = SaveFileLocator.Exists();
            if (GUILayout.Button("Continuer", UITheme.Button, GUILayout.Height(36)))
            {
                ContinueGame();
            }
            GUI.enabled = true;

            if (GUILayout.Button("Quitter", UITheme.Button, GUILayout.Height(36)))
            {
                Application.Quit();
            }

            GUILayout.EndArea();
        }

        private void StartNewGame()
        {
            SaveFileLocator.DeleteIfExists();

            if (ServiceLocator.TryGet(out IGameClock gameClock))
            {
                gameClock.ResetToStart();
            }

            LoadGalaxyMap();
        }

        private void ContinueGame()
        {
            LoadGalaxyMap();
        }

        private void LoadGalaxyMap()
        {
            if (!ServiceLocator.TryGet(out ISceneLoader sceneLoader))
            {
                GameLog.Error("[MainMenu] ISceneLoader indisponible : impossible de charger la galaxie.");
                return;
            }

            sceneLoader.LoadScene(GalaxyMapSceneName);
        }
    }
}
