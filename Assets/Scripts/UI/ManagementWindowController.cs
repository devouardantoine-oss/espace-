using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Espionage;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.Research;
using Espace.Gameplay.Save;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>Un onglet de la fenetre de gestion (voir <see cref="ManagementWindowController"/>).</summary>
    public enum ManagementTab
    {
        Empires,
        Flottes,
        Operations,
        Diplomatie,
        Recherche,
        Espionnage,
        Sauvegarde
    }

    /// <summary>
    /// Fenetre unique a onglets, ouverte/fermee par le bouton « Gestion » de <see cref="HudController"/> :
    /// regroupe les cinq ecrans qui ne portent pas sur un systeme particulier (contrairement a
    /// <see cref="SystemInfoPanelController"/>) — empires, diplomatie, recherche, espionnage,
    /// sauvegarde.
    /// <para>
    /// <b>Remplace cinq panneaux de diagnostic distincts (Phases 5, 7, 8, 9, 10) :</b>
    /// <c>EmpireDebugPanel</c>, <c>DiplomacyDebugPanel</c>, <c>ResearchDebugPanel</c>,
    /// <c>EspionageDebugPanel</c>, <c>SaveDebugPanel</c>. Chacun s'affichait auparavant dans
    /// son propre encart flottant, tous simultanement a l'ecran ; les regrouper en onglets
    /// libere l'espace pour la carte et rend chaque ecran plus lisible (plus de contrainte de
    /// hauteur empilee sur les autres). Le contenu de chaque onglet est la logique exacte des
    /// panneaux d'origine, seulement redessinee avec <see cref="UITheme"/> dans une zone
    /// partagee plutot que sa propre <c>GUI.Box</c>.
    /// </para>
    /// <para>
    /// Pas d'onglet « Economie » distinct : le tresor et les impots du joueur sont deja
    /// visibles en permanence dans <see cref="HudController"/>, et les actions economiques
    /// (construire, investir) portent toujours sur un systeme precis, donc vivent dans
    /// <see cref="SystemInfoPanelController"/> — un onglet dedie n'aurait rien montre de plus.
    /// </para>
    /// </summary>
    public sealed class ManagementWindowController : MonoBehaviour
    {
        private const int WindowWidth = 660;
        private const int WindowHeight = 460;
        private const int TabStripWidth = 130;

        /// <summary>Pas du reglage fiscal, repris de la barre superieure d'ou il vient (Phase 23).</summary>
        private const float TaxStep = 0.1f;

        private static readonly ManagementTab[] AllTabs = (ManagementTab[])Enum.GetValues(typeof(ManagementTab));
        private static readonly ResearchDomain[] AllDomains = (ResearchDomain[])Enum.GetValues(typeof(ResearchDomain));

        /// <summary>
        /// Derniers denouements d'operation, du plus recent au plus ancien (Phase 20).
        /// <para>
        /// <b>Memorises ici plutot que dans un service :</b> une bataille se resout en un
        /// instant — il n'existe aucun etat « en combat » a interroger apres coup. Sans cette
        /// trace, une offensive lancee puis resolue pendant que le joueur regardait ailleurs ne
        /// laisserait aucune trace consultable. Le journal est volontairement court et non
        /// sauvegarde : c'est un fil d'actualite, pas un historique.
        /// </para>
        /// </summary>
        private readonly List<string> _operationReports = new List<string>();

        /// <summary>Au-dela, les rapports les plus anciens sont oublies.</summary>
        private const int MaxOperationReports = 6;

        private IEventBus _operationEventBus;

        private bool _visible;
        private ManagementTab _activeTab = ManagementTab.Empires;
        private Vector2 _contentScroll;

        private EmpireRegistry _empireRegistry;
        private IEconomyService _economy;
        private GalaxyMap _map;
        private IDiplomacyService _diplomacy;
        private IResearchService _research;
        private IEspionageService _espionage;
        private ISaveService _save;
        private IMilitaryService _military;

        private string _lastSaveResult = string.Empty;
        private readonly Dictionary<int, UnitBundle> _lastDiscoveredArmies = new Dictionary<int, UnitBundle>();

        /// <summary>Ouvre ou ferme la fenetre. Appele par <see cref="HudController"/>.</summary>
        public void ToggleVisible() => _visible = !_visible;

        private void OnGUI()
        {
            UITheme.BeginScaledLayout();
            try
            {
                if (!_visible)
                {
                    return;
                }

                ResolveServices();

                var rect = new Rect((UITheme.ScreenWidth - WindowWidth) / 2f, (UITheme.ScreenHeight - WindowHeight) / 2f, WindowWidth, WindowHeight);
                UiScreenRegions.Occupy(rect, UITheme.Scale);
                GUI.Box(rect, string.Empty, UITheme.Panel);

                GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, WindowWidth - 16, WindowHeight - 12));
                GUILayout.BeginHorizontal();

                DrawTabStrip();

                GUILayout.BeginVertical();
                _contentScroll = GUILayout.BeginScrollView(_contentScroll);
                DrawActiveTabContent();
                GUILayout.EndScrollView();
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        private void ResolveServices()
        {
            if (_empireRegistry == null) ServiceLocator.TryGet(out _empireRegistry);
            if (_economy == null) ServiceLocator.TryGet(out _economy);
            if (_map == null) ServiceLocator.TryGet(out _map);
            if (_diplomacy == null) ServiceLocator.TryGet(out _diplomacy);
            if (_research == null) ServiceLocator.TryGet(out _research);
            if (_espionage == null) ServiceLocator.TryGet(out _espionage);
            if (_save == null) ServiceLocator.TryGet(out _save);
            if (_military == null) ServiceLocator.TryGet(out _military);
        }

        private void OnEnable()
        {
            if (ServiceLocator.TryGet(out _operationEventBus))
            {
                _operationEventBus.Subscribe<BattleResolvedEvent>(OnBattleResolved);
                _operationEventBus.Subscribe<SystemColonizedEvent>(OnSystemColonized);
            }
        }

        private void OnDisable()
        {
            if (_operationEventBus == null)
            {
                return;
            }

            _operationEventBus.Unsubscribe<BattleResolvedEvent>(OnBattleResolved);
            _operationEventBus.Unsubscribe<SystemColonizedEvent>(OnSystemColonized);
            _operationEventBus = null;
        }

        private void OnBattleResolved(BattleResolvedEvent battle)
        {
            if (battle.AttackerEmpireId != EconomyService.PlayerOwnerId
                && battle.DefenderEmpireId != EconomyService.PlayerOwnerId)
            {
                // Les batailles entre tiers ne sont pas des operations du joueur : les lister
                // reviendrait a lui offrir un renseignement qu'il n'a pas paye.
                return;
            }

            bool attacking = battle.AttackerEmpireId == EconomyService.PlayerOwnerId;
            string verdict = battle.AttackerWon
                ? attacking ? "victoire" : "systeme perdu"
                : attacking ? "offensive repoussee" : "assaut repousse";

            PushReport($"{LocationLabel(battle.SystemId)} — {verdict} (pertes {(attacking ? battle.AttackerLosses : battle.DefenderLosses)})");
        }

        private void OnSystemColonized(SystemColonizedEvent colonized)
        {
            if (colonized.EmpireId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            PushReport($"{LocationLabel(colonized.SystemId)} — colonise ({colonized.InfantryLost} Infanterie perdue)");
        }

        private void PushReport(string report)
        {
            _operationReports.Insert(0, report);
            if (_operationReports.Count > MaxOperationReports)
            {
                _operationReports.RemoveAt(_operationReports.Count - 1);
            }
        }

        /// <summary>
        /// Suivi des operations en cours (Phase 20) : uniquement ce qui bouge, avec son etat.
        /// L'onglet Flottes reste l'inventaire complet ; celui-ci est le tableau de bord.
        /// </summary>
        private void DrawOperationsTab()
        {
            GUILayout.Label("Operations", UITheme.Title);

            if (_military == null || _map == null)
            {
                GUILayout.Label("Service militaire indisponible.", UITheme.MutedLabel);
                return;
            }

            var campaigning = new List<Fleet>();
            foreach (Fleet fleet in _military.GetFleetsForEmpire(EconomyService.PlayerOwnerId))
            {
                if (fleet.Status != FleetStatus.Stationed)
                {
                    campaigning.Add(fleet);
                }
            }

            GUILayout.Label(
                _military.CanDeployAnotherFleet(EconomyService.PlayerOwnerId)
                    ? $"{campaigning.Count} flotte(s) en campagne — une place reste libre"
                    : $"{campaigning.Count} flotte(s) en campagne — plafond atteint, recherchez la Logistique",
                UITheme.MutedLabel);

            GUILayout.Space(6);

            if (campaigning.Count == 0)
            {
                GUILayout.Label("Aucune operation en cours.", UITheme.MutedLabel);
            }

            foreach (Fleet fleet in campaigning)
            {
                DrawOperationRow(fleet);
            }

            GUILayout.Space(10);
            GUILayout.Label("Rapports recents", UITheme.Title);

            if (_operationReports.Count == 0)
            {
                GUILayout.Label("Aucun denouement depuis l'ouverture de la partie.", UITheme.MutedLabel);
                return;
            }

            foreach (string report in _operationReports)
            {
                GUILayout.Label(report, UITheme.Label);
            }
        }

        private void DrawOperationRow(Fleet fleet)
        {
            string destination = fleet.DestinationSystemId.HasValue
                ? LocationLabel(fleet.DestinationSystemId.Value)
                : "destination inconnue";

            string state = fleet.Status == FleetStatus.AwaitingEncounter
                ? "RENCONTRE"
                : "EN ROUTE";

            int remainingHops = fleet.Route != null ? fleet.Route.Count - 1 - fleet.RouteIndex : 0;

            GUILayout.Label($"[{state}]  {fleet.Name}  ->  {destination}", UITheme.Label);
            GUILayout.Label(
                $"{fleet.Composition.TotalCount} unites · {remainingHops} saut(s) restant(s)"
                + (fleet.ArrivalDate.HasValue ? $" · etape le {fleet.ArrivalDate.Value}" : string.Empty)
                + (fleet.IsRetreating ? " · repli" : string.Empty),
                UITheme.MutedLabel);
            GUILayout.Space(4);
        }

        private void DrawTabStrip()
        {
            GUILayout.BeginVertical(GUILayout.Width(TabStripWidth));

            foreach (ManagementTab tab in AllTabs)
            {
                GUIStyle style = tab == _activeTab ? UITheme.ActiveTabButton : UITheme.TabButton;
                if (GUILayout.Button(tab.ToString(), style, GUILayout.Height(32)))
                {
                    _activeTab = tab;
                    _contentScroll = Vector2.zero;
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Fermer", UITheme.Button, GUILayout.Height(28)))
            {
                _visible = false;
            }

            GUILayout.EndVertical();
        }

        private void DrawActiveTabContent()
        {
            switch (_activeTab)
            {
                case ManagementTab.Empires:
                    DrawEmpiresTab();
                    break;
                case ManagementTab.Flottes:
                    DrawFlottesTab();
                    break;
                case ManagementTab.Operations:
                    DrawOperationsTab();
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
                case ManagementTab.Sauvegarde:
                    DrawSauvegardeTab();
                    break;
            }
        }

        // --- Empires ---------------------------------------------------------------------

        private void DrawEmpiresTab()
        {
            DrawPlayerEconomy();

            GUILayout.Label("Empires", UITheme.Title);

            if (_empireRegistry == null)
            {
                GUILayout.Label("Registre des empires indisponible.", UITheme.MutedLabel);
                return;
            }

            foreach (Empire empire in _empireRegistry.Empires)
            {
                int systemCount = CountOwnedSystems(empire.Id);
                float credits = _economy?.GetTreasury(empire.Id).Credits ?? 0f;
                string role = empire.IsPlayerControlled ? "Vous" : empire.Personality.ToString();

                GUILayout.Label($"{empire.Name}  —  {role}  —  {systemCount} systeme(s)  —  {HudFormatter.FormatResource(credits)} Cr", UITheme.Label);
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

            foreach (Fleet fleet in fleets)
            {
                DrawFleetRow(fleet);
            }
        }

        private void DrawFleetRow(Fleet fleet)
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

            GUILayout.Label($"{fleet.Name}  —  {location}  —  puissance ~{_military.EstimatePower(fleet.Composition):0}", UITheme.Label);
            GUILayout.Label(fleet.Composition.ToString(), UITheme.MutedLabel);
            GUILayout.Label(
                $"Amiral {fleet.Admiral.Name} — Attaque {HudFormatter.FormatSigned(fleet.Admiral.AttackBonus * 100f)}% "
                + $"/ Vitesse {HudFormatter.FormatSigned(fleet.Admiral.SpeedBonus * 100f)}% "
                + $"/ Defense {HudFormatter.FormatSigned(fleet.Admiral.DefenseBonus * 100f)}%",
                UITheme.MutedLabel);
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
            GUILayout.Label($"{target.Name} (contre-espionnage {counterPower:0})", UITheme.Label);

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

        // --- Sauvegarde -----------------------------------------------------------------

        private void DrawSauvegardeTab()
        {
            GUILayout.Label("Sauvegarde", UITheme.Title);

            if (_save == null)
            {
                GUILayout.Label("Service de sauvegarde indisponible.", UITheme.MutedLabel);
                return;
            }

            GUILayout.Label(_save.SaveFileExists ? "Sauvegarde : presente" : "Sauvegarde : aucune", UITheme.Label);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sauvegarder maintenant", UITheme.Button))
            {
                _save.SaveNow();
                _lastSaveResult = "Sauvegarde ecrite.";
            }

            if (GUILayout.Button("Recharger", UITheme.Button))
            {
                _lastSaveResult = _save.TryLoadAndApply(out string error) ? "Sauvegarde rechargee." : $"Echec : {error}";
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_lastSaveResult))
            {
                GUILayout.Label(_lastSaveResult, UITheme.MutedLabel);
            }
        }
    }
}
