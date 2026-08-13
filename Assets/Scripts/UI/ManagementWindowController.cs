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
        Journal
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

        /// <summary>Denouements recents rappeles en bas de l'entree Flottes.</summary>
        private const int OperationReportCount = 6;

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
            }
        }

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

            foreach (Fleet fleet in fleets)
            {
                DrawFleetRow(fleet);
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
    }
}
