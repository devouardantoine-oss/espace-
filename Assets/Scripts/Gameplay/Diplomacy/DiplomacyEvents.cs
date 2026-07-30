using Espace.Core;

namespace Espace.Gameplay.Diplomacy
{
    /// <summary>Publie quand le statut symetrique entre deux empires change (guerre, paix, alliance, pacte de non-agression).</summary>
    public readonly struct DiplomaticStatusChangedEvent : IGameEvent
    {
        public readonly int EmpireAId;
        public readonly int EmpireBId;
        public readonly DiplomaticStatus OldStatus;
        public readonly DiplomaticStatus NewStatus;

        public DiplomaticStatusChangedEvent(int empireAId, int empireBId, DiplomaticStatus oldStatus, DiplomaticStatus newStatus)
        {
            EmpireAId = empireAId;
            EmpireBId = empireBId;
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }
    }

    /// <summary>Publie quand un traite commercial entre deux empires est etabli ou rompu.</summary>
    public readonly struct TradeTreatyChangedEvent : IGameEvent
    {
        public readonly int EmpireAId;
        public readonly int EmpireBId;
        public readonly bool Active;

        public TradeTreatyChangedEvent(int empireAId, int empireBId, bool active)
        {
            EmpireAId = empireAId;
            EmpireBId = empireBId;
            Active = active;
        }
    }

    /// <summary>Publie quand un embargo dirige (de <see cref="FromEmpireId"/> vers <see cref="ToEmpireId"/>) est impose ou leve.</summary>
    public readonly struct EmbargoChangedEvent : IGameEvent
    {
        public readonly int FromEmpireId;
        public readonly int ToEmpireId;
        public readonly bool Active;

        public EmbargoChangedEvent(int fromEmpireId, int toEmpireId, bool active)
        {
            FromEmpireId = fromEmpireId;
            ToEmpireId = toEmpireId;
            Active = active;
        }
    }

    /// <summary>
    /// Publie uniquement quand une proposition cible le joueur (les propositions entre IA se
    /// resolvent instantanement, voir <see cref="DiplomacyService"/>) : c'est le signal que
    /// <c>DiplomacyDebugPanel</c> attend pour afficher un bouton accepter/refuser.
    /// </summary>
    public readonly struct ProposalReceivedEvent : IGameEvent
    {
        public readonly DiplomaticProposal Proposal;

        public ProposalReceivedEvent(DiplomaticProposal proposal)
        {
            Proposal = proposal;
        }
    }

    /// <summary>Publie quand une proposition (IA ou joueur) est acceptee ou refusee.</summary>
    public readonly struct ProposalResolvedEvent : IGameEvent
    {
        public readonly int ProposalId;
        public readonly int ProposerId;
        public readonly int TargetId;
        public readonly ProposalType Type;
        public readonly bool Accepted;

        public ProposalResolvedEvent(int proposalId, int proposerId, int targetId, ProposalType type, bool accepted)
        {
            ProposalId = proposalId;
            ProposerId = proposerId;
            TargetId = targetId;
            Type = type;
            Accepted = accepted;
        }
    }
}
