using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
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
    /// <b>Deplacements limites aux voisins directs :</b> aucun calcul d'itineraire multi-sauts
    /// en v1 (voir <see cref="Espace.Gameplay.Galaxy.GalaxyMap.AreLinked"/>) — une consequence
    /// est que le facteur « ravitaillement » de la formule de combat reste implicitement
    /// favorable (lignes de communication toujours courtes), a revisiter si le pathfinding
    /// multi-sauts est introduit plus tard.
    /// </para>
    /// <para>
    /// <b>Entretien impaye :</b> si le tresor d'un empire ne couvre pas l'entretien du jour, la
    /// depense echoue silencieusement (aucune dette, aucune desertion en Phase 6) — a affiner
    /// en Phase 12 si necessaire.
    /// </para>
    /// </summary>
    public sealed class MilitaryService : IMilitaryService, IGameService
    {
        /// <summary>Bonus de fortification par niveau de developpement du systeme defendu (« terrain »).</summary>
        private const float TerrainBonusPerDevelopmentLevel = 0.1f;

        private readonly GalaxyMap _map;
        private readonly IGameClock _gameClock;
        private readonly IEventBus _eventBus;
        private readonly IEconomyService _economy;
        private readonly EmpireRegistry _empireRegistry;
        private readonly List<UnitTypeDefinition> _unitCatalog;

        private readonly List<Fleet> _fleets = new List<Fleet>();
        private readonly List<RecruitmentOrder> _recruitmentOrders = new List<RecruitmentOrder>();
        private int _nextFleetId;

        /// <inheritdoc />
        public IReadOnlyList<UnitTypeDefinition> UnitCatalog => _unitCatalog;

        public MilitaryService(
            GalaxyMap map, IGameClock gameClock, IEventBus eventBus, IEconomyService economy,
            EmpireRegistry empireRegistry, IReadOnlyList<UnitTypeDefinition> unitCatalog)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _empireRegistry = empireRegistry ?? throw new ArgumentNullException(nameof(empireRegistry));
            _unitCatalog = new List<UnitTypeDefinition>(unitCatalog ?? Array.Empty<UnitTypeDefinition>());
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _fleets.Clear();
            _recruitmentOrders.Clear();
            _nextFleetId = 1;
            _eventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _eventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
            _fleets.Clear();
            _recruitmentOrders.Clear();
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

            if (!_map.AreLinked(fleet.CurrentSystemId, destinationSystemId))
            {
                error = "Cette destination n'est pas directement reliee par une route hyperspatiale.";
                return false;
            }

            if (!_map.TryGetSystem(destinationSystemId, out StarSystemState destination))
            {
                error = "Systeme de destination introuvable.";
                return false;
            }

            StarSystemState origin = _map.GetSystem(fleet.CurrentSystemId);
            GameDate arrivalDate = ComputeArrivalDate(origin, destination, fleet.Composition);
            fleet.BeginMove(destinationSystemId, arrivalDate, isRetreating: false);

            _eventBus.Publish(new FleetDepartedEvent(fleet.Id, fleet.OwnerId, origin.Id, destinationSystemId, isRetreating: false));

            error = null;
            return true;
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

        private GameDate ComputeArrivalDate(StarSystemState origin, StarSystemState destination, UnitBundle composition)
        {
            float distance = Vector2.Distance(origin.Position, destination.Position);
            float speed = SlowestSpeed(composition);
            int days = Mathf.Max(1, Mathf.CeilToInt(distance / speed));
            return _gameClock.CurrentDate.AddDays(days);
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

                ResolveArrival(fleet);
            }
        }

        private void ResolveArrival(Fleet fleet)
        {
            StarSystemId destinationId = fleet.DestinationSystemId.Value;
            StarSystemState system = _map.GetSystem(destinationId);

            if (system.OwnerId == StarSystemState.UnownedOwnerId)
            {
                system.OwnerId = fleet.OwnerId;
                // Les colons ne restent pas une armee mobile : la flotte se dissout dans la
                // colonie qu'elle vient de fonder.
                _fleets.Remove(fleet);
                _eventBus.Publish(new SystemColonizedEvent(destinationId, fleet.OwnerId));
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

        private void ResolveBattle(Fleet attackerFleet, StarSystemState system)
        {
            int defenderEmpireId = system.OwnerId;
            TryGetStationedFleet(system.Id, defenderEmpireId, out Fleet defenderFleet);
            UnitBundle defenderComposition = defenderFleet?.Composition ?? UnitBundle.Zero;

            float attackerModifier = ComputeAttackerModifier(attackerFleet);
            float defenderModifier = ComputeDefenderModifier(defenderEmpireId, system);

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

                system.OwnerId = attackerFleet.OwnerId;
                attackerFleet.SetComposition(outcome.AttackerSurvivors);
                attackerFleet.CompleteMove(system.Id);
                MergeIntoStationedFleet(attackerFleet);
                return;
            }

            if (defenderFleet != null)
            {
                defenderFleet.SetComposition(outcome.DefenderSurvivors);
            }

            if (outcome.AttackerSurvivors.IsEmpty)
            {
                _fleets.Remove(attackerFleet);
                return;
            }

            StarSystemId retreatTo = attackerFleet.OriginSystemId;
            attackerFleet.SetComposition(outcome.AttackerSurvivors);
            GameDate retreatArrival = ComputeArrivalDate(system, _map.GetSystem(retreatTo), outcome.AttackerSurvivors);
            attackerFleet.BeginMove(retreatTo, retreatArrival, isRetreating: true);
        }

        /// <summary>Moral approxime par la stabilite du systeme d'origine de l'attaquant, module par le commandement de sa personnalite.</summary>
        private float ComputeAttackerModifier(Fleet attackerFleet)
        {
            float morale = _map.TryGetSystem(attackerFleet.OriginSystemId, out StarSystemState origin) ? origin.Stability : 1f;
            return morale * CommandModifierFor(attackerFleet.OwnerId);
        }

        /// <summary>Moral du defenseur (stabilite du systeme attaque), avantage du terrain (fortifications liees au developpement), et commandement.</summary>
        private float ComputeDefenderModifier(int defenderEmpireId, StarSystemState system)
        {
            float morale = system.Stability;
            float terrainBonus = 1f + system.DevelopmentLevel * TerrainBonusPerDevelopmentLevel;
            return morale * terrainBonus * CommandModifierFor(defenderEmpireId);
        }

        private float CommandModifierFor(int empireId)
        {
            return _empireRegistry.TryGetEmpire(empireId, out Empire empire)
                ? EmpirePersonalityProfile.Get(empire.Personality).CommandModifier
                : 1f;
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

        private Fleet GetOrCreateStationedFleet(StarSystemId systemId, int ownerId)
        {
            if (TryGetStationedFleet(systemId, ownerId, out Fleet existing))
            {
                return existing;
            }

            var fleet = new Fleet(_nextFleetId++, ownerId, systemId, UnitBundle.Zero);
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
