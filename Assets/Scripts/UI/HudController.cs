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

        private const int Padding = 8;
        private const int Gap = 4;

        /// <summary>Largeur du bloc date + vitesse active.</summary>
        private const int DateWidth = 104;

        /// <summary>Largeur des cinq boutons de vitesse reunis.</summary>
        private const int SpeedGroupWidth = 182;

        /// <summary>Largeur du bloc impots (taux + deux boutons).</summary>
        private const int TaxGroupWidth = 96;

        private const int ManagementButtonWidth = 76;
        private const int MenuButtonWidth = 58;

        /// <summary>En deca, une colonne de ressource afficherait un nombre tronque : elle est omise.</summary>
        private const int MinResourceColumnWidth = 62;

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
                if (_gameClock == null) ServiceLocator.TryGet(out _gameClock);
                if (_economy == null) ServiceLocator.TryGet(out _economy);

                var barRect = new Rect(0, 0, UITheme.ScreenWidth, BarHeight);
                GUI.Box(barRect, GUIContent.none, UITheme.Header);

                // La barre couvre toute la largeur : sans cette declaration, un appui sur un de
                // ses boutons traverserait jusqu'a la carte et deselectionnerait le systeme.
                UiScreenRegions.Occupy(barRect, UITheme.Scale);

                DrawBar(new Rect(Padding, 5, UITheme.ScreenWidth - 2 * Padding, BarHeight - 10));
            }
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        /// <summary>
        /// Dispose la barre en rectangles calcules, jamais en <c>GUILayout</c> a largeurs fixes.
        /// <para>
        /// <b>L'ancienne version demandait environ 1330 unites de large</b> — date 340, cinq
        /// boutons de vitesse 350, tresor 360, impots 128, fenetres 150 — sur un ecran qui en
        /// offre 700 au minimum garanti. « Gestion » et « Menu » se retrouvaient donc hors de
        /// l'ecran sur telephone, et le tresor tronque, sans qu'aucun message ne le signale :
        /// <c>GUILayout</c> deborde en silence.
        /// </para>
        /// <para>
        /// <b>Les commandes sont ancrees aux deux bords</b> et le tresor occupe ce qui reste. Ce
        /// qui doit disparaitre en premier sur un ecran etroit, c'est de l'information, jamais
        /// une commande : on peut relire un chiffre dans la fenetre de gestion, pas ouvrir un
        /// menu dont le bouton n'existe plus.
        /// </para>
        /// </summary>
        private void DrawBar(Rect rect)
        {
            float x = rect.x;

            // --- horloge, a gauche ---
            if (_gameClock != null)
            {
                var dateRect = new Rect(x, rect.y, DateWidth, rect.height);
                GUI.Label(new Rect(dateRect.x, dateRect.y + 1, dateRect.width, UITheme.ValueHeight),
                    HudFormatter.FormatDate(_gameClock.CurrentDate), UITheme.Value);
                GUI.Label(new Rect(dateRect.x, dateRect.y + 1 + UITheme.ValueHeight, dateRect.width, UITheme.CaptionHeight),
                    _gameClock.IsPaused ? "EN PAUSE" : $"x{_gameClock.CurrentMultiplier:0.#}", UITheme.Caption);

                x += DateWidth + Gap;
                x = DrawSpeedControls(new Rect(x, rect.y, SpeedGroupWidth, rect.height));
            }

            // --- fenetres, a droite, toujours visibles ---
            float rightEdge = rect.xMax;
            if (_pauseMenu != null)
            {
                var menuRect = new Rect(rightEdge - MenuButtonWidth, rect.y, MenuButtonWidth, rect.height);
                if (GUI.Button(menuRect, "Menu", UITheme.Button))
                {
                    _pauseMenu.ToggleVisible();
                }

                rightEdge = menuRect.x - Gap;
            }

            if (_managementWindow != null)
            {
                var gestionRect = new Rect(rightEdge - ManagementButtonWidth, rect.y, ManagementButtonWidth, rect.height);
                if (GUI.Button(gestionRect, "Gestion", UITheme.Button))
                {
                    _managementWindow.ToggleVisible();
                }

                rightEdge = gestionRect.x - Gap;
            }

            // --- impots, juste avant les fenetres ---
            if (_economy != null && rightEdge - x > TaxGroupWidth + MinResourceColumnWidth)
            {
                rightEdge = DrawTaxControls(new Rect(rightEdge - TaxGroupWidth, rect.y, TaxGroupWidth, rect.height)) - Gap;
            }

            // --- tresor : occupe ce qui reste, et n'affiche que ce qui tient ---
            if (_economy != null && rightEdge > x)
            {
                DrawTreasury(new Rect(x + Gap, rect.y, rightEdge - x - Gap, rect.height));
            }
        }

        /// <summary>
        /// Pause et quatre vitesses, en libelles courts. « Tres rapide » demandait a lui seul
        /// 90 unites ; « x3 » en demande 40 et se lit aussi bien, la vitesse active etant de
        /// toute facon rappelee sous la date.
        /// </summary>
        private float DrawSpeedControls(Rect rect)
        {
            float buttonWidth = (rect.width - 4 * Gap) / 5f;
            float x = rect.x;

            if (GUI.Button(new Rect(x, rect.y, buttonWidth, rect.height), _gameClock.IsPaused ? "\u25B6" : "II", UITheme.Button))
            {
                _gameClock.TogglePause();
            }
            x += buttonWidth + Gap;

            DrawSpeedButton(new Rect(x, rect.y, buttonWidth, rect.height), "x1", GameSpeed.Normal);
            x += buttonWidth + Gap;
            DrawSpeedButton(new Rect(x, rect.y, buttonWidth, rect.height), "x2", GameSpeed.Fast);
            x += buttonWidth + Gap;
            DrawSpeedButton(new Rect(x, rect.y, buttonWidth, rect.height), "x3", GameSpeed.Faster);
            x += buttonWidth + Gap;
            DrawSpeedButton(new Rect(x, rect.y, buttonWidth, rect.height), "x4", GameSpeed.Fastest);

            return rect.xMax + Gap;
        }

        /// <summary>La vitesse active est mise en evidence : sans cela, rien ne dit laquelle des cinq est en cours.</summary>
        private void DrawSpeedButton(Rect rect, string label, GameSpeed speed)
        {
            bool active = !_gameClock.IsPaused && _gameClock.CurrentSpeed == speed;
            if (GUI.Button(rect, label, active ? UITheme.ActiveTabButton : UITheme.Button))
            {
                _gameClock.SetSpeed(speed);
            }
        }

        private float DrawTaxControls(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y + 1, 36, UITheme.CaptionHeight), "IMPOT", UITheme.Caption);
            GUI.Label(new Rect(rect.x, rect.y + 1 + UITheme.CaptionHeight, 36, UITheme.ValueHeight),
                HudFormatter.FormatPercent(_economy.TaxRate), UITheme.Value);

            float buttonWidth = (rect.width - 40 - Gap) / 2f;
            if (GUI.Button(new Rect(rect.x + 40, rect.y, buttonWidth, rect.height), "-", UITheme.Button))
            {
                _economy.SetTaxRate(_economy.TaxRate - TaxStep);
            }

            if (GUI.Button(new Rect(rect.x + 40 + buttonWidth + Gap, rect.y, buttonWidth, rect.height), "+", UITheme.Button))
            {
                _economy.SetTaxRate(_economy.TaxRate + TaxStep);
            }

            return rect.x;
        }

        /// <summary>
        /// Ressources en colonnes egales, <b>seulement celles qui tiennent</b>. Une ressource
        /// omise vaut mieux qu'un tresor tronque en plein milieu d'un nombre, qui se lit de
        /// travers sans qu'on s'en apercoive.
        /// </summary>
        private void DrawTreasury(Rect rect)
        {
            ResourceBundle treasury = _economy.Treasury;

            var entries = new[]
            {
                ("CREDITS", treasury.Credits),
                ("MINERAI", treasury.Minerals),
                ("ENERGIE", treasury.Energy),
                ("ALLIAGE", treasury.Food),
                ("INFLU.", treasury.Influence),
            };

            int fits = Mathf.Clamp(Mathf.FloorToInt(rect.width / MinResourceColumnWidth), 0, entries.Length);
            if (fits == 0)
            {
                return;
            }

            float columnWidth = rect.width / fits;
            for (int i = 0; i < fits; i++)
            {
                float x = rect.x + i * columnWidth;
                GUI.Label(new Rect(x, rect.y + 1, columnWidth - Gap, UITheme.CaptionHeight), entries[i].Item1, UITheme.Caption);
                GUI.Label(new Rect(x, rect.y + 1 + UITheme.CaptionHeight, columnWidth - Gap, UITheme.ValueHeight),
                    HudFormatter.FormatResource(entries[i].Item2), UITheme.Value);
            }
        }
    }
}
