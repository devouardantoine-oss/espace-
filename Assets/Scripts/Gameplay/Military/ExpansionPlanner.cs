using System;
using System.Collections.Generic;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Choix d'une cible de colonisation ou d'offensive pour un empire IA, au-dela de ses
    /// voisins immediats (Phase 18).
    /// <para>
    /// Fonctions statiques pures, meme esprit que <see cref="ColonizationRules"/> et
    /// <see cref="EncounterRules"/> : aucune dependance a <c>ServiceLocator</c> ni a un
    /// <c>MonoBehaviour</c>, testables sans scene, aucun tirage.
    /// </para>
    /// <para>
    /// <b>Un plan n'est renvoye que s'il est executable.</b> C'est la raison d'etre de cette
    /// classe autant que le choix de cible : <c>IMilitaryService.TryDetachFleet</c> n'a pas
    /// d'inverse, donc detacher une flotte puis constater que <c>TryMoveFleet</c> la refuse
    /// laisse une flotte orpheline stationnee a cote de la garnison, qui se fragmente un peu
    /// plus a chaque mois. C'etait quasi inoffensif tant que tout etait adjacent et verifie
    /// d'avance ; avec des cibles a plusieurs sauts et un plafond de deux flottes simultanees,
    /// l'echec devient le cas courant. Le detachement n'a donc lieu qu'apres validation
    /// complete de la composition, de la reserve et de la portee.
    /// </para>
    /// </summary>
    public static class ExpansionPlanner
    {
        /// <summary>Une cible validee : d'ou partir, ou aller, et avec quoi.</summary>
        public readonly struct ExpansionPlan
        {
            /// <summary>Systeme possede d'ou la flotte sera detachee.</summary>
            public readonly StarSystemState Origin;

            /// <summary>Destination finale du voyage.</summary>
            public readonly StarSystemId TargetId;

            /// <summary>Composition exacte a detacher.</summary>
            public readonly UnitBundle Force;

            /// <summary>Distance en sauts (sert uniquement au departage et aux tests).</summary>
            public readonly int Hops;

            public ExpansionPlan(StarSystemState origin, StarSystemId targetId, UnitBundle force, int hops)
            {
                Origin = origin;
                TargetId = targetId;
                Force = force;
                Hops = hops;
            }
        }

        /// <summary>
        /// Cherche le systeme libre le plus interessant a coloniser dans un rayon de
        /// <paramref name="maxHops"/> sauts, et verifie que la garnison du systeme d'ou il
        /// faudrait partir peut reellement fournir les colons.
        /// <para>
        /// <b>Trois criteres totalement ordonnes</b> — exigence en Infanterie croissante, puis
        /// nombre de sauts, puis identifiant : aucune egalite ne subsiste, donc aucun tirage
        /// n'est necessaire pour departager. L'exigence passe avant la distance parce qu'un
        /// systeme trop peuple reste hors de portee pendant des dizaines de mois, alors qu'un
        /// systeme abordable trois sauts plus loin est colonisable des maintenant : c'est
        /// l'extension a toute la galaxie de la correction de la Phase 16, qui faisait deja
        /// viser a l'IA le voisin le moins exigeant plutot que le premier trouve.
        /// </para>
        /// </summary>
        public static bool TryPlanColonization(
            int empireId, GalaxyMap map, IMilitaryService military, int maxHops, int minimumGarrisonToKeep,
            out ExpansionPlan plan)
        {
            plan = default;
            if (map == null || military == null)
            {
                return false;
            }

            Dictionary<StarSystemId, FleetRouting.Reach> reachable = FleetRouting.HopDistances(empireId, map, maxHops);
            HashSet<StarSystemId> alreadyTargeted = DestinationsInFlight(empireId, military);

            StarSystemState bestTarget = null;
            FleetRouting.Reach bestReach = default;
            int bestRequirement = int.MaxValue;

            foreach (KeyValuePair<StarSystemId, FleetRouting.Reach> entry in reachable)
            {
                if (!map.TryGetSystem(entry.Key, out StarSystemState candidate)
                    || candidate.OwnerId != StarSystemState.UnownedOwnerId
                    || alreadyTargeted.Contains(candidate.Id))
                {
                    continue;
                }

                int requirement = ColonizationRules.RequiredInfantry(candidate);
                if (!IsBetterColonizationTarget(requirement, entry.Value, candidate, bestRequirement, bestReach, bestTarget))
                {
                    continue;
                }

                bestTarget = candidate;
                bestReach = entry.Value;
                bestRequirement = requirement;
            }

            if (bestTarget == null)
            {
                return false;
            }

            // Validation : la garnison du systeme d'origine doit fournir les colons ET garder
            // sa reserve. Sans cette verification, on detacherait une flotte que le service
            // refuserait ensuite de faire partir.
            UnitBundle garrison = military.GetGarrison(bestReach.Origin, empireId);
            if (garrison.Infantry < bestRequirement || garrison.TotalCount - bestRequirement < minimumGarrisonToKeep)
            {
                return false;
            }

            if (!map.TryGetSystem(bestReach.Origin, out StarSystemState origin))
            {
                return false;
            }

            plan = new ExpansionPlan(origin, bestTarget.Id, UnitBundle.Of(UnitType.Infantry, bestRequirement), bestReach.Hops);
            return true;
        }

        /// <summary>
        /// Destinations deja visees par une flotte de l'empire actuellement en voyage.
        /// <para>
        /// Avec un plafond de deux flottes simultanees, viser deux fois le meme systeme libre
        /// gaspillerait la moitie de la capacite d'expansion de l'empire : la seconde flotte
        /// arriverait sur un systeme devenu le sien et se contenterait de renforcer la garnison
        /// de la premiere. Ce sont ses <b>propres</b> flottes que l'IA consulte ici : aucune
        /// information qu'elle ne possede pas legitimement, contrairement a un balayage des
        /// mouvements adverses.
        /// </para>
        /// </summary>
        private static HashSet<StarSystemId> DestinationsInFlight(int empireId, IMilitaryService military)
        {
            var destinations = new HashSet<StarSystemId>();

            foreach (Fleet fleet in military.GetFleetsForEmpire(empireId))
            {
                if (fleet != null && fleet.Status != FleetStatus.Stationed && fleet.DestinationSystemId.HasValue)
                {
                    destinations.Add(fleet.DestinationSystemId.Value);
                }
            }

            return destinations;
        }

        /// <summary>Compare deux candidats a la colonisation selon les trois criteres ordonnes.</summary>
        private static bool IsBetterColonizationTarget(
            int requirement, FleetRouting.Reach reach, StarSystemState candidate,
            int bestRequirement, FleetRouting.Reach bestReach, StarSystemState bestTarget)
        {
            if (bestTarget == null)
            {
                return true;
            }

            if (requirement != bestRequirement)
            {
                return requirement < bestRequirement;
            }

            if (reach.Hops != bestReach.Hops)
            {
                return reach.Hops < bestReach.Hops;
            }

            return candidate.Id.Value < bestTarget.Id.Value;
        }

        /// <summary>
        /// Cherche le systeme <b>ennemi le moins defendu</b> atteignable dans un rayon de
        /// <paramref name="maxHops"/> sauts, et compose une force d'attaque qui laisse
        /// <paramref name="minimumGarrisonToKeep"/> unites a domicile.
        /// <para>
        /// Ne considere que les empires avec qui la guerre est <b>deja declaree</b> : le verrou
        /// de la Phase 7 est inchange, decider <i>qui</i> attaquer reste le role de
        /// <see cref="Espace.Gameplay.Diplomacy.DiplomacyDecisionMaker"/>.
        /// </para>
        /// <para>
        /// Viser le moins defendu plutot que le premier trouve est le pendant offensif de la
        /// regle de colonisation : sans cela l'IA se cassait indefiniment les dents sur le
        /// premier voisin de sa liste, meme quand un systeme sans garnison etait a portee.
        /// </para>
        /// <para>
        /// <b>Contrairement a la colonisation, les cibles deja visees ne sont pas exclues :</b>
        /// deux flottes convergeant vers le meme systeme libre gaspillent une capacite
        /// d'expansion, mais deux vagues successives sur le meme systeme ennemi sont une
        /// concentration de force parfaitement legitime.
        /// </para>
        /// </summary>
        public static bool TryPlanOffensive(
            int empireId, GalaxyMap map, IMilitaryService military, IDiplomacyService diplomacy,
            int maxHops, int minimumGarrisonToKeep, out ExpansionPlan plan)
        {
            plan = default;
            if (map == null || military == null || diplomacy == null)
            {
                return false;
            }

            Dictionary<StarSystemId, FleetRouting.Reach> reachable = FleetRouting.HopDistances(empireId, map, maxHops);

            StarSystemState bestTarget = null;
            FleetRouting.Reach bestReach = default;
            float bestDefense = float.MaxValue;

            foreach (KeyValuePair<StarSystemId, FleetRouting.Reach> entry in reachable)
            {
                if (!map.TryGetSystem(entry.Key, out StarSystemState candidate)
                    || candidate.OwnerId == StarSystemState.UnownedOwnerId
                    || candidate.OwnerId == empireId
                    || diplomacy.GetStatus(empireId, candidate.OwnerId) != DiplomaticStatus.War)
                {
                    continue;
                }

                float defense = military.EstimatePower(military.GetGarrison(candidate.Id, candidate.OwnerId));
                if (!IsBetterOffensiveTarget(defense, entry.Value, candidate, bestDefense, bestReach, bestTarget))
                {
                    continue;
                }

                bestTarget = candidate;
                bestReach = entry.Value;
                bestDefense = defense;
            }

            if (bestTarget == null)
            {
                return false;
            }

            UnitBundle garrison = military.GetGarrison(bestReach.Origin, empireId);
            if (garrison.TotalCount < minimumGarrisonToKeep + 1)
            {
                return false;
            }

            // Sans Infanterie, une victoire ne capturerait rien (verrou d'invasion, Phase 16) :
            // autant garder ses unites plutot qu'engager une offensive sterile.
            if (garrison.Infantry <= 0)
            {
                return false;
            }

            UnitBundle force = SplitAttackForce(garrison, minimumGarrisonToKeep);
            if (force.IsEmpty)
            {
                return false;
            }

            if (!map.TryGetSystem(bestReach.Origin, out StarSystemState origin))
            {
                return false;
            }

            plan = new ExpansionPlan(origin, bestTarget.Id, force, bestReach.Hops);
            return true;
        }

        /// <summary>Compare deux candidats offensifs : defense la plus faible, puis distance, puis identifiant.</summary>
        private static bool IsBetterOffensiveTarget(
            float defense, FleetRouting.Reach reach, StarSystemState candidate,
            float bestDefense, FleetRouting.Reach bestReach, StarSystemState bestTarget)
        {
            if (bestTarget == null)
            {
                return true;
            }

            // Comparaison a epsilon : deux garnisons identiques doivent tomber dans le
            // departage par distance, pas dependre d'un bruit de virgule flottante.
            if (Math.Abs(defense - bestDefense) > 0.001f)
            {
                return defense < bestDefense;
            }

            if (reach.Hops != bestReach.Hops)
            {
                return reach.Hops < bestReach.Hops;
            }

            return candidate.Id.Value < bestTarget.Id.Value;
        }

        /// <summary>
        /// Garde <paramref name="reserveCount"/> unites a domicile en priorisant l'Infanterie
        /// (la moins utile au combat), envoie le reste — les unites les plus fortes en premier.
        /// Generique sur <see cref="UnitTypes.All"/> : l'ordre de l'enumeration
        /// <see cref="UnitType"/> va volontairement du moins utile a l'attaque (Infanterie) au
        /// plus puissant (Cuirasse).
        /// <para>
        /// <b>Au moins une Infanterie part toujours a l'attaque :</b> depuis que l'occupation
        /// d'un systeme conquis exige de l'Infanterie survivante (Phase 16), reserver
        /// <i>toute</i> l'Infanterie a domicile produirait des forces incapables de capturer
        /// quoi que ce soit — victoires steriles en boucle. La reserve n'en prend donc jamais la
        /// derniere unite.
        /// </para>
        /// </summary>
        public static UnitBundle SplitAttackForce(UnitBundle garrison, int reserveCount)
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
