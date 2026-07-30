using System;
using Espace.Core;
using Espace.Gameplay.Economy;
using UnityEngine;

namespace Espace.Gameplay.Research
{
    /// <summary>
    /// Panneau de diagnostic temporaire pour la recherche du joueur : domaine actif,
    /// progression vers le prochain palier, bonus cumulatif par domaine, et un bouton par
    /// domaine pour y rediriger le focus.
    /// <para>
    /// Meme statut IMGUI temporaire que les autres panneaux depuis la Phase 2 ; empile en bas
    /// a gauche, juste au-dessus de l'encart du tresor (<c>EconomyDebugPanel</c>).
    /// </para>
    /// </summary>
    public sealed class ResearchDebugPanel : MonoBehaviour
    {
        private const int PanelWidth = 280;
        private const int PanelHeight = 260;
        private const int Gap = 10;

        /// <summary>Hauteur de l'encart du tresor d'<c>EconomyDebugPanel</c>, ancre en bas a gauche : ce panneau se place juste au-dessus.</summary>
        private const int TreasuryPanelHeight = 150;

        private static readonly ResearchDomain[] AllDomains = (ResearchDomain[])Enum.GetValues(typeof(ResearchDomain));

        private IResearchService _research;

        private void OnGUI()
        {
            if (_research == null && !ServiceLocator.TryGet(out _research))
            {
                return;
            }

            var rect = new Rect(Gap, Screen.height - TreasuryPanelHeight - Gap - PanelHeight - Gap, PanelWidth, PanelHeight);
            GUI.Box(rect, string.Empty);

            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, PanelWidth - 16, PanelHeight - 12));

            GUILayout.Label("Recherche");

            int playerId = EconomyService.PlayerOwnerId;
            ResearchDomain? active = _research.GetActiveDomain(playerId);
            GUILayout.Label(active == null ? "Aucun domaine actif." : $"Focus : {DomainLabel(active.Value)}");

            foreach (ResearchDomain domain in AllDomains)
            {
                DrawDomainRow(playerId, domain, active);
            }

            GUILayout.EndArea();
        }

        private void DrawDomainRow(int playerId, ResearchDomain domain, ResearchDomain? active)
        {
            int completedTiers = _research.GetCompletedTierCount(playerId, domain);
            float bonus = _research.GetBonus(playerId, domain);
            TechnologyDefinition next = _research.GetNextTechnology(playerId, domain);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{DomainLabel(domain)} : palier {completedTiers} (+{bonus * 100f:0}%)", GUILayout.Width(190));

            if (domain == active)
            {
                GUILayout.Label("Actif");
            }
            else if (next == null)
            {
                GUILayout.Label("Max");
            }
            else if (GUILayout.Button("Activer"))
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
    }
}
