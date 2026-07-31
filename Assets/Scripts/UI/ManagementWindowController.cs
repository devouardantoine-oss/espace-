using System;
using System.Collections.Generic;
using Espace.Core;
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

        private static readonly ManagementTab[] AllTabs = (ManagementTab[])Enum.GetValues(typeof(ManagementTab));
        private static readonly ResearchDomain[] AllDomains = (ResearchDomain[])Enum.GetValues(typeof(ResearchDomain));

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
            if (!_visible)
            {
                return;
            }

            ResolveServices();

            var rect = new Rect((Screen.width - WindowWidth) / 2f, (Screen.height - WindowHeight) / 2f, WindowWidth, WindowHeight);
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
            string location = fleet.Status == FleetStatus.Stationed
                ? LocationLabel(fleet.CurrentSystemId)
                : $"En route vers {LocationLabel(fleet.DestinationSystemId.Value)}";

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

        private StarSystemId? FindPrimarySystemId(int empireId)
        {
            foreach (StarSystemState system in _map.Systems)
            {
                if (system.OwnerId == empireId)
                {
                    return system.Id;
                }
            }

            return null;
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
