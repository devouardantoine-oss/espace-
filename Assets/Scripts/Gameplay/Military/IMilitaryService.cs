using System.Collections.Generic;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Armees de tous les empires : recrutement, garnisons, deplacements de flottes,
    /// resolution automatique des arrivees (colonisation ou bataille).
    /// </summary>
    public interface IMilitaryService
    {
        /// <summary>Catalogue des types d'unites recrutables, commun a tous les empires.</summary>
        IReadOnlyList<UnitTypeDefinition> UnitCatalog { get; }

        /// <summary>La flotte stationnee de <paramref name="empireId"/> sur <paramref name="systemId"/>, s'il en existe une.</summary>
        bool TryGetStationedFleet(StarSystemId systemId, int empireId, out Fleet fleet);

        /// <summary>Toutes les flottes stationnees sur <paramref name="systemId"/>, tous proprietaires confondus.</summary>
        IReadOnlyList<Fleet> GetFleetsAt(StarSystemId systemId);

        /// <summary>Composition de la garnison de <paramref name="empireId"/> sur <paramref name="systemId"/> (vide si aucune).</summary>
        UnitBundle GetGarrison(StarSystemId systemId, int empireId);

        /// <summary>Puissance de combat d'une composition selon le catalogue courant (voir <see cref="CombatResolver.ComputePower"/>).</summary>
        float EstimatePower(UnitBundle composition);

        /// <summary>
        /// Lance le recrutement de <paramref name="count"/> unites de <paramref name="unitType"/>
        /// sur <paramref name="systemId"/>, si le systeme a un proprietaire, respecte le
        /// developpement minimal requis, et si son tresor peut en couvrir le cout.
        /// </summary>
        bool TryRecruitUnits(StarSystemId systemId, UnitTypeDefinition unitType, int count, out string error);

        /// <summary>
        /// Envoie <paramref name="fleet"/> (actuellement stationnee) vers <paramref name="destinationSystemId"/>,
        /// si celui-ci est directement relie par une route hyperspatiale. La resolution de
        /// l'arrivee (colonisation, renfort, ou bataille) a lieu automatiquement le jour ou
        /// elle survient.
        /// </summary>
        bool TryMoveFleet(Fleet fleet, StarSystemId destinationSystemId, out string error);

        /// <summary>
        /// Retire <paramref name="unitsToDetach"/> de la garnison de <paramref name="empireId"/>
        /// sur <paramref name="systemId"/> pour en faire une nouvelle flotte stationnee sur le
        /// meme systeme, prete a etre envoyee ailleurs via <see cref="TryMoveFleet"/> — sans
        /// avoir a deplacer la garnison entiere et laisser le systeme sans defense.
        /// </summary>
        bool TryDetachFleet(StarSystemId systemId, int empireId, UnitBundle unitsToDetach, out Fleet detachedFleet, out string error);

        /// <summary>
        /// Definit directement la garnison stationnee de <paramref name="empireId"/> sur
        /// <paramref name="systemId"/>, sans recrutement, cout ni evenement publie — reserve
        /// au chargement d'une sauvegarde (Phase 10). Les flottes en transit et les commandes
        /// de recrutement en cours ne font volontairement pas partie de la sauvegarde
        /// (limitation v1 documentee : la fenetre de risque est faible, l'autosauvegarde etant
        /// mensuelle et les trajets ne durant que quelques jours).
        /// </summary>
        void RestoreGarrison(StarSystemId systemId, int empireId, UnitBundle composition);
    }
}
