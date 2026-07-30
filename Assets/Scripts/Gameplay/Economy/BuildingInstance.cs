using Espace.Core;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Economy
{
    /// <summary>Etat d'avancement d'un <see cref="BuildingInstance"/>.</summary>
    public enum BuildingStatus
    {
        UnderConstruction,
        Completed
    }

    /// <summary>
    /// Batiment construit (ou en cours de construction) sur un systeme precis.
    /// <para>
    /// Classe (et non struct) : possede un cycle de vie propre (avance de
    /// <see cref="BuildingStatus.UnderConstruction"/> vers <see cref="BuildingStatus.Completed"/>)
    /// et est stockee par reference dans <c>EconomyService</c>, qui a besoin de muter son
    /// statut au fil des jours sans reconstruire la liste qui la contient.
    /// </para>
    /// </summary>
    public sealed class BuildingInstance
    {
        /// <summary>Systeme sur lequel ce batiment est construit.</summary>
        public StarSystemId SystemId { get; }

        /// <summary>Type de batiment (cout, production, duree...).</summary>
        public BuildingType Type { get; }

        /// <summary>Date a laquelle la construction se termine.</summary>
        public GameDate CompletionDate { get; }

        /// <summary>Statut courant.</summary>
        public BuildingStatus Status { get; private set; }

        public BuildingInstance(StarSystemId systemId, BuildingType type, GameDate completionDate)
        {
            SystemId = systemId;
            Type = type;
            CompletionDate = completionDate;
            Status = BuildingStatus.UnderConstruction;
        }

        /// <summary>Marque la construction comme terminee. Appele par <c>EconomyService</c> une fois <see cref="CompletionDate"/> atteinte.</summary>
        public void Complete()
        {
            Status = BuildingStatus.Completed;
        }
    }
}
