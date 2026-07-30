using System.Linq;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Panneau de diagnostic temporaire pour l'economie : tresor, impots, construction et
    /// investissement sur le systeme selectionne.
    /// <para>
    /// Meme statut que les panneaux des Phases 2 et 3 : outil de mise au point en IMGUI,
    /// pilotant le vrai <see cref="IEconomyService"/>, pas l'ecran final (Phase 11). Place en
    /// bas de l'ecran pour ne pas recouvrir les encarts existants (carte en haut a gauche,
    /// horloge en haut a droite).
    /// </para>
    /// <para>
    /// <b>Resolution paresseuse dans <see cref="OnGUI"/> :</b> comme pour la selection de
    /// systeme (Phase 2), <see cref="IEconomyService"/> peut n'etre enregistre qu'apres le
    /// <c>Start</c> de ce composant (voir <c>EconomyController</c>). <c>OnGUI</c> s'execute
    /// systematiquement apres tous les <c>Start</c> de la frame, ce qui evite d'avoir a fixer
    /// un ordre d'execution entre les deux composants.
    /// </para>
    /// </summary>
    public sealed class EconomyDebugPanel : MonoBehaviour
    {
        private const int TreasuryPanelWidth = 260;
        private const int TreasuryPanelHeight = 150;
        private const int ActionsPanelWidth = 300;
        private const int ActionsPanelHeight = 240;

        private IEventBus _eventBus;
        private IEconomyService _economy;
        private GalaxyMap _map;
        private StarSystemId? _selectedSystemId;

        private void OnEnable()
        {
            if (ServiceLocator.TryGet(out _eventBus))
            {
                _eventBus.Subscribe<SystemSelectedEvent>(OnSystemSelected);
                _eventBus.Subscribe<SystemDeselectedEvent>(OnSystemDeselected);
            }
            else
            {
                GameLog.Warning("[EconomyDebugPanel] IEventBus indisponible : la selection de systeme ne sera pas suivie.");
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

        private void OnSystemSelected(SystemSelectedEvent selectedEvent)
        {
            _selectedSystemId = selectedEvent.SystemId;
        }

        private void OnSystemDeselected(SystemDeselectedEvent deselectedEvent)
        {
            _selectedSystemId = null;
        }

        private void OnGUI()
        {
            if (_economy == null && !ServiceLocator.TryGet(out _economy))
            {
                return;
            }

            if (_map == null)
            {
                ServiceLocator.TryGet(out _map);
            }

            DrawTreasuryPanel();

            if (_selectedSystemId.HasValue && _map != null
                && _map.TryGetSystem(_selectedSystemId.Value, out StarSystemState system)
                && system.OwnerId == EconomyService.PlayerOwnerId)
            {
                DrawSystemActionsPanel(system);
            }
        }

        private void DrawTreasuryPanel()
        {
            var rect = new Rect(10, Screen.height - TreasuryPanelHeight - 10, TreasuryPanelWidth, TreasuryPanelHeight);
            GUI.Box(rect, string.Empty);

            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, TreasuryPanelWidth - 16, TreasuryPanelHeight - 12));

            ResourceBundleView(_economy.Treasury);

            GUILayout.Label($"Impots : {_economy.TaxRate * 100f:0}%");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("- 10%")) _economy.SetTaxRate(_economy.TaxRate - 0.1f);
            if (GUILayout.Button("+ 10%")) _economy.SetTaxRate(_economy.TaxRate + 0.1f);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private static void ResourceBundleView(ResourceBundle bundle)
        {
            GUILayout.Label("Tresor de l'empire");
            GUILayout.Label($"Credits : {bundle.Credits:0}");
            GUILayout.Label($"Minerais : {bundle.Minerals:0}");
            GUILayout.Label($"Energie : {bundle.Energy:0}");
            GUILayout.Label($"Nourriture : {bundle.Food:0}");
            GUILayout.Label($"Influence : {bundle.Influence:0}");
        }

        private void DrawSystemActionsPanel(StarSystemState system)
        {
            var rect = new Rect(Screen.width - ActionsPanelWidth - 10, Screen.height - ActionsPanelHeight - 10, ActionsPanelWidth, ActionsPanelHeight);
            GUI.Box(rect, string.Empty);

            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, ActionsPanelWidth - 16, ActionsPanelHeight - 12));

            GUILayout.Label($"{system.Name} (votre systeme)");
            GUILayout.Label($"Developpement : {system.DevelopmentLevel}/5");

            float investCost = _economy.GetInvestmentCost(system.Id);
            if (GUILayout.Button($"Investir ({investCost:0} Cr)"))
            {
                if (!_economy.TryInvestInDevelopment(system.Id, out string error))
                {
                    GameLog.Warning($"[Economy] {error}");
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("Batiments :");

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
                if (GUILayout.Button(label) && !alreadyBuilt)
                {
                    if (!_economy.TryStartConstruction(system.Id, building, out string error))
                    {
                        GameLog.Warning($"[Economy] {error}");
                    }
                }
                GUI.enabled = true;
            }

            GUILayout.EndArea();
        }
    }
}
