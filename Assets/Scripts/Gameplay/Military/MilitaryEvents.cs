using Espace.Core;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Military
{
    /// <summary>Publie quand une commande de recrutement se termine et rejoint la garnison du systeme.</summary>
    public readonly struct RecruitmentCompletedEvent : IGameEvent
    {
        public readonly StarSystemId SystemId;
        public readonly int OwnerId;
        public readonly UnitTypeDefinition UnitType;
        public readonly int Count;

        public RecruitmentCompletedEvent(StarSystemId systemId, int ownerId, UnitTypeDefinition unitType, int count)
        {
            SystemId = systemId;
            OwnerId = ownerId;
            UnitType = unitType;
            Count = count;
        }
    }

    /// <summary>Publie quand une flotte quitte un systeme pour un autre.</summary>
    public readonly struct FleetDepartedEvent : IGameEvent
    {
        public readonly int FleetId;
        public readonly int OwnerId;
        public readonly StarSystemId FromSystemId;
        public readonly StarSystemId ToSystemId;
        public readonly bool IsRetreating;

        public FleetDepartedEvent(int fleetId, int ownerId, StarSystemId fromSystemId, StarSystemId toSystemId, bool isRetreating)
        {
            FleetId = fleetId;
            OwnerId = ownerId;
            FromSystemId = fromSystemId;
            ToSystemId = toSystemId;
            IsRetreating = isRetreating;
        }
    }

    /// <summary>
    /// Publie quand une flotte arrivee sur un systeme non possede l'a colonise pour le compte
    /// de son proprietaire.
    /// </summary>
    public readonly struct SystemColonizedEvent : IGameEvent
    {
        public readonly StarSystemId SystemId;
        public readonly int EmpireId;

        public SystemColonizedEvent(StarSystemId systemId, int empireId)
        {
            SystemId = systemId;
            EmpireId = empireId;
        }
    }

    /// <summary>
    /// Rapport detaille publie apres chaque bataille resolue automatiquement (voir
    /// <see cref="CombatResolver"/>).
    /// </summary>
    public readonly struct BattleResolvedEvent : IGameEvent
    {
        public readonly StarSystemId SystemId;
        public readonly int AttackerEmpireId;
        public readonly int DefenderEmpireId;
        public readonly bool AttackerWon;
        public readonly float AttackerPower;
        public readonly float DefenderPower;
        public readonly UnitBundle AttackerLosses;
        public readonly UnitBundle DefenderLosses;

        public BattleResolvedEvent(
            StarSystemId systemId, int attackerEmpireId, int defenderEmpireId, bool attackerWon,
            float attackerPower, float defenderPower, UnitBundle attackerLosses, UnitBundle defenderLosses)
        {
            SystemId = systemId;
            AttackerEmpireId = attackerEmpireId;
            DefenderEmpireId = defenderEmpireId;
            AttackerWon = attackerWon;
            AttackerPower = attackerPower;
            DefenderPower = defenderPower;
            AttackerLosses = attackerLosses;
            DefenderLosses = defenderLosses;
        }
    }
}
