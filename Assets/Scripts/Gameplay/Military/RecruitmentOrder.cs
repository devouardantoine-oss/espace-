using Espace.Core;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Commande de recrutement en cours sur un systeme.
    /// <para>
    /// Meme role que <see cref="Espace.Gameplay.Economy.BuildingInstance"/> : un objet dont
    /// <see cref="MilitaryService"/> suit l'echeance jour apres jour. A son achevement, les
    /// unites rejoignent la flotte stationnee du systeme (creee si necessaire).
    /// </para>
    /// </summary>
    public sealed class RecruitmentOrder
    {
        public StarSystemId SystemId { get; }
        public int OwnerId { get; }
        public UnitTypeDefinition UnitType { get; }
        public int Count { get; }
        public GameDate CompletionDate { get; }

        public RecruitmentOrder(StarSystemId systemId, int ownerId, UnitTypeDefinition unitType, int count, GameDate completionDate)
        {
            SystemId = systemId;
            OwnerId = ownerId;
            UnitType = unitType;
            Count = count;
            CompletionDate = completionDate;
        }
    }
}
