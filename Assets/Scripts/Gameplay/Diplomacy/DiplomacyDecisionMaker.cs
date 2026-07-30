using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;

namespace Espace.Gameplay.Diplomacy
{
    /// <summary>
    /// Decision diplomatique autonome d'un empire IA pour un tour de reflexion (un mois de
    /// jeu, voir <c>AIController</c>).
    /// <para>
    /// Fonction statique pure vis-a-vis de Unity, meme esprit que
    /// <see cref="Espace.Gameplay.Military.MilitaryDecisionMaker"/> : aucune dependance a
    /// <c>ServiceLocator</c> ni a un <c>MonoBehaviour</c>, testable sans scene.
    /// </para>
    /// <para>
    /// <b>Une seule action par appel</b>, dans cet ordre : proposer la paix a un voisin en
    /// guerre si le rapport de force devient defavorable (lassitude), sinon declarer la
    /// guerre a un voisin faible si la personnalite est agressive, sinon proposer un pacte de
    /// non-agression (ou une alliance si un pacte est deja en vigueur et la relation
    /// excellente) a un voisin apprecie.
    /// </para>
    /// <para>
    /// <b>Appele avant <see cref="MilitaryDecisionMaker"/> dans <c>AIController</c></b> (ordre
    /// Economie -> Diplomatie -> Militaire) : une guerre declaree ce mois-ci peut donc etre
    /// exploitee par l'armee ce meme mois, sans attendre le mois suivant.
    /// </para>
    /// </summary>
    public static class DiplomacyDecisionMaker
    {
        /// <summary>
        /// Opinion supplementaire, au-dela du seuil de proposition de pacte de la
        /// personnalite, a partir de laquelle une Alliance est proposee plutot qu'un simple
        /// Pacte de non-agression (une relation deja pacifique doit encore s'ameliorer avant
        /// l'engagement plus fort d'une alliance).
        /// </summary>
        private const float AllianceOpinionBonus = 25f;

        public static void DecideAndAct(Empire empire, GalaxyMap map, IMilitaryService military, IDiplomacyService diplomacy)
        {
            StarSystemState homeSystem = FindPrimarySystem(empire, map);
            if (homeSystem == null)
            {
                return;
            }

            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);

            if (TryProposePeace(empire, homeSystem, map, military, diplomacy, profile))
            {
                return;
            }

            if (profile.AggressionThreshold != null && TryDeclareWar(empire, homeSystem, map, military, diplomacy, profile))
            {
                return;
            }

            TryProposePact(empire, homeSystem, map, diplomacy, profile);
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

        /// <summary>Propose la paix au premier voisin en guerre dont la puissance releguerait la sienne sous le seuil de lassitude de la personnalite.</summary>
        private static bool TryProposePeace(
            Empire empire, StarSystemState system, GalaxyMap map, IMilitaryService military, IDiplomacyService diplomacy, EmpirePersonalityProfileData profile)
        {
            float ownPower = military.EstimatePower(military.GetGarrison(system.Id, empire.Id));

            foreach (StarSystemId neighborId in map.GetNeighbors(system.Id))
            {
                if (!map.TryGetSystem(neighborId, out StarSystemState neighbor)
                    || neighbor.OwnerId == StarSystemState.UnownedOwnerId
                    || neighbor.OwnerId == empire.Id
                    || diplomacy.GetStatus(empire.Id, neighbor.OwnerId) != DiplomaticStatus.War)
                {
                    continue;
                }

                float enemyPower = military.EstimatePower(military.GetGarrison(neighborId, neighbor.OwnerId));
                if (enemyPower > 0f && ownPower / enemyPower >= profile.PeacePowerRatioThreshold)
                {
                    continue;
                }

                if (diplomacy.TrySubmitProposal(empire.Id, neighbor.OwnerId, ProposalType.PeaceTreaty, default, default, null, null, out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Declare la guerre au premier voisin en Paix simple dont la puissance estimee est ecrasee par la sienne.</summary>
        private static bool TryDeclareWar(
            Empire empire, StarSystemState system, GalaxyMap map, IMilitaryService military, IDiplomacyService diplomacy, EmpirePersonalityProfileData profile)
        {
            float ownPower = military.EstimatePower(military.GetGarrison(system.Id, empire.Id));

            foreach (StarSystemId neighborId in map.GetNeighbors(system.Id))
            {
                if (!map.TryGetSystem(neighborId, out StarSystemState neighbor)
                    || neighbor.OwnerId == StarSystemState.UnownedOwnerId
                    || neighbor.OwnerId == empire.Id
                    || diplomacy.GetStatus(empire.Id, neighbor.OwnerId) != DiplomaticStatus.Peace)
                {
                    continue;
                }

                float enemyPower = military.EstimatePower(military.GetGarrison(neighborId, neighbor.OwnerId));
                if (enemyPower > 0f && ownPower < enemyPower * profile.AggressionThreshold.Value)
                {
                    continue;
                }

                if (diplomacy.TryDeclareWar(empire.Id, neighbor.OwnerId, out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Propose un Pacte de non-agression (ou une Alliance si un pacte est deja en vigueur et la relation excellente) au premier voisin suffisamment apprecie.</summary>
        private static bool TryProposePact(
            Empire empire, StarSystemState system, GalaxyMap map, IDiplomacyService diplomacy, EmpirePersonalityProfileData profile)
        {
            foreach (StarSystemId neighborId in map.GetNeighbors(system.Id))
            {
                if (!map.TryGetSystem(neighborId, out StarSystemState neighbor)
                    || neighbor.OwnerId == StarSystemState.UnownedOwnerId
                    || neighbor.OwnerId == empire.Id)
                {
                    continue;
                }

                float opinion = diplomacy.GetOpinion(empire.Id, neighbor.OwnerId);
                if (opinion < profile.ProactivePactOpinionThreshold)
                {
                    continue;
                }

                DiplomaticStatus status = diplomacy.GetStatus(empire.Id, neighbor.OwnerId);
                ProposalType type;
                if (status == DiplomaticStatus.NonAggressionPact && opinion >= profile.ProactivePactOpinionThreshold + AllianceOpinionBonus)
                {
                    type = ProposalType.Alliance;
                }
                else if (status == DiplomaticStatus.Peace)
                {
                    type = ProposalType.NonAggressionPact;
                }
                else
                {
                    continue;
                }

                if (diplomacy.TrySubmitProposal(empire.Id, neighbor.OwnerId, type, default, default, null, null, out _))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
