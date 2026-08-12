using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Chronicle;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.Research;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Interface permanente de la scene <c>GalaxyMap</c> : un liseré d'état sur toute la largeur,
    /// puis deux petites grappes ancrées dans les angles supérieurs.
    /// <para>
    /// <b>Ce qui a changé (Phase 23, tranche B).</b> Jusqu'ici, une barre pleine largeur de 44
    /// unités occupait 15 % de la hauteur garantie (286 unités sur un téléphone 22:9 dense), en
    /// permanence, et bloquait tous les appuis sur cette bande — impossible de sélectionner un
    /// système situé en haut de la carte. Elle affichait cinq ressources, un taux d'imposition et
    /// une date exacte : des nombres qu'on ne lit pas en continu.
    /// </para>
    /// <para>
    /// <b>Le liseré répond aux questions qu'on se pose sans s'arrêter</b> — suis-je solvable, mes
    /// provinces tiennent-elles, suis-je menacé, ma recherche avance-t-elle — en cinq unités de
    /// haut et sans un seul chiffre (voir <see cref="EmpireStateBand"/>). Les chiffres exacts et
    /// le réglage fiscal ont rejoint la fenêtre de gestion : <b>aucune fonction n'a disparu</b>,
    /// elles ont été rangées là où l'on vient déjà pour décider.
    /// </para>
    /// <para>
    /// <b>Seules les grappes bloquent les appuis</b>, plus la bande entière : le haut de la carte
    /// redevient cliquable entre les deux angles. Le liseré lui-même laisse passer le toucher, il
    /// n'a aucun élément interactif.
    /// </para>
    /// <para>
    /// <b>Bascule des autres écrans par <c>GetComponent</c>, pas par événement :</b> ce composant
    /// vit sur le même GameObject <c>[UI]</c> que <see cref="ManagementWindowController"/> et
    /// <see cref="PauseMenuController"/>. Un simple <c>GetComponent</c> en <c>Awake</c> suffit ;
    /// inventer un événement pour deux composants du même objet ajouterait de l'indirection sans
    /// bénéfice.
    /// </para>
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        // Les largeurs et la règle de repli vivent dans HudLayout, fonction pure vérifiée sur
        // tous les formats d'écran. Ce composant ne fait que dessiner ce qu'elle dispose.

        /// <summary>Opacité du remplissage d'un segment au repos.</summary>
        private const float SegmentOpacity = 0.85f;

        /// <summary>Opacité du fond d'un segment, sous son remplissage.</summary>
        private const float SegmentTrackOpacity = 0.22f;

        /// <summary>Période de la pulsation d'un segment alarmant, en secondes.</summary>
        private const float AlarmPeriodSeconds = 1.8f;

        /// <summary>Opacité minimale atteinte au creux de la pulsation.</summary>
        private const float AlarmTroughOpacity = 0.45f;

        private IGameClock _gameClock;
        private IEconomyService _economy;
        private IChronicleService _chronicle;
        private IEventBus _eventBus;
        private EmpireRegistry _empireRegistry;
        private GalaxyMap _map;
        private IMilitaryService _military;
        private IResearchService _research;

        private ManagementWindowController _managementWindow;
        private PauseMenuController _pauseMenu;

        /// <summary>
        /// Segments du liseré, recalculés une fois par jour de jeu.
        /// <para>
        /// <b>Jamais dans <c>OnGUI</c> :</b> Unity appelle <c>OnGUI</c> plusieurs fois par image
        /// (une passe de disposition, une passe de rendu, une par événement d'entrée), et
        /// <see cref="EmpireAssessmentFactory.Assess"/> parcourt tous les systèmes possédés et
        /// leurs voisins. Le calculer là serait le refaire des centaines de fois par seconde.
        /// </para>
        /// </summary>
        private StateSegment[] _band;

        private void Awake()
        {
            _managementWindow = GetComponent<ManagementWindowController>();
            _pauseMenu = GetComponent<PauseMenuController>();
        }

        private void Start()
        {
            if (ServiceLocator.TryGet(out _eventBus))
            {
                _eventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
            }
        }

        private void OnDestroy()
        {
            _eventBus?.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        private void OnDayAdvanced(DayAdvancedEvent dayAdvancedEvent)
        {
            RebuildBand();
        }

        private void OnGUI()
        {
            UITheme.BeginScaledLayout();
            try
            {
                if (_gameClock == null) ServiceLocator.TryGet(out _gameClock);
                if (_economy == null) ServiceLocator.TryGet(out _economy);

                HudLayout layout = HudLayout.For(UITheme.ScreenWidth, _gameClock != null);

                DrawBand();
                DrawRightCluster(layout);

                if (layout.ShowsCredits)
                {
                    DrawLeftCluster(layout);
                }
            }
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        // --- Liseré d'état ----------------------------------------------------------------

        /// <summary>
        /// Quatre segments de largeur égale, chacun rempli à hauteur de son indicateur.
        /// <para>
        /// <b>Le liseré n'est pas déclaré occupé</b> (pas d'appel à <c>UiScreenRegions.Occupy</c>) :
        /// il ne porte aucun bouton, donc un appui doit le traverser et atteindre la carte. C'est
        /// ce qui rend le haut de l'écran de nouveau utilisable.
        /// </para>
        /// </summary>
        private void DrawBand()
        {
            if (_band == null)
            {
                RebuildBand();
            }

            if (_band == null)
            {
                return;
            }

            float segmentWidth = UITheme.ScreenWidth / (float)EmpireStateBand.SegmentCount;

            for (int i = 0; i < _band.Length; i++)
            {
                var track = new Rect(i * segmentWidth, 0f, segmentWidth - 1f, HudLayout.BandHeight);
                StateSegment segment = _band[i];

                Color trackColor = segment.Color;
                trackColor.a = SegmentTrackOpacity;
                GUI.DrawTexture(track, UITheme.SolidTexture(trackColor), ScaleMode.StretchToFill, alphaBlend: true);

                if (segment.Fill <= 0f)
                {
                    continue;
                }

                Color fillColor = segment.Color;
                fillColor.a = segment.Alarming ? AlarmOpacity() : SegmentOpacity;

                var fill = new Rect(track.x, track.y, track.width * segment.Fill, track.height);
                GUI.DrawTexture(fill, UITheme.SolidTexture(fillColor), ScaleMode.StretchToFill, alphaBlend: true);
            }
        }

        /// <summary>
        /// Opacité pulsée d'un segment alarmant.
        /// <para>
        /// Basée sur <c>Time.unscaledTime</c> : une alerte doit battre au même rythme en pause
        /// qu'à vitesse quadruple — c'est précisément en pause qu'on la regarde.
        /// </para>
        /// </summary>
        private static float AlarmOpacity()
        {
            float phase = Mathf.Sin(Time.unscaledTime / AlarmPeriodSeconds * 2f * Mathf.PI) * 0.5f + 0.5f;
            return Mathf.Lerp(AlarmTroughOpacity, 1f, phase);
        }

        /// <summary>
        /// Recalcule les quatre segments à partir de la situation du joueur, exactement comme
        /// elle est calculée pour chaque IA.
        /// <para>
        /// Les services manquants sont tolérés : ils s'enregistrent à des instants différents du
        /// démarrage de la scène, et un liseré partiel vaut mieux qu'un liseré absent.
        /// </para>
        /// </summary>
        private void RebuildBand()
        {
            if (_empireRegistry == null) ServiceLocator.TryGet(out _empireRegistry);
            if (_map == null) ServiceLocator.TryGet(out _map);
            if (_economy == null) ServiceLocator.TryGet(out _economy);
            if (_military == null) ServiceLocator.TryGet(out _military);
            if (_research == null) ServiceLocator.TryGet(out _research);

            if (_empireRegistry == null || _map == null || _economy == null)
            {
                return;
            }

            if (!_empireRegistry.TryGetEmpire(EconomyService.PlayerOwnerId, out Empire player))
            {
                return;
            }

            EmpireAssessment assessment = EmpireAssessmentFactory.Assess(player, _map, _economy, _military);
            _band = EmpireStateBand.From(assessment, ResearchFraction());
        }

        /// <summary>
        /// Avancement vers le prochain palier du domaine actif, entre 0 et 1. Zéro si aucun
        /// domaine n'est choisi ou si le domaine est déjà au maximum.
        /// </summary>
        private float ResearchFraction()
        {
            if (_research == null)
            {
                return 0f;
            }

            ResearchDomain? domain = _research.GetActiveDomain(EconomyService.PlayerOwnerId);
            if (domain == null)
            {
                return 0f;
            }

            TechnologyDefinition next = _research.GetNextTechnology(EconomyService.PlayerOwnerId, domain.Value);
            if (next == null || next.ResearchPointCost <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(_research.GetProgress(EconomyService.PlayerOwnerId, domain.Value) / next.ResearchPointCost);
        }

        // --- Grappes d'angle ---------------------------------------------------------------

        /// <summary>
        /// Angle gauche : les Crédits, et eux seuls.
        /// <para>
        /// Les quatre autres ressources ont rejoint la fenêtre de gestion. Les Crédits restent
        /// parce qu'ils sont la monnaie de presque toutes les décisions prises <em>sur la
        /// carte</em> — construire, investir, recruter — et qu'aller les vérifier ailleurs
        /// interromprait le geste à chaque fois.
        /// </para>
        /// </summary>
        private void DrawLeftCluster(HudLayout layout)
        {
            if (_economy == null)
            {
                return;
            }

            Rect rect = layout.LeftCluster;
            GUI.Box(rect, GUIContent.none, UITheme.Header);
            UiScreenRegions.Occupy(rect, UITheme.Scale);

            ResourceBundle treasury = _economy.Treasury;
            GUI.Label(new Rect(rect.x + HudLayout.Gap, rect.y + 1, rect.width - 2 * HudLayout.Gap, UITheme.CaptionHeight), "CREDITS", UITheme.Caption);
            GUI.Label(
                new Rect(rect.x + HudLayout.Gap, rect.y + 1 + UITheme.CaptionHeight, rect.width - 2 * HudLayout.Gap, UITheme.ValueHeight),
                HudFormatter.FormatResource(treasury.Credits), UITheme.Value);
        }

        /// <summary>
        /// Angle droit : date, contrôle du temps, et les deux fenêtres.
        /// <para>
        /// <b>Les cinq boutons de vitesse sont conservés en accès direct</b> plutôt que remplacés
        /// par un cycle plus compact : changer de vitesse est le geste le plus fréquent d'un jeu
        /// de stratégie, et le faire en trois appuis au lieu d'un serait une régression que les
        /// unités gagnées ne paieraient pas.
        /// </para>
        /// </summary>
        private void DrawRightCluster(HudLayout layout)
        {
            Rect rect = layout.RightCluster;
            GUI.Box(rect, GUIContent.none, UITheme.Header);
            UiScreenRegions.Occupy(rect, UITheme.Scale);

            float x = rect.x + HudLayout.Gap;

            if (_gameClock != null && layout.ShowsDate)
            {
                GUI.Label(new Rect(x, rect.y + 1, HudLayout.DateWidth, UITheme.CaptionHeight),
                    _gameClock.IsPaused ? "EN PAUSE" : $"x{_gameClock.CurrentMultiplier:0.#}", UITheme.Caption);
                GUI.Label(new Rect(x, rect.y + 1 + UITheme.CaptionHeight, HudLayout.DateWidth, UITheme.ValueHeight),
                    HudFormatter.FormatDate(_gameClock.CurrentDate), UITheme.Value);

                x += HudLayout.DateWidth + HudLayout.Gap;
            }

            if (_gameClock != null)
            {
                x = DrawSpeedControls(new Rect(x, rect.y + 2, HudLayout.SpeedGroupWidth(), rect.height - 4)) + HudLayout.Gap;
            }

            x = DrawAlertBadge(new Rect(x, rect.y + 2, HudLayout.AlertBadgeWidth, rect.height - 4));

            if (_managementWindow != null)
            {
                if (GUI.Button(new Rect(x, rect.y + 2, HudLayout.ManagementButtonWidth, rect.height - 4), "Gestion", UITheme.Button))
                {
                    _managementWindow.ToggleVisible();
                }

                x += HudLayout.ManagementButtonWidth + HudLayout.Gap;
            }

            if (_pauseMenu != null && GUI.Button(new Rect(x, rect.y + 2, HudLayout.MenuButtonWidth, rect.height - 4), "Menu", UITheme.Button))
            {
                _pauseMenu.ToggleVisible();
            }
        }

        /// <summary>
        /// Compteur d'alertes (Phase 24, étape 1).
        /// <para>
        /// Il n'affiche que les avis <b>importants ou critiques</b> non lus : un compteur qui
        /// monterait à chaque départ de flotte afficherait un grand nombre en permanence et
        /// cesserait d'être une information.
        /// </para>
        /// <para>
        /// L'appui ouvre la fenêtre de gestion sur le journal et remet le compteur à zéro. Le
        /// bouton est toujours dessiné, même à zéro : voir <see cref="HudLayout.AlertBadgeWidth"/>
        /// pour la raison.
        /// </para>
        /// </summary>
        /// <returns>L'abscisse où reprendre le tracé.</returns>
        private float DrawAlertBadge(Rect rect)
        {
            if (_chronicle == null)
            {
                ServiceLocator.TryGet(out _chronicle);
            }

            int unread = _chronicle?.Log.UnreadCount ?? 0;
            bool critical = _chronicle != null && _chronicle.Log.HasUnreadCritical;

            string label = unread > 0 ? $"⚑{unread}" : "⚑";
            GUIStyle style = unread > 0 ? UITheme.ActiveTabButton : UITheme.Button;

            if (critical)
            {
                UITheme.DrawGlow(rect, EmpireStateBand.HealthColor(0f, 1f, 2f));
            }

            if (GUI.Button(rect, label, style) && _managementWindow != null)
            {
                _managementWindow.OpenJournal();
                _chronicle?.Log.MarkAllRead();
            }

            return rect.x + HudLayout.AlertBadgeWidth + HudLayout.Gap;
        }

        /// <summary>Pause et quatre vitesses, en libellés courts.</summary>
        private float DrawSpeedControls(Rect rect)
        {
            float x = rect.x;

            if (GUI.Button(new Rect(x, rect.y, HudLayout.SpeedButtonWidth, rect.height), _gameClock.IsPaused ? "▶" : "II", UITheme.Button))
            {
                _gameClock.TogglePause();
            }

            x += HudLayout.SpeedButtonWidth + HudLayout.Gap;
            x = DrawSpeedButton(x, rect, "x1", GameSpeed.Normal);
            x = DrawSpeedButton(x, rect, "x2", GameSpeed.Fast);
            x = DrawSpeedButton(x, rect, "x3", GameSpeed.Faster);
            x = DrawSpeedButton(x, rect, "x4", GameSpeed.Fastest);

            return x - HudLayout.Gap;
        }

        /// <summary>La vitesse active est mise en évidence : sans cela, rien ne dit laquelle est en cours.</summary>
        private float DrawSpeedButton(float x, Rect rect, string label, GameSpeed speed)
        {
            bool active = !_gameClock.IsPaused && _gameClock.CurrentSpeed == speed;
            if (GUI.Button(new Rect(x, rect.y, HudLayout.SpeedButtonWidth, rect.height), label, active ? UITheme.ActiveTabButton : UITheme.Button))
            {
                _gameClock.SetSpeed(speed);
            }

            return x + HudLayout.SpeedButtonWidth + HudLayout.Gap;
        }
    }
}
