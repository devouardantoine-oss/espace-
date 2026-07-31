using System.Collections.Generic;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Rencontre spatiale detectee mais pas encore tranchee (Phase 17).
    /// <para>
    /// La detection n'a le droit de rien muter : elle gele les deux flottes et empile un de ces
    /// enregistrements. La resolution a lieu plus tard, hors du parcours de la liste des flottes
    /// (voir <c>MilitaryService.ProcessPendingEncounters</c>).
    /// </para>
    /// </summary>
    public sealed class PendingEncounter
    {
        /// <summary>Identifiant de la rencontre, pour la resoudre sans ambiguite depuis l'interface.</summary>
        public int Id { get; }

        /// <summary>Flotte qui vient d'entamer son etape et a provoque la rencontre.</summary>
        public Fleet Initiator { get; }

        /// <summary>Flotte deja engagee sur le meme tronçon.</summary>
        public Fleet Other { get; }

        /// <summary>Statut diplomatique entre les deux empires au moment de la rencontre.</summary>
        public DiplomaticStatus Status { get; }

        /// <summary>Tronçon sur lequel la rencontre a lieu (pour les rapports et l'affichage).</summary>
        public StarSystemId LegFrom { get; }
        public StarSystemId LegTo { get; }

        /// <summary>Issues proposables, calculees une fois a la detection (voir <see cref="EncounterRules.AvailableOptions"/>).</summary>
        public IReadOnlyList<EncounterOption> Options { get; }

        public PendingEncounter(
            int id, Fleet initiator, Fleet other, DiplomaticStatus status,
            StarSystemId legFrom, StarSystemId legTo, IReadOnlyList<EncounterOption> options)
        {
            Id = id;
            Initiator = initiator;
            Other = other;
            Status = status;
            LegFrom = legFrom;
            LegTo = legTo;
            Options = options;
        }

        /// <summary>Vrai si <paramref name="empireId"/> est l'un des deux camps.</summary>
        public bool Involves(int empireId) => Initiator.OwnerId == empireId || Other.OwnerId == empireId;

        /// <summary>La flotte de <paramref name="empireId"/> dans cette rencontre, et celle d'en face.</summary>
        public void GetSides(int empireId, out Fleet own, out Fleet opponent)
        {
            if (Initiator.OwnerId == empireId)
            {
                own = Initiator;
                opponent = Other;
                return;
            }

            own = Other;
            opponent = Initiator;
        }
    }
}
