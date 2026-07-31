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

            if (TryRecruit(empire, homeSystem, economy, military, profile))
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

        private static bool TryRecruit(
            Empire empire, StarSystemState system, IEconomyService economy, IMilitaryService military, EmpirePersonalityProfileData profile)
        {
            UnitBundle garrison = military.GetGarrison(system.Id, empire.Id);
            if (garrison.TotalCount >= profile.TargetGarrisonSize)
            {
                return false;
            }

            UnitTypeDefinition choice = ChooseRecruitType(system, economy, military, profile);
            if (choice == null)
            {
                return false;
            }

            return military.TryRecruitUnits(system.Id, choice, 1, out _);
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

        private static bool TryColonizeAdjacent(Empire empire, StarSystemState system, GalaxyMap map, IMilitaryService military)
        {
            UnitBundle garrison = military.GetGarrison(system.Id, empire.Id);
            if (garrison.TotalCount < MinimumGarrisonToKeep + 1)
            {
                return false;
            }

            StarSystemId? target = FindAdjacentUnowned(system.Id, map);
            if (target == null)
            {
                return false;
            }

            UnitBundle settlers = ChooseSingleUnitToDetach(garrison);
            if (!military.TryDetachFleet(system.Id, empire.Id, settlers, out Fleet colonizer, out _))
            {
                return false;
            }

            return military.TryMoveFleet(colonizer, target.Value, out _);
        }

        private static StarSystemId? FindAdjacentUnowned(StarSystemId systemId, GalaxyMap map)
        {
            foreach (StarSystemId neighborId in map.GetNeighbors(systemId))
            {
                if (map.TryGetSystem(neighborId, out StarSystemState neighbor) && neighbor.OwnerId == StarSystemState.UnownedOwnerId)
                {
                    return neighborId;
                }
            }

            return null;
        }

        /// <summary>Une seule unite, la moins chere disponible (l'Infanterie en priorite) : dediee a coloniser, pas a combattre.</summary>
        private static UnitBundle ChooseSingleUnitToDetach(UnitBundle garrison)
        {
            foreach (UnitType type in UnitTypes.All)
            {
                if (garrison.Get(type) > 0)
                {
                    return UnitBundle.Of(type, 1);
                }
            }

            return UnitBundle.Zero;
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
        /// (la moins utile a l'attaque), envoie le reste — les unites les plus fortes en
        /// premier. Generique sur <see cref="UnitTypes.All"/> (Phase 14) : l'ordre de
        /// l'enumeration <see cref="UnitType"/> reste volontairement du moins utile a
        /// l'attaque (Infanterie) au plus puissant (Cuirasse).
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

                int taken = Math.Min(garrison.Get(type), remaining);
                reserve += UnitBundle.Of(type, taken);
                remaining -= taken;
            }

            return garrison - reserve;
        }
    }
}
