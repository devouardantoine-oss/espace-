using Espace.Core;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.Research;

namespace Espace.Gameplay.Espionage
{
    /// <summary>Publie quand un vol de technologie reussit.</summary>
    public readonly struct TechnologyStolenEvent : IGameEvent
    {
        public readonly int ProposerId;
        public readonly int TargetEmpireId;
        public readonly ResearchDomain Domain;

        public TechnologyStolenEvent(int proposerId, int targetEmpireId, ResearchDomain domain)
        {
            ProposerId = proposerId;
            TargetEmpireId = targetEmpireId;
            Domain = domain;
        }
    }

    /// <summary>Publie quand un sabotage reussit.</summary>
    public readonly struct SystemSabotagedEvent : IGameEvent
    {
        public readonly int ProposerId;
        public readonly int TargetEmpireId;
        public readonly StarSystemId SystemId;

        public SystemSabotagedEvent(int proposerId, int targetEmpireId, StarSystemId systemId)
        {
            ProposerId = proposerId;
            TargetEmpireId = targetEmpireId;
            SystemId = systemId;
        }
    }

    /// <summary>Publie quand une incitation a la revolte reussit.</summary>
    public readonly struct RevoltIncitedEvent : IGameEvent
    {
        public readonly int ProposerId;
        public readonly int TargetEmpireId;
        public readonly StarSystemId SystemId;

        public RevoltIncitedEvent(int proposerId, int targetEmpireId, StarSystemId systemId)
        {
            ProposerId = proposerId;
            TargetEmpireId = targetEmpireId;
            SystemId = systemId;
        }
    }

    /// <summary>Publie quand une influence de gouvernement reussit.</summary>
    public readonly struct GovernmentInfluencedEvent : IGameEvent
    {
        public readonly int ProposerId;
        public readonly int TargetEmpireId;

        public GovernmentInfluencedEvent(int proposerId, int targetEmpireId)
        {
            ProposerId = proposerId;
            TargetEmpireId = targetEmpireId;
        }
    }

    /// <summary>Publie quand une decouverte d'armees reussit, avec la garnison reellement decouverte.</summary>
    public readonly struct ArmiesDiscoveredEvent : IGameEvent
    {
        public readonly int ProposerId;
        public readonly int TargetEmpireId;
        public readonly StarSystemId SystemId;
        public readonly UnitBundle DiscoveredGarrison;

        public ArmiesDiscoveredEvent(int proposerId, int targetEmpireId, StarSystemId systemId, UnitBundle discoveredGarrison)
        {
            ProposerId = proposerId;
            TargetEmpireId = targetEmpireId;
            SystemId = systemId;
            DiscoveredGarrison = discoveredGarrison;
        }
    }

    /// <summary>
    /// Publie quand une mission d'espionnage, quel que soit son type, echoue (contre-espionnage
    /// de la cible superieur ou egal a la puissance du proposeur) — la cible decouvre toujours
    /// une mission ratee, jamais une reussie.
    /// </summary>
    public readonly struct MissionFailedEvent : IGameEvent
    {
        public readonly int ProposerId;
        public readonly int TargetEmpireId;
        public readonly EspionageMissionType Type;

        public MissionFailedEvent(int proposerId, int targetEmpireId, EspionageMissionType type)
        {
            ProposerId = proposerId;
            TargetEmpireId = targetEmpireId;
            Type = type;
        }
    }
}
