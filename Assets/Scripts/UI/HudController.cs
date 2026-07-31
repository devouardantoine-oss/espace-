using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Economy;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Barre superieure, toujours visible dans la scene <c>GalaxyMap</c> : date et vitesse de
    /// l'horloge, tresor et impots du joueur, boutons d'ouverture de la fenetre de gestion et
    /// du menu pause.
    /// <para>
    /// Remplace <c>GameClockDebugPanel</c> (horloge) et l'encart tresor d'<c>EconomyDebugPanel</c>
    /// (Phases 3 et 4) : ces deux panneaux de diagnostic sont retires en Phase 11, leur contenu
    /// migre ici tel quel (memes methodes de service, juste redessine avec <see cref="UITheme"/>
    /// dans une seule barre au lieu de deux encarts empiles).
    /// </para>
    /// <para>
    /// <b>Bascule des autres ecrans par <c>GetComponent</c>, pas par evenement :</b> ce
    /// composant vit sur le meme GameObject <c>[UI]</c> que
    /// <see cref="ManagementWindowController"/> et <see cref="PauseMenuController"/> (voir la
    /// scene). Un simple <c>GetComponent</c> en <c>Awake</c> suffit ; inventer un evenement
    /// pour une communication entre deux composants du meme objet ajouterait de l'indirection
    /// sans benefice.
    /// </para>
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        private const int BarHeight = 44;
        private const float TaxStep = 0.1f;

        private IGameClock _gameClock;
        private IEconomyService _economy;
        private ManagementWindowController _managementWindow;
        private PauseMenuController _pauseMenu;

        private void Awake()
        {
            _managementWindow = GetComponent<ManagementWindowController>();
            _pauseMenu = GetComponent<PauseMenuController>();
        }

        private void OnGUI()
        {
            UITheme.BeginScaledLayout();
            try
            {
                if (_gameClock == null)
                {
                    ServiceLocator.TryGet(out _gameClock);
                }

                if (_economy == null)
                {
                    ServiceLocator.TryGet(out _economy);
                }

                var barRect = new Rect(0, 0, UITheme.ScreenWidth, BarHeight);
                GUI.Box(barRect, string.Empty, UITheme.Header);

                GUILayout.BeginArea(new Rect(8, 6, UITheme.ScreenWidth - 16, BarHeight - 8));
                GUILayout.BeginHorizontal();

                DrawClockSection();
                GUILayout.Space(20);
                DrawTreasurySection();

                GUILayout.FlexibleSpace();

                DrawWindowToggles();

                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        private void DrawClockSection()
        {
            if (_gameClock == null)
            {
                GUILayout.Label("Horloge indisponible", UITheme.MutedLabel, GUILayout.Width(260));
                return;
            }

            string speedLabel = _gameClock.IsPaused
                ? "Pause"
                : $"{_gameClock.CurrentSpeed} (x{_gameClock.CurrentMultiplier:0.#})";
            GUILayout.Label($"{HudFormatter.FormatDate(_gameClock.CurrentDate)}  —  {speedLabel}", UITheme.Label, GUILayout.Width(340));

            if (GUILayout.Button(_gameClock.IsPaused ? "Lecture" : "Pause", UITheme.Button, GUILayout.Width(70)))
            {
                _gameClock.TogglePause();
            }

            if (GUILayout.Button("Normal", UITheme.Button, GUILayout.Width(60))) _gameClock.SetSpeed(GameSpeed.Normal);
            if (GUILayout.Button("Rapide", UITheme.Button, GUILayout.Width(60))) _gameClock.SetSpeed(GameSpeed.Fast);
            if (GUILayout.Button("Tres rapide", UITheme.Button, GUILayout.Width(90))) _gameClock.SetSpeed(GameSpeed.Faster);
            if (GUILayout.Button("Maximum", UITheme.Button, GUILayout.Width(70))) _gameClock.SetSpeed(GameSpeed.Fastest);
        }

        private void DrawTreasurySection()
        {
            if (_economy == null)
            {
                GUILayout.Label("Tresor indisponible", UITheme.MutedLabel, GUILayout.Width(200));
                return;
            }

            ResourceBundle treasury = _economy.Treasury;
            string summary = $"{HudFormatter.FormatResource(treasury.Credits)} Cr  |  " +
                              $"{HudFormatter.FormatResource(treasury.Minerals)} Mn  |  " +
                              $"{HudFormatter.FormatResource(treasury.Energy)} En  |  " +
                              $"{HudFormatter.FormatResource(treasury.Food)} Al  |  " +
                              $"{HudFormatter.FormatResource(treasury.Influence)} Inf";
            GUILayout.Label(summary, UITheme.Label, GUILayout.Width(360));

            GUILayout.Label($"Impots {HudFormatter.FormatPercent(_economy.TaxRate)}", UITheme.MutedLabel, GUILayout.Width(80));
            if (GUILayout.Button("-", UITheme.Button, GUILayout.Width(24))) _economy.SetTaxRate(_economy.TaxRate - TaxStep);
            if (GUILayout.Button("+", UITheme.Button, GUILayout.Width(24))) _economy.SetTaxRate(_economy.TaxRate + TaxStep);
        }

        private void DrawWindowToggles()
        {
            if (_managementWindow != null && GUILayout.Button("Gestion", UITheme.Button, GUILayout.Width(80)))
            {
                _managementWindow.ToggleVisible();
            }

            if (_pauseMenu != null && GUILayout.Button("Menu", UITheme.Button, GUILayout.Width(70)))
            {
                _pauseMenu.ToggleVisible();
            }
        }
    }
}
