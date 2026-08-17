using Espace.Core;
using Espace.Gameplay.Decisions;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// La fenetre qui pose une question au joueur (Phase 24, etape 5).
    /// <para>
    /// <b>Pourquoi une fenetre et non une huitieme entree de rail.</b> Le rail est de la
    /// navigation : on y va quand on veut. Une decision fait l'inverse — c'est elle qui vient
    /// chercher le joueur. La ranger dans un onglet qu'il faut penser a ouvrir en ferait un
    /// courrier administratif, et le seul systeme du jeu qui <i>impose</i> un choix perdrait
    /// exactement ce qui le distingue. C'est aussi pour cela que le rail reste a sept entrees,
    /// la huitieme ne tenant de toute facon pas sur l'ecran le plus court.
    /// </para>
    /// <para>
    /// <b>Meme patron que <see cref="EncounterWindowController"/></b>, y compris la convention
    /// de pause : on ne relache l'horloge que si c'est bien cette fenetre qui l'avait arretee.
    /// La pause n'est pas une garantie — <c>GameClock.AdvanceDays</c> ne teste <c>IsPaused</c>
    /// qu'a l'entree du tick — elle evite seulement que le monde defile pendant qu'on lit.
    /// Aucune regle ne repose dessus : une question non repondue reste simplement en attente.
    /// </para>
    /// <para>
    /// <b>Les deux couts sont montres avant le choix, jamais apres.</b> C'est toute la difference
    /// entre un dilemme et un piege : le joueur doit pouvoir se tromper en connaissance de cause.
    /// </para>
    /// </summary>
    public sealed class DecisionWindowController : MonoBehaviour
    {
        private const int WindowWidth = 440;
        private const int WindowHeight = 260;

        private IDecisionService _decisions;
        private IGameClock _gameClock;
        private GalaxyMap _map;

        private bool _pausedByThisWindow;

        private void OnGUI()
        {
            UITheme.BeginScaledLayout();
            try
            {
                ResolveServices();

                if (_decisions == null || _decisions.Pending.Count == 0)
                {
                    ReleaseClock();
                    return;
                }

                HoldClock();

                // La plus ancienne d'abord : plusieurs systemes peuvent basculer le meme jour, et
                // les traiter dans le desordre donnerait l'impression que le jeu en oublie.
                DrawWindow(_decisions.Pending[0]);
            }
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        private void ResolveServices()
        {
            if (_decisions == null) ServiceLocator.TryGet(out _decisions);
            if (_gameClock == null) ServiceLocator.TryGet(out _gameClock);
            if (_map == null) ServiceLocator.TryGet(out _map);
        }

        private void HoldClock()
        {
            if (_gameClock != null && !_gameClock.IsPaused)
            {
                _gameClock.Pause();
                _pausedByThisWindow = true;
            }
        }

        private void ReleaseClock()
        {
            if (_gameClock != null && _pausedByThisWindow)
            {
                _gameClock.Resume();
            }

            _pausedByThisWindow = false;
        }

        private void DrawWindow(PendingDecision decision)
        {
            var rect = new Rect(
                (UITheme.ScreenWidth - WindowWidth) / 2f,
                (UITheme.ScreenHeight - WindowHeight) / 2f,
                WindowWidth, WindowHeight);

            UiScreenRegions.Occupy(rect, UITheme.Scale);
            GUI.Box(rect, string.Empty, UITheme.Panel);

            GUILayout.BeginArea(new Rect(rect.x + 14, rect.y + 12, WindowWidth - 28, WindowHeight - 24));

            GUILayout.Label(decision.Title, UITheme.Title);
            GUILayout.Label(decision.Question, UITheme.Label);
            GUILayout.Space(8);

            for (int i = 0; i < decision.Options.Count; i++)
            {
                DrawOption(decision, i);
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Une option : son libelle, ce qu'elle coute maintenant, ce qu'elle coutera plus tard.
        /// <para>
        /// L'ardoise differee est ecrite <b>sous</b> le bouton et dans le style attenue : elle
        /// doit se lire avant de choisir, sans pour autant peser autant que le cout immediat,
        /// qui est celui qu'on paie a coup sur.
        /// </para>
        /// </summary>
        private void DrawOption(PendingDecision decision, int index)
        {
            DecisionOption option = decision.Options[index];

            if (GUILayout.Button(option.Label, UITheme.Button, GUILayout.Height(26)))
            {
                _decisions.Answer(decision.Id, index);
            }

            GUILayout.Label($"    maintenant : {option.ImmediateText}", UITheme.MutedLabel);
            GUILayout.Label($"    plus tard : {option.DeferredText}", UITheme.MutedLabel);
            GUILayout.Space(4);
        }
    }
}
