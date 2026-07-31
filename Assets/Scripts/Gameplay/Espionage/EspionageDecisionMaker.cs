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
    /// <b>Pas besoin d'adjacence :</b> l'espionnage n'implique aucun trajet physique — ce
    /// module considere tous les autres empires du <c>EmpireRegistry</c>, pas seulement les
    /// voisins de carte, et sans se soucier du rayon d'expansion des personnalites.
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
    /// <b>S'arrete des la premiere mission reussie :</b> tente la mission preferee de la
    /// personnalite contre le premier adversaire dont le contre-espionnage estime est
    /// surmontable. Si cette tentative precise echoue pour une raison independante du rapport
    /// de puissance deja verifie (rien a voler pour un vol de technologie, tresor insuffisant),
    /// passe a l'adversaire suivant plutot que de gaspiller le mois — mais s'arrete des qu'une
    /// mission aboutit.
    /// </para>
    /// </summary>
    public static class EspionageDecisionMaker
    {
        public static void DecideAndAct(Empire empire, EmpireRegistry empireRegistry, GalaxyMap map, IEspionageService espionage)
        {
            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);
            if (profile.EspionageThreshold == null)
            {
                return;
            }

            float ownPower = espionage.GetEspionagePower(empire.Id);

            foreach (Empire target in empireRegistry.Empires)
            {
                if (target.Id == empire.Id)
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

                if (TryExecuteMission(empire.Id, target.Id, targetSystem.Id, profile.PreferredEspionageMission, espionage))
                {
                    return;
                }
            }
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
