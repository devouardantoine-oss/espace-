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
    /// <b>Pas besoin d'adjacence :</b> contrairement a <see cref="Espace.Gameplay.Military.MilitaryDecisionMaker"/>
    /// (limite aux voisins directs faute de deplacement de flotte multi-sauts), l'espionnage
    /// n'implique aucun trajet physique — ce module considere donc tous les autres empires du
    /// <c>EmpireRegistry</c>, pas seulement les voisins de carte.
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

                StarSystemId? targetSystemId = FindPrimarySystemId(target.Id, map);
                if (targetSystemId == null)
                {
                    continue;
                }

                float counterPower = espionage.GetCounterEspionagePower(target.Id, targetSystemId.Value);
                if (ownPower < counterPower * profile.EspionageThreshold.Value)
                {
                    continue;
                }

                if (TryExecuteMission(empire.Id, target.Id, targetSystemId.Value, profile.PreferredEspionageMission, espionage))
                {
                    return;
                }
            }
        }

        private static StarSystemId? FindPrimarySystemId(int empireId, GalaxyMap map)
        {
            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId == empireId)
                {
                    return system.Id;
                }
            }

            return null;
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
