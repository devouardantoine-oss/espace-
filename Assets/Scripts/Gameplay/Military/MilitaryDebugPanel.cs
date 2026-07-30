using Espace.Core;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Panneau de diagnostic temporaire pour l'armee du joueur : garnison, recrutement,
    /// envoi de la garnison vers un systeme adjacent.
    /// <para>
    /// Meme statut que les autres panneaux depuis la Phase 2 : IMGUI, pas l'ecran final
    /// (Phase 11). Empile juste au-dessus de l'encart de construction economique
    /// (<c>EconomyDebugPanel</c>, ancre en bas a droite) — les deux portent sur le meme
    /// systeme selectionne, ils restent volontairement l'un a cote de l'autre a l'ecran.
    /// </para>
    /// <para>
    /// <b>Envoi de la garnison entiere, pas d'un sous-ensemble :</b> choisir combien
    /// d'unites de chaque type detacher demanderait des curseurs numeriques peu pratiques
    /// en IMGUI tactile. <see cref="IMilitaryService.TryDetachFleet"/> existe et est exerce
    /// par l'IA et les tests ; ce panneau se limite au geste le plus simple pour un outil de
    /// mise au point.
    /// </para>
    /// </summary>
    public sealed class MilitaryDebugPanel : MonoBehaviour
    {
        private const int PanelWidth = 300;
        private const int PanelHeight = 210;
        private const int Gap = 10;

        /// <summary>
        /// Hauteur de l'encart de construction d'<c>EconomyDebugPanel</c>, ancre en bas a
        /// droite : ce panneau se place juste au-dessus, avec le meme decalage horizontal.
        /// Duplique plutot que partage entre deux outils temporaires distincts.
        /// </summary>
        private const int EconomyActionsPanelHeight = 240;

        private IEventBus _eventBus;
        private IMilitaryService _military;
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
                GameLog.Warning("[MilitaryDebugPanel] IEventBus indisponible : la selection de systeme ne sera pas suivie.");
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
            if (_military == null && !ServiceLocator.TryGet(out _military))
            {
                return;
            }

            if (_map == null)
            {
                ServiceLocator.TryGet(out _map);
            }

            if (!_selectedSystemId.HasValue || _map == null)
            {
                return;
            }

            if (!_map.TryGetSystem(_selectedSystemId.Value, out StarSystemState system) || system.OwnerId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            DrawPanel(system);
        }

        private void DrawPanel(StarSystemState system)
        {
            var rect = new Rect(
                Screen.width - PanelWidth - Gap,
                Screen.height - EconomyActionsPanelHeight - Gap - PanelHeight - Gap,
                PanelWidth, PanelHeight);
            GUI.Box(rect, string.Empty);

            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, PanelWidth - 16, PanelHeight - 12));

            UnitBundle garrison = _military.GetGarrison(system.Id, EconomyService.PlayerOwnerId);
            GUILayout.Label($"Garnison : {garrison.TotalCount} unites (puissance ~{_military.EstimatePower(garrison):0})");
            GUILayout.Label(garrison.ToString());

            GUILayout.Label("Recruter :");
            GUILayout.BeginHorizontal();
            foreach (UnitTypeDefinition unitType in _military.UnitCatalog)
            {
                if (unitType == null)
                {
                    continue;
                }

                if (GUILayout.Button($"{unitType.DisplayName}\n{unitType.CreditsCost:0} Cr"))
                {
                    if (!_military.TryRecruitUnits(system.Id, unitType, 1, out string error))
                    {
                        GameLog.Warning($"[Military] {error}");
                    }
                }
            }
            GUILayout.EndHorizontal();

            if (garrison.TotalCount > 0)
            {
                GUILayout.Label("Envoyer la garnison vers :");
                GUILayout.BeginHorizontal();
                foreach (StarSystemId neighborId in _map.GetNeighbors(system.Id))
                {
                    if (!_map.TryGetSystem(neighborId, out StarSystemState neighbor))
                    {
                        continue;
                    }

                    if (GUILayout.Button(neighbor.Name) && _military.TryGetStationedFleet(system.Id, EconomyService.PlayerOwnerId, out Fleet fleet))
                    {
                        if (!_military.TryMoveFleet(fleet, neighborId, out string error))
                        {
                            GameLog.Warning($"[Military] {error}");
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }
    }
}
