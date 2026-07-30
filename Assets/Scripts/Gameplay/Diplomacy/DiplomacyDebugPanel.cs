using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using UnityEngine;

namespace Espace.Gameplay.Diplomacy
{
    /// <summary>
    /// Panneau de diagnostic temporaire pour la diplomatie du joueur : statut et opinion
    /// envers chaque empire IA, actions (guerre, pacte, alliance, paix, rupture), et
    /// propositions recues en attente de reponse.
    /// <para>
    /// Meme statut IMGUI temporaire que les autres panneaux depuis la Phase 2 ; ancre en
    /// haut a droite, seul emplacement encore libre (haut-gauche : selection de systeme ;
    /// haut-centre : liste des empires ; bas : economie et armee).
    /// </para>
    /// <para>
    /// <b>Echange de ressources, de territoires et ultimatums non exposes ici :</b> ces
    /// propositions ont une charge utile numerique (montants precis, systeme choisi) qui
    /// demanderait des curseurs peu pratiques en IMGUI tactile — meme limitation deja
    /// acceptee par <c>MilitaryDebugPanel</c> pour le detachement partiel de garnison. Elles
    /// restent entierement implementees et testees au niveau du service, simplement non
    /// cablees a un bouton dans cet outil de mise au point.
    /// </para>
    /// </summary>
    public sealed class DiplomacyDebugPanel : MonoBehaviour
    {
        private const int PanelWidth = 320;
        private const int PanelHeight = 320;
        private const int Gap = 10;

        private IDiplomacyService _diplomacy;
        private EmpireRegistry _empireRegistry;

        private void OnGUI()
        {
            if (_diplomacy == null && !ServiceLocator.TryGet(out _diplomacy))
            {
                return;
            }

            if (_empireRegistry == null && !ServiceLocator.TryGet(out _empireRegistry))
            {
                return;
            }

            var rect = new Rect(Screen.width - PanelWidth - Gap, Gap, PanelWidth, PanelHeight);
            GUI.Box(rect, string.Empty);

            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, PanelWidth - 16, PanelHeight - 12));

            GUILayout.Label("Diplomatie");

            foreach (Empire empire in _empireRegistry.Empires)
            {
                if (empire.IsPlayerControlled)
                {
                    continue;
                }

                DrawRelationRow(empire);
            }

            DrawPendingProposals();

            GUILayout.EndArea();
        }

        private void DrawRelationRow(Empire empire)
        {
            int playerId = EconomyService.PlayerOwnerId;
            DiplomaticStatus status = _diplomacy.GetStatus(playerId, empire.Id);
            float opinion = _diplomacy.GetOpinion(empire.Id, playerId);

            GUILayout.Label($"{empire.Name} : {StatusLabel(status)} (opinion {opinion:0})");

            GUILayout.BeginHorizontal();
            switch (status)
            {
                case DiplomaticStatus.Peace:
                    if (GUILayout.Button("Guerre"))
                    {
                        _diplomacy.TryDeclareWar(playerId, empire.Id, out _);
                    }

                    if (GUILayout.Button("Pacte"))
                    {
                        _diplomacy.TrySubmitProposal(playerId, empire.Id, ProposalType.NonAggressionPact, default, default, null, null, out _);
                    }

                    if (GUILayout.Button("Alliance"))
                    {
                        _diplomacy.TrySubmitProposal(playerId, empire.Id, ProposalType.Alliance, default, default, null, null, out _);
                    }

                    break;

                case DiplomaticStatus.War:
                    if (GUILayout.Button("Paix"))
                    {
                        _diplomacy.TrySubmitProposal(playerId, empire.Id, ProposalType.PeaceTreaty, default, default, null, null, out _);
                    }

                    break;

                case DiplomaticStatus.Alliance:
                case DiplomaticStatus.NonAggressionPact:
                    if (GUILayout.Button("Rompre"))
                    {
                        _diplomacy.TryBreakPact(playerId, empire.Id, out _);
                    }

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

            GUILayout.Label("Propositions recues :");
            foreach (DiplomaticProposal proposal in pending)
            {
                string proposerName = _empireRegistry.TryGetEmpire(proposal.ProposerId, out Empire proposer) ? proposer.Name : proposal.ProposerId.ToString();
                GUILayout.Label($"{proposerName} : {proposal.Type}");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Accepter"))
                {
                    _diplomacy.TryRespondToProposal(proposal.Id, true, out _);
                }

                if (GUILayout.Button("Refuser"))
                {
                    _diplomacy.TryRespondToProposal(proposal.Id, false, out _);
                }

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
    }
}
