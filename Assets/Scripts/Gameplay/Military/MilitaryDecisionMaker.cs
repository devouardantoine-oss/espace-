using System;
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
    /// <b>Une seule action par appel :</b> recrutement d'abord (tant que la garnison est sous
    /// sa cible), puis a defaut colonisation d'un voisin libre, puis a defaut seulement une
    /// tentative d'attaque — jamais deux actions le meme mois. Garde toujours au moins
    /// <see cref="MinimumGarrisonToKeep"/> unites a domicile avant de detacher quoi que ce
    /// soit : un empire ne se laisse jamais totalement sans defense de son propre chef.
    /// </para>
    /// <para>
    /// <b>Depuis la Phase 7, n'attaque plus que les voisins deja en guerre :</b> le choix de
    /// <i>qui</i> attaquer (rapport de puissance, seuil d'agressivite) revient desormais a
    /// <see cref="Espace.Gameplay.Diplomacy.DiplomacyDecisionMaker"/>, appele avant celui-ci
    /// dans <c>AIController</c> et seul habilite a declarer la guerre. Ce module ne fait plus
    /// qu'exploiter militairement un etat de guerre deja declare.
    /// </para>
    /// </summary>
    public static class MilitaryDecisionMaker
    {
        /// <summary>Unites minimales gardees en garnison avant d'en detacher pour coloniser ou attaquer.</summary>
        private const int MinimumGarrisonToKeep = 2;

        public static void DecideAndAct(Empire empire, GalaxyMap map, IEconomyService economy, IMilitaryService military, IDiplomacyService diplomacy)
        {
            StarSystemState homeSystem = FindPrimarySystem(empire, map);
            if (homeSystem == null)
            {
                return;
            }

            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);

            if (TryRecruit(empire, homeSystem, map, economy, military, diplomacy, profile))
            {
                return;
            }

            if (TryColonizeAdjacent(empire, homeSystem, map, military))
            {
                return;
            }

            TryAttackAdjacent(empire, homeSystem, map, military, diplomacy);
        }

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

        /// <summary>
        /// Recrute une unite si la garnison est sous sa cible.
        /// <para>
        /// <b>Cible ajustee a la colonisation (Phase 16) :</b> la cible de personnalite seule
        /// rendrait la colonisation structurellement impossible pour les profils a petite
        /// garnison — coloniser demande <see cref="MinimumGarrisonToKeep"/> + l'exigence du
        /// systeme vise, soit jusqu'a 8 unites, quand le Pacifiste ne vise que 2. La cible
        /// effective monte donc au niveau du voisin libre le moins exigeant quand il y en a un
        /// (maximum 2 + 6 = 8, toujours sous le plafond de 10 unites par flotte).
        /// </para>
        /// <para>
        /// <b>Plancher d'Infanterie (Phase 16) :</b> <see cref="ChooseRecruitType"/> choisit au
        /// prix ou a la puissance, jamais par type — un Militariste (qui prefere le plus
        /// puissant) n'aurait donc jamais la moindre Infanterie, et serait incapable de
        /// coloniser comme d'envahir. L'Infanterie est recrutee en priorite tant que la
        /// garnison n'en a pas assez pour coloniser le voisin le moins exigeant, ou au moins
        /// une unite si l'empire est en guerre avec un voisin (pour pouvoir occuper).
        /// </para>
        /// </summary>
        private static bool TryRecruit(
            Empire empire, StarSystemState system, GalaxyMap map, IEconomyService economy, IMilitaryService military,
            IDiplomacyService diplomacy, EmpirePersonalityProfileData profile)
        {
            UnitBundle garrison = military.GetGarrison(system.Id, empire.Id);

            int? cheapestColonizationNeed = FindCheapestColonizationNeed(system.Id, map);
            int effectiveTarget = profile.TargetGarrisonSize;
            if (cheapestColonizationNeed.HasValue)
            {
                effectiveTarget = Math.Max(effectiveTarget, MinimumGarrisonToKeep + cheapestColonizationNeed.Value);
            }

            if (garrison.TotalCount >= effectiveTarget)
            {
                return false;
            }

            int infantryFloor = Math.Max(
                cheapestColonizationNeed ?? 0,
                IsAtWarWithAnyNeighbor(empire, system.Id, map, diplomacy) ? 1 : 0);

            UnitTypeDefinition choice = garrison.Infantry < infantryFloor
                ? FindInfantryType(system, economy, military) ?? ChooseRecruitType(system, economy, military, profile)
                : ChooseRecruitType(system, economy, military, profile);

            if (choice == null)
            {
                return false;
            }

            return military.TryRecruitUnits(system.Id, choice, 1, out _);
        }

        /// <summary>Exigence de colonisation la plus basse parmi les voisins libres, ou <c>null</c> s'il n'y en a aucun.</summary>
        private static int? FindCheapestColonizationNeed(StarSystemId systemId, GalaxyMap map)
        {
            int? cheapest = null;

            foreach (StarSystemId neighborId in map.GetNeighbors(systemId))
            {
                if (!map.TryGetSystem(neighborId, out StarSystemState neighbor) || neighbor.OwnerId != StarSystemState.UnownedOwnerId)
                {
                    continue;
                }

                int required = ColonizationRules.RequiredInfantry(neighbor);
                if (cheapest == null || required < cheapest.Value)
                {
                    cheapest = required;
                }
            }

            return cheapest;
        }

        private static bool IsAtWarWithAnyNeighbor(Empire empire, StarSystemId systemId, GalaxyMap map, IDiplomacyService diplomacy)
        {
            foreach (StarSystemId neighborId in map.GetNeighbors(systemId))
            {
                if (map.TryGetSystem(neighborId, out StarSystemState neighbor)
                    && neighbor.OwnerId != StarSystemState.UnownedOwnerId
                    && neighbor.OwnerId != empire.Id
                    && diplomacy.GetStatus(empire.Id, neighbor.OwnerId) == DiplomaticStatus.War)
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

        /// <summary>
        /// Detache l'Infanterie necessaire vers le voisin libre le <b>moins exigeant</b>
        /// (Phase 16) — viser systematiquement le premier voisin trouve, comme avant cette
        /// phase, bloquerait l'IA indefiniment sur un systeme trop peuple pour sa garnison
        /// alors qu'un voisin abordable existe peut-etre juste a cote.
        /// </summary>
        private static bool TryColonizeAdjacent(Empire empire, StarSystemState system, GalaxyMap map, IMilitaryService military)
        {
            UnitBundle garrison = military.GetGarrison(system.Id, empire.Id);

            StarSystemId? target = null;
            int required = 0;

            foreach (StarSystemId neighborId in map.GetNeighbors(system.Id))
            {
                if (!map.TryGetSystem(neighborId, out StarSystemState neighbor) || neighbor.OwnerId != StarSystemState.UnownedOwnerId)
                {
                    continue;
                }

                int neighborRequirement = ColonizationRules.RequiredInfantry(neighbor);
                if (target == null || neighborRequirement < required)
                {
                    target = neighborId;
                    required = neighborRequirement;
                }
            }

            if (target == null)
            {
                return false;
            }

            // Assez d'Infanterie pour s'installer, et assez d'unites restantes pour ne pas
            // laisser le systeme d'origine sans defense.
            if (garrison.Infantry < required || garrison.TotalCount - required < MinimumGarrisonToKeep)
            {
                return false;
            }

            UnitBundle settlers = UnitBundle.Of(UnitType.Infantry, required);
            if (!military.TryDetachFleet(system.Id, empire.Id, settlers, out Fleet colonizer, out _))
            {
                return false;
            }

            return military.TryMoveFleet(colonizer, target.Value, out _);
        }

        /// <summary>
        /// Attaque le premier voisin avec lequel un etat de guerre est deja declare (voir
        /// <see cref="Espace.Gameplay.Diplomacy.DiplomacyDecisionMaker"/>, seul a decider
        /// <i>qui</i> attaquer). Une personnalite pacifique/commercante (<c>AggressionThreshold</c>
        /// nul) n'engage jamais l'offensive de son propre chef, meme si elle se retrouve en
        /// guerre parce qu'attaquee : elle se defend passivement (la resolution de bataille a
        /// lieu automatiquement des qu'une flotte ennemie entre sur son systeme).
        /// </summary>
        private static bool TryAttackAdjacent(
            Empire empire, StarSystemState system, GalaxyMap map, IMilitaryService military, IDiplomacyService diplomacy)
        {
            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);
            if (profile.AggressionThreshold == null)
            {
                return false;
            }

            UnitBundle garrison = military.GetGarrison(system.Id, empire.Id);
            if (garrison.TotalCount < MinimumGarrisonToKeep + 1)
            {
                return false;
            }

            // Sans Infanterie, une victoire ne capturerait rien (Phase 16) : autant garder ses
            // unites plutot que d'engager une offensive qui ne peut rien conquerir.
            if (garrison.Infantry <= 0)
            {
                return false;
            }

            foreach (StarSystemId neighborId in map.GetNeighbors(system.Id))
            {
                if (!map.TryGetSystem(neighborId, out StarSystemState neighbor)
                    || neighbor.OwnerId == StarSystemState.UnownedOwnerId
                    || neighbor.OwnerId == empire.Id
                    || diplomacy.GetStatus(empire.Id, neighbor.OwnerId) != DiplomaticStatus.War)
                {
                    continue;
                }

                UnitBundle attackForce = SplitAttackForce(garrison, MinimumGarrisonToKeep);
                if (attackForce.IsEmpty)
                {
                    continue;
                }

                if (!military.TryDetachFleet(system.Id, empire.Id, attackForce, out Fleet fleet, out _))
                {
                    continue;
                }

                if (military.TryMoveFleet(fleet, neighborId, out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Garde <paramref name="reserveCount"/> unites a domicile en priorisant l'Infanterie
        /// (la moins utile au combat), envoie le reste — les unites les plus fortes en
        /// premier. Generique sur <see cref="UnitTypes.All"/> (Phase 14) : l'ordre de
        /// l'enumeration <see cref="UnitType"/> reste volontairement du moins utile a
        /// l'attaque (Infanterie) au plus puissant (Cuirasse).
        /// <para>
        /// <b>Au moins une Infanterie part toujours a l'attaque (Phase 16) :</b> depuis que
        /// l'occupation d'un systeme conquis exige de l'Infanterie survivante, reserver
        /// <i>toute</i> l'Infanterie a domicile produirait des forces d'attaque incapables de
        /// capturer quoi que ce soit — victoires steriles en boucle. La reserve n'en prend
        /// donc jamais la derniere unite.
        /// </para>
        /// </summary>
        private static UnitBundle SplitAttackForce(UnitBundle garrison, int reserveCount)
        {
            int remaining = reserveCount;
            UnitBundle reserve = UnitBundle.Zero;

            foreach (UnitType type in UnitTypes.All)
            {
                if (remaining <= 0)
                {
                    break;
                }

                int available = garrison.Get(type);
                if (type == UnitType.Infantry)
                {
                    available = Math.Max(0, available - 1);
                }

                int taken = Math.Min(available, remaining);
                reserve += UnitBundle.Of(type, taken);
                remaining -= taken;
            }

            return garrison - reserve;
        }
    }
}
