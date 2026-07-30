using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Implementation par defaut de <see cref="IEconomyService"/>.
    /// <para>
    /// <b>Boucle de production :</b> s'abonne a <see cref="DayAdvancedEvent"/> (Phase 3).
    /// Chaque jour ecoule, chaque systeme possede par le joueur produit des ressources
    /// selon une formule simple (voir <see cref="ComputeSystemProduction"/>), et les
    /// constructions dont la date d'achevement est atteinte se terminent.
    /// </para>
    /// <para>
    /// <b>Un seul tresor pour l'instant :</b> <see cref="PlayerOwnerId"/> est la seule
    /// « faction » existante (les empires et leur IA arrivent en Phase 5). Le reste de
    /// l'API (couts, production, construction) est deja ecrit de façon generalisable : la
    /// Phase 5 n'aura qu'a faire de <c>OwnerId</c> un identifiant d'empire quelconque et
    /// instancier un <see cref="EconomyService"/> (ou equivalent) par empire.
    /// </para>
    /// </summary>
    public sealed class EconomyService : IEconomyService, IGameService
    {
        /// <summary>Identifiant d'empire reserve au joueur. Les autres valeurs seront les futurs empires IA (Phase 5).</summary>
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

        private ResourceBundle _treasury;
        private float _taxRate;

        /// <inheritdoc />
        public ResourceBundle Treasury => _treasury;

        /// <inheritdoc />
        public float TaxRate => _taxRate;

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
            _treasury = ResourceBundle.Zero;
            _taxRate = DefaultTaxRate;
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
        public void SetTaxRate(float rate)
        {
            _taxRate = rate < 0f ? 0f : rate > 1f ? 1f : rate;
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

            if (system.OwnerId != PlayerOwnerId)
            {
                error = "Ce systeme ne vous appartient pas.";
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
            if (!_treasury.IsGreaterOrEqualTo(cost))
            {
                error = "Credits insuffisants.";
                return false;
            }

            _treasury -= cost;
            GameDate completionDate = _gameClock.CurrentDate.AddDays(buildingType.ConstructionDurationDays);
            buildings.Add(new BuildingInstance(systemId, buildingType, completionDate));

            _eventBus.Publish(new TreasuryChangedEvent(_treasury));
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

            if (system.OwnerId != PlayerOwnerId)
            {
                error = "Ce systeme ne vous appartient pas.";
                return false;
            }

            if (system.DevelopmentLevel >= MaxDevelopmentLevel)
            {
                error = "Developpement deja maximal.";
                return false;
            }

            var cost = new ResourceBundle(credits: GetInvestmentCost(systemId));
            if (!_treasury.IsGreaterOrEqualTo(cost))
            {
                error = "Credits insuffisants.";
                return false;
            }

            _treasury -= cost;
            system.DevelopmentLevel += 1;

            _eventBus.Publish(new TreasuryChangedEvent(_treasury));

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

        private void ProduceResources()
        {
            ResourceBundle dailyTotal = ResourceBundle.Zero;

            foreach (StarSystemState system in _map.Systems)
            {
                if (system.OwnerId != PlayerOwnerId)
                {
                    continue;
                }

                dailyTotal += ComputeSystemProduction(system);
            }

            if (dailyTotal == ResourceBundle.Zero)
            {
                // Aucun systeme possede : pas de tresor a mettre a jour, et surtout pas
                // d'evenement a publier pour rien a chaque jour qui passe.
                return;
            }

            _treasury += dailyTotal;
            _eventBus.Publish(new ResourceProducedEvent(dailyTotal));
            _eventBus.Publish(new TreasuryChangedEvent(_treasury));
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

            var baseProduction = new ResourceBundle(
                credits: system.Wealth * CreditsPerWealthPoint * _taxRate * DepositFactor(system, ResourceType.Credits),
                minerals: system.Population * MineralsPerPopulationPoint * DepositFactor(system, ResourceType.Minerals),
                energy: system.Population * EnergyPerPopulationPoint * DepositFactor(system, ResourceType.Energy),
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
