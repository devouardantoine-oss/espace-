using System;
using System.Collections.Generic;
using System.IO;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
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
        private readonly EmpireRegistry _empireRegistry;
        private readonly string _filePath;

        public SaveService(
            GalaxyMap map, IGameClock clock, IEconomyService economy, IMilitaryService military,
            IDiplomacyService diplomacy, IResearchService research, EmpireRegistry empireRegistry, string filePath)
        {
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

            if (data.Date != null)
            {
                _clock.SetDate(new GameDate(data.Date.Year, data.Date.Month, data.Date.Day));
            }

            _clock.SetSpeed((GameSpeed)data.Speed);
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
    }
}
