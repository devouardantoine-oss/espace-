using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Economy
{
    /// <summary>Publie chaque jour ou un empire possede au moins un systeme, avec sa production totale du jour.</summary>
    public readonly struct ResourceProducedEvent : IGameEvent
    {
        public readonly int EmpireId;
        public readonly ResourceBundle Produced;

        public ResourceProducedEvent(int empireId, ResourceBundle produced)
        {
            EmpireId = empireId;
            Produced = produced;
        }
    }

    /// <summary>Publie a chaque changement du tresor d'un empire (production journaliere, depense de construction ou d'investissement).</summary>
    public readonly struct TreasuryChangedEvent : IGameEvent
    {
        public readonly int EmpireId;
        public readonly ResourceBundle Treasury;

        public TreasuryChangedEvent(int empireId, ResourceBundle treasury)
        {
            EmpireId = empireId;
            Treasury = treasury;
        }
    }

    /// <summary>Publie quand une construction est lancee.</summary>
    public readonly struct BuildingConstructionStartedEvent : IGameEvent
    {
        public readonly StarSystemId SystemId;
        public readonly BuildingType BuildingType;

        public BuildingConstructionStartedEvent(StarSystemId systemId, BuildingType buildingType)
        {
            SystemId = systemId;
            BuildingType = buildingType;
        }
    }

    /// <summary>Publie quand une construction se termine et commence a produire.</summary>
    public readonly struct BuildingCompletedEvent : IGameEvent
    {
        public readonly StarSystemId SystemId;
        public readonly BuildingType BuildingType;

        public BuildingCompletedEvent(StarSystemId systemId, BuildingType buildingType)
        {
            SystemId = systemId;
            BuildingType = buildingType;
        }
    }
}
