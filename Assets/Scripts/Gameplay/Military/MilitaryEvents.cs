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

        /// <summary>Unites d'Infanterie consommees par l'installation (Phase 16, voir <see cref="ColonizationRules.InfantryLost"/>).</summary>
        public readonly int InfantryLost;

        public SystemColonizedEvent(StarSystemId systemId, int empireId, int infantryLost)
        {
            SystemId = systemId;
            EmpireId = empireId;
            InfantryLost = infantryLost;
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

    /// <summary>
    /// Publie quand deux flottes d'empires differents se croisent sur un meme tronçon et qu'une
    /// decision est attendue (Phase 17). Les rencontres entre IA sont resolues dans la foulee ;
    /// celle qui implique le joueur attend son choix.
    /// </summary>
    public readonly struct EncounterStartedEvent : IGameEvent
    {
        public readonly int EncounterId;
        public readonly int InitiatorEmpireId;
        public readonly int OtherEmpireId;
        public readonly StarSystemId LegFrom;
        public readonly StarSystemId LegTo;

        public EncounterStartedEvent(int encounterId, int initiatorEmpireId, int otherEmpireId, StarSystemId legFrom, StarSystemId legTo)
        {
            EncounterId = encounterId;
            InitiatorEmpireId = initiatorEmpireId;
            OtherEmpireId = otherEmpireId;
            LegFrom = legFrom;
            LegTo = legTo;
        }
    }

    /// <summary>Rapport publie une fois la rencontre tranchee (Phase 17).</summary>
    public readonly struct EncounterResolvedEvent : IGameEvent
    {
        public readonly int EncounterId;
        public readonly int DecidingEmpireId;
        public readonly int OtherEmpireId;

        /// <summary>Issue effectivement appliquee.</summary>
        public readonly EncounterOption Choice;

        public EncounterResolvedEvent(int encounterId, int decidingEmpireId, int otherEmpireId, EncounterOption choice)
        {
            EncounterId = encounterId;
            DecidingEmpireId = decidingEmpireId;
            OtherEmpireId = otherEmpireId;
            Choice = choice;
        }
    }
}
