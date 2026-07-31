namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Rencontres spatiales en attente d'une decision du joueur (Phase 17).
    /// <para>
    /// <b>Interface separee d'<see cref="IMilitaryService"/> bien que portee par la meme instance
    /// de <c>MilitaryService</c></b> (le <c>ServiceLocator</c> est indexe par type, un meme objet
    /// peut donc etre enregistre sous deux cles) : les trois doublures d'<c>IMilitaryService</c>
    /// ecrites a la main dans les tests devraient sinon implementer des membres qui ne les
    /// concernent pas.
    /// </para>
    /// <para>
    /// Les rencontres entre deux IA se resolvent seules, immediatement. Seules celles impliquant
    /// le joueur remontent ici, <b>une a la fois</b> : les autres restent gelees, ce qui est sans
    /// consequence puisqu'une flotte gelee n'avance pas.
    /// </para>
    /// </summary>
    public interface IEncounterService
    {
        /// <summary>
        /// Rencontre impliquant <paramref name="empireId"/> qui attend une decision, ou
        /// <c>null</c>. Une seule est exposee a la fois.
        /// </summary>
        PendingEncounter GetPendingEncounterFor(int empireId);

        /// <summary>
        /// Applique <paramref name="choice"/> a la rencontre <paramref name="encounterId"/> au nom
        /// de <paramref name="empireId"/>. Echoue si la rencontre n'existe plus (les deux flottes
        /// ont pu disparaitre entre-temps) ou si l'issue n'est pas proposee pour ce statut
        /// diplomatique.
        /// </summary>
        bool TryResolveEncounter(int encounterId, int empireId, EncounterOption choice, out string error);
    }
}
