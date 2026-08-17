using System;
using System.Collections.Generic;
using System.IO;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Chronicle;
using Espace.Gameplay.Decisions;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.People;
using Espace.Gameplay.Research;
using UnityEngine;

namespace Espace.Gameplay.Save
{
    /// <summary>
    /// Implementation par defaut d'<see cref="ISaveService"/>.
    /// <para>
    /// <b>Depend de tous les autres services de gameplay, sans exception :</b> a la difference
    /// de <see cref="Espace.Gameplay.Diplomacy.DiplomacyService"/> ou
    /// <see cref="Espace.Gameplay.Espionage.EspionageService"/> (qui evitent les dependances de
    /// construction pour ne jamais imposer d'ordre d'initialisation aux autres), ce service
    /// n'a justement <b>aucun</b> autre service qui dependrait de lui en retour — rien ne
    /// l'attend, donc rien n'empeche <c>SaveController</c> d'attendre patiemment (par sondage
    /// dans <c>Update</c>, comme <c>MilitaryController</c>) que tous les autres soient prets
    /// avant de construire celui-ci avec des dependances classiques au constructeur.
    /// </para>
    /// <para>
    /// <b>Uniquement l'etat mutable, jamais le contenu regenerable</b> — voir le commentaire
    /// de <see cref="GameSaveData"/> pour le detail des limitations v1 assumees (flottes en
    /// transit, recrutement en cours, propositions en attente non sauvegardees).
    /// </para>
    /// </summary>
    public sealed class SaveService : ISaveService
    {
        private static readonly ResearchDomain[] AllDomains = (ResearchDomain[])Enum.GetValues(typeof(ResearchDomain));

        private readonly GalaxyMap _map;
        private readonly IGameClock _clock;
        private readonly IEconomyService _economy;
        private readonly IMilitaryService _military;
        private readonly IDiplomacyService _diplomacy;
        private readonly IResearchService _research;
        private readonly ICodexService _codex;
        private readonly IDecisionService _decisions;
        private readonly IGovernorService _governors;
        private readonly EmpireRegistry _empireRegistry;
        private readonly string _filePath;

        /// <param name="decisions">
        /// Optionnel, meme raison que <paramref name="codex"/>. Absent, les questions en attente
        /// et les ardoises ne sont ni ecrites ni relues — ce qui reste correct, mais fait
        /// disparaitre une ardoise en cours au rechargement. C'est pourquoi la scene de la carte,
        /// elle, le fournit toujours.
        /// </param>
        /// <param name="codex">
        /// Optionnel (Phase 24, etape 3). Absent, la sauvegarde ne contient aucun fragment et se
        /// recharge sans en restaurer — ce qui reste correct, <c>CodexService</c> relisant l'etat
        /// du monde chaque jour. Le rendre facultatif evite d'imposer une dependance de plus aux
        /// scenes et aux tests qui n'ont rien a faire du codex.
        /// </param>
        public SaveService(
            GalaxyMap map, IGameClock clock, IEconomyService economy, IMilitaryService military,
            IDiplomacyService diplomacy, IResearchService research, EmpireRegistry empireRegistry, string filePath,
            ICodexService codex = null, IDecisionService decisions = null, IGovernorService governors = null)
        {
            _codex = codex;
            _decisions = decisions;
            _governors = governors;

            _map = map ?? throw new ArgumentNullException(nameof(map));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _military = military ?? throw new ArgumentNullException(nameof(military));
            _diplomacy = diplomacy ?? throw new ArgumentNullException(nameof(diplomacy));
            _research = research ?? throw new ArgumentNullException(nameof(research));
            _empireRegistry = empireRegistry ?? throw new ArgumentNullException(nameof(empireRegistry));

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("Le chemin de sauvegarde ne peut pas etre vide.", nameof(filePath));
            }

            _filePath = filePath;
        }

        /// <inheritdoc />
        public bool SaveFileExists => File.Exists(_filePath);

        /// <inheritdoc />
        public void SaveNow()
        {
            GameSaveData data = Capture();
            string json = JsonUtility.ToJson(data, prettyPrint: true);

            try
            {
                File.WriteAllText(_filePath, json);
                GameLog.Info($"[Save] Sauvegarde ecrite : {data.Systems.Count} systemes, {data.Empires.Count} empires, {data.Garrisons.Count} garnisons.");
            }
            catch (IOException exception)
            {
                GameLog.Error($"[Save] Echec d'ecriture de la sauvegarde : {exception.Message}");
            }
        }

        /// <inheritdoc />
        public bool TryLoadAndApply(out string error)
        {
            if (!File.Exists(_filePath))
            {
                error = "Aucun fichier de sauvegarde.";
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(_filePath);
            }
            catch (IOException exception)
            {
                error = $"Lecture de la sauvegarde impossible : {exception.Message}";
                return false;
            }

            GameSaveData data;
            try
            {
                data = JsonUtility.FromJson<GameSaveData>(json);
                if (data == null)
                {
                    error = "Fichier de sauvegarde vide ou invalide.";
                    return false;
                }

                Apply(data);
            }
            catch (Exception exception)
            {
                // Volontairement large : un JSON corrompu ou une sauvegarde d'une version
                // incompatible peut echouer de bien des facons differentes (JsonUtility,
                // GameDate hors bornes, etc.) — jamais une raison de planter le jeu.
                error = $"Fichier de sauvegarde illisible ou corrompu : {exception.Message}";
                return false;
            }

            GameLog.Info($"[Save] Sauvegarde chargee : {data.Systems.Count} systemes, {data.Empires.Count} empires, {data.Garrisons.Count} garnisons.");

            error = null;
            return true;
        }

        private GameSaveData Capture()
        {
            IReadOnlyList<Empire> empires = _empireRegistry.Empires;

            var data = new GameSaveData
            {
                Date = new GameDateData { Year = _clock.CurrentDate.Year, Month = _clock.CurrentDate.Month, Day = _clock.CurrentDate.Day },
                Speed = (int)_clock.CurrentSpeed
            };

            foreach (StarSystemState system in _map.Systems)
            {
                data.Systems.Add(new StarSystemSaveData
                {
                    SystemId = system.Id.Value,
                    OwnerId = system.OwnerId,
                    Population = system.Population,
                    Wealth = system.Wealth,
                    DevelopmentLevel = system.DevelopmentLevel,
                    Stability = system.Stability
                });

                foreach (BuildingInstance building in _economy.GetBuildings(system.Id))
                {
                    if (building.Status != BuildingStatus.Completed)
                    {
                        continue;
                    }

                    data.Buildings.Add(new BuildingSaveData { SystemId = system.Id.Value, BuildingTypeDisplayName = building.Type.DisplayName });
                }

                foreach (Empire empire in empires)
                {
                    if (!_military.TryGetStationedFleet(system.Id, empire.Id, out Fleet fleet) || fleet.Composition.IsEmpty)
                    {
                        continue;
                    }

                    data.Garrisons.Add(new GarrisonSaveData
                    {
                        SystemId = system.Id.Value,
                        OwnerId = empire.Id,
                        FleetName = fleet.Name,
                        Infantry = fleet.Composition.Infantry,
                        Armored = fleet.Composition.Armored,
                        SpecialForces = fleet.Composition.SpecialForces,
                        Fighter = fleet.Composition.Fighter,
                        Frigate = fleet.Composition.Frigate,
                        Cruiser = fleet.Composition.Cruiser,
                        Battleship = fleet.Composition.Battleship,
                        AdmiralName = fleet.Admiral.Name,
                        AdmiralAttackBonus = fleet.Admiral.AttackBonus,
                        AdmiralSpeedBonus = fleet.Admiral.SpeedBonus,
                        AdmiralDefenseBonus = fleet.Admiral.DefenseBonus
                    });
                }
            }

            // Flottes en voyage (Phase 17) : invisibles de la boucle ci-dessus, qui parcourt la
            // carte et ne voit donc que les garnisons stationnees.
            foreach (Fleet fleet in _military.GetFleetsInTransit())
            {
                data.FleetsInTransit.Add(new FleetInTransitSaveData
                {
                    OwnerId = fleet.OwnerId,
                    FleetName = fleet.Name,
                    Infantry = fleet.Composition.Infantry,
                    Armored = fleet.Composition.Armored,
                    SpecialForces = fleet.Composition.SpecialForces,
                    Fighter = fleet.Composition.Fighter,
                    Frigate = fleet.Composition.Frigate,
                    Cruiser = fleet.Composition.Cruiser,
                    Battleship = fleet.Composition.Battleship,
                    AdmiralName = fleet.Admiral.Name,
                    AdmiralAttackBonus = fleet.Admiral.AttackBonus,
                    AdmiralSpeedBonus = fleet.Admiral.SpeedBonus,
                    AdmiralDefenseBonus = fleet.Admiral.DefenseBonus,
                    Route = ToIdList(fleet.Route),
                    RouteIndex = fleet.RouteIndex,
                    OriginSystemId = fleet.OriginSystemId.Value,
                    JourneyStartDate = ToDateData(fleet.JourneyStartDate ?? _clock.CurrentDate),
                    DepartureDate = ToDateData(fleet.DepartureDate ?? _clock.CurrentDate),
                    LegArrivalDate = ToDateData(fleet.ArrivalDate ?? _clock.CurrentDate),
                    IsRetreating = fleet.IsRetreating
                });
            }

            foreach (Empire empire in empires)
            {
                ResourceBundle treasury = _economy.GetTreasury(empire.Id);
                data.Empires.Add(new EmpireEconomySaveData
                {
                    EmpireId = empire.Id,
                    Credits = treasury.Credits,
                    Minerals = treasury.Minerals,
                    Energy = treasury.Energy,
                    Food = treasury.Food,
                    Influence = treasury.Influence,
                    TaxRate = _economy.GetTaxRate(empire.Id)
                });

                ResearchDomain? activeDomain = _research.GetActiveDomain(empire.Id);
                foreach (ResearchDomain domain in AllDomains)
                {
                    int completedTiers = _research.GetCompletedTierCount(empire.Id, domain);
                    float progress = _research.GetProgress(empire.Id, domain);
                    bool isActive = activeDomain == domain;

                    if (completedTiers == 0 && progress <= 0f && !isActive)
                    {
                        continue;
                    }

                    data.Research.Add(new ResearchProgressSaveData
                    {
                        EmpireId = empire.Id,
                        Domain = (int)domain,
                        CompletedTiers = completedTiers,
                        Progress = progress,
                        IsActiveDomain = isActive
                    });
                }
            }

            if (_codex != null)
            {
                data.UnlockedFragments.AddRange(_codex.UnlockedNumbers);
            }

            CaptureDecisions(data);
            CaptureGovernors(data);

            for (int i = 0; i < empires.Count; i++)
            {
                for (int j = i + 1; j < empires.Count; j++)
                {
                    data.Diplomacy.Statuses.Add(new DiplomaticStatusSaveData
                    {
                        EmpireAId = empires[i].Id,
                        EmpireBId = empires[j].Id,
                        Status = (int)_diplomacy.GetStatus(empires[i].Id, empires[j].Id),
                        HasTradeTreaty = _diplomacy.HasTradeTreaty(empires[i].Id, empires[j].Id)
                    });
                }
            }

            foreach (Empire observer in empires)
            {
                foreach (Empire target in empires)
                {
                    if (observer.Id == target.Id)
                    {
                        continue;
                    }

                    float opinion = _diplomacy.GetOpinion(observer.Id, target.Id);
                    if (opinion != 0f)
                    {
                        data.Diplomacy.Opinions.Add(new OpinionSaveData { ObserverId = observer.Id, TargetId = target.Id, Value = opinion });
                    }

                    if (_diplomacy.IsEmbargoing(observer.Id, target.Id))
                    {
                        data.Diplomacy.Embargoes.Add(new EmbargoSaveData { FromEmpireId = observer.Id, ToEmpireId = target.Id });
                    }
                }
            }

            return data;
        }

        private void Apply(GameSaveData data)
        {
            foreach (StarSystemSaveData systemData in data.Systems)
            {
                if (!_map.TryGetSystem(new StarSystemId(systemData.SystemId), out StarSystemState system))
                {
                    continue;
                }

                system.OwnerId = systemData.OwnerId;
                system.Population = systemData.Population;
                system.Wealth = systemData.Wealth;
                system.DevelopmentLevel = systemData.DevelopmentLevel;
                system.Stability = systemData.Stability;
            }

            foreach (BuildingSaveData buildingData in data.Buildings)
            {
                BuildingType buildingType = FindBuildingType(buildingData.BuildingTypeDisplayName);
                if (buildingType != null)
                {
                    _economy.RestoreCompletedBuilding(new StarSystemId(buildingData.SystemId), buildingType);
                }
            }

            // Seule restauration a reutiliser des methodes publiques existantes (Grant,
            // SetTaxRate) plutot qu'un Restore* dedie : sans risque puisque le tresor d'une
            // economie fraichement initialisee est toujours a zero, mais a la difference de
            // tous les autres Restore* de cette methode, Grant publie TreasuryChangedEvent.
            // Sans consequence ici (aucune UI ne reagit encore aux evenements, seulement a
            // interrogation directe en OnGUI), donc pas juge necessaire d'ajouter un
            // quatrieme chemin silencieux a IEconomyService pour ce seul cas.
            foreach (EmpireEconomySaveData empireData in data.Empires)
            {
                _economy.Grant(empireData.EmpireId, new ResourceBundle(
                    empireData.Credits, empireData.Minerals, empireData.Energy, empireData.Food, empireData.Influence));
                _economy.SetTaxRate(empireData.EmpireId, empireData.TaxRate);
            }

            // Phase 17 : vider avant de restaurer. RestoreGarrison ecrase (get-or-create), mais
            // une flotte en voyage n'a pas de cle equivalente — sans ce nettoyage, chaque
            // « Recharger » depuis le menu pause dupliquerait toutes les flottes en vol.
            _military.ClearFleetsInTransit();

            foreach (FleetInTransitSaveData fleetData in data.FleetsInTransit)
            {
                if (fleetData?.Route == null || fleetData.Route.Count < 2)
                {
                    continue;
                }

                var route = new List<StarSystemId>(fleetData.Route.Count);
                foreach (int systemId in fleetData.Route)
                {
                    route.Add(new StarSystemId(systemId));
                }

                _military.RestoreFleetInTransit(
                    fleetData.OwnerId,
                    new UnitBundle(
                        fleetData.Infantry, fleetData.Armored, fleetData.SpecialForces,
                        fleetData.Fighter, fleetData.Frigate, fleetData.Cruiser, fleetData.Battleship),
                    fleetData.FleetName,
                    new Admiral(fleetData.AdmiralName, fleetData.AdmiralAttackBonus, fleetData.AdmiralSpeedBonus, fleetData.AdmiralDefenseBonus),
                    route, fleetData.RouteIndex, new StarSystemId(fleetData.OriginSystemId),
                    FromDateData(fleetData.JourneyStartDate), FromDateData(fleetData.DepartureDate),
                    FromDateData(fleetData.LegArrivalDate), fleetData.IsRetreating);
            }

            foreach (GarrisonSaveData garrisonData in data.Garrisons)
            {
                // Sauvegarde anterieure a la Phase 15 (Version < 3) : pas de champs Amiral dans
                // le fichier, donc pas d'Amiral "tout a zero" restaure a partir des defauts
                // JsonUtility — un nouvel Amiral est genere, comme pour une toute nouvelle flotte.
                Admiral? admiral = data.Version >= 3
                    ? new Admiral(garrisonData.AdmiralName, garrisonData.AdmiralAttackBonus, garrisonData.AdmiralSpeedBonus, garrisonData.AdmiralDefenseBonus)
                    : (Admiral?)null;

                _military.RestoreGarrison(
                    new StarSystemId(garrisonData.SystemId), garrisonData.OwnerId,
                    new UnitBundle(
                        garrisonData.Infantry, garrisonData.Armored, garrisonData.SpecialForces,
                        garrisonData.Fighter, garrisonData.Frigate, garrisonData.Cruiser, garrisonData.Battleship),
                    garrisonData.FleetName, admiral);
            }

            foreach (DiplomaticStatusSaveData statusData in data.Diplomacy.Statuses)
            {
                _diplomacy.RestoreRelations(statusData.EmpireAId, statusData.EmpireBId, (DiplomaticStatus)statusData.Status, statusData.HasTradeTreaty);
            }

            foreach (OpinionSaveData opinionData in data.Diplomacy.Opinions)
            {
                _diplomacy.RestoreOpinion(opinionData.ObserverId, opinionData.TargetId, opinionData.Value);
            }

            foreach (EmbargoSaveData embargoData in data.Diplomacy.Embargoes)
            {
                _diplomacy.RestoreEmbargo(embargoData.FromEmpireId, embargoData.ToEmpireId);
            }

            foreach (ResearchProgressSaveData researchData in data.Research)
            {
                var domain = (ResearchDomain)researchData.Domain;
                _research.RestoreProgress(researchData.EmpireId, domain, researchData.CompletedTiers, researchData.Progress);

                if (researchData.IsActiveDomain)
                {
                    _research.RestoreActiveDomain(researchData.EmpireId, domain);
                }
            }

            // Sur une sauvegarde anterieure a la version 5, la liste est vide et le codex repart
            // a zero — puis se recompose des le lendemain, puisque ses regles relisent l'etat du
            // monde plutot qu'un historique. Rien a migrer, donc rien a tester de plus.
            if (_codex != null)
            {
                _codex.Restore(data.UnlockedFragments);
            }

            RestoreDecisions(data);
            RestoreGovernors(data);

            if (data.Date != null)
            {
                _clock.SetDate(new GameDate(data.Date.Year, data.Date.Month, data.Date.Day));
            }

            _clock.SetSpeed((GameSpeed)data.Speed);
        }

        private static GameDateData ToDateData(GameDate date) =>
            new GameDateData { Year = date.Year, Month = date.Month, Day = date.Day };

        private static GameDate FromDateData(GameDateData data) =>
            data == null ? GameDate.StartOfGame : new GameDate(data.Year, data.Month, data.Day);

        private static List<int> ToIdList(IReadOnlyList<StarSystemId> systemIds)
        {
            var ids = new List<int>(systemIds?.Count ?? 0);
            if (systemIds == null)
            {
                return ids;
            }

            foreach (StarSystemId systemId in systemIds)
            {
                ids.Add(systemId.Value);
            }

            return ids;
        }

        private BuildingType FindBuildingType(string displayName)
        {
            foreach (BuildingType buildingType in _economy.BuildingCatalog)
            {
                if (buildingType != null && buildingType.DisplayName == displayName)
                {
                    return buildingType;
                }
            }

            return null;
        }

        // --- Decisions (Phase 24, etape 5) --------------------------------------------------

        private void CaptureDecisions(GameSaveData data)
        {
            if (_decisions == null)
            {
                return;
            }

            data.NextDecisionId = _decisions.NextId;

            foreach (PendingDecision decision in _decisions.Pending)
            {
                data.Decisions.Add(new PendingDecisionSaveData
                {
                    Id = decision.Id,
                    Kind = (int)decision.Kind,
                    SystemId = decision.SystemId.Value,
                    RaisedOn = ToDateData(decision.RaisedOn)
                });
            }

            foreach (ScheduledConsequence consequence in _decisions.Scheduled)
            {
                data.Consequences.Add(new ScheduledConsequenceSaveData
                {
                    SystemId = consequence.SystemId.Value,
                    DueOn = ToDateData(consequence.DueOn),
                    Credits = consequence.Credits,
                    GarrisonFraction = consequence.GarrisonFraction,
                    Stability = consequence.Stability,
                    Text = consequence.Text
                });
            }
        }

        /// <summary>
        /// Reconstruit les questions en attente et les ardoises.
        /// <para>
        /// <b>Les questions sont rejouees depuis le catalogue</b>, a partir des quatre valeurs
        /// sauvegardees ; les <b>ardoises, elles, sont relues telles quelles</b>. La difference
        /// est voulue : une question n'a pas encore ete tranchee, elle doit donc afficher les
        /// couts de la version en cours ; une ardoise est le resultat d'un choix deja fait, a des
        /// couts deja annonces au joueur, et les recalculer reviendrait a changer le prix apres
        /// coup.
        /// </para>
        /// </summary>
        private void RestoreDecisions(GameSaveData data)
        {
            if (_decisions == null)
            {
                return;
            }

            var pending = new List<PendingDecision>();
            foreach (PendingDecisionSaveData saved in data.Decisions)
            {
                var systemId = new StarSystemId(saved.SystemId);
                _map.TryGetSystem(systemId, out StarSystemState system);

                pending.Add(DecisionCatalogue.Unrest(saved.Id, system, FromDateData(saved.RaisedOn)));
            }

            var scheduled = new List<ScheduledConsequence>();
            foreach (ScheduledConsequenceSaveData saved in data.Consequences)
            {
                scheduled.Add(new ScheduledConsequence(
                    new StarSystemId(saved.SystemId),
                    FromDateData(saved.DueOn),
                    saved.Credits,
                    saved.GarrisonFraction,
                    saved.Stability,
                    saved.Text));
            }

            _decisions.Restore(pending, scheduled, data.NextDecisionId);
        }

        // --- Gouverneurs (Phase 24, etape 6) ------------------------------------------------

        private void CaptureGovernors(GameSaveData data)
        {
            if (_governors == null)
            {
                return;
            }

            foreach (StarSystemId systemId in _governors.RememberedSystems)
            {
                Governor governor = _governors.GetGovernor(systemId);
                if (governor == null)
                {
                    continue;
                }

                foreach (GovernorFact fact in governor.Memory)
                {
                    data.GovernorMemory.Add(new GovernorFactSaveData
                    {
                        SystemId = systemId.Value,
                        Kind = (int)fact.Kind,
                        On = ToDateData(fact.On)
                    });
                }
            }
        }

        /// <summary>
        /// Redonne a chaque gouverneur les faits qu'il avait retenus.
        /// <para>
        /// Les faits arrivent a plat : on les regroupe par systeme avant de les rendre, sinon
        /// chaque appel a <c>RestoreMemory</c> ecraserait le precedent.
        /// </para>
        /// </summary>
        private void RestoreGovernors(GameSaveData data)
        {
            if (_governors == null)
            {
                return;
            }

            var bySystem = new Dictionary<int, List<GovernorFact>>();

            foreach (GovernorFactSaveData saved in data.GovernorMemory)
            {
                if (!bySystem.TryGetValue(saved.SystemId, out List<GovernorFact> facts))
                {
                    facts = new List<GovernorFact>();
                    bySystem[saved.SystemId] = facts;
                }

                facts.Add(new GovernorFact((GovernorFactKind)saved.Kind, FromDateData(saved.On)));
            }

            foreach (KeyValuePair<int, List<GovernorFact>> entry in bySystem)
            {
                _governors.RestoreMemory(new StarSystemId(entry.Key), entry.Value);
            }
        }
    }
}
