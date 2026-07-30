using System.Collections.Generic;
using Espace.Data;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Diplomacy
{
    /// <summary>
    /// Relations diplomatiques entre tous les empires : statut de guerre/paix/alliance/pacte,
    /// opinion dirigee, traites commerciaux, embargos, et propositions en attente de reponse.
    /// </summary>
    public interface IDiplomacyService
    {
        /// <summary>Statut diplomatique entre deux empires (symetrique). <see cref="DiplomaticStatus.Peace"/> par defaut.</summary>
        DiplomaticStatus GetStatus(int empireAId, int empireBId);

        /// <summary>Opinion de <paramref name="observerId"/> envers <paramref name="targetId"/> (dirigee, -100 a 100, 0 par defaut).</summary>
        float GetOpinion(int observerId, int targetId);

        /// <summary>Vrai si un traite commercial actif lie ces deux empires (symetrique, independant du statut).</summary>
        bool HasTradeTreaty(int empireAId, int empireBId);

        /// <summary>Vrai si <paramref name="fromEmpireId"/> embargue <paramref name="toEmpireId"/> (dirige, independant du statut).</summary>
        bool IsEmbargoing(int fromEmpireId, int toEmpireId);

        /// <summary>Propositions en attente d'une reponse de <paramref name="empireId"/>.</summary>
        IReadOnlyList<DiplomaticProposal> GetPendingProposalsFor(int empireId);

        /// <summary>
        /// Declare la guerre de <paramref name="declarerId"/> a <paramref name="targetId"/>.
        /// Echoue si deja en guerre, ou si une Alliance/un Pacte de non-agression est en
        /// vigueur (rompez-le d'abord via <see cref="TryBreakPact"/>). Met fin a tout traite
        /// commercial actif entre les deux empires.
        /// </summary>
        bool TryDeclareWar(int declarerId, int targetId, out string error);

        /// <summary>Impose ou leve un embargo unilateral et dirige de <paramref name="fromEmpireId"/> envers <paramref name="toEmpireId"/>.</summary>
        bool TrySetEmbargo(int fromEmpireId, int toEmpireId, bool active, out string error);

        /// <summary>
        /// Rompt unilateralement l'Alliance ou le Pacte de non-agression entre ces deux
        /// empires (retour a la Paix simple), avec une penalite d'opinion pour l'initiateur.
        /// </summary>
        bool TryBreakPact(int fromEmpireId, int toEmpireId, out string error);

        /// <summary>
        /// Soumet une proposition de <paramref name="proposerId"/> vers <paramref name="targetId"/>.
        /// Si la cible est une IA, resolue instantanement (voir <see cref="ProposalEvaluator"/>) ;
        /// si c'est le joueur, mise en attente et signalee par <see cref="ProposalReceivedEvent"/>.
        /// </summary>
        bool TrySubmitProposal(
            int proposerId, int targetId, ProposalType type,
            ResourceBundle offeredResources, ResourceBundle requestedResources,
            StarSystemId? offeredSystemId, StarSystemId? requestedSystemId,
            out string error);

        /// <summary>Reponse du joueur a une proposition recue et toujours en attente.</summary>
        bool TryRespondToProposal(int proposalId, bool accept, out string error);

        /// <summary>
        /// Modifie directement l'opinion de <paramref name="observerId"/> envers <paramref name="targetId"/>
        /// (bornee comme toute opinion). Reservee aux effets qui n'entrent dans aucune des
        /// mecaniques ci-dessus : penalite d'une mission d'espionnage decouverte, propagande
        /// d'une mission d'influence de gouvernement reussie (Phase 9).
        /// </summary>
        void ApplyOpinionShift(int observerId, int targetId, float delta);
    }
}
