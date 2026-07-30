using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Economy
{
    /// <summary>Publie chaque jour ou le joueur possede au moins un systeme, avec la production totale du jour.</summary>
    public readonly struct ResourceProducedEvent : IGameEvent
    {
        public readonly ResourceBundle Produced;

        public ResourceProducedEvent(ResourceBundle produced)
        {
            Produced = produced;
        }
    }

    /// <summary>Publie a chaque changement du tresor (production journaliere, depense de construction ou d'investissement).</summary>
    public readonly struct TreasuryChangedEvent : IGameEvent
    {
        public readonly ResourceBundle Treasury;

        public TreasuryChangedEvent(ResourceBundle treasury)
        {
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
