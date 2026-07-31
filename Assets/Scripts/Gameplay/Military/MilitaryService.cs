using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Research;
using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Implementation par defaut de <see cref="IMilitaryService"/>.
    /// <para>
    /// <b>Boucle journaliere :</b> s'abonne a <see cref="DayAdvancedEvent"/> (Phase 3). Chaque
    /// jour : les commandes de recrutement echues rejoignent leur garnison, les flottes
    /// arrivees a destination sont resolues (colonisation d'un systeme libre, renfort d'un
    /// systeme deja a soi, ou bataille contre un systeme ennemi), puis l'entretien des unites
    /// en service est preleve sur le tresor de chaque proprietaire.
    /// </para>
    /// <para>
    /// <b>Deplacements longue distance (Phase 17) :</b> une flotte rejoint n'importe quel
    /// systeme atteignable, l'itineraire etant calcule le long des routes hyperspatiales par
    /// <see cref="Espace.Gameplay.Galaxy.HyperlanePathfinder"/> et parcouru etape par etape.
    /// Les points de passage se limitent aux systemes libres ou possedes par la flotte : on ne
    /// traverse pas le territoire d'un tiers, faute de quoi un simple ordre de deplacement
    /// pourrait declencher une bataille surprise a mi-parcours. Seule l'arrivee sur la
    /// destination finale declenche une resolution — un point de passage est traverse, jamais
    /// occupe ni colonise.
    /// </para>
    /// <para>
    /// La traversabilite est une contrainte <i>de planification</i> : elle n'est jamais
    /// reverifiee en vol. Une flotte qui survole un systeme colonise entre-temps le survole.
    /// Limitation v1 assumee, qui evite tout un pan de cas limites (recalcul d'itineraire,
    /// halte a mi-parcours) sans contrepartie de jeu. En revanche la <i>destination</i> est bien
    /// revalidee a l'arrivee (voir <see cref="ResolveArrival"/>) : sur plusieurs semaines elle
    /// peut avoir change de mains, et resoudre aveuglement contournerait le verrou de guerre.
    /// </para>
    /// <para>
    /// Le facteur « ravitaillement » de la formule de combat, jusqu'ici implicitement favorable
    /// parce que les lignes de communication etaient toujours courtes, ne l'est plus vraiment :
    /// il reste neanmoins hors formule, a revisiter lors d'une passe d'equilibrage.
    /// </para>
    /// <para>
    /// <b>Rencontres spatiales (Phase 17) :</b> deux flottes d'empires differents qui empruntent
    /// le meme tronçon se rencontrent (voir <see cref="EncounterRules"/>). La detection a lieu
    /// au <i>debut</i> d'une etape et ne mute rien : elle gele les deux flottes et empile la
    /// rencontre, qui est resolue hors du tick par <see cref="ProcessPendingEncounters"/>.
    /// Ce service ne touche jamais l'horloge — la mise en pause est l'affaire de l'interface et
    /// reste purement cosmetique.
    /// </para>
    /// <para>
    /// <b>Entretien impaye :</b> si le tresor d'un empire ne couvre pas l'entretien du jour, la
    /// depense echoue silencieusement (aucune dette, aucune desertion en Phase 6) — a affiner
    /// en Phase 12 si necessaire.
    /// </para>
    /// <para>
    /// <b>Recherche (Phase 8) :</b> le domaine Armement augmente <see cref="CommandModifierFor"/>
    /// (donc la puissance de combat, attaquant comme defenseur) et le domaine Logistique
    /// accelere les deplacements de flotte (<see cref="ComputeLegArrivalDate"/>) et plafonne le
    /// nombre de flottes en deplacement simultane (<see cref="TryMoveFleet"/>, Phase 14) — le
    /// tout via <see cref="Espace.Gameplay.Research.IResearchService"/>, resolu paresseusement,
    /// sans effet tant que rien n'a ete recherche.
    /// </para>
    /// <para>
    /// <b>Entree en territoire etranger conditionnee a la guerre (Phase 7) :</b>
    /// <see cref="TryMoveFleet"/> refuse desormais tout deplacement vers un systeme possede
    /// par un autre empire tant qu'un <see cref="DiplomaticStatus.War"/> n'a pas ete declare
    /// entre les deux (voir <see cref="IDiplomacyService"/>) — avant la Phase 7, n'importe
    /// quel empire pouvait attaquer n'importe quel voisin sans justification diplomatique.
    /// La colonisation d'un systeme non possede reste, elle, entierement libre.
    /// </para>
    /// <para>
    /// <b>Colonisation et invasion (Phase 16) :</b> coloniser un systeme libre exige
    /// desormais un nombre d'Infanterie fonction de sa population et de son developpement, et
    /// en consomme une partie a l'installation (voir <see cref="ColonizationRules"/> et
    /// <see cref="ResolveColonization"/>) ; l'exigence est verifiee des le depart dans
    /// <see cref="TryMoveFleet"/>. Symetriquement, remporter une bataille ne capture le
    /// systeme que s'il reste de l'Infanterie pour l'occuper — sans quoi la garnison adverse
    /// est detruite mais le territoire ne change pas de main.
    /// </para>
    /// <para>
    /// <b>Amiraux (Phase 15) :</b> chaque <see cref="Fleet"/> a un <see cref="Admiral"/>
    /// genere automatiquement a sa creation (voir <see cref="Fleet"/>) dont les bonus/malus
    /// s'ajoutent aux facteurs existants — attaque dans <see cref="ComputeAttackerModifier"/>,
    /// defense dans <see cref="ComputeDefenderModifier"/>, vitesse dans
    /// <see cref="ComputeLegArrivalDate"/> — sans toucher <see cref="CombatResolver"/>, deja
    /// generique sur un simple facteur multiplicatif par camp.
    /// </para>
    /// </summary>
    public sealed class MilitaryService : IMilitaryService, IEncounterService, IGameService
    {
        /// <summary>Bonus de fortification par niveau de developpement du systeme defendu (« terrain »).</summary>
        private const float TerrainBonusPerDevelopmentLevel = 0.1f;

        /// <summary>
        /// Plafond d'unites par flotte (Phase 14, brief). Verifie au lancement du recrutement
        /// (garnison actuelle + commandes deja en attente + quantite demandee) plutot qu'a sa
        /// completion, pour ne jamais faire depenser des ressources pour un recrutement voue a
        /// etre refuse. Les fusions de flottes a l'arrivee peuvent encore depasser ce plafond
        /// (limitation v1 documentee, meme esprit que l'absence de pathfinding multi-sauts).
        /// </summary>
        private const int MaxUnitsPerFleet = 10;

        /// <summary>
        /// Flottes qu'un empire peut avoir deployees simultanement sans aucune recherche en
        /// Logistique (Phase 14, releve de 1 a 2 en Phase 17).
        /// <para>
        /// Avec les trajets longue distance, un plafond de 1 privait un empire sans recherche de
        /// <b>tout</b> mouvement — y compris une colonisation voisine — pendant les semaines que
        /// dure une traversee, ce qui contredisait la promesse « clique n'importe quelle
        /// destination ». La Logistique reste pleinement utile : elle ajoute toujours un palier
        /// par niveau.
        /// </para>
        /// </summary>
        private const int BaseSimultaneousFleetCap = 2;

        private readonly GalaxyMap _map;
        private readonly IGameClock _gameClock;
        private readonly IEventBus _eventBus;
        private readonly IEconomyService _economy;
        private readonly IDiplomacyService _diplomacy;
        private readonly EmpireRegistry _empireRegistry;
        private readonly List<UnitTypeDefinition> _unitCatalog;

        private readonly List<Fleet> _fleets = new List<Fleet>();
        private readonly List<RecruitmentOrder> _recruitmentOrders = new List<RecruitmentOrder>();
        private readonly List<PendingEncounter> _pendingEncounters = new List<PendingEncounter>();
        private int _nextFleetId;
        private int _nextEncounterId;

        /// <summary>Garde-fou de reentrance : resoudre une rencontre peut en declencher d'autres.</summary>
        private bool _resolvingEncounters;

        /// <inheritdoc />
        public IReadOnlyList<UnitTypeDefinition> UnitCatalog => _unitCatalog;

        public MilitaryService(
            GalaxyMap map, IGameClock gameClock, IEventBus eventBus, IEconomyService economy, IDiplomacyService diplomacy,
            EmpireRegistry empireRegistry, IReadOnlyList<UnitTypeDefinition> unitCatalog)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _diplomacy = diplomacy ?? throw new ArgumentNullException(nameof(diplomacy));
            _empireRegistry = empireRegistry ?? throw new ArgumentNullException(nameof(empireRegistry));
            _unitCatalog = new List<UnitTypeDefinition>(unitCatalog ?? Array.Empty<UnitTypeDefinition>());
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _fleets.Clear();
            _recruitmentOrders.Clear();
            _pendingEncounters.Clear();
            _nextFleetId = 1;
            _nextEncounterId = 1;
            _eventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _eventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
            _fleets.Clear();
            _recruitmentOrders.Clear();
            _pendingEncounters.Clear();
        }

        /// <inheritdoc />
        public bool TryGetStationedFleet(StarSystemId systemId, int empireId, out Fleet fleet)
        {
            fleet = _fleets.Find(f => f.Status == FleetStatus.Stationed && f.CurrentSystemId.Equals(systemId) && f.OwnerId == empireId);
            return fleet != null;
        }

        /// <inheritdoc />
        public IReadOnlyList<Fleet> GetFleetsAt(StarSystemId systemId)
        {
            return _fleets.FindAll(f => f.Status == FleetStatus.Stationed && f.CurrentSystemId.Equals(systemId));
        }

        /// <inheritdoc />
        public IReadOnlyList<Fleet> GetFleetsForEmpire(int empireId)
        {
            return _fleets.FindAll(f => f.OwnerId == empireId);
        }

        /// <inheritdoc />
        public UnitBundle GetGarrison(StarSystemId systemId, int empireId)
        {
            return TryGetStationedFleet(systemId, empireId, out Fleet fleet) ? fleet.Composition : UnitBundle.Zero;
        }

        /// <inheritdoc />
        public float EstimatePower(UnitBundle composition)
        {
            return CombatResolver.ComputePower(composition, _unitCatalog);
        }

        /// <inheritdoc />
        public bool TryRecruitUnits(StarSystemId systemId, UnitTypeDefinition unitType, int count, out string error)
        {
            if (unitType == null)
            {
                error = "Type d'unite invalide.";
                return false;
            }

            if (count <= 0)
            {
                error = "La quantite doit etre positive.";
                return false;
            }

            if (!_map.TryGetSystem(systemId, out StarSystemState system))
            {
                error = "Systeme introuvable.";
                return false;
            }

            if (system.OwnerId == StarSystemState.UnownedOwnerId)
            {
                error = "Ce systeme n'a pas de proprietaire.";
                return false;
            }

            if (system.DevelopmentLevel < unitType.MinimumDevelopmentLevel)
            {
                error = $"Necessite un developpement de niveau {unitType.MinimumDevelopmentLevel}.";
                return false;
            }

            int currentGarrison = GetGarrison(systemId, system.OwnerId).TotalCount;
            int pendingCount = SumPendingRecruitment(systemId, system.OwnerId);
            if (currentGarrison + pendingCount + count > MaxUnitsPerFleet)
            {
                error = $"Plafond de {MaxUnitsPerFleet} unites par flotte atteint sur ce systeme.";
                return false;
            }

            var cost = new ResourceBundle(credits: unitType.CreditsCost * count, minerals: unitType.MineralsCost * count);
            if (!_economy.TrySpend(system.OwnerId, cost, out error))
            {
                return false;
            }

            GameDate completionDate = _gameClock.CurrentDate.AddDays(unitType.RecruitmentDays);
            _recruitmentOrders.Add(new RecruitmentOrder(systemId, system.OwnerId, unitType, count, completionDate));

            error = null;
            return true;
        }

        /// <summary>Somme des quantites deja en commande de recrutement pour ce (systeme, proprietaire), pour verifier le plafond avant d'en ajouter une nouvelle.</summary>
        private int SumPendingRecruitment(StarSystemId systemId, int ownerId)
        {
            int total = 0;
            foreach (RecruitmentOrder order in _recruitmentOrders)
            {
                if (order.SystemId.Equals(systemId) && order.OwnerId == ownerId)
                {
                    total += order.Count;
                }
            }

            return total;
        }

        /// <inheritdoc />
        public bool TryMoveFleet(Fleet fleet, StarSystemId destinationSystemId, out string error)
        {
            if (fleet == null)
            {
                error = "Flotte invalide.";
                return false;
            }

            if (fleet.Status != FleetStatus.Stationed)
            {
                error = "Cette flotte est deja en deplacement.";
                return false;
            }

            if (fleet.Composition.IsEmpty)
            {
                error = "Cette flotte ne contient aucune unite.";
                return false;
            }

            // Une flotte immobilisee par une rencontre compte dans le plafond : sans quoi laisser
            // une rencontre en attente serait un moyen de lancer une flotte supplementaire.
            int deployedFleetCount = _fleets.FindAll(f => f.OwnerId == fleet.OwnerId && f.Status != FleetStatus.Stationed).Count;
            int fleetCap = BaseSimultaneousFleetCap + ResearchTierCount(fleet.OwnerId, ResearchDomain.Logistics);
            if (deployedFleetCount >= fleetCap)
            {
                error = $"Plafond de flottes en deplacement simultane atteint ({fleetCap}) : recherchez la Logistique pour en deployer davantage.";
                return false;
            }

            if (!_map.TryGetSystem(destinationSystemId, out StarSystemState destination))
            {
                error = "Systeme de destination introuvable.";
                return false;
            }

            if (!TryPlanRoute(fleet, destinationSystemId, out IReadOnlyList<StarSystemId> route))
            {
                error = "Aucune route hyperspatiale praticable ne mene a cette destination.";
                return false;
            }

            // Colonisation (Phase 16) : verifiee au depart plutot qu'a l'arrivee, meme
            // philosophie que les plafonds de la Phase 14 — ne jamais laisser partir un
            // voyage voue a l'echec. Les statistiques d'un systeme libre ne changent jamais
            // (rien ne les fait evoluer tant qu'il n'a pas de proprietaire), donc l'exigence
            // calculee ici vaut encore a l'arrivee.
            if (destination.OwnerId == StarSystemState.UnownedOwnerId)
            {
                int requiredInfantry = ColonizationRules.RequiredInfantry(destination);
                if (fleet.Composition.Infantry < requiredInfantry)
                {
                    error = $"Colonisation refusee : {requiredInfantry} Infanterie requise pour {destination.Name}, "
                        + $"cette flotte n'en transporte que {fleet.Composition.Infantry}.";
                    return false;
                }
            }

            if (destination.OwnerId != StarSystemState.UnownedOwnerId
                && destination.OwnerId != fleet.OwnerId
                && _diplomacy.GetStatus(fleet.OwnerId, destination.OwnerId) != DiplomaticStatus.War)
            {
                error = "Deplacement refuse : aucune guerre declaree avec le proprietaire de ce systeme.";
                return false;
            }

            StarSystemState origin = _map.GetSystem(fleet.CurrentSystemId);
            fleet.BeginJourney(route, _gameClock.CurrentDate, ComputeLegArrivalDate(fleet, route, legIndex: 0), isRetreating: false);

            _eventBus.Publish(new FleetDepartedEvent(fleet.Id, fleet.OwnerId, origin.Id, destinationSystemId, isRetreating: false));
            ScanForEncounter(fleet);
            ProcessPendingEncounters();

            error = null;
            return true;
        }

        /// <summary>
        /// Calcule l'itineraire d'une flotte vers <paramref name="destinationSystemId"/> (Phase 17).
        /// <para>
        /// <b>Les points de passage sont limites aux systemes libres ou appartenant a la flotte</b>,
        /// alors que la destination finale echappe au filtre (c'est elle qu'on vient coloniser ou
        /// attaquer). Traverser le territoire d'un tiers demanderait de resoudre une bataille au
        /// milieu d'un trajet, ce qui transformerait un simple ordre de deplacement en surprise :
        /// mieux vaut refuser l'itineraire et laisser le joueur declarer la guerre ou contourner.
        /// </para>
        /// </summary>
        private bool TryPlanRoute(Fleet fleet, StarSystemId destinationSystemId, out IReadOnlyList<StarSystemId> route)
        {
            return HyperlanePathfinder.TryFindPath(
                _map, fleet.CurrentSystemId, destinationSystemId,
                waypoint => waypoint.OwnerId == StarSystemState.UnownedOwnerId || waypoint.OwnerId == fleet.OwnerId,
                out route);
        }

        /// <inheritdoc />
        public bool TryDetachFleet(StarSystemId systemId, int empireId, UnitBundle unitsToDetach, out Fleet detachedFleet, out string error)
        {
            detachedFleet = null;

            if (unitsToDetach.IsEmpty)
            {
                error = "Aucune unite a detacher.";
                return false;
            }

            if (!TryGetStationedFleet(systemId, empireId, out Fleet garrison))
            {
                error = "Aucune garnison sur ce systeme.";
                return false;
            }

            if (!garrison.Composition.IsGreaterOrEqualTo(unitsToDetach))
            {
                error = "La garnison ne contient pas assez d'unites de ce type.";
                return false;
            }

            garrison.SetComposition(garrison.Composition - unitsToDetach);
            detachedFleet = new Fleet(_nextFleetId++, empireId, systemId, unitsToDetach);
            _fleets.Add(detachedFleet);

            if (garrison.Composition.IsEmpty)
            {
                _fleets.Remove(garrison);
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Date d'arrivee de l'etape <paramref name="legIndex"/> de <paramref name="route"/>,
        /// comptee depuis le depart du voyage.
        /// <para>
        /// <b>L'arrondi n'a lieu qu'une fois, sur la distance cumulee</b> (Phase 17) : arrondir
        /// chaque etape separement ferait payer a un trajet de quinze sauts quinze arrondis et
        /// quinze planchers d'un jour, alors que « la duree depend de la distance ». Le plancher
        /// <c>legIndex + 1</c> garantit malgre tout au moins un jour par etape et une suite de
        /// dates strictement croissante.
        /// </para>
        /// </summary>
        private GameDate ComputeLegArrivalDate(Fleet fleet, IReadOnlyList<StarSystemId> route, int legIndex)
        {
            float speed = SlowestSpeed(fleet.Composition) * ResearchMultiplier(fleet.OwnerId, ResearchDomain.Logistics)
                * (1f + fleet.Admiral.SpeedBonus);

            float cumulativeDistance = 0f;
            for (int leg = 0; leg <= legIndex && leg + 1 < route.Count; leg++)
            {
                cumulativeDistance += HyperlanePathfinder.LegDistance(_map, route[leg], route[leg + 1]);
            }

            int daysSinceDeparture = Mathf.Max(legIndex + 1, Mathf.CeilToInt(cumulativeDistance / speed));
            return (fleet.JourneyStartDate ?? _gameClock.CurrentDate).AddDays(daysSinceDeparture);
        }

        /// <summary>La vitesse d'une flotte mixte est celle de son unite la plus lente.</summary>
        private float SlowestSpeed(UnitBundle composition)
        {
            float slowest = float.MaxValue;
            bool any = false;

            foreach (UnitTypeDefinition unitType in _unitCatalog)
            {
                if (unitType == null || composition.Get(unitType.UnitType) <= 0)
                {
                    continue;
                }

                any = true;
                if (unitType.Speed < slowest)
                {
                    slowest = unitType.Speed;
                }
            }

            return any ? slowest : 1f;
        }

        private void OnDayAdvanced(DayAdvancedEvent dayAdvancedEvent)
        {
            CompleteFinishedRecruitments(dayAdvancedEvent.Date);
            CompleteArrivals(dayAdvancedEvent.Date);
            ChargeUpkeep();

            // Draine la file APRES la copie defensive de CompleteArrivals : resoudre une rencontre
            // mute _fleets (combat, repli, suppression), ce qui casserait l'enumeration en cours.
            ProcessPendingEncounters();
        }

        private void CompleteFinishedRecruitments(GameDate date)
        {
            for (int i = _recruitmentOrders.Count - 1; i >= 0; i--)
            {
                RecruitmentOrder order = _recruitmentOrders[i];
                if (order.CompletionDate > date)
                {
                    continue;
                }

                Fleet garrison = GetOrCreateStationedFleet(order.SystemId, order.OwnerId);
                garrison.AddUnits(UnitBundle.Of(order.UnitType.UnitType, order.Count));
                _recruitmentOrders.RemoveAt(i);

                _eventBus.Publish(new RecruitmentCompletedEvent(order.SystemId, order.OwnerId, order.UnitType, order.Count));
            }
        }

        private void CompleteArrivals(GameDate date)
        {
            // Copie defensive : resoudre une arrivee modifie _fleets (fusion, suppression,
            // depart en retraite), ce qui casserait une enumeration directe.
            foreach (Fleet fleet in new List<Fleet>(_fleets))
            {
                if (fleet.Status != FleetStatus.Moving || fleet.ArrivalDate == null || fleet.ArrivalDate.Value > date)
                {
                    continue;
                }

                if (fleet.IsOnFinalLeg)
                {
                    ResolveArrival(fleet);
                    continue;
                }

                // Point de passage franchi (Phase 17) : la flotte ne s'y arrete pas.
                // **Surtout ne pas appeler ResolveArrival ici** — celui-ci colonise tout systeme
                // libre ou une flotte arrive, ce qui coloniserait le premier systeme neutre
                // traverse par n'importe quel trajet.
                fleet.AdvanceToNextLeg(date, ComputeLegArrivalDate(fleet, fleet.Route, fleet.RouteIndex + 1));
                ScanForEncounter(fleet);
            }
        }

        private void ResolveArrival(Fleet fleet)
        {
            StarSystemId destinationId = fleet.DestinationSystemId.Value;
            StarSystemState system = _map.GetSystem(destinationId);

            // Revalidation de la destination (Phase 17). Les verifications faites au depart
            // supposaient un trajet de quelques jours ; un voyage longue distance dure des
            // semaines, pendant lesquelles la destination peut changer de mains. Sans ce
            // controle, un systeme libre colonise entre-temps enverrait la flotte en bataille
            // contre un empire avec qui on est en paix, contournant le verrou de guerre de la
            // Phase 7.
            if (system.OwnerId != StarSystemState.UnownedOwnerId
                && system.OwnerId != fleet.OwnerId
                && _diplomacy.GetStatus(fleet.OwnerId, system.OwnerId) != DiplomaticStatus.War)
            {
                GameLog.Info(
                    $"[Fleet] {fleet.Name} renonce a {system.Name} : le systeme a change de proprietaire "
                    + "pendant le trajet et aucune guerre n'est declaree. Repli sur le systeme d'origine.");
                RetreatToOrigin(fleet, system);
                return;
            }

            if (system.OwnerId == StarSystemState.UnownedOwnerId)
            {
                ResolveColonization(fleet, system);
                return;
            }

            if (system.OwnerId == fleet.OwnerId)
            {
                fleet.CompleteMove(destinationId);
                MergeIntoStationedFleet(fleet);
                return;
            }

            ResolveBattle(fleet, system);
        }

        /// <summary>
        /// Installation d'une flotte sur un systeme libre (Phase 16) : consomme
        /// <see cref="ColonizationRules.InfantryLost"/> unites d'Infanterie, le reste de la
        /// flotte devenant la garnison de la nouvelle colonie.
        /// <para>
        /// <b>Les colons ne se dissolvent plus systematiquement</b> (comportement d'avant la
        /// Phase 16) : seuls les fantassins consommes par l'installation restent sur place.
        /// Une flotte qui n'embarquait que le strict necessaire disparait donc toujours
        /// entierement, mais une flotte plus fournie laisse une vraie garnison.
        /// </para>
        /// <para>
        /// <b>Le repli en cas d'Infanterie insuffisante est de la ceinture-bretelles :</b>
        /// <see cref="TryMoveFleet"/> refuse deja le depart, et le cas est en pratique
        /// inatteignable (les statistiques d'un systeme libre ne changent jamais, aucun
        /// systeme ne redevient libre, la composition d'une flotte est figee en vol). Il est
        /// traite quand meme plutot que de laisser une colonisation gratuite passer si l'une
        /// de ces trois hypotheses tombait un jour.
        /// </para>
        /// </summary>
        private void ResolveColonization(Fleet fleet, StarSystemState system)
        {
            int required = ColonizationRules.RequiredInfantry(system);

            if (fleet.Composition.Infantry < required)
            {
                GameLog.Warning(
                    $"[Colonization] {system.Name} : {required} Infanterie requise, la flotte {fleet.Name} n'en a que "
                    + $"{fleet.Composition.Infantry}. Repli sur le systeme d'origine.");
                RetreatToOrigin(fleet, system);
                return;
            }

            int lost = ColonizationRules.InfantryLost(system);
            system.OwnerId = fleet.OwnerId;
            fleet.SetComposition(fleet.Composition - UnitBundle.Of(UnitType.Infantry, lost));

            GameLog.Info(
                $"[Colonization] {system.Name} colonise par l'empire {fleet.OwnerId} "
                + $"(population {system.Population} M, developpement {system.DevelopmentLevel}, stabilite {system.Stability:0.00}) : "
                + $"{required} Infanterie engagee, {lost} perdue a l'installation.");

            if (fleet.Composition.IsEmpty)
            {
                // Toute la flotte s'est fondue dans la colonie qu'elle vient de fonder.
                _fleets.Remove(fleet);
            }
            else
            {
                fleet.CompleteMove(system.Id);
                MergeIntoStationedFleet(fleet);
            }

            _eventBus.Publish(new SystemColonizedEvent(system.Id, fleet.OwnerId, lost));
        }

        /// <summary>
        /// Renvoie <paramref name="fleet"/> vers son systeme d'origine, ou la retire si elle n'a
        /// plus rien a replier. Partage par la retraite apres defaite, le repli de colonisation et
        /// le repli de rencontre.
        /// <para>
        /// <b>Aucun balayage de rencontre sur le trajet de repli :</b> il repartirait sur la meme
        /// paire de systemes et re-declencherait immediatement une rencontre avec la flotte qu'on
        /// vient de fuir — boucle sans fin.
        /// </para>
        /// </summary>
        private void RetreatToOrigin(Fleet fleet, StarSystemState from)
        {
            if (fleet.Composition.IsEmpty)
            {
                _fleets.Remove(fleet);
                return;
            }

            // On repart d'ou l'on est, pas d'ou le voyage avait commence : la flotte est
            // physiquement sur `from`, meme si CurrentSystemId designe encore son point de depart.
            if (!HyperlanePathfinder.TryFindPath(
                    _map, from.Id, fleet.OriginSystemId,
                    waypoint => waypoint.OwnerId == StarSystemState.UnownedOwnerId || waypoint.OwnerId == fleet.OwnerId,
                    out IReadOnlyList<StarSystemId> route)
                || route.Count < 2)
            {
                // Plus aucune route praticable vers la base : la flotte se disperse plutot que de
                // rester indefiniment dans un etat impossible.
                GameLog.Warning($"[Fleet] {fleet.Name} ne trouve aucune route de repli depuis {from.Name} : la flotte est dispersee.");
                _fleets.Remove(fleet);
                return;
            }

            fleet.BeginJourney(route, _gameClock.CurrentDate, ComputeLegArrivalDate(fleet, route, legIndex: 0), isRetreating: true);
        }

        private void ResolveBattle(Fleet attackerFleet, StarSystemState system)
        {
            int defenderEmpireId = system.OwnerId;
            TryGetStationedFleet(system.Id, defenderEmpireId, out Fleet defenderFleet);
            UnitBundle defenderComposition = defenderFleet?.Composition ?? UnitBundle.Zero;

            float attackerModifier = ComputeAttackerModifier(attackerFleet);
            float defenderModifier = ComputeDefenderModifier(defenderEmpireId, system, defenderFleet);

            CombatResolver.BattleOutcome outcome = CombatResolver.Resolve(
                attackerFleet.Composition, attackerModifier, defenderComposition, defenderModifier, _unitCatalog);

            _eventBus.Publish(new BattleResolvedEvent(
                system.Id, attackerFleet.OwnerId, defenderEmpireId, outcome.AttackerWon,
                outcome.AttackerPower, outcome.DefenderPower, outcome.AttackerLosses, outcome.DefenderLosses));

            // Rapport detaille systematique, meme sans panneau ouvert pour le lire : la
            // console reste le seul endroit garanti de recevoir chaque bataille (brief :
            // « afficher un rapport detaille apres chaque bataille »).
            GameLog.Info(
                $"[Battle] {system.Name} : empire {attackerFleet.OwnerId} (puissance {outcome.AttackerPower:0}) "
                + $"attaque empire {defenderEmpireId} (puissance {outcome.DefenderPower:0}) -> "
                + $"{(outcome.AttackerWon ? "victoire de l'attaquant, systeme conquis" : "l'attaquant est repousse")}. "
                + $"Pertes attaquant : {outcome.AttackerLosses} | Pertes defenseur : {outcome.DefenderLosses}");

            if (outcome.AttackerWon)
            {
                if (defenderFleet != null)
                {
                    _fleets.Remove(defenderFleet);
                }

                attackerFleet.SetComposition(outcome.AttackerSurvivors);

                // Invasion (Phase 16, point 9 du brief) : gagner la bataille spatiale ne suffit
                // pas a prendre le systeme, il faut de l'Infanterie survivante pour l'occuper.
                // Une frappe de Chasseurs seuls reste une tactique valable — elle detruit la
                // garnison adverse — mais laisse le systeme a son proprietaire.
                if (outcome.AttackerSurvivors.Infantry <= 0)
                {
                    GameLog.Info(
                        $"[Battle] {system.Name} : victoire sans occupation — aucune Infanterie survivante pour envahir, "
                        + "le systeme reste a son proprietaire.");
                    RetreatToOrigin(attackerFleet, system);
                    return;
                }

                system.OwnerId = attackerFleet.OwnerId;
                attackerFleet.CompleteMove(system.Id);
                MergeIntoStationedFleet(attackerFleet);
                return;
            }

            if (defenderFleet != null)
            {
                defenderFleet.SetComposition(outcome.DefenderSurvivors);
            }

            attackerFleet.SetComposition(outcome.AttackerSurvivors);
            RetreatToOrigin(attackerFleet, system);
        }

        /// <summary>Moral approxime par la stabilite du systeme d'origine de l'attaquant, module par le commandement de sa personnalite et le bonus d'attaque de son Amiral (Phase 15).</summary>
        private float ComputeAttackerModifier(Fleet attackerFleet)
        {
            float morale = _map.TryGetSystem(attackerFleet.OriginSystemId, out StarSystemState origin) ? origin.Stability : 1f;
            return morale * CommandModifierFor(attackerFleet.OwnerId) * (1f + attackerFleet.Admiral.AttackBonus);
        }

        /// <summary>Moral du defenseur (stabilite du systeme attaque), avantage du terrain (fortifications liees au developpement), commandement, et bonus de defense de son Amiral s'il y en a un (Phase 15 — un systeme peut etre sans garnison).</summary>
        private float ComputeDefenderModifier(int defenderEmpireId, StarSystemState system, Fleet defenderFleet)
        {
            float morale = system.Stability;
            float terrainBonus = 1f + system.DevelopmentLevel * TerrainBonusPerDevelopmentLevel;
            float admiralBonus = 1f + (defenderFleet?.Admiral.DefenseBonus ?? 0f);
            return morale * terrainBonus * CommandModifierFor(defenderEmpireId) * admiralBonus;
        }

        private float CommandModifierFor(int empireId)
        {
            float personalityModifier = _empireRegistry.TryGetEmpire(empireId, out Empire empire)
                ? EmpirePersonalityProfile.Get(empire.Personality).CommandModifier
                : 1f;

            return personalityModifier * ResearchMultiplier(empireId, ResearchDomain.Weapons);
        }

        /// <summary>
        /// Multiplicateur issu de la recherche (Phase 8) : <c>1 + bonus cumule</c> du domaine
        /// correspondant. Meme resolution paresseuse via <see cref="ServiceLocator"/> que
        /// <see cref="Espace.Gameplay.Economy.EconomyService"/>, pour la meme raison : eviter
        /// tout ordre d'initialisation impose entre <c>MilitaryController</c> et
        /// <c>ResearchController</c>.
        /// </summary>
        private static float ResearchMultiplier(int empireId, ResearchDomain domain)
        {
            return ServiceLocator.TryGet(out IResearchService research) ? 1f + research.GetBonus(empireId, domain) : 1f;
        }

        /// <summary>Nombre de paliers completes dans <paramref name="domain"/> pour <paramref name="empireId"/>, 0 si la recherche est indisponible. Meme resolution paresseuse que <see cref="ResearchMultiplier"/>.</summary>
        private static int ResearchTierCount(int empireId, ResearchDomain domain)
        {
            return ServiceLocator.TryGet(out IResearchService research) ? research.GetCompletedTierCount(empireId, domain) : 0;
        }

        private void MergeIntoStationedFleet(Fleet arrivingFleet)
        {
            Fleet existing = _fleets.Find(f =>
                !ReferenceEquals(f, arrivingFleet) && f.Status == FleetStatus.Stationed
                && f.CurrentSystemId.Equals(arrivingFleet.CurrentSystemId) && f.OwnerId == arrivingFleet.OwnerId);

            if (existing != null)
            {
                existing.AddUnits(arrivingFleet.Composition);
                _fleets.Remove(arrivingFleet);
            }
        }

        // --- Rencontres spatiales (Phase 17) ------------------------------------------------

        /// <summary>
        /// Cherche une rencontre pour <paramref name="fleet"/>, qui vient d'entamer une etape.
        /// <para>
        /// <b>Pourquoi au debut d'une etape, et pas chaque jour :</b> de deux flottes partageant
        /// un tronçon sur des fenetres qui se chevauchent, la seconde a demarrer voit toujours la
        /// premiere. Balayer au demarrage n'a donc aucun faux negatif, tout en garantissant qu'une
        /// meme paire ne se declenche qu'une fois — sans registre « deja rencontre ».
        /// </para>
        /// <para>
        /// <b>Une seule rencontre par demarrage</b> (la premiere par identifiant croissant, pour
        /// rester deterministe) : deux rencontres simultanees mutileraient deux fois la meme
        /// composition.
        /// </para>
        /// <para>
        /// <b>Ne mute que le statut des deux flottes.</b> Aucun combat, aucun transfert, aucun
        /// appel a l'horloge : la detection survient au milieu du parcours de <c>_fleets</c>, la
        /// resolution attend <see cref="ProcessPendingEncounters"/>.
        /// </para>
        /// </summary>
        private void ScanForEncounter(Fleet fleet)
        {
            if (fleet.Status != FleetStatus.Moving || fleet.CurrentLegFrom == null || fleet.CurrentLegTo == null)
            {
                return;
            }

            StarSystemId legFrom = fleet.CurrentLegFrom.Value;
            StarSystemId legTo = fleet.CurrentLegTo.Value;

            Fleet match = null;
            foreach (Fleet other in _fleets)
            {
                if (ReferenceEquals(other, fleet)
                    || other.OwnerId == fleet.OwnerId // Deux flottes du meme empire se croisent sans histoire.
                    || other.Status != FleetStatus.Moving
                    || !SharesLeg(other, legFrom, legTo))
                {
                    continue;
                }

                if (match == null || other.Id < match.Id)
                {
                    match = other;
                }
            }

            if (match == null)
            {
                return;
            }

            DiplomaticStatus status = _diplomacy.GetStatus(fleet.OwnerId, match.OwnerId);
            GameDate today = _gameClock.CurrentDate;

            fleet.FreezeForEncounter(today);
            match.FreezeForEncounter(today);

            var encounter = new PendingEncounter(
                _nextEncounterId++, fleet, match, status, legFrom, legTo, EncounterRules.AvailableOptions(status));
            _pendingEncounters.Add(encounter);

            _eventBus.Publish(new EncounterStartedEvent(encounter.Id, fleet.OwnerId, match.OwnerId, legFrom, legTo));
        }

        /// <summary>Vrai si <paramref name="other"/> emprunte le meme tronçon, dans un sens ou dans l'autre.</summary>
        private static bool SharesLeg(Fleet other, StarSystemId legFrom, StarSystemId legTo)
        {
            if (other.CurrentLegFrom == null || other.CurrentLegTo == null)
            {
                return false;
            }

            StarSystemId otherFrom = other.CurrentLegFrom.Value;
            StarSystemId otherTo = other.CurrentLegTo.Value;

            return (otherFrom.Equals(legFrom) && otherTo.Equals(legTo))
                || (otherFrom.Equals(legTo) && otherTo.Equals(legFrom));
        }

        /// <summary>
        /// Tranche les rencontres en attente qui n'impliquent pas le joueur, hors de tout parcours
        /// de <c>_fleets</c> — c'est ici qu'on a le droit de muter la liste des flottes.
        /// </summary>
        private void ProcessPendingEncounters()
        {
            if (_resolvingEncounters)
            {
                return;
            }

            _resolvingEncounters = true;
            try
            {
                for (int i = _pendingEncounters.Count - 1; i >= 0; i--)
                {
                    PendingEncounter encounter = _pendingEncounters[i];

                    if (!IsStillValid(encounter))
                    {
                        _pendingEncounters.RemoveAt(i);
                        continue;
                    }

                    if (encounter.Involves(EconomyService.PlayerOwnerId))
                    {
                        continue; // Attend la decision du joueur (voir IEncounterService).
                    }

                    _pendingEncounters.RemoveAt(i);
                    ApplyOutcome(encounter, encounter.Initiator.OwnerId, ChooseAiOption(encounter, encounter.Initiator, encounter.Other));
                }
            }
            finally
            {
                _resolvingEncounters = false;
            }
        }

        /// <summary>Une rencontre dont une flotte a disparu ou a ete degelee entre-temps n'a plus lieu d'etre.</summary>
        private bool IsStillValid(PendingEncounter encounter)
        {
            return _fleets.Contains(encounter.Initiator)
                && _fleets.Contains(encounter.Other)
                && encounter.Initiator.Status == FleetStatus.AwaitingEncounter
                && encounter.Other.Status == FleetStatus.AwaitingEncounter;
        }

        private EncounterOption ChooseAiOption(PendingEncounter encounter, Fleet decider, Fleet opponent)
        {
            EmpirePersonalityProfileData profile = _empireRegistry.TryGetEmpire(decider.OwnerId, out Empire empire)
                ? EmpirePersonalityProfile.Get(empire.Personality)
                : EmpirePersonalityProfile.Get(EmpirePersonality.Pacifist);

            return EncounterRules.ChooseForAi(
                profile, encounter.Status,
                EstimatePower(decider.Composition), EstimatePower(opponent.Composition),
                _diplomacy.GetOpinion(decider.OwnerId, opponent.OwnerId),
                _diplomacy.HasTradeTreaty(decider.OwnerId, opponent.OwnerId));
        }

        /// <inheritdoc />
        public PendingEncounter GetPendingEncounterFor(int empireId)
        {
            return _pendingEncounters.Find(e => IsStillValid(e) && e.Involves(empireId));
        }

        /// <inheritdoc />
        public bool TryResolveEncounter(int encounterId, int empireId, EncounterOption choice, out string error)
        {
            int index = _pendingEncounters.FindIndex(e => e.Id == encounterId);
            if (index < 0)
            {
                error = "Cette rencontre n'est plus d'actualite.";
                return false;
            }

            PendingEncounter encounter = _pendingEncounters[index];

            if (!encounter.Involves(empireId))
            {
                error = "Cette rencontre ne concerne pas cet empire.";
                return false;
            }

            bool offered = false;
            foreach (EncounterOption option in encounter.Options)
            {
                if (option == choice)
                {
                    offered = true;
                    break;
                }
            }

            if (!offered)
            {
                error = "Cette issue n'est pas possible avec le statut diplomatique actuel.";
                return false;
            }

            // Retiree AVANT toute mutation : une issue peut relancer une etape, donc rappeler le
            // balayage, donc revenir ici.
            _pendingEncounters.RemoveAt(index);

            if (!IsStillValid(encounter))
            {
                error = "Cette rencontre n'est plus d'actualite.";
                return false;
            }

            ApplyOutcome(encounter, empireId, choice);
            ProcessPendingEncounters();

            error = null;
            return true;
        }

        /// <summary>
        /// Applique l'issue choisie. Toutes les consequences reutilisent les services existants :
        /// <see cref="CombatResolver"/> pour le combat, <c>IEconomyService</c> pour les transferts,
        /// <c>IDiplomacyService.ApplyOpinionShift</c> pour l'opinion.
        /// </summary>
        private void ApplyOutcome(PendingEncounter encounter, int decidingEmpireId, EncounterOption choice)
        {
            encounter.GetSides(decidingEmpireId, out Fleet own, out Fleet opponent);
            GameDate today = _gameClock.CurrentDate;

            // Les deux flottes repartent : le combat et le repli les remettront en mouvement
            // eux-memes si besoin.
            own.ResumeAfterEncounter(today);
            opponent.ResumeAfterEncounter(today);

            switch (choice)
            {
                case EncounterOption.Fight:
                    ResolveEncounterBattle(own, opponent);
                    break;

                case EncounterOption.Withdraw:
                    // Repli sans balayage : sinon la flotte re-rencontrerait aussitot celle qu'elle fuit.
                    RetreatToOrigin(own, _map.GetSystem(encounter.LegFrom));
                    break;

                case EncounterOption.Negotiate:
                    _diplomacy.ApplyOpinionShift(own.OwnerId, opponent.OwnerId, EncounterRules.NegotiationOpinionGain);
                    _diplomacy.ApplyOpinionShift(opponent.OwnerId, own.OwnerId, EncounterRules.NegotiationOpinionGain);
                    break;

                case EncounterOption.Trade:
                    _economy.Grant(own.OwnerId, new ResourceBundle(credits: EncounterRules.TradeCreditsPerSide));
                    _economy.Grant(opponent.OwnerId, new ResourceBundle(credits: EncounterRules.TradeCreditsPerSide));
                    _diplomacy.ApplyOpinionShift(own.OwnerId, opponent.OwnerId, EncounterRules.TradeOpinionGain);
                    _diplomacy.ApplyOpinionShift(opponent.OwnerId, own.OwnerId, EncounterRules.TradeOpinionGain);
                    break;

                case EncounterOption.Piracy:
                    ResolvePiracy(own, opponent);
                    break;
            }

            GameLog.Info(
                $"[Encounter] Empire {own.OwnerId} ({own.Name}) croise empire {opponent.OwnerId} ({opponent.Name}) "
                + $"entre {_map.GetSystem(encounter.LegFrom).Name} et {_map.GetSystem(encounter.LegTo).Name} -> {choice}.");

            _eventBus.Publish(new EncounterResolvedEvent(encounter.Id, own.OwnerId, opponent.OwnerId, choice));
        }

        /// <summary>
        /// Combat en espace profond : aucun bonus de terrain (il n'y a pas de terrain), et
        /// l'attaquant est celui qui a choisi d'engager — <see cref="CombatResolver"/> donnant
        /// l'egalite au defenseur, ce role doit venir du sens de l'action et non d'un identifiant.
        /// </summary>
        private void ResolveEncounterBattle(Fleet attacker, Fleet defender)
        {
            float attackerModifier = CommandModifierFor(attacker.OwnerId) * (1f + attacker.Admiral.AttackBonus);
            float defenderModifier = CommandModifierFor(defender.OwnerId) * (1f + defender.Admiral.DefenseBonus);

            CombatResolver.BattleOutcome outcome = CombatResolver.Resolve(
                attacker.Composition, attackerModifier, defender.Composition, defenderModifier, _unitCatalog);

            attacker.SetComposition(outcome.AttackerSurvivors);
            defender.SetComposition(outcome.DefenderSurvivors);

            GameLog.Info(
                $"[Encounter] Combat : {attacker.Name} (puissance {outcome.AttackerPower:0}) contre "
                + $"{defender.Name} (puissance {outcome.DefenderPower:0}) -> "
                + $"{(outcome.AttackerWon ? "l'assaillant l'emporte" : "l'assaillant est repousse")}. "
                + $"Pertes : {outcome.AttackerLosses} | {outcome.DefenderLosses}");

            RemoveIfAnnihilated(attacker);
            RemoveIfAnnihilated(defender);
        }

        private void RemoveIfAnnihilated(Fleet fleet)
        {
            if (fleet.Composition.IsEmpty)
            {
                _fleets.Remove(fleet);
            }
        }

        /// <summary>Saisit une part du tresor adverse et fait chuter l'opinion de la victime envers le pirate.</summary>
        private void ResolvePiracy(Fleet raider, Fleet victim)
        {
            float loot = _economy.GetTreasury(victim.OwnerId).Credits * EncounterRules.PiracyTreasuryShare;

            if (loot > 0f && _economy.TrySpend(victim.OwnerId, new ResourceBundle(credits: loot), out _))
            {
                _economy.Grant(raider.OwnerId, new ResourceBundle(credits: loot));
                GameLog.Info($"[Encounter] Piraterie : {loot:0} Credits saisis sur l'empire {victim.OwnerId}.");
            }

            // Opinion dirigee : c'est la victime qui en veut au pirate, pas l'inverse.
            _diplomacy.ApplyOpinionShift(victim.OwnerId, raider.OwnerId, -EncounterRules.PiracyOpinionPenalty);
        }

        /// <inheritdoc />
        public void RestoreGarrison(StarSystemId systemId, int empireId, UnitBundle composition, string fleetName = null, Admiral? admiral = null)
        {
            Fleet garrison = GetOrCreateStationedFleet(systemId, empireId, fleetName, admiral);
            garrison.SetComposition(composition);
        }

        /// <inheritdoc />
        public IReadOnlyList<Fleet> GetFleetsInTransit()
        {
            return _fleets.FindAll(f => f.Status != FleetStatus.Stationed && f.Route != null);
        }

        /// <inheritdoc />
        public void ClearFleetsInTransit()
        {
            _fleets.RemoveAll(f => f.Status != FleetStatus.Stationed);
            _pendingEncounters.Clear();
        }

        /// <inheritdoc />
        public void RestoreFleetInTransit(
            int empireId, UnitBundle composition, string fleetName, Admiral? admiral,
            IReadOnlyList<StarSystemId> route, int routeIndex, StarSystemId originSystemId,
            GameDate journeyStartDate, GameDate departureDate, GameDate legArrivalDate, bool isRetreating)
        {
            if (route == null || route.Count < 2)
            {
                return;
            }

            var fleet = new Fleet(_nextFleetId++, empireId, route[0], composition, fleetName, admiral);
            fleet.RestoreJourney(route, routeIndex, originSystemId, journeyStartDate, departureDate, legArrivalDate, isRetreating);
            _fleets.Add(fleet);
        }

        private Fleet GetOrCreateStationedFleet(StarSystemId systemId, int ownerId, string fleetName = null, Admiral? admiral = null)
        {
            if (TryGetStationedFleet(systemId, ownerId, out Fleet existing))
            {
                return existing;
            }

            var fleet = new Fleet(_nextFleetId++, ownerId, systemId, UnitBundle.Zero, fleetName, admiral);
            _fleets.Add(fleet);
            return fleet;
        }

        private void ChargeUpkeep()
        {
            var upkeepByEmpire = new Dictionary<int, float>();

            foreach (Fleet fleet in _fleets)
            {
                float upkeep = ComputeUpkeep(fleet.Composition);
                if (upkeep <= 0f)
                {
                    continue;
                }

                upkeepByEmpire[fleet.OwnerId] = upkeepByEmpire.TryGetValue(fleet.OwnerId, out float existing) ? existing + upkeep : upkeep;
            }

            foreach (KeyValuePair<int, float> entry in upkeepByEmpire)
            {
                _economy.TrySpend(entry.Key, new ResourceBundle(credits: entry.Value), out _);
            }
        }

        private float ComputeUpkeep(UnitBundle composition)
        {
            float total = 0f;
            foreach (UnitTypeDefinition unitType in _unitCatalog)
            {
                if (unitType == null)
                {
                    continue;
                }

                total += composition.Get(unitType.UnitType) * unitType.UpkeepPerDay;
            }

            return total;
        }
    }
}
