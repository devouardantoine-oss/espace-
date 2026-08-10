using System.Collections.Generic;
using Espace.Data;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using UnityEngine;

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
        /// Plafond du rapport de forces. Au-dela, « je domine tres largement » et « j'ecrase »
        /// appellent la meme decision : inutile de laisser une division par une puissance quasi
        /// nulle produire des nombres astronomiques.
        /// </summary>
        private const float MaximumMilitaryRatio = 5f;

        /// <summary>
        /// Applique la décision de <paramref name="empire"/> pour ce tour. Ne fait rien
        /// (silencieusement) si l'empire ne possède aucun système, ou si rien n'est
        /// finançable pour le moment — ce sont des situations normales, pas des erreurs.
        /// </summary>
        public static void DecideAndAct(Empire empire, GalaxyMap map, IEconomyService economy, IMilitaryService military)
        {
            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);

            StarSystemState target = ChooseDevelopmentTarget(empire, map, profile);
            if (target == null)
            {
                return;
            }

            // Le taux d'imposition est un réglage d'empire, pas de système : il est réaffirmé
            // une seule fois quelle que soit la cible retenue.
            //
            // Phase 22 (P7) : il découle désormais de la SITUATION, tempérée par le caractère,
            // et non plus du seul caractère. L'ancienne version réaffirmait chaque mois la même
            // valeur — 0,20 pour un pacifiste, 0,35 pour un militariste — qu'il soit en faillite
            // ou opulent : une IA qui ne regarde pas ses comptes ne prend pas de décision, elle
            // applique une constante.
            EmpireAssessment assessment = AssessSituation(empire, map, economy, military);
            economy.SetTaxRate(empire.Id, BlendTaxRate(assessment.SuggestedTaxRate(), profile.PreferredTaxRate));

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
        /// <summary>
        /// Photographie de la situation de l'empire, a partir de ce que le joueur voit lui-meme.
        /// <para>
        /// <b>Aucun bonus cache.</b> Tresorerie, stabilite moyenne, place restante : tout est
        /// lisible sur l'interface. Une IA mieux informee que le joueur serait une triche, et
        /// une triche n'apprend rien au joueur sur le jeu.
        /// </para>
        /// <para>
        /// <b>Le rapport de forces se lit sur les seuls voisins immediats</b>, pas sur la
        /// galaxie entiere : un empire lointain trois fois plus puissant ne menace personne
        /// tant qu'aucune frontiere ne le separe de vous. C'est aussi ce que le joueur percoit
        /// en regardant sa carte, donc l'IA ne sait rien de plus que lui.
        /// </para>
        /// <para>
        /// <b>Sans voisin, le rapport vaut 1</b> — ni menace, ni proie. Un empire isole se
        /// juge donc sur sa seule situation interieure, ce qui est exactement ce qu'il devrait
        /// faire.
        /// </para>
        /// </summary>
        private static EmpireAssessment AssessSituation(Empire empire, GalaxyMap map, IEconomyService economy, IMilitaryService military)
        {
            List<StarSystemState> owned = EmpireHoldings.OwnedSystems(empire.Id, map);
            if (owned.Count == 0)
            {
                return new EmpireAssessment(0f, 0f, 1f, 0f, 0);
            }

            float stabilitySum = 0f;
            int population = 0;
            int capacity = 0;

            foreach (StarSystemState system in owned)
            {
                stabilitySum += system.Stability;
                population += system.Population;
                capacity += PopulationModel.CapacityFor(system.DevelopmentLevel);
            }

            // Depenses de reference : le cout d'administration, seule charge recurrente que ce
            // module connaisse. L'entretien de flotte lui echappe, ce qui rend l'estimation
            // optimiste — donc prudente dans le bon sens : l'IA se croira un peu plus riche
            // qu'elle ne l'est, jamais l'inverse.
            float monthlyOutgoings = Mathf.Max(1f, AdministrationModel.InfluenceUpkeep(owned.Count));
            float runway = economy.GetTreasury(empire.Id).Credits / monthlyOutgoings;

            float headroom = capacity > 0 ? 1f - population / (float)capacity : 0f;

            return new EmpireAssessment(
                runway,
                stabilitySum / owned.Count,
                MilitaryRatioAgainstNeighbours(empire.Id, map, military),
                headroom,
                owned.Count);
        }

        /// <summary>
        /// Puissance militaire de l'empire rapportee a celle de son voisin le plus fort.
        /// <para>
        /// Un empire sans armee face a un voisin arme obtient zero, ce qui le fait basculer en
        /// posture defensive — le bon reflexe. A l'inverse, un empire arme face a des voisins
        /// desarmes obtient un rapport plafonne : sans plafond, une division par une puissance
        /// quasi nulle donnerait un rapport astronomique et n'apporterait rien de plus que
        /// « je domine tres largement ».
        /// </para>
        /// </summary>
        private static float MilitaryRatioAgainstNeighbours(int empireId, GalaxyMap map, IMilitaryService military)
        {
            if (military == null)
            {
                return 1f;
            }

            List<int> neighbours = EmpireHoldings.NeighboringEmpires(empireId, map);
            if (neighbours.Count == 0)
            {
                // Ni menace ni proie : l'empire se juge sur sa seule situation interieure.
                return 1f;
            }

            float strongestNeighbour = 0f;
            foreach (int neighbourId in neighbours)
            {
                strongestNeighbour = Mathf.Max(strongestNeighbour, EmpireHoldings.TotalPower(neighbourId, map, military));
            }

            float own = EmpireHoldings.TotalPower(empireId, map, military);

            if (strongestNeighbour <= 0f)
            {
                return own > 0f ? MaximumMilitaryRatio : 1f;
            }

            return Mathf.Min(MaximumMilitaryRatio, own / strongestNeighbour);
        }

        /// <summary>
        /// Melange le taux appele par la situation et celui qu'appelle le temperament.
        /// <para>
        /// La situation pese deux tiers : un empire en faillite serre la vis quelle que soit sa
        /// personnalite, mais un pacifiste reste sensiblement plus doux qu'un militariste dans
        /// la meme situation. Le caractere cesse d'etre la reponse entiere sans cesser
        /// d'exister.
        /// </para>
        /// </summary>
        private static float BlendTaxRate(float situational, float temperamental)
        {
            return Mathf.Clamp01(situational * 0.66f + temperamental * 0.34f);
        }

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
