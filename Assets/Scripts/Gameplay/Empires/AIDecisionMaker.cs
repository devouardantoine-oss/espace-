using Espace.Data;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Décision économique autonome d'un empire IA pour un tour de réflexion (un mois de
    /// jeu, voir <see cref="AIController"/>).
    /// <para>
    /// Fonction statique pure vis-à-vis de Unity (aucun <c>MonoBehaviour</c>, aucun
    /// <c>ServiceLocator</c>) : elle ne prend que les services dont elle a besoin en
    /// paramètre, ce qui la rend testable sans scène — même séparation que
    /// <see cref="Espace.Gameplay.Galaxy.GalaxyGenerator"/> vis-à-vis de son orchestrateur.
    /// </para>
    /// <para>
    /// <b>Une seule action par appel :</b> impôts d'abord (toujours réaffirmés), puis soit
    /// une construction, soit à défaut un investissement — jamais les deux. Une IA qui
    /// dépenserait tout son trésor d'un coup dès qu'elle le peut serait à la fois moins
    /// lisible à observer (impossible de distinguer "elle a décidé X" de "elle a tout fait
    /// d'un coup") et moins représentative d'une gestion progressive.
    /// </para>
    /// </summary>
    public static class AIDecisionMaker
    {
        /// <summary>
        /// Applique la décision de <paramref name="empire"/> pour ce tour. Ne fait rien
        /// (silencieusement) si l'empire ne possède aucun système, ou si rien n'est
        /// finançable pour le moment — ce sont des situations normales, pas des erreurs.
        /// </summary>
        public static void DecideAndAct(Empire empire, GalaxyMap map, IEconomyService economy)
        {
            StarSystemState homeSystem = FindPrimarySystem(empire, map);
            if (homeSystem == null)
            {
                return;
            }

            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);

            economy.SetTaxRate(empire.Id, profile.PreferredTaxRate);

            if (TryBuild(empire, homeSystem, economy, profile))
            {
                return;
            }

            TryInvest(empire, homeSystem, economy, profile);
        }

        /// <summary>
        /// Le seul système possédé par l'empire pour l'instant (aucune colonisation avant la
        /// Phase 6). Retourne le premier trouvé si, un jour, un empire venait à en posséder
        /// plusieurs — évite un plantage plutôt que de présupposer l'unicité en dur.
        /// </summary>
        private static StarSystemState FindPrimarySystem(Empire empire, GalaxyMap map)
        {
            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId == empire.Id)
                {
                    return system;
                }
            }

            return null;
        }

        private static bool TryBuild(Empire empire, StarSystemState system, IEconomyService economy, EmpirePersonalityProfileData profile)
        {
            BuildingType candidate = profile.PicksCheapestAffordable
                ? ChooseCheapestAffordable(system, economy)
                : ChooseByPriority(system, economy, profile.BuildPriority);

            if (candidate == null)
            {
                return false;
            }

            return economy.TryStartConstruction(system.Id, candidate, out _);
        }

        /// <summary>
        /// Premier type de bâtiment, dans l'ordre de <paramref name="priority"/>, pas encore
        /// présent sur <paramref name="system"/>, dont le développement requis est atteint et
        /// le coût couvert par le trésor de l'empire.
        /// </summary>
        private static BuildingType ChooseByPriority(StarSystemState system, IEconomyService economy, ResourceType[] priority)
        {
            if (priority == null)
            {
                return null;
            }

            foreach (ResourceType preferredResource in priority)
            {
                foreach (BuildingType building in economy.BuildingCatalog)
                {
                    if (building.ProducedResource == preferredResource && IsBuildable(system, economy, building))
                    {
                        return building;
                    }
                }
            }

            return null;
        }

        /// <summary>Parmi les bâtiments finançables et éligibles, celui dont le coût est le plus bas.</summary>
        private static BuildingType ChooseCheapestAffordable(StarSystemState system, IEconomyService economy)
        {
            BuildingType cheapest = null;

            foreach (BuildingType building in economy.BuildingCatalog)
            {
                if (!IsBuildable(system, economy, building))
                {
                    continue;
                }

                if (cheapest == null || building.CreditsCost < cheapest.CreditsCost)
                {
                    cheapest = building;
                }
            }

            return cheapest;
        }

        private static bool IsBuildable(StarSystemState system, IEconomyService economy, BuildingType building)
        {
            if (system.DevelopmentLevel < building.MinimumDevelopmentLevel)
            {
                return false;
            }

            foreach (BuildingInstance existing in economy.GetBuildings(system.Id))
            {
                if (existing.Type == building)
                {
                    return false;
                }
            }

            var cost = new ResourceBundle(credits: building.CreditsCost);
            return economy.GetTreasury(system.OwnerId).IsGreaterOrEqualTo(cost);
        }

        private static void TryInvest(Empire empire, StarSystemState system, IEconomyService economy, EmpirePersonalityProfileData profile)
        {
            float cost = economy.GetInvestmentCost(system.Id);
            var requiredTreasury = new ResourceBundle(credits: cost * profile.InvestmentEagerness);

            if (economy.GetTreasury(empire.Id).IsGreaterOrEqualTo(requiredTreasury))
            {
                economy.TryInvestInDevelopment(system.Id, out _);
            }
        }
    }
}
