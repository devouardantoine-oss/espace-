using System.Collections.Generic;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Espionage
{
    /// <summary>
    /// Decision d'espionnage autonome d'un empire IA pour un tour de reflexion (un mois de
    /// jeu, voir <c>AIController</c>).
    /// <para>
    /// Fonction statique pure vis-a-vis de Unity, meme esprit que les autres decision makers :
    /// aucune dependance a <c>ServiceLocator</c> ni a un <c>MonoBehaviour</c>, testable sans
    /// scene.
    /// </para>
    /// <para>
    /// <b>Cible la capitale de l'adversaire depuis la Phase 18</b> (<see cref="EmpireHoldings.Capital"/>,
    /// le systeme le plus developpe) plutot que le premier de ses systemes rencontre dans
    /// l'ordre de la carte. Une fois la colonisation reellement en service (Phase 16), « le
    /// premier trouve » designait un systeme arbitraire, souvent une colonie vide : le
    /// contre-espionnage y est faible, ce qui rendait les missions trop faciles, et saboter un
    /// caillou sans population n'avait aucun interet strategique.
    /// </para>
    /// <para>
    /// <b>S'arrete des la premiere mission reussie :</b> tente la mission retenue contre le
    /// premier adversaire dont le contre-espionnage estime est surmontable. Si cette tentative
    /// precise echoue pour une raison independante du rapport de puissance deja verifie (rien a
    /// voler pour un vol de technologie, tresor insuffisant), passe a l'adversaire suivant
    /// plutot que de gaspiller le mois — mais s'arrete des qu'une mission aboutit.
    /// </para>
    /// <para>
    /// <b>Ce que la situation change (Phase 22, P7).</b> Trois choses, chacune pour une raison
    /// distincte :
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Un empire en consolidation n'espionne plus du tout.</b> Depuis P6,
    /// une operation engage de l'<i>influence</i> (voir <see cref="EspionageService"/>) — la
    /// meme reserve qui paie l'administration de l'empire (P4). Comploter quand on ne tient
    /// deja pas ses comptes revient a financer une aventure avec l'argent du fonctionnement.
    /// Personne n'a eu a ecrire cette regle comme une interdiction : elle tombe du partage
    /// d'une ressource.</description></item>
    /// <item><description><b>En posture militaire, on n'espionne que ses voisins.</b> Renseigner
    /// ou affaiblir un empire qu'aucune frontiere ne rend joignable ne sert a rien — les flottes
    /// n'iront jamais. Hors posture militaire, la portee reste illimitee : l'espionnage
    /// n'implique aucun trajet physique, et un empire tranquille peut tres bien voler la
    /// technologie d'un lointain.</description></item>
    /// <item><description><b>La mission suit le besoin, pas seulement le gout</b> (voir
    /// <see cref="MissionCalledForBy"/>).</description></item>
    /// </list>
    /// <para>
    /// <b>La personnalite garde le dernier mot sur le principe meme d'espionner :</b> un
    /// Pacifiste (<c>EspionageThreshold</c> nul) ne monte aucune operation, quelle que soit sa
    /// posture, et le seuil de risque de chacun reste le sien.
    /// </para>
    /// </summary>
    public static class EspionageDecisionMaker
    {
        public static void DecideAndAct(
            Empire empire, EmpireRegistry empireRegistry, GalaxyMap map, IEspionageService espionage, EmpireAssessment assessment)
        {
            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);
            if (profile.EspionageThreshold == null)
            {
                return;
            }

            if (assessment.Posture == StrategicPosture.Consolidating)
            {
                // L'influence engagee dans une operation est celle qui manque a l'administration.
                return;
            }

            EspionageMissionType mission = MissionCalledForBy(assessment.Posture) ?? profile.PreferredEspionageMission;
            List<int> reachable = ReachableTargets(empire.Id, map, assessment.Posture);
            float ownPower = espionage.GetEspionagePower(empire.Id);

            foreach (Empire target in empireRegistry.Empires)
            {
                if (target.Id == empire.Id)
                {
                    continue;
                }

                if (reachable != null && !reachable.Contains(target.Id))
                {
                    continue;
                }

                StarSystemState targetSystem = EmpireHoldings.Capital(target.Id, map);
                if (targetSystem == null)
                {
                    continue;
                }

                float counterPower = espionage.GetCounterEspionagePower(target.Id, targetSystem.Id);
                if (ownPower < counterPower * profile.EspionageThreshold.Value)
                {
                    continue;
                }

                if (TryExecuteMission(empire.Id, target.Id, targetSystem.Id, mission, espionage))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Mission que la situation appelle, ou <c>null</c> si rien ne presse et que le gout de
        /// la personnalite doit decider.
        /// <para>
        /// Chaque association repond a un manque precis :
        /// </para>
        /// <list type="bullet">
        /// <item><description><b>Defending</b> → <see cref="EspionageMissionType.DiscoverArmies"/> :
        /// quand on est le plus faible, la premiere chose qui manque est l'information. Savoir ce
        /// qui borde vraiment sa frontiere vaut mieux que d'agacer un adversaire plus fort.</description></item>
        /// <item><description><b>Aggressive</b> → <see cref="EspionageMissionType.IncitesRevolt"/> :
        /// la revolte fait chuter la stabilite du systeme vise, et
        /// <see cref="IEspionageService.GetCounterEspionagePower"/> se calcule <i>a partir de
        /// cette stabilite</i>. Affaiblir une cible la rend donc mecaniquement plus penetrable
        /// ensuite — l'effet compose sans qu'aucun bonus n'ait ete ajoute pour l'obtenir.</description></item>
        /// <item><description><b>Expanding</b> → rien : la personnalite decide, comme avant.</description></item>
        /// <item><description><b>Consolidating</b> → sans objet, aucune operation n'est montee.</description></item>
        /// </list>
        /// </summary>
        public static EspionageMissionType? MissionCalledForBy(StrategicPosture posture)
        {
            switch (posture)
            {
                case StrategicPosture.Defending:
                    return EspionageMissionType.DiscoverArmies;

                case StrategicPosture.Aggressive:
                    return EspionageMissionType.IncitesRevolt;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Empires que la posture autorise a viser, ou <c>null</c> pour « aucune restriction ».
        /// <para>
        /// Une liste vide n'est pas <c>null</c> : elle signifie « posture militaire, mais aucun
        /// voisin » — l'empire ne monte alors aucune operation, ce qui est le comportement voulu
        /// et non un oubli.
        /// </para>
        /// </summary>
        private static List<int> ReachableTargets(int empireId, GalaxyMap map, StrategicPosture posture)
        {
            if (posture != StrategicPosture.Defending && posture != StrategicPosture.Aggressive)
            {
                return null;
            }

            return EmpireHoldings.NeighboringEmpires(empireId, map);
        }

        private static bool TryExecuteMission(
            int proposerId, int targetEmpireId, StarSystemId targetSystemId, EspionageMissionType type, IEspionageService espionage)
        {
            switch (type)
            {
                case EspionageMissionType.StealTechnology:
                    return espionage.TryStealTechnology(proposerId, targetEmpireId, out _);

                case EspionageMissionType.Sabotage:
                    return espionage.TrySabotage(proposerId, targetSystemId, out _);

                case EspionageMissionType.IncitesRevolt:
                    return espionage.TryInciteRevolt(proposerId, targetSystemId, out _);

                case EspionageMissionType.InfluenceGovernment:
                    return espionage.TryInfluenceGovernment(proposerId, targetEmpireId, out _);

                case EspionageMissionType.DiscoverArmies:
                    return espionage.TryDiscoverArmies(proposerId, targetEmpireId, targetSystemId, out _, out _);

                default:
                    return false;
            }
        }
    }
}
