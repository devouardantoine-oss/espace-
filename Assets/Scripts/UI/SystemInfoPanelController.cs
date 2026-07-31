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

        /// <summary>
        /// Systeme de depart de la flotte a deplacer quand le mode de ciblage est arme
        /// (Phase 17). <b>Stocke a part de <see cref="_selectedSystemId"/></b> : au clic sur la
        /// destination, la selection bascule sur ce nouveau systeme (souvent etranger, donc le
        /// panneau n'afficherait meme plus la section Armee) — l'origine doit survivre a ce
        /// basculement.
        /// </summary>
        private StarSystemId? _moveOriginSystemId;

        private string _moveFeedback;

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

        /// <summary>
        /// Le mode de ciblage est consomme <b>avant</b> de reecrire la selection : c'est le clic
        /// sur la destination qui declenche l'ordre, et l'origine ne doit pas avoir ete perdue
        /// entre-temps.
        /// </summary>
        private void OnSystemSelected(SystemSelectedEvent selectedEvent)
        {
            if (_moveOriginSystemId.HasValue)
            {
                StarSystemId origin = _moveOriginSystemId.Value;
                _moveOriginSystemId = null;
                ExecuteMoveOrder(origin, selectedEvent.SystemId);
            }

            _selectedSystemId = selectedEvent.SystemId;
        }

        private void OnSystemDeselected(SystemDeselectedEvent deselectedEvent)
        {
            // Un clic dans le vide annule un ordre de deplacement en cours plutot que de le
            // laisser arme sans que rien ne le rappelle a l'ecran.
            _moveOriginSystemId = null;
            _selectedSystemId = null;
        }

        /// <summary>Envoie la garnison de <paramref name="originId"/> vers <paramref name="destinationId"/>, en revalidant que la flotte existe toujours.</summary>
        private void ExecuteMoveOrder(StarSystemId originId, StarSystemId destinationId)
        {
            if (originId.Equals(destinationId))
            {
                _moveFeedback = "Deplacement annule : la flotte est deja sur ce systeme.";
                return;
            }

            if (_military == null && !ServiceLocator.TryGet(out _military))
            {
                return;
            }

            // La flotte a pu partir, fusionner ou etre detruite entre l'armement du mode et le
            // clic sur la destination.
            if (!_military.TryGetStationedFleet(originId, EconomyService.PlayerOwnerId, out Fleet fleet)
                || fleet.Status != FleetStatus.Stationed)
            {
                _moveFeedback = "Deplacement annule : plus aucune flotte disponible sur le systeme de depart.";
                return;
            }

            if (_military.TryMoveFleet(fleet, destinationId, out string error))
            {
                string destinationName = _map != null && _map.TryGetSystem(destinationId, out StarSystemState destination)
                    ? destination.Name
                    : destinationId.ToString();
                _moveFeedback = $"{fleet.Name} fait route vers {destinationName}.";
                GameLog.Info($"[Military] {_moveFeedback}");
                return;
            }

            _moveFeedback = error;
            GameLog.Warning($"[Military] {error}");
        }

        private void OnGUI()
        {
            UITheme.BeginScaledLayout();
            try
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

                var rect = new Rect(Gap, UITheme.ScreenHeight - PanelHeight - Gap, PanelWidth, PanelHeight);
                GUI.Box(rect, string.Empty, UITheme.Panel);

                GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, PanelWidth - 16, PanelHeight - 12));
                _scroll = GUILayout.BeginScrollView(_scroll);

                DrawIdentity(system);

                if (system.OwnerId == StarSystemState.UnownedOwnerId)
                {
                    GUILayout.Space(6);
                    DrawColonizationInfo(system);
                }

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
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        /// <summary>
        /// Cout de colonisation d'un systeme libre (Phase 16). Appelle directement les regles
        /// pures <see cref="ColonizationRules"/> plutot que de passer par une methode de
        /// service : <c>Espace.UI</c> reference deja <c>Espace.Gameplay</c>, et il n'y a aucun
        /// etat a interroger.
        /// </summary>
        private void DrawColonizationInfo(StarSystemState system)
        {
            int required = ColonizationRules.RequiredInfantry(system);
            int lost = ColonizationRules.InfantryLost(system);

            GUILayout.Label($"Colonisation : {required} Infanterie requise", UITheme.Label);
            GUILayout.Label($"dont {lost} perdue(s) a l'installation", UITheme.MutedLabel);
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
                GUILayout.Space(4);

                if (_moveOriginSystemId.HasValue)
                {
                    GUILayout.Label("Cliquez le systeme de destination sur la carte.", UITheme.Label);
                    if (GUILayout.Button("Annuler le deplacement", UITheme.Button, GUILayout.Height(28)))
                    {
                        _moveOriginSystemId = null;
                    }
                }
                else if (GUILayout.Button("Deplacer une flotte", UITheme.Button, GUILayout.Height(32)))
                {
                    // Le mode reste arme jusqu'au prochain clic sur la carte : c'est ce clic qui
                    // designe la destination (Phase 17, plus besoin d'adjacence).
                    _moveOriginSystemId = system.Id;
                    _moveFeedback = null;
                }
            }

            if (!string.IsNullOrEmpty(_moveFeedback))
            {
                GUILayout.Label(_moveFeedback, UITheme.MutedLabel);
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
