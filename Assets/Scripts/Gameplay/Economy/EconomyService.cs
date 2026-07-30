using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Research;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Implementation par defaut de <see cref="IEconomyService"/>.
    /// <para>
    /// <b>Boucle de production :</b> s'abonne a <see cref="DayAdvancedEvent"/> (Phase 3).
    /// Chaque jour ecoule, chaque systeme possede produit des ressources selon une formule
    /// simple (voir <see cref="ComputeSystemProduction"/>) creditees au tresor de son
    /// proprietaire, et les constructions dont la date d'achevement est atteinte se
    /// terminent.
    /// </para>
    /// <para>
    /// <b>Un tresor par empire (Phase 5) :</b> un seul service gere tous les empires — une
    /// instance par empire dupliquerait l'abonnement a <see cref="DayAdvancedEvent"/> et la
    /// boucle sur tous les systemes, pour un cout en O(empires x systemes) au lieu de
    /// O(systemes). <see cref="PlayerOwnerId"/> reste l'identifiant reserve au joueur ; les
    /// empires IA recoivent n'importe quel autre entier positif (voir <c>EmpireFactory</c>).
    /// </para>
    /// </summary>
    public sealed class EconomyService : IEconomyService, IGameService
    {
        /// <summary>Identifiant d'empire reserve au joueur.</summary>
        public const int PlayerOwnerId = 0;

        private const float DefaultTaxRate = 0.25f;
        private const int MaxDevelopmentLevel = 5;
        private const float InvestmentCostPerLevel = 200f;

        /// <summary>Gisement de la ressource concernee : double sa production de base.</summary>
        private const float DepositMultiplier = 2f;

        // Poids de la formule de production journaliere (voir ComputeSystemProduction).
        // Valeurs de depart raisonnables, a affiner en Phase 12 (equilibrage).
        private const float CreditsPerWealthPoint = 0.05f;
        private const float MineralsPerPopulationPoint = 0.01f;
        private const float EnergyPerPopulationPoint = 0.008f;
        private const float FoodPerPopulationPoint = 0.012f;
        private const float InfluencePerDevelopmentPoint = 0.4f;

        private readonly GalaxyMap _map;
        private readonly IGameClock _gameClock;
        private readonly IEventBus _eventBus;
        private readonly List<BuildingType> _buildingCatalog;
        private readonly Dictionary<StarSystemId, List<BuildingInstance>> _buildingsBySystem = new Dictionary<StarSystemId, List<BuildingInstance>>();
        private readonly Dictionary<int, ResourceBundle> _treasuriesByEmpire = new Dictionary<int, ResourceBundle>();
        private readonly Dictionary<int, float> _taxRatesByEmpire = new Dictionary<int, float>();

        /// <inheritdoc />
        public ResourceBundle Treasury => GetTreasury(PlayerOwnerId);

        /// <inheritdoc />
        public float TaxRate => GetTaxRate(PlayerOwnerId);

        /// <inheritdoc />
        public IReadOnlyList<BuildingType> BuildingCatalog => _buildingCatalog;

        public EconomyService(GalaxyMap map, IGameClock gameClock, IEventBus eventBus, IReadOnlyList<BuildingType> buildingCatalog)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _buildingCatalog = new List<BuildingType>(buildingCatalog ?? Array.Empty<BuildingType>());
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _treasuriesByEmpire.Clear();
            _taxRatesByEmpire.Clear();
            _buildingsBySystem.Clear();
            _eventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _eventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
            _buildingsBySystem.Clear();
        }

        /// <inheritdoc />
        public ResourceBundle GetTreasury(int empireId)
        {
            return _treasuriesByEmpire.TryGetValue(empireId, out ResourceBundle treasury) ? treasury : ResourceBundle.Zero;
        }

        /// <inheritdoc />
        public float GetTaxRate(int empireId)
        {
            return _taxRatesByEmpire.TryGetValue(empireId, out float rate) ? rate : DefaultTaxRate;
        }

        /// <inheritdoc />
        public void SetTaxRate(float rate)
        {
            SetTaxRate(PlayerOwnerId, rate);
        }

        /// <inheritdoc />
        public void SetTaxRate(int empireId, float rate)
        {
            _taxRatesByEmpire[empireId] = rate < 0f ? 0f : rate > 1f ? 1f : rate;
        }

        /// <inheritdoc />
        public bool TrySpend(int empireId, ResourceBundle cost, out string error)
        {
            ResourceBundle treasury = GetTreasury(empireId);
            if (!treasury.IsGreaterOrEqualTo(cost))
            {
                error = "Ressources insuffisantes.";
                return false;
            }

            ResourceBundle newTreasury = treasury - cost;
            _treasuriesByEmpire[empireId] = newTreasury;
            _eventBus.Publish(new TreasuryChangedEvent(empireId, newTreasury));

            error = null;
            return true;
        }

        /// <inheritdoc />
        public void Grant(int empireId, ResourceBundle amount)
        {
            ResourceBundle newTreasury = GetTreasury(empireId) + amount;
            _treasuriesByEmpire[empireId] = newTreasury;
            _eventBus.Publish(new TreasuryChangedEvent(empireId, newTreasury));
        }

        /// <inheritdoc />
        public bool TryStartConstruction(StarSystemId systemId, BuildingType buildingType, out string error)
        {
            if (buildingType == null)
            {
                error = "Type de batiment invalide.";
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

            if (system.DevelopmentLevel < buildingType.MinimumDevelopmentLevel)
            {
                error = $"Necessite un developpement de niveau {buildingType.MinimumDevelopmentLevel}.";
                return false;
            }

            List<BuildingInstance> buildings = GetOrCreateBuildingList(systemId);
            if (buildings.Exists(b => b.Type == buildingType))
            {
                error = "Ce batiment existe deja sur ce systeme.";
                return false;
            }

            var cost = new ResourceBundle(credits: buildingType.CreditsCost);
            if (!TrySpend(system.OwnerId, cost, out error))
            {
                error = "Credits insuffisants.";
                return false;
            }

            GameDate completionDate = _gameClock.CurrentDate.AddDays(buildingType.ConstructionDurationDays);
            buildings.Add(new BuildingInstance(systemId, buildingType, completionDate));

            _eventBus.Publish(new BuildingConstructionStartedEvent(systemId, buildingType));

            error = null;
            return true;
        }

        /// <inheritdoc />
        public float GetInvestmentCost(StarSystemId systemId)
        {
            if (!_map.TryGetSystem(systemId, out StarSystemState system))
            {
                return float.PositiveInfinity;
            }

            return (system.DevelopmentLevel + 1) * InvestmentCostPerLevel;
        }

        /// <inheritdoc />
        public bool TryInvestInDevelopment(StarSystemId systemId, out string error)
        {
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

            if (system.DevelopmentLevel >= MaxDevelopmentLevel)
            {
                error = "Developpement deja maximal.";
                return false;
            }

            var cost = new ResourceBundle(credits: GetInvestmentCost(systemId));
            if (!TrySpend(system.OwnerId, cost, out error))
            {
                error = "Credits insuffisants.";
                return false;
            }

            system.DevelopmentLevel += 1;

            error = null;
            return true;
        }

        /// <inheritdoc />
        public IReadOnlyList<BuildingInstance> GetBuildings(StarSystemId systemId)
        {
            return _buildingsBySystem.TryGetValue(systemId, out List<BuildingInstance> buildings)
                ? buildings
                : Array.Empty<BuildingInstance>();
        }

        /// <inheritdoc />
        public void RestoreCompletedBuilding(StarSystemId systemId, BuildingType buildingType)
        {
            List<BuildingInstance> buildings = GetOrCreateBuildingList(systemId);
            if (buildings.Exists(b => b.Type == buildingType))
            {
                return;
            }

            var building = new BuildingInstance(systemId, buildingType, _gameClock.CurrentDate);
            building.Complete();
            buildings.Add(building);
        }

        private void OnDayAdvanced(DayAdvancedEvent dayAdvancedEvent)
        {
            CompleteFinishedConstructions(dayAdvancedEvent.Date);
            ProduceResources();
        }

        private void CompleteFinishedConstructions(GameDate date)
        {
            foreach (List<BuildingInstance> buildings in _buildingsBySystem.Values)
            {
                foreach (BuildingInstance building in buildings)
                {
                    if (building.Status == BuildingStatus.UnderConstruction && building.CompletionDate <= date)
                    {
                        building.Complete();
                        _eventBus.Publish(new BuildingCompletedEvent(building.SystemId, building.Type));
                    }
                }
            }
        }

        /// <summary>
        /// Production journaliere de tous les systemes possedes, regroupee par empire
        /// proprietaire avant d'etre creditee : deux empires produisent dans deux tresors
        /// entierement separes.
        /// </summary>
        private void ProduceResources()
        {
            var dailyByEmpire = new Dictionary<int, ResourceBundle>();

            foreach (StarSystemState system in _map.Systems)
            {
                if (system.OwnerId == StarSystemState.UnownedOwnerId)
                {
                    continue;
                }

                ResourceBundle production = ComputeSystemProduction(system);
                dailyByEmpire[system.OwnerId] = dailyByEmpire.TryGetValue(system.OwnerId, out ResourceBundle accumulated)
                    ? accumulated + production
                    : production;
            }

            foreach (KeyValuePair<int, ResourceBundle> entry in dailyByEmpire)
            {
                if (entry.Value == ResourceBundle.Zero)
                {
                    continue;
                }

                ResourceBundle newTreasury = GetTreasury(entry.Key) + entry.Value;
                _treasuriesByEmpire[entry.Key] = newTreasury;

                _eventBus.Publish(new ResourceProducedEvent(entry.Key, entry.Value));
                _eventBus.Publish(new TreasuryChangedEvent(entry.Key, newTreasury));
            }
        }

        /// <summary>
        /// Production journaliere d'un systeme : Credits depuis sa richesse (imposee),
        /// Minerais/Energie/Nourriture depuis sa population, Influence depuis son
        /// developpement — chacune doublee si le systeme dispose d'un gisement du type
        /// correspondant, et mise a l'echelle par sa stabilite (un systeme instable produit
        /// moins de tout). Les batiments completes ajoutent leur propre production, mise a
        /// l'echelle par la stabilite mais pas par le bonus de gisement (ce sont des
        /// infrastructures, pas de l'extraction brute).
        /// </summary>
        private ResourceBundle ComputeSystemProduction(StarSystemState system)
        {
            float stability = system.Stability;
            float taxRate = GetTaxRate(system.OwnerId);

            var baseProduction = new ResourceBundle(
                credits: system.Wealth * CreditsPerWealthPoint * taxRate * DepositFactor(system, ResourceType.Credits) * ResearchMultiplier(system.OwnerId, ResearchDomain.Economy),
                minerals: system.Population * MineralsPerPopulationPoint * DepositFactor(system, ResourceType.Minerals) * ResearchMultiplier(system.OwnerId, ResearchDomain.Industry),
                energy: system.Population * EnergyPerPopulationPoint * DepositFactor(system, ResourceType.Energy) * ResearchMultiplier(system.OwnerId, ResearchDomain.Energy),
                food: system.Population * FoodPerPopulationPoint * DepositFactor(system, ResourceType.Food),
                influence: system.DevelopmentLevel * InfluencePerDevelopmentPoint * DepositFactor(system, ResourceType.Influence));

            ResourceBundle buildingBonus = ResourceBundle.Zero;
            if (_buildingsBySystem.TryGetValue(system.Id, out List<BuildingInstance> buildings))
            {
                foreach (BuildingInstance building in buildings)
                {
                    if (building.Status != BuildingStatus.Completed)
                    {
                        continue;
                    }

                    buildingBonus += ResourceBundleFor(building.Type.ProducedResource, building.Type.ProductionPerDay);
                }
            }

            return baseProduction * stability + buildingBonus * stability;
        }

        /// <summary>
        /// Multiplicateur de production issu de la recherche (Phase 7) : <c>1 + bonus cumule</c>
        /// du domaine correspondant pour le proprietaire du systeme. Resolu paresseusement via
        /// <see cref="ServiceLocator"/> plutot qu'injecte au constructeur, pour la meme raison
        /// que <see cref="Espace.Gameplay.Diplomacy.DiplomacyService"/> resout
        /// <see cref="Espace.Gameplay.Military.IMilitaryService"/> paresseusement : eviter tout
        /// ordre d'initialisation impose entre <c>EconomyController</c> et
        /// <c>ResearchController</c>. Vaut 1 (aucun effet) si la recherche n'est pas encore
        /// disponible ou si l'empire n'a rien recherche dans ce domaine — Aliment et Influence
        /// n'ont volontairement aucun domaine de recherche associe.
        /// </summary>
        private static float ResearchMultiplier(int empireId, ResearchDomain domain)
        {
            return ServiceLocator.TryGet(out IResearchService research) ? 1f + research.GetBonus(empireId, domain) : 1f;
        }

        private static float DepositFactor(StarSystemState system, ResourceType type)
        {
            return Array.IndexOf(system.ResourceDeposits, type) >= 0 ? DepositMultiplier : 1f;
        }

        private static ResourceBundle ResourceBundleFor(ResourceType type, float amount)
        {
            switch (type)
            {
                case ResourceType.Credits: return new ResourceBundle(credits: amount);
                case ResourceType.Minerals: return new ResourceBundle(minerals: amount);
                case ResourceType.Energy: return new ResourceBundle(energy: amount);
                case ResourceType.Food: return new ResourceBundle(food: amount);
                case ResourceType.Influence: return new ResourceBundle(influence: amount);
                default: return ResourceBundle.Zero;
            }
        }

        private List<BuildingInstance> GetOrCreateBuildingList(StarSystemId systemId)
        {
            if (!_buildingsBySystem.TryGetValue(systemId, out List<BuildingInstance> list))
            {
                list = new List<BuildingInstance>();
                _buildingsBySystem[systemId] = list;
            }

            return list;
        }
    }
}
