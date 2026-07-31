using System.Collections.Generic;
using System.Linq;
using Espace.Core;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Panneau du systeme selectionne, en bas a gauche de l'ecran : fiche d'identite (nom,
    /// population, richesse, developpement, stabilite, proprietaire, gisements, routes,
    /// garnisons de tous les empires) et, si le systeme appartient au joueur, les actions qui
    /// s'y rapportent (investir, construire, recruter, deplacer la garnison).
    /// <para>
    /// <b>Fusionne trois panneaux de diagnostic distincts des Phases 2, 4 et 6 :</b> l'ancien
    /// encart de selection d'<c>GalaxyMapController</c>, l'encart d'actions
    /// d'<c>EconomyDebugPanel</c> et la totalite de <c>MilitaryDebugPanel</c> portaient tous
    /// sur le meme systeme selectionne et s'empilaient a l'ecran independamment. Un seul
    /// panneau contextuel est plus lisible et evite de recalculer trois fois la meme position
    /// d'ancrage.
    /// </para>
    /// </summary>
    public sealed class SystemInfoPanelController : MonoBehaviour
    {
        private const int PanelWidth = 360;
        private const int PanelHeight = 420;
        private const int Gap = 10;

        /// <summary>Meme borne que <c>EconomyService.MaxDevelopmentLevel</c> (non exposee sur l'interface) : desactive le bouton Investir au lieu de laisser <c>TryInvestInDevelopment</c> echouer silencieusement.</summary>
        private const int MaxDevelopmentLevel = 5;

        private IEventBus _eventBus;
        private GalaxyMap _map;
        private EmpireRegistry _empireRegistry;
        private IEconomyService _economy;
        private IMilitaryService _military;
        private StarSystemId? _selectedSystemId;
        private Vector2 _scroll;

        private void OnEnable()
        {
            if (ServiceLocator.TryGet(out _eventBus))
            {
                _eventBus.Subscribe<SystemSelectedEvent>(OnSystemSelected);
                _eventBus.Subscribe<SystemDeselectedEvent>(OnSystemDeselected);
            }
        }

        private void OnDisable()
        {
            if (_eventBus == null)
            {
                return;
            }

            _eventBus.Unsubscribe<SystemSelectedEvent>(OnSystemSelected);
            _eventBus.Unsubscribe<SystemDeselectedEvent>(OnSystemDeselected);
            _eventBus = null;
        }

        private void OnSystemSelected(SystemSelectedEvent selectedEvent) => _selectedSystemId = selectedEvent.SystemId;

        private void OnSystemDeselected(SystemDeselectedEvent deselectedEvent) => _selectedSystemId = null;

        private void OnGUI()
        {
            if (!_selectedSystemId.HasValue)
            {
                return;
            }

            if (_map == null && !ServiceLocator.TryGet(out _map))
            {
                return;
            }

            if (!_map.TryGetSystem(_selectedSystemId.Value, out StarSystemState system))
            {
                return;
            }

            if (_empireRegistry == null) ServiceLocator.TryGet(out _empireRegistry);
            if (_economy == null) ServiceLocator.TryGet(out _economy);
            if (_military == null) ServiceLocator.TryGet(out _military);

            var rect = new Rect(Gap, Screen.height - PanelHeight - Gap, PanelWidth, PanelHeight);
            GUI.Box(rect, string.Empty, UITheme.Panel);

            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, PanelWidth - 16, PanelHeight - 12));
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawIdentity(system);

            bool ownedByPlayer = system.OwnerId == EconomyService.PlayerOwnerId;
            if (ownedByPlayer && _economy != null)
            {
                GUILayout.Space(6);
                DrawEconomyActions(system);
            }

            if (ownedByPlayer && _military != null)
            {
                GUILayout.Space(6);
                DrawMilitaryActions(system);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawIdentity(StarSystemState system)
        {
            GUILayout.Label(system.Name, UITheme.Title);
            GUILayout.Label($"Population : {system.Population} M   |   Richesse : {system.Wealth}/100", UITheme.Label);
            GUILayout.Label($"Developpement : {system.DevelopmentLevel}/{MaxDevelopmentLevel}   |   Stabilite : {HudFormatter.FormatPercent(system.Stability)}", UITheme.Label);
            GUILayout.Label($"Proprietaire : {OwnerLabel(system.OwnerId)}", UITheme.Label);
            GUILayout.Label($"Gisements : {(system.ResourceDeposits.Length == 0 ? "aucun" : string.Join(", ", system.ResourceDeposits))}", UITheme.MutedLabel);
            GUILayout.Label($"Routes hyperspatiales : {_map.GetNeighbors(system.Id).Count}", UITheme.MutedLabel);
            GUILayout.Label($"Garnisons : {GarrisonSummaryLabel(system)}", UITheme.MutedLabel);
        }

        private void DrawEconomyActions(StarSystemState system)
        {
            GUILayout.Label("Economie", UITheme.Title);

            bool atMaxDevelopment = system.DevelopmentLevel >= MaxDevelopmentLevel;
            string investLabel = atMaxDevelopment
                ? "Developpement maximal atteint"
                : $"Investir dans le developpement ({_economy.GetInvestmentCost(system.Id):0} Cr)";

            GUI.enabled = !atMaxDevelopment;
            if (GUILayout.Button(investLabel, UITheme.Button) && !atMaxDevelopment)
            {
                if (!_economy.TryInvestInDevelopment(system.Id, out string error))
                {
                    GameLog.Warning($"[Economy] {error}");
                }
            }
            GUI.enabled = true;

            foreach (BuildingType building in _economy.BuildingCatalog)
            {
                if (building == null)
                {
                    continue;
                }

                bool alreadyBuilt = _economy.GetBuildings(system.Id).Any(b => b.Type == building);
                string label = alreadyBuilt
                    ? $"{building.DisplayName} (construit)"
                    : $"{building.DisplayName} ({building.CreditsCost:0} Cr)";

                GUI.enabled = !alreadyBuilt;
                if (GUILayout.Button(label, UITheme.Button) && !alreadyBuilt)
                {
                    if (!_economy.TryStartConstruction(system.Id, building, out string error))
                    {
                        GameLog.Warning($"[Economy] {error}");
                    }
                }

                GUI.enabled = true;
            }
        }

        private void DrawMilitaryActions(StarSystemState system)
        {
            GUILayout.Label("Armee", UITheme.Title);

            UnitBundle garrison = _military.GetGarrison(system.Id, EconomyService.PlayerOwnerId);
            GUILayout.Label($"Garnison : {garrison.TotalCount} unites (puissance ~{_military.EstimatePower(garrison):0})", UITheme.Label);
            GUILayout.Label(garrison.ToString(), UITheme.MutedLabel);

            if (_military.TryGetStationedFleet(system.Id, EconomyService.PlayerOwnerId, out Fleet garrisonFleet))
            {
                GUILayout.Label(
                    $"Amiral {garrisonFleet.Admiral.Name} — Attaque {HudFormatter.FormatSigned(garrisonFleet.Admiral.AttackBonus * 100f)}% "
                    + $"/ Vitesse {HudFormatter.FormatSigned(garrisonFleet.Admiral.SpeedBonus * 100f)}% "
                    + $"/ Defense {HudFormatter.FormatSigned(garrisonFleet.Admiral.DefenseBonus * 100f)}%",
                    UITheme.MutedLabel);
            }

            GUILayout.Label("Recruter :", UITheme.MutedLabel);
            GUILayout.BeginHorizontal();
            foreach (UnitTypeDefinition unitType in _military.UnitCatalog)
            {
                if (unitType == null)
                {
                    continue;
                }

                GUILayout.BeginVertical();
                if (GUILayout.Button($"{unitType.DisplayName}\n{unitType.CreditsCost:0} Cr", UITheme.Button))
                {
                    if (!_military.TryRecruitUnits(system.Id, unitType, 1, out string error))
                    {
                        GameLog.Warning($"[Military] {error}");
                    }
                }

                if (!string.IsNullOrEmpty(unitType.RoleDescription))
                {
                    GUILayout.Label(unitType.RoleDescription, UITheme.MutedLabel, GUILayout.Width(90));
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            if (garrison.TotalCount > 0)
            {
                GUILayout.Label("Envoyer la garnison vers :", UITheme.MutedLabel);
                GUILayout.BeginHorizontal();
                foreach (StarSystemId neighborId in _map.GetNeighbors(system.Id))
                {
                    if (!_map.TryGetSystem(neighborId, out StarSystemState neighbor))
                    {
                        continue;
                    }

                    if (GUILayout.Button(neighbor.Name, UITheme.Button) && _military.TryGetStationedFleet(system.Id, EconomyService.PlayerOwnerId, out Fleet fleet))
                    {
                        if (!_military.TryMoveFleet(fleet, neighborId, out string error))
                        {
                            GameLog.Warning($"[Military] {error}");
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        private string OwnerLabel(int ownerId)
        {
            if (ownerId == StarSystemState.UnownedOwnerId)
            {
                return "Independant";
            }

            if (_empireRegistry != null && _empireRegistry.TryGetEmpire(ownerId, out Empire empire))
            {
                return empire.Name;
            }

            return ownerId.ToString();
        }

        private string GarrisonSummaryLabel(StarSystemState system)
        {
            if (_military == null)
            {
                return "inconnue";
            }

            IReadOnlyList<Fleet> fleets = _military.GetFleetsAt(system.Id);
            if (fleets.Count == 0)
            {
                return "aucune";
            }

            var parts = new List<string>(fleets.Count);
            foreach (Fleet fleet in fleets)
            {
                parts.Add($"{OwnerLabel(fleet.OwnerId)} : {fleet.Composition.TotalCount}");
            }

            return string.Join(" | ", parts);
        }
    }
}
