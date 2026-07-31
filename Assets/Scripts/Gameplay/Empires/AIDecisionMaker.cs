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
    /// <para>
    /// <b>Multi-système depuis la Phase 18 :</b> l'action reste unique, mais le système sur
    /// lequel elle porte est désormais <i>choisi</i> (voir <see cref="ChooseDevelopmentTarget"/>)
    /// au lieu d'être toujours le premier système trouvé. Passer à « une action par système »
    /// aurait multiplié le rythme de dépense par le nombre de systèmes et vidé le trésor d'un
    /// empire étendu ; choisir la bonne cible garde le rythme constant tout en corrigeant le
    /// vrai défaut — des colonies laissées au développement 0 et sans aucun bâtiment,
    /// indéfiniment, depuis que la colonisation fonctionne (Phase 16).
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
            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);

            StarSystemState target = ChooseDevelopmentTarget(empire, map, profile);
            if (target == null)
            {
                return;
            }

            // Le taux d'imposition est un réglage d'empire, pas de système : il est réaffirmé
            // une seule fois quelle que soit la cible retenue.
            economy.SetTaxRate(empire.Id, profile.PreferredTaxRate);

            // Aucun repli sur un autre système n'est nécessaire, et ce n'est pas un oubli : la
            // règle de rattrapage ne peut pas faire perdre un mois. Une colonie neuve ne peut
            // souvent rien construire (aucun bâtiment n'atteint son niveau de développement
            // requis), mais elle peut toujours être développée — et son investissement, coûtant
            // `(niveau + 1) × 200`, est par construction le moins cher de l'empire. Si elle
            // n'est pas finançable, aucun autre système ne l'est non plus.
            if (TryBuild(empire, target, economy, profile))
            {
                return;
            }

            TryInvest(empire, target, economy, profile);
        }

        /// <summary>
        /// Le système sur lequel porter l'effort de ce mois, ou <c>null</c> si l'empire n'en
        /// possède aucun (situation normale après une conquête totale, pas une erreur).
        /// <para>
        /// <b>Rattrapage par défaut, capitale pour le Militariste</b> (Phase 18). Le rattrapage
        /// — viser le système possédé le moins développé — est aussi la stratégie la moins
        /// chère, puisque <c>GetInvestmentCost</c> vaut <c>(niveau + 1) × coût</c> : un système
        /// en retard coûte toujours moins à faire progresser que celui qui est déjà en tête.
        /// Le Militariste fait exception parce que ses meilleures unités exigent un
        /// <c>MinimumDevelopmentLevel</c> de 4 : cinq systèmes médiocres ne lui donneraient
        /// jamais un seul Cuirassé.
        /// </para>
        /// <para>
        /// Départage sur le plus petit identifiant, comme partout ailleurs : le projet ne
        /// contient aucun tirage au runtime.
        /// </para>
        /// </summary>
        private static StarSystemState ChooseDevelopmentTarget(Empire empire, GalaxyMap map, EmpirePersonalityProfileData profile)
        {
            if (profile.DevelopsCapitalFirst)
            {
                return EmpireHoldings.Capital(empire.Id, map);
            }

            StarSystemState leastDeveloped = null;
            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId != empire.Id)
                {
                    continue;
                }

                if (leastDeveloped == null
                    || system.DevelopmentLevel < leastDeveloped.DevelopmentLevel
                    || (system.DevelopmentLevel == leastDeveloped.DevelopmentLevel && system.Id.Value < leastDeveloped.Id.Value))
                {
                    leastDeveloped = system;
                }
            }

            return leastDeveloped;
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
