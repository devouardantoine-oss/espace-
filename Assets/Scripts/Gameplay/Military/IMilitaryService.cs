using System.Collections.Generic;
using Espace.Core;
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

        /// <summary>
        /// Toutes les flottes de <paramref name="empireId"/>, stationnees ou en deplacement,
        /// tous systemes confondus (Phase 14, onglet « Flottes » de la fenetre de gestion).
        /// </summary>
        IReadOnlyList<Fleet> GetFleetsForEmpire(int empireId);

        /// <summary>Composition de la garnison de <paramref name="empireId"/> sur <paramref name="systemId"/> (vide si aucune).</summary>
        UnitBundle GetGarrison(StarSystemId systemId, int empireId);

        /// <summary>Puissance de combat d'une composition selon le catalogue courant (voir <see cref="CombatResolver.ComputePower"/>).</summary>
        float EstimatePower(UnitBundle composition);

        /// <summary>
        /// Retire une part de la garnison de <paramref name="systemId"/> (Phase 24, etape 5).
        /// <para>
        /// Meme mecanique que la desertion pour entretien impaye — <c>UnitBundle.Scale</c>, puis
        /// dissolution si plus personne ne reste — mais portee a un seul systeme. Les decisions
        /// s'en servent pour les couts payes en hommes : reprimer engage des unites, et une
        /// garnison laissee sans instructions finit par partir.
        /// </para>
        /// </summary>
        /// <param name="lostFraction">Part perdue, de 0 a 1. Hors de cet intervalle, elle est bornee.</param>
        /// <returns>Nombre d'unites effectivement perdues.</returns>
        int ReduceGarrison(StarSystemId systemId, int empireId, float lostFraction);

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
        /// Reste-t-il a <paramref name="empireId"/> une place sous le plafond de flottes en
        /// deplacement simultane (Phase 18) ?
        /// <para>
        /// <b>A consulter avant tout detachement :</b> <see cref="TryDetachFleet"/> n'a pas
        /// d'inverse, donc detacher puis se voir refuser <see cref="TryMoveFleet"/> laisse une
        /// flotte orpheline stationnee a cote de la garnison. Avec des trajets longue distance
        /// de plusieurs semaines, le plafond est atteint la plupart du temps : le verifier
        /// d'avance est la seule facon de ne pas fragmenter les garnisons mois apres mois.
        /// </para>
        /// </summary>
        bool CanDeployAnotherFleet(int empireId);

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
        /// au chargement d'une sauvegarde (Phase 10). Les flottes en voyage sont restaurees a
        /// part, par <see cref="RestoreFleetInTransit"/> (Phase 17) ; seules les commandes de
        /// recrutement en cours restent volontairement hors sauvegarde (elles ne durent que
        /// quelques jours, contrairement aux trajets longue distance).
        /// <para><paramref name="fleetName"/> restaure le nom sauvegarde (Phase 14) ; <c>null</c> ou vide en genere un nouveau.</para>
        /// <para><paramref name="admiral"/> restaure l'Amiral sauvegarde (Phase 15) ; <c>null</c> en genere un nouveau.</para>
        /// </summary>
        void RestoreGarrison(StarSystemId systemId, int empireId, UnitBundle composition, string fleetName = null, Admiral? admiral = null);

        /// <summary>
        /// Toutes les flottes actuellement en voyage, pour la sauvegarde (Phase 17). Elles sont
        /// structurellement invisibles de <see cref="TryGetStationedFleet"/>, sur lequel
        /// <c>SaveService</c> s'appuyait jusque-la.
        /// </summary>
        IReadOnlyList<Fleet> GetFleetsInTransit();

        /// <summary>
        /// Vide les flottes en voyage avant une restauration (Phase 17).
        /// <para>
        /// <b>Indispensable :</b> <c>SaveService.Apply</c> ne vide jamais rien et
        /// <see cref="RestoreGarrison"/> ecrase (get-or-create), mais une flotte en voyage n'a pas
        /// de cle equivalente : sans ce nettoyage, « Recharger » depuis le menu pause dupliquerait
        /// chaque flotte en vol a chaque appel.
        /// </para>
        /// </summary>
        void ClearFleetsInTransit();

        /// <summary>
        /// Recree une flotte en voyage depuis une sauvegarde (Phase 17), avec son itineraire, son
        /// etape en cours et ses dates. Un identifiant neuf lui est attribue : rien ne persiste ni
        /// ne reference un identifiant de flotte, seuls le nom et l'Amiral doivent survivre.
        /// </summary>
        void RestoreFleetInTransit(
            int empireId, UnitBundle composition, string fleetName, Admiral? admiral,
            IReadOnlyList<StarSystemId> route, int routeIndex, StarSystemId originSystemId,
            GameDate journeyStartDate, GameDate departureDate, GameDate legArrivalDate, bool isRetreating);
    }
}
