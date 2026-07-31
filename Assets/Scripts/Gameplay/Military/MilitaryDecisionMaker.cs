using System;
using System.Collections.Generic;
using Espace.Data;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Decision militaire autonome d'un empire IA pour un tour de reflexion (un mois de jeu,
    /// voir <c>AIController</c>).
    /// <para>
    /// Fonction statique pure vis-a-vis de Unity, meme esprit que
    /// <see cref="Espace.Gameplay.Empires.AIDecisionMaker"/> (economie) : aucune dependance a
    /// <c>ServiceLocator</c> ni a un <c>MonoBehaviour</c>, testable sans scene.
    /// </para>
    /// <para>
    /// <b>Une seule action par appel :</b> recrutement d'abord (tant qu'une garnison est sous
    /// sa cible), puis a defaut colonisation, puis a defaut seulement une tentative d'attaque —
    /// jamais deux actions le meme mois. Garde toujours au moins
    /// <see cref="MinimumGarrisonToKeep"/> unites sur le systeme de depart avant de detacher
    /// quoi que ce soit : un empire ne se laisse jamais totalement sans defense de son propre
    /// chef.
    /// </para>
    /// <para>
    /// <b>Depuis la Phase 7, n'attaque plus que les empires deja en guerre :</b> le choix de
    /// <i>qui</i> attaquer (rapport de puissance, seuil d'agressivite) revient a
    /// <see cref="Espace.Gameplay.Diplomacy.DiplomacyDecisionMaker"/>, appele avant celui-ci
    /// dans <c>AIController</c> et seul habilite a declarer la guerre. Ce module ne fait
    /// qu'exploiter militairement un etat de guerre deja declare.
    /// </para>
    /// <para>
    /// <b>Depuis la Phase 18, l'IA gere tout son territoire et vise loin.</b> Elle recrute sur
    /// le systeme possede dont la garnison est <i>la plus loin de sa cible</i> (voir
    /// <see cref="TryRecruit"/>) plutot que toujours sur le meme, et confie le choix d'une cible de colonisation ou
    /// d'offensive a <see cref="ExpansionPlanner"/>, qui la cherche dans tout le rayon
    /// d'expansion de la personnalite au lieu des seuls voisins directs. Sans cela l'IA cessait
    /// definitivement de s'etendre des que les voisins immediats de sa capitale etaient pris,
    /// et ses colonies n'avaient jamais la moindre garnison.
    /// </para>
    /// </summary>
    public static class MilitaryDecisionMaker
    {
        /// <summary>Unites minimales gardees en garnison avant d'en detacher pour coloniser ou attaquer.</summary>
        private const int MinimumGarrisonToKeep = 2;

        /// <summary>
        /// Diviseur applique a <see cref="EmpirePersonalityProfileData.TargetGarrisonSize"/> sur
        /// les systemes autres que la capitale (Phase 18).
        /// <para>
        /// Appliquer la cible pleine partout multiplierait l'entretien par le nombre de
        /// systemes : un Militariste a cinq systemes viserait quarante unites, soit jusqu'a
        /// deux cents credits par jour, et se ruinerait. La capitale reste le bastion, les
        /// colonies n'ont qu'une garnison de tenue.
        /// </para>
        /// </summary>
        private const int ColonyGarrisonDivisor = 2;

        public static void DecideAndAct(Empire empire, GalaxyMap map, IEconomyService economy, IMilitaryService military, IDiplomacyService diplomacy)
        {
            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);

            if (TryRecruit(empire, map, economy, military, diplomacy, profile))
            {
                return;
            }

            // Le plafond de flottes en deplacement simultane est verifie une seule fois ici :
            // en dessous, ni la colonisation ni l'offensive ne peuvent aboutir, et surtout
            // aucune des deux ne doit detacher une flotte qui resterait ensuite bloquee.
            if (!military.CanDeployAnotherFleet(empire.Id))
            {
                return;
            }

            if (TryColonize(empire, map, military, profile))
            {
                return;
            }

            TryAttack(empire, map, military, diplomacy, profile);
        }

        // --- Recrutement ------------------------------------------------------------------

        /// <summary>
        /// Recrute une unite sur le systeme possede dont la garnison est <b>la plus loin de sa
        /// cible</b>.
        /// <para>
        /// <b>Le plus grand deficit, pas simplement le moins defendu (Phase 18).</b> Viser le
        /// systeme le plus faible en valeur absolue produit un blocage : il atteint sa petite
        /// cible de colonie, le recrutement s'arrete, et le systeme d'ou devrait partir la
        /// prochaine vague de colons — dont la cible est relevee par l'exigence de sa cible de
        /// colonisation — n'est jamais renforce. L'empire cesse alors toute activite. Comparer
        /// des ecarts a la cible fait converger <i>chaque</i> systeme vers la sienne.
        /// </para>
        /// <para>
        /// C'est aussi, gratuitement, le comportement defensif de la phase : un systeme vide
        /// par un depart de flotte ou decime par une bataille affiche le plus gros deficit et
        /// passe en tete. L'IA ne <i>voit</i> deliberement pas les flottes ennemies en approche —
        /// ce serait une omniscience que le joueur n'a pas, alors que le projet a justement un
        /// systeme d'espionnage pour que l'information se merite.
        /// </para>
        /// <para>
        /// <b>Cible ajustee a la colonisation (Phase 16) :</b> la cible de personnalite seule
        /// rendrait la colonisation structurellement impossible pour les profils a petite
        /// garnison — coloniser demande <see cref="MinimumGarrisonToKeep"/> + l'exigence du
        /// systeme vise, soit jusqu'a 8 unites, quand le Pacifiste ne vise que 2.
        /// </para>
        /// <para>
        /// <b>Plancher d'Infanterie (Phase 16) :</b> <see cref="ChooseRecruitType"/> choisit au
        /// prix ou a la puissance, jamais par type — un Militariste (qui prefere le plus
        /// puissant) n'aurait donc jamais la moindre Infanterie, et serait incapable de
        /// coloniser comme d'envahir.
        /// </para>
        /// </summary>
        private static bool TryRecruit(
            Empire empire, GalaxyMap map, IEconomyService economy, IMilitaryService military,
            IDiplomacyService diplomacy, EmpirePersonalityProfileData profile)
        {
            // Un seul parcours pour tout l'empire : l'exigence de colonisation la plus basse
            // atteignable depuis chaque systeme possede.
            Dictionary<StarSystemId, int> colonizationNeedByOrigin = CheapestColonizationNeedByOrigin(empire.Id, map, profile);

            StarSystemState target = null;
            int targetDeficit = 0;
            int targetNeed = 0;

            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId != empire.Id)
                {
                    continue;
                }

                colonizationNeedByOrigin.TryGetValue(system.Id, out int need);

                int effectiveTarget = TargetGarrisonFor(empire, system, map, profile);
                if (need > 0)
                {
                    effectiveTarget = Math.Max(effectiveTarget, MinimumGarrisonToKeep + need);
                }

                int deficit = effectiveTarget - military.GetGarrison(system.Id, empire.Id).TotalCount;
                if (deficit <= 0)
                {
                    continue;
                }

                // Departage sur le plus petit identifiant : aucun tirage au runtime.
                if (target == null || deficit > targetDeficit
                    || (deficit == targetDeficit && system.Id.Value < target.Id.Value))
                {
                    target = system;
                    targetDeficit = deficit;
                    targetNeed = need;
                }
            }

            if (target == null)
            {
                return false;
            }

            UnitBundle garrison = military.GetGarrison(target.Id, empire.Id);
            int infantryFloor = Math.Max(targetNeed, IsAtWarWithAnyNeighbor(empire, map, diplomacy) ? 1 : 0);

            UnitTypeDefinition choice = garrison.Infantry < infantryFloor
                ? FindInfantryType(target, economy, military) ?? ChooseRecruitType(target, economy, military, profile)
                : ChooseRecruitType(target, economy, military, profile);

            if (choice == null)
            {
                return false;
            }

            return military.TryRecruitUnits(target.Id, choice, 1, out _);
        }

        /// <summary>
        /// Cible de garnison de <paramref name="system"/> : la cible pleine de la personnalite
        /// sur la capitale, sa moitie (au moins <see cref="MinimumGarrisonToKeep"/>) ailleurs.
        /// Voir <see cref="ColonyGarrisonDivisor"/> pour la raison.
        /// </summary>
        private static int TargetGarrisonFor(Empire empire, StarSystemState system, GalaxyMap map, EmpirePersonalityProfileData profile)
        {
            StarSystemState capital = EmpireHoldings.Capital(empire.Id, map);
            if (capital != null && capital.Id == system.Id)
            {
                return profile.TargetGarrisonSize;
            }

            return Math.Max(MinimumGarrisonToKeep, profile.TargetGarrisonSize / ColonyGarrisonDivisor);
        }

        /// <summary>
        /// Pour chaque systeme possede, l'exigence en Infanterie la plus basse parmi les
        /// systemes libres que ce systeme est le mieux place pour coloniser (un seul parcours
        /// en largeur pour tout l'empire).
        /// <para>
        /// Indexe par l'origine renvoyee par <see cref="FleetRouting.HopDistances"/> : inutile
        /// de faire monter la garnison d'un systeme pour une cible qu'une flotte partira
        /// d'ailleurs coloniser.
        /// </para>
        /// </summary>
        private static Dictionary<StarSystemId, int> CheapestColonizationNeedByOrigin(
            int empireId, GalaxyMap map, EmpirePersonalityProfileData profile)
        {
            var cheapestByOrigin = new Dictionary<StarSystemId, int>();

            foreach (KeyValuePair<StarSystemId, FleetRouting.Reach> entry in FleetRouting.HopDistances(empireId, map, profile.ExpansionRange))
            {
                if (!map.TryGetSystem(entry.Key, out StarSystemState candidate)
                    || candidate.OwnerId != StarSystemState.UnownedOwnerId)
                {
                    continue;
                }

                int required = ColonizationRules.RequiredInfantry(candidate);
                if (!cheapestByOrigin.TryGetValue(entry.Value.Origin, out int known) || required < known)
                {
                    cheapestByOrigin[entry.Value.Origin] = required;
                }
            }

            return cheapestByOrigin;
        }

        /// <summary>Un empire bordant l'un quelconque des systemes possedes est-il en guerre avec nous ?</summary>
        private static bool IsAtWarWithAnyNeighbor(Empire empire, GalaxyMap map, IDiplomacyService diplomacy)
        {
            foreach (int neighborEmpireId in EmpireHoldings.NeighboringEmpires(empire.Id, map))
            {
                if (diplomacy.GetStatus(empire.Id, neighborEmpireId) == DiplomaticStatus.War)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>L'Infanterie du catalogue, si le systeme peut la recruter et que le tresor la couvre.</summary>
        private static UnitTypeDefinition FindInfantryType(StarSystemState system, IEconomyService economy, IMilitaryService military)
        {
            ResourceBundle treasury = economy.GetTreasury(system.OwnerId);

            foreach (UnitTypeDefinition unitType in military.UnitCatalog)
            {
                if (unitType == null || unitType.UnitType != UnitType.Infantry
                    || system.DevelopmentLevel < unitType.MinimumDevelopmentLevel)
                {
                    continue;
                }

                var cost = new ResourceBundle(credits: unitType.CreditsCost, minerals: unitType.MineralsCost);
                if (treasury.IsGreaterOrEqualTo(cost))
                {
                    return unitType;
                }
            }

            return null;
        }

        /// <summary>
        /// Le type d'unite le plus puissant que l'empire peut se permettre s'il prefere la
        /// force (<see cref="EmpirePersonalityProfileData.PrefersStrongestUnit"/>), sinon le
        /// moins cher — parmi ceux eligibles au developpement du systeme.
        /// </summary>
        private static UnitTypeDefinition ChooseRecruitType(
            StarSystemState system, IEconomyService economy, IMilitaryService military, EmpirePersonalityProfileData profile)
        {
            UnitTypeDefinition best = null;
            ResourceBundle treasury = economy.GetTreasury(system.OwnerId);

            foreach (UnitTypeDefinition unitType in military.UnitCatalog)
            {
                if (unitType == null || system.DevelopmentLevel < unitType.MinimumDevelopmentLevel)
                {
                    continue;
                }

                var cost = new ResourceBundle(credits: unitType.CreditsCost, minerals: unitType.MineralsCost);
                if (!treasury.IsGreaterOrEqualTo(cost))
                {
                    continue;
                }

                if (best == null)
                {
                    best = unitType;
                    continue;
                }

                bool better = profile.PrefersStrongestUnit ? unitType.Power > best.Power : unitType.CreditsCost < best.CreditsCost;
                if (better)
                {
                    best = unitType;
                }
            }

            return best;
        }

        // --- Expansion --------------------------------------------------------------------

        /// <summary>
        /// Envoie des colons vers le systeme libre le plus interessant du rayon d'expansion.
        /// <para>
        /// <b>Le plan est valide avant tout detachement</b> (voir <see cref="ExpansionPlanner"/>) :
        /// <c>TryDetachFleet</c> n'a pas d'inverse, donc detacher puis se voir refuser le
        /// depart laisserait une flotte orpheline stationnee a cote de la garnison, qui se
        /// fragmenterait un peu plus chaque mois.
        /// </para>
        /// </summary>
        private static bool TryColonize(Empire empire, GalaxyMap map, IMilitaryService military, EmpirePersonalityProfileData profile)
        {
            if (!ExpansionPlanner.TryPlanColonization(
                    empire.Id, map, military, profile.ExpansionRange, MinimumGarrisonToKeep,
                    out ExpansionPlanner.ExpansionPlan plan))
            {
                return false;
            }

            if (!military.TryDetachFleet(plan.Origin.Id, empire.Id, plan.Force, out Fleet colonizer, out _))
            {
                return false;
            }

            return military.TryMoveFleet(colonizer, plan.TargetId, out _);
        }

        /// <summary>
        /// Attaque le systeme ennemi le moins defendu du rayon d'expansion, parmi les empires
        /// avec qui la guerre est deja declaree.
        /// <para>
        /// Une personnalite pacifique ou commercante (<c>AggressionThreshold</c> nul) n'engage
        /// jamais l'offensive de son propre chef, meme si elle se retrouve en guerre parce
        /// qu'attaquee : elle se defend passivement (la resolution de bataille a lieu
        /// automatiquement des qu'une flotte ennemie entre sur son systeme).
        /// </para>
        /// </summary>
        private static bool TryAttack(
            Empire empire, GalaxyMap map, IMilitaryService military, IDiplomacyService diplomacy, EmpirePersonalityProfileData profile)
        {
            if (profile.AggressionThreshold == null)
            {
                return false;
            }

            if (!ExpansionPlanner.TryPlanOffensive(
                    empire.Id, map, military, diplomacy, profile.ExpansionRange, MinimumGarrisonToKeep,
                    out ExpansionPlanner.ExpansionPlan plan))
            {
                return false;
            }

            if (!military.TryDetachFleet(plan.Origin.Id, empire.Id, plan.Force, out Fleet fleet, out _))
            {
                return false;
            }

            return military.TryMoveFleet(fleet, plan.TargetId, out _);
        }
    }
}
