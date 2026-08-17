using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Chronicle;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Espionage;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.Research;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>Un onglet de la fenetre de gestion (voir <see cref="ManagementWindowController"/>).</summary>
    public enum ManagementTab
    {
        Empire,
        Flottes,
        Diplomatie,
        Recherche,
        Espionnage,
        Journal,
        Codex
    }

    /// <summary>
    /// Le rail de gestion : six entrees permanentes contre le bord gauche, et un panneau qui se
    /// pose a cote (Phase 24, etape 2).
    /// <para>
    /// <b>Ce qui est remplace.</b> Une fenetre modale de 660 × 460, centree — donc <b>coupee</b>
    /// sur un ecran de 286 unites de haut, et recouvrant la carte entierement. Le jeu avait deux
    /// modes : regarder, ou gerer. Comparer deux systemes etait impossible, suivre une flotte en
    /// vol pendant qu'on en commande une autre aussi.
    /// </para>
    /// <para>
    /// <b>Le rail est la navigation, pas une fenetre :</b> il est toujours affiche, toujours a la
    /// meme place, et le bouton « Gestion » du bandeau a disparu avec lui. Seul le panneau
    /// s'ouvre et se ferme — en appuyant sur l'entree deja ouverte, ou sur la croix.
    /// </para>
    /// <para>
    /// <b>Six entrees et non huit.</b> « Operations » est repliee dans Flottes : les deux
    /// montraient des flottes, rien n'indiquait laquelle ouvrir, et une flotte en campagne
    /// figurait dans les deux. « Sauvegarde » rejoint le menu pause, qui portait deja les memes
    /// boutons — sauvegarder n'est pas une decision de jeu, c'est une operation sur la partie.
    /// </para>
    /// <para>
    /// Pas d'entree « Economie » : le tresor complet et le reglage fiscal vivent dans Empire
    /// depuis la Phase 23, et les actions economiques portent toujours sur un systeme precis,
    /// donc vivent dans <see cref="SystemInfoPanelController"/>.
    /// </para>
    /// </summary>
    public sealed class ManagementWindowController : MonoBehaviour
    {
        /// <summary>Pas du reglage fiscal, repris de la barre superieure d'ou il vient (Phase 23).</summary>
        private const float TaxStep = 0.1f;

        private static readonly ManagementTab[] AllTabs = (ManagementTab[])Enum.GetValues(typeof(ManagementTab));
        private static readonly ResearchDomain[] AllDomains = (ResearchDomain[])Enum.GetValues(typeof(ResearchDomain));

        /// <summary>
        /// Journal de la partie (Phase 24, etape 1).
        /// <para>
        /// <b>Remplace les rapports d'operation de la Phase 20.</b> Cette fenetre tenait sa
        /// propre liste de six lignes et s'abonnait elle-meme au bus d'evenements pour la
        /// remplir — un contournement acceptable tant qu'il n'existait rien d'autre. Le journal
        /// est desormais un service, et l'interface se contente de le lire.
        /// </para>
        /// </summary>
        private IChronicleService _chronicle;
        private ICodexService _codex;

        /// <summary>Denouements recents rappeles en bas de l'entree Flottes.</summary>
        private const int OperationReportCount = 6;

        /// <summary>
        /// Au-dela de cet effectif, une flotte stationnee est une force de defense plutot qu'une
        /// simple garnison. Distinction d'affichage seulement : aucune regle ne s'y attache.
        /// </summary>
        private const int GarrisonSizedFleet = 3;

        /// <summary>Largeur de la jauge de puissance des flottes.</summary>
        private const float PowerBarWidth = 76f;

        /// <summary>
        /// Entree ouverte, ou <c>null</c> si seul le rail est visible.
        /// <para>
        /// Le rail, lui, est <b>toujours</b> affiche : c'est la navigation, pas une fenetre.
        /// </para>
        /// </summary>
        private ManagementTab? _openTab;

        private Vector2 _contentScroll;

        private EmpireRegistry _empireRegistry;
        private IEconomyService _economy;
        private GalaxyMap _map;
        private IDiplomacyService _diplomacy;
        private IResearchService _research;
        private IEspionageService _espionage;
        private IMilitaryService _military;

        private readonly Dictionary<int, UnitBundle> _lastDiscoveredArmies = new Dictionary<int, UnitBundle>();

        private IEventBus _eventBus;

        /// <summary>
        /// Bilan du joueur et systemes qui demandent quelque chose, recalcules une fois par jour
        /// de jeu (Phase 24, etape 4).
        /// <para>
        /// <b>Jamais dans <c>OnGUI</c>, pour la meme raison que le lisere d'etat :</b> Unity
        /// appelle <c>OnGUI</c> plusieurs fois par image, et
        /// <see cref="EmpireAssessmentFactory.Assess"/> parcourt tous les systemes possedes et
        /// leurs voisins — en allouant une liste au passage. Le calculer la reviendrait a le
        /// refaire des centaines de fois par seconde et a produire des ordures a chaque image.
        /// </para>
        /// </summary>
        private EmpireAssessment _assessment;

        private readonly List<SystemAttention> _attention = new List<SystemAttention>(SystemAttentionList.MaximumEntries);

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
            RefreshEmpireDigest();
        }

        /// <summary>Recalcule le bilan et la liste d'attention. Sans effet si les services manquent encore.</summary>
        private void RefreshEmpireDigest()
        {
            ResolveServices();

            Empire player = _empireRegistry?.PlayerEmpire;
            if (player == null || _map == null)
            {
                return;
            }

            _assessment = EmpireAssessmentFactory.Assess(player, _map, _economy, _military);
            SystemAttentionList.Fill(_attention, player.Id, _map, _military, _diplomacy);
        }

        private void OnGUI()
        {
            UITheme.BeginScaledLayout();
            try
            {
                ResolveServices();

                ManagementLayout layout = ManagementLayout.For(UITheme.ScreenWidth, UITheme.ScreenHeight);

                DrawRail(layout);

                if (_openTab.HasValue)
                {
                    DrawPanel(layout, _openTab.Value);
                }
            }
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        /// <summary>
        /// Le rail : six entrees, toujours visibles, toujours a la meme place.
        /// <para>
        /// <b>C'est ce qui permet a la memoire musculaire de s'installer</b> : le panneau change,
        /// jamais le rail. Appuyer sur l'entree deja ouverte referme — un seul geste pour ouvrir
        /// et fermer.
        /// </para>
        /// </summary>
        private void DrawRail(ManagementLayout layout)
        {
            GUI.Box(layout.Rail, GUIContent.none, UITheme.Panel);
            UiScreenRegions.Occupy(layout.Rail, UITheme.Scale);

            for (int i = 0; i < AllTabs.Length; i++)
            {
                ManagementTab tab = AllTabs[i];
                var itemRect = new Rect(
                    layout.Rail.x, layout.Rail.y + i * ManagementLayout.RailItemHeight,
                    layout.Rail.width, ManagementLayout.RailItemHeight);

                if (itemRect.yMax > layout.Rail.yMax)
                {
                    break;
                }

                bool open = _openTab.HasValue && _openTab.Value == tab;
                GUIStyle style = open ? UITheme.ActiveTabButton : UITheme.TabButton;

                if (GUI.Button(itemRect, RailLabel(tab), style))
                {
                    _openTab = open ? (ManagementTab?)null : tab;
                    _contentScroll = Vector2.zero;
                }
            }
        }

        /// <summary>
        /// Libelle d'une entree, avec le compteur d'avis non lus sur le Journal.
        /// <para>
        /// Le compteur apparait a deux endroits — ici et dans le bandeau — volontairement : deux
        /// chemins vers le meme endroit, et aucun element orphelin.
        /// </para>
        /// </summary>
        private string RailLabel(ManagementTab tab)
        {
            if (tab != ManagementTab.Journal)
            {
                return tab.ToString();
            }

            int unread = _chronicle?.Log.UnreadCount ?? 0;
            return unread > 0 ? $"Journal  {unread}" : "Journal";
        }

        /// <summary>
        /// Le panneau, pose <b>a cote</b> du rail et jamais par-dessus la carte entiere : il
        /// reste toujours de la galaxie manipulable a droite (voir <see cref="ManagementLayout"/>).
        /// </summary>
        private void DrawPanel(ManagementLayout layout, ManagementTab tab)
        {
            GUI.Box(layout.Panel, GUIContent.none, UITheme.Panel);
            UiScreenRegions.Occupy(layout.Panel, UITheme.Scale);

            var closeRect = new Rect(layout.Panel.xMax - 26, layout.Panel.y + 5, 20, 18);
            if (GUI.Button(closeRect, "\u00D7", UITheme.Button))
            {
                _openTab = null;
                return;
            }

            GUILayout.BeginArea(new Rect(layout.Panel.x + 8, layout.Panel.y + 6, layout.Panel.width - 38, layout.Panel.height - 12));
            _contentScroll = GUILayout.BeginScrollView(_contentScroll);

            DrawTabContent(tab);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void ResolveServices()
        {
            if (_empireRegistry == null) ServiceLocator.TryGet(out _empireRegistry);
            if (_economy == null) ServiceLocator.TryGet(out _economy);
            if (_map == null) ServiceLocator.TryGet(out _map);
            if (_diplomacy == null) ServiceLocator.TryGet(out _diplomacy);
            if (_research == null) ServiceLocator.TryGet(out _research);
            if (_espionage == null) ServiceLocator.TryGet(out _espionage);
            if (_military == null) ServiceLocator.TryGet(out _military);
            if (_chronicle == null) ServiceLocator.TryGet(out _chronicle);
            if (_codex == null) ServiceLocator.TryGet(out _codex);
        }

        /// <summary>Ouvre la fenetre directement sur le journal. Appele par le compteur d'alertes.</summary>
        public void OpenJournal()
        {
            _openTab = ManagementTab.Journal;
            _contentScroll = Vector2.zero;
        }

        /// <summary>
        /// Le journal de la partie (Phase 24, etape 1) : ce qui s'est passe, du plus recent au
        /// plus ancien.
        /// <para>
        /// <b>Le niveau se lit a la marge</b>, pas dans le texte : un avis critique porte un
        /// reperage colore, les autres non. Ecrire « CRITIQUE » devant chaque ligne mangerait de
        /// la largeur et rendrait le fil plus difficile a parcourir, pas plus clair.
        /// </para>
        /// <para>
        /// L'ouverture de cet onglet remet le compteur d'alertes a zero : ce que le joueur a
        /// sous les yeux est, par definition, lu.
        /// </para>
        /// </summary>
        private void DrawJournalTab()
        {
            GUILayout.Label("Journal", UITheme.Title);

            if (_chronicle == null)
            {
                GUILayout.Label("Journal indisponible.", UITheme.MutedLabel);
                return;
            }

            _chronicle.Log.MarkAllRead();

            IReadOnlyList<GameNotice> notices = _chronicle.Log.Notices;

            if (notices.Count == 0)
            {
                GUILayout.Label("Rien ne s'est encore produit.", UITheme.MutedLabel);
                return;
            }

            foreach (GameNotice notice in notices)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label(notice.Date.ToString(), UITheme.MutedLabel, GUILayout.Width(92));
                GUILayout.Label(
                    notice.Text,
                    notice.Tier == NoticeTier.Information ? UITheme.MutedLabel : UITheme.Label);

                GUILayout.FlexibleSpace();

                if (notice.Tier == NoticeTier.Critical)
                {
                    GUILayout.Label("!", UITheme.Value, GUILayout.Width(14));
                }

                GUILayout.EndHorizontal();
            }
        }

        /// <summary>Aiguille vers le contenu de l'entree ouverte.</summary>
        private void DrawTabContent(ManagementTab tab)
        {
            switch (tab)
            {
                case ManagementTab.Empire:
                    DrawEmpiresTab();
                    break;
                case ManagementTab.Flottes:
                    DrawFlottesTab();
                    break;
                case ManagementTab.Diplomatie:
                    DrawDiplomatieTab();
                    break;
                case ManagementTab.Recherche:
                    DrawRechercheTab();
                    break;
                case ManagementTab.Espionnage:
                    DrawEspionnageTab();
                    break;
                case ManagementTab.Journal:
                    DrawJournalTab();
                    break;
                case ManagementTab.Codex:
                    DrawCodexTab();
                    break;
            }
        }

        /// <summary>
        /// Le codex : les fragments du journal de l'Empire disparu (Phase 24, etape 3).
        /// <para>
        /// <b>Pourquoi une entree separee du Journal.</b> Les deux sont des historiques, mais pas
        /// de la meme chose et pas de la meme duree. Le journal est le recit de <i>cette</i>
        /// partie et vit dans un tampon circulaire de soixante avis : une ligne y disparait au
        /// bout d'un moment, ce qui est exactement ce qu'on veut d'un fil d'actualite. Un
        /// fragment, lui, est acquis pour toujours. Les melanger ferait defiler hors de portee la
        /// seule chose du jeu qu'on ne peut pas retrouver autrement.
        /// </para>
        /// <para>
        /// <b>Les fragments manquants sont montres, jamais decrits.</b> On affiche leur numero et
        /// rien d'autre. Voir qu'il en manque sept est une invitation ; savoir ce qu'ils
        /// contiennent supprimerait la raison d'aller les chercher.
        /// </para>
        /// </summary>
        private void DrawCodexTab()
        {
            GUILayout.Label("Codex", UITheme.Title);

            if (_codex == null)
            {
                GUILayout.Label("Codex indisponible.", UITheme.MutedLabel);
                return;
            }

            FactionLineage playerLineage = PlayerLineage();

            GUILayout.Label(
                $"{_codex.UnlockedCount} fragment(s) sur {CodexLibrary.Count}.",
                UITheme.MutedLabel);

            if (_codex.UnlockedCount == 0)
            {
                GUILayout.Space(4f);
                GUILayout.Label(
                    "Des debris de registres circulent encore entre les mondes. Aucun ne vous est"
                    + " parvenu.",
                    UITheme.MutedLabel);
                return;
            }

            foreach (CodexFragment fragment in CodexLibrary.All)
            {
                GUILayout.Space(6f);

                if (!_codex.IsUnlocked(fragment.Number))
                {
                    // « Jamais » et « pas encore » ne se disent pas pareil. Un joueur bloque a
                    // 11 sur 12 sans explication croit avoir manque quelque chose.
                    GUILayout.Label(
                        fragment.IsBeyondReachFor(playerLineage)
                            ? $"{fragment.Numeral} — vos propres registres. Detruits avec ceux de l'Empire."
                            : $"{fragment.Numeral} — ?",
                        UITheme.MutedLabel);
                    continue;
                }

                GUILayout.Label($"{fragment.Numeral} · {fragment.Title}", UITheme.Value);
                GUILayout.Label(fragment.Text, UITheme.Label);
                GUILayout.Label(fragment.Reveal, UITheme.MutedLabel);

                if (fragment.ShowsPlayerLedger)
                {
                    DrawPlayerLedger();
                }
            }
        }

        /// <summary>Filiation de la faction jouee, ou <c>Unknown</c> si le registre n'est pas encore la.</summary>
        private FactionLineage PlayerLineage()
        {
            Empire player = _empireRegistry?.PlayerEmpire;
            return player != null ? player.Lineage : FactionLineage.Unknown;
        }

        /// <summary>
        /// Les colonnes que le fragment III demande de comparer — celles du joueur, reelles.
        /// <para>
        /// C'est le moment ou la lettre cesse de parler d'un autre. Les chiffres ne sont pas
        /// illustratifs : ils viennent de l'empire en cours, et le rapprochement que le texte
        /// suggere est verifiable.
        /// </para>
        /// </summary>
        private void DrawPlayerLedger()
        {
            if (_economy == null || _map == null)
            {
                return;
            }

            int systems = 0;
            foreach (StarSystemState system in _map.Systems)
            {
                if (system.OwnerId == EconomyService.PlayerOwnerId)
                {
                    systems++;
                }
            }

            // La pression est absolue (0 a MaximumPressure). L'afficher telle quelle donnerait
            // « 35 % » a l'instant ou elle est en realite au plafond : on la rapporte donc a son
            // maximum, qui est ce que la phrase annonce.
            float pressure = _economy.GetAdministrativePressure(EconomyService.PlayerOwnerId);
            float shareOfCeiling = pressure / AdministrationModel.MaximumPressure;

            GUILayout.Space(2f);
            GUILayout.Label(
                $"Vos colonnes : {systems} systeme(s) tenu(s), pression administrative a "
                + $"{shareOfCeiling:P0} du plafond.",
                UITheme.Value);
        }

        /// <summary>
        /// Le panneau Empire repond a une seule question : <b>« est-ce que je vais bien ? »</b>
        /// (Phase 24, etape 4).
        /// <para>
        /// <b>Ce qu'il remplacait.</b> Une liste plate des six empires avec leur trésor et leur
        /// nombre de systemes. Elle repondait a « qui existe ? », question que personne ne se
        /// pose, et laissait sans reponse la seule qui compte vraiment.
        /// </para>
        /// <para>
        /// <b>La posture et les quatre indicateurs ne sont pas nouveaux</b> : ce sont ceux du
        /// lisere d'etat, en detail, et ceux-la memes sur lesquels chaque IA decide de sa posture
        /// depuis la Phase 22. Le joueur regarde le tableau de bord de ses adversaires.
        /// </para>
        /// <para>
        /// <b>Puis vient la suite immediate : ou faut-il aller ?</b> C'est la liste d'attention,
        /// et c'est la seule vraie nouveaute de ce panneau.
        /// </para>
        /// </summary>
        private void DrawEmpiresTab()
        {
            DrawPlayerEconomy();
            DrawPosture();
            DrawAttentionList();
        }

        private void DrawPosture()
        {
            Empire player = _empireRegistry?.PlayerEmpire;
            if (player == null)
            {
                GUILayout.Label("Registre des empires indisponible.", UITheme.MutedLabel);
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label("Posture", UITheme.Title);
            GUILayout.Label(_assessment.Posture.ToString().ToUpperInvariant(), UITheme.Value);
            GUILayout.Label(_assessment.Explain(), UITheme.MutedLabel);

            GUILayout.Space(4f);
            DrawIndicator("Tresorerie", $"{_assessment.FinancialRunwayMonths:F1} mois");
            DrawIndicator("Stabilite", $"{_assessment.AverageStability:P0}");
            DrawIndicator("Forces", $"{_assessment.MilitaryRatio:F1} x");
            DrawIndicator("Place a prendre", $"{_assessment.GrowthHeadroom:P0}");
        }

        private static void DrawIndicator(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, UITheme.MutedLabel, GUILayout.Width(110));
            GUILayout.Label(value, UITheme.Value);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Les systemes qui demandent quelque chose.
        /// <para>
        /// <b>Une liste vide est une bonne nouvelle, et doit se lire comme telle</b> — d'ou une
        /// phrase, et non un cadre vide qui laisse croire que l'affichage est casse.
        /// </para>
        /// </summary>
        private void DrawAttentionList()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Demandent quelque chose", UITheme.Title);

            if (_attention.Count == 0)
            {
                GUILayout.Label("Aucun systeme ne reclame d'attention.", UITheme.MutedLabel);
                return;
            }

            foreach (SystemAttention entry in _attention)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(entry.Name, UITheme.Label, GUILayout.Width(120));
                GUILayout.Label(entry.Describe(), UITheme.MutedLabel);
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Tresor complet et taux d'imposition du joueur (Phase 23, tranche B).
        /// <para>
        /// <b>Ces deux blocs vivaient dans la barre superieure</b> et lui coutaient a eux seuls
        /// plus de 200 unites de large, en permanence, pour une information qu'on ne consulte pas
        /// en continu. Le lisere d'etat repond desormais a la seule question qui se pose sans
        /// s'arreter — « suis-je solvable ? » — et les chiffres exacts, comme le reglage fiscal,
        /// ont leur place ici, la ou l'on vient deja pour decider.
        /// </para>
        /// <para>
        /// C'est ce deplacement qui rend la reduction de la barre possible <b>sans perdre une
        /// seule fonction</b> : rien n'a ete supprime, tout a ete range.
        /// </para>
        /// </summary>
        private void DrawPlayerEconomy()
        {
            if (_economy == null)
            {
                return;
            }

            GUILayout.Label("Tresor", UITheme.Title);

            ResourceBundle treasury = _economy.GetTreasury(EconomyService.PlayerOwnerId);
            GUILayout.Label(
                $"Credits {HudFormatter.FormatResource(treasury.Credits)}   "
                + $"Minerai {HudFormatter.FormatResource(treasury.Minerals)}   "
                + $"Energie {HudFormatter.FormatResource(treasury.Energy)}   "
                + $"Alliage {HudFormatter.FormatResource(treasury.Food)}   "
                + $"Influence {HudFormatter.FormatResource(treasury.Influence)}",
                UITheme.Label);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Imposition {HudFormatter.FormatPercent(_economy.TaxRate)}", UITheme.Label, GUILayout.Width(140));

            if (GUILayout.Button("-", UITheme.Button, GUILayout.Width(34)))
            {
                _economy.SetTaxRate(_economy.TaxRate - TaxStep);
            }

            if (GUILayout.Button("+", UITheme.Button, GUILayout.Width(34)))
            {
                _economy.SetTaxRate(_economy.TaxRate + TaxStep);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
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

        // --- Flottes (Phase 14) -----------------------------------------------------------

        private void DrawFlottesTab()
        {
            GUILayout.Label("Flottes", UITheme.Title);

            if (_military == null || _map == null)
            {
                GUILayout.Label("Service militaire indisponible.", UITheme.MutedLabel);
                return;
            }

            IReadOnlyList<Fleet> fleets = _military.GetFleetsForEmpire(EconomyService.PlayerOwnerId);
            if (fleets.Count == 0)
            {
                GUILayout.Label("Aucune flotte.", UITheme.MutedLabel);
                return;
            }

            int campaigning = 0;
            foreach (Fleet fleet in fleets)
            {
                if (fleet.Status != FleetStatus.Stationed)
                {
                    campaigning++;
                }
            }

            GUILayout.Label(
                _military.CanDeployAnotherFleet(EconomyService.PlayerOwnerId)
                    ? $"{campaigning} en campagne — une place reste libre"
                    : $"{campaigning} en campagne — plafond atteint, recherchez la Logistique",
                UITheme.MutedLabel);

            GUILayout.Space(6);

            // La barre est relative a la plus forte flotte du joueur : c'est cette reference,
            // et non une constante, qui garde l'echelle utile en fin de partie.
            float strongest = 0f;
            foreach (Fleet fleet in fleets)
            {
                float power = _military.EstimatePower(fleet.Composition);
                if (power > strongest)
                {
                    strongest = power;
                }
            }

            foreach (Fleet fleet in fleets)
            {
                DrawFleetRow(fleet, strongest);
            }

            DrawRecentOutcomes();
        }

        /// <summary>
        /// Denouements recents, repris de l'ancien onglet Operations (Phase 20).
        /// <para>
        /// Ils vivent desormais sous la liste des flottes plutot que dans une entree distincte :
        /// « qu'est-il arrive a mes flottes » et « ou sont mes flottes » sont la meme question,
        /// et les separer obligeait le joueur a choisir laquelle poser.
        /// </para>
        /// </summary>
        private void DrawRecentOutcomes()
        {
            List<GameNotice> recent = _chronicle?.Log.MostRecent(OperationReportCount, NoticeTier.Important);
            if (recent == null || recent.Count == 0)
            {
                return;
            }

            GUILayout.Space(10);
            GUILayout.Label("Denouements recents", UITheme.Title);

            foreach (GameNotice notice in recent)
            {
                GUILayout.Label($"{notice.Date}  —  {notice.Text}", UITheme.Label);
            }
        }

        /// <summary>
        /// Une ligne de la liste des flottes (Phase 24, etape 4).
        /// <para>
        /// <b>La barre donne la puissance relative, le libelle donne la mission, la ligne en
        /// retrait donne la destination.</b> Aucun chiffre absolu : « puissance ~418 » ne veut
        /// rien dire tant qu'on ignore ce que vaut le reste, et l'action la plus frequente sur
        /// cet ecran est de comparer deux flottes, pas d'en lire une.
        /// </para>
        /// </summary>
        private void DrawFleetRow(Fleet fleet, float strongestPower)
        {
            string location;
            switch (fleet.Status)
            {
                case FleetStatus.Stationed:
                    location = LocationLabel(fleet.CurrentSystemId);
                    break;

                case FleetStatus.AwaitingEncounter:
                    location = $"Rencontre en cours, en route vers {LocationLabel(fleet.DestinationSystemId.Value)}";
                    break;

                default:
                    // Itineraire restant (Phase 17) : une traversee peut compter plusieurs sauts.
                    int remainingHops = fleet.Route.Count - 1 - fleet.RouteIndex;
                    location = $"En route vers {LocationLabel(fleet.DestinationSystemId.Value)}"
                        + $" ({remainingHops} saut(s) restant(s), arrivee d'etape le {fleet.ArrivalDate})";
                    break;
            }

            float power = _military.EstimatePower(fleet.Composition);
            string mission = FleetRoster.MissionLabel(fleet.Status, fleet.Composition.TotalCount <= GarrisonSizedFleet);

            GUILayout.BeginHorizontal();
            GUILayout.Label(fleet.Name, UITheme.Value, GUILayout.Width(96));
            DrawPowerBar(FleetRoster.PowerShare(power, strongestPower));
            GUILayout.Label(
                FleetRoster.IsCrippled(power, strongestPower) ? "decimee" : mission,
                UITheme.MutedLabel,
                GUILayout.Width(84));
            GUILayout.EndHorizontal();

            GUILayout.Label($"    {location}", UITheme.MutedLabel);
            GUILayout.Label($"    {fleet.Composition}", UITheme.MutedLabel);
            GUILayout.Label(
                $"Amiral {fleet.Admiral.Name} — Attaque {HudFormatter.FormatSigned(fleet.Admiral.AttackBonus * 100f)}% "
                + $"/ Vitesse {HudFormatter.FormatSigned(fleet.Admiral.SpeedBonus * 100f)}% "
                + $"/ Defense {HudFormatter.FormatSigned(fleet.Admiral.DefenseBonus * 100f)}%",
                UITheme.MutedLabel);
        }

        /// <summary>
        /// Jauge de puissance d'une flotte. La teinte suit l'etat : une flotte decimee doit se
        /// distinguer d'une flotte simplement plus petite.
        /// </summary>
        private static void DrawPowerBar(float share)
        {
            Rect rect = GUILayoutUtility.GetRect(PowerBarWidth, UITheme.LabelHeight, GUILayout.Width(PowerBarWidth));
            rect = new Rect(rect.x, rect.y + rect.height * 0.3f, rect.width, rect.height * 0.4f);

            UITheme.DrawMeter(rect, share, EmpireStateBand.HealthColor(share, 0.2f, 0.6f));
        }

        private string LocationLabel(StarSystemId systemId)
        {
            return _map.TryGetSystem(systemId, out StarSystemState system) ? system.Name : systemId.ToString();
        }

        // --- Diplomatie --------------------------------------------------------------------

        private void DrawDiplomatieTab()
        {
            GUILayout.Label("Diplomatie", UITheme.Title);

            if (_diplomacy == null || _empireRegistry == null)
            {
                GUILayout.Label("Service diplomatique indisponible.", UITheme.MutedLabel);
                return;
            }

            foreach (Empire empire in _empireRegistry.Empires)
            {
                if (empire.IsPlayerControlled)
                {
                    continue;
                }

                DrawRelationRow(empire);
            }

            GUILayout.Space(10);
            DrawPendingProposals();
        }

        private void DrawRelationRow(Empire empire)
        {
            int playerId = EconomyService.PlayerOwnerId;
            DiplomaticStatus status = _diplomacy.GetStatus(playerId, empire.Id);
            float opinion = _diplomacy.GetOpinion(empire.Id, playerId);

            GUILayout.Label($"{empire.Name} : {StatusLabel(status)} (opinion {opinion:0})", UITheme.Label);

            GUILayout.BeginHorizontal();
            switch (status)
            {
                case DiplomaticStatus.Peace:
                    if (GUILayout.Button("Guerre", UITheme.Button)) _diplomacy.TryDeclareWar(playerId, empire.Id, out _);
                    if (GUILayout.Button("Pacte", UITheme.Button)) _diplomacy.TrySubmitProposal(playerId, empire.Id, ProposalType.NonAggressionPact, default, default, null, null, out _);
                    if (GUILayout.Button("Alliance", UITheme.Button)) _diplomacy.TrySubmitProposal(playerId, empire.Id, ProposalType.Alliance, default, default, null, null, out _);
                    break;

                case DiplomaticStatus.War:
                    if (GUILayout.Button("Paix", UITheme.Button)) _diplomacy.TrySubmitProposal(playerId, empire.Id, ProposalType.PeaceTreaty, default, default, null, null, out _);
                    break;

                case DiplomaticStatus.Alliance:
                case DiplomaticStatus.NonAggressionPact:
                    if (GUILayout.Button("Rompre", UITheme.Button)) _diplomacy.TryBreakPact(playerId, empire.Id, out _);
                    break;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawPendingProposals()
        {
            IReadOnlyList<DiplomaticProposal> pending = _diplomacy.GetPendingProposalsFor(EconomyService.PlayerOwnerId);
            if (pending.Count == 0)
            {
                return;
            }

            GUILayout.Label("Propositions recues :", UITheme.Title);
            foreach (DiplomaticProposal proposal in pending)
            {
                string proposerName = _empireRegistry.TryGetEmpire(proposal.ProposerId, out Empire proposer) ? proposer.Name : proposal.ProposerId.ToString();
                GUILayout.Label($"{proposerName} : {proposal.Type}", UITheme.Label);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Accepter", UITheme.Button)) _diplomacy.TryRespondToProposal(proposal.Id, true, out _);
                if (GUILayout.Button("Refuser", UITheme.Button)) _diplomacy.TryRespondToProposal(proposal.Id, false, out _);
                GUILayout.EndHorizontal();
            }
        }

        private static string StatusLabel(DiplomaticStatus status)
        {
            switch (status)
            {
                case DiplomaticStatus.War: return "Guerre";
                case DiplomaticStatus.Alliance: return "Alliance";
                case DiplomaticStatus.NonAggressionPact: return "Pacte de non-agression";
                default: return "Paix";
            }
        }

        // --- Recherche -----------------------------------------------------------------

        private void DrawRechercheTab()
        {
            GUILayout.Label("Recherche", UITheme.Title);

            if (_research == null)
            {
                GUILayout.Label("Service de recherche indisponible.", UITheme.MutedLabel);
                return;
            }

            int playerId = EconomyService.PlayerOwnerId;
            ResearchDomain? active = _research.GetActiveDomain(playerId);
            GUILayout.Label(active == null ? "Aucun domaine actif." : $"Focus : {DomainLabel(active.Value)}", UITheme.Label);

            foreach (ResearchDomain domain in AllDomains)
            {
                DrawDomainRow(playerId, domain, active);
            }
        }

        private void DrawDomainRow(int playerId, ResearchDomain domain, ResearchDomain? active)
        {
            int completedTiers = _research.GetCompletedTierCount(playerId, domain);
            float bonus = _research.GetBonus(playerId, domain);
            TechnologyDefinition next = _research.GetNextTechnology(playerId, domain);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{DomainLabel(domain)} : palier {completedTiers} ({HudFormatter.FormatSigned(bonus * 100f)}%)", UITheme.Label, GUILayout.Width(280));

            if (domain == active)
            {
                GUILayout.Label("Actif", UITheme.MutedLabel);
            }
            else if (next == null)
            {
                GUILayout.Label("Max", UITheme.MutedLabel);
            }
            else if (GUILayout.Button("Activer", UITheme.Button))
            {
                _research.TrySetActiveDomain(playerId, domain, out string error);
                if (error != null)
                {
                    GameLog.Warning($"[Research] {error}");
                }
            }
            GUILayout.EndHorizontal();
        }

        private static string DomainLabel(ResearchDomain domain)
        {
            switch (domain)
            {
                case ResearchDomain.Economy: return "Economie";
                case ResearchDomain.Industry: return "Industrie";
                case ResearchDomain.Weapons: return "Armement";
                case ResearchDomain.Energy: return "Energie";
                case ResearchDomain.Diplomacy: return "Diplomatie";
                case ResearchDomain.Espionage: return "Espionnage";
                case ResearchDomain.Logistics: return "Logistique";
                default: return domain.ToString();
            }
        }

        // --- Espionnage -----------------------------------------------------------------

        private void DrawEspionnageTab()
        {
            GUILayout.Label("Espionnage", UITheme.Title);

            if (_espionage == null || _empireRegistry == null || _map == null)
            {
                GUILayout.Label("Service d'espionnage indisponible.", UITheme.MutedLabel);
                return;
            }

            int playerId = EconomyService.PlayerOwnerId;
            GUILayout.Label($"Puissance d'espionnage : {_espionage.GetEspionagePower(playerId):0}", UITheme.Label);

            foreach (Empire empire in _empireRegistry.Empires)
            {
                if (empire.IsPlayerControlled)
                {
                    continue;
                }

                DrawEspionageRow(playerId, empire);
            }
        }

        private void DrawEspionageRow(int playerId, Empire target)
        {
            StarSystemId? targetSystemId = FindPrimarySystemId(target.Id);
            if (targetSystemId == null)
            {
                return;
            }

            float counterPower = _espionage.GetCounterEspionagePower(target.Id, targetSystemId.Value);
            float vigilance = _espionage.GetVigilance(target.Id);
            float ceiling = _espionage.MaximumVigilance;

            GUILayout.BeginHorizontal();
            GUILayout.Label(target.Name, UITheme.Label, GUILayout.Width(150));
            GUILayout.Label($"vigilance {vigilance:0.0}", UITheme.MutedLabel, GUILayout.Width(90));
            GUILayout.Label($"contre-espionnage {counterPower:0}", UITheme.MutedLabel);
            GUILayout.EndHorizontal();

            // Les mots avant le nombre : « vigilance 1,8 » n'apprend rien a qui ignore le
            // plafond. La phrase se lit d'un coup d'oeil, le nombre reste pour comparer.
            GUILayout.Label(
                VigilanceReading.Describe(vigilance, ceiling),
                VigilanceReading.ShouldLetItCoolDown(vigilance, ceiling) ? UITheme.Value : UITheme.MutedLabel);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Vol tech", UITheme.Button))
            {
                _espionage.TryStealTechnology(playerId, target.Id, out string error);
                LogIfFailed(error);
            }

            if (GUILayout.Button("Sabotage", UITheme.Button))
            {
                _espionage.TrySabotage(playerId, targetSystemId.Value, out string error);
                LogIfFailed(error);
            }

            if (GUILayout.Button("Revolte", UITheme.Button))
            {
                _espionage.TryInciteRevolt(playerId, targetSystemId.Value, out string error);
                LogIfFailed(error);
            }

            if (GUILayout.Button("Influence", UITheme.Button))
            {
                _espionage.TryInfluenceGovernment(playerId, target.Id, out string error);
                LogIfFailed(error);
            }

            if (GUILayout.Button("Decouvrir", UITheme.Button))
            {
                if (_espionage.TryDiscoverArmies(playerId, target.Id, targetSystemId.Value, out UnitBundle discovered, out string error))
                {
                    _lastDiscoveredArmies[target.Id] = discovered;
                }

                LogIfFailed(error);
            }

            GUILayout.EndHorizontal();

            if (_lastDiscoveredArmies.TryGetValue(target.Id, out UnitBundle garrison))
            {
                GUILayout.Label($"Derniere decouverte : {garrison}", UITheme.MutedLabel);
            }
        }

        /// <summary>
        /// Systeme de reference affiche pour le contre-espionnage d'un empire : sa capitale
        /// (Phase 18), exactement celui sur lequel <c>EspionageService</c> calcule reellement —
        /// afficher un autre systeme donnerait au joueur un chiffre qui ne correspond a rien.
        /// </summary>
        private StarSystemId? FindPrimarySystemId(int empireId)
        {
            StarSystemState capital = EmpireHoldings.Capital(empireId, _map);
            return capital?.Id;
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
