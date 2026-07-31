using System.Collections.Generic;
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
    /// <para>
    /// <b>Raisonne sur des empires entiers depuis la Phase 18.</b> Jusque-la ce module
    /// comparait la garnison d'<i>un</i> systeme a la garnison d'<i>un</i> systeme adverse et
    /// ne connaissait que les voisins de ce systeme : des que les deux camps possedent
    /// plusieurs systemes, le rapport de force ainsi mesure ne veut plus rien dire, et un rival
    /// ne bordant que les colonies n'existait tout simplement pas a ses yeux — ni pacte, ni
    /// guerre, ni paix possible avec lui. Il compare desormais des
    /// <see cref="EmpireHoldings.TotalPower"/> et parcourt tous les empires limitrophes du
    /// territoire (<see cref="EmpireHoldings.NeighboringEmpires"/>). Effet de bord voulu : les
    /// ecarts deviennent plus marques, donc les declarations de guerre et les demandes de paix
    /// plus tranchees. Sur une partie ou chaque empire n'a qu'un systeme, les valeurs sont
    /// exactement les anciennes.
    /// </para>
    /// <para>
    /// <b>La declaration de guerre reste fondee sur l'adjacence :</b> un empire limitrophe est
    /// toujours joignable, puisqu'un itineraire vers un voisin direct ne compte aucune etape
    /// intermediaire et echappe donc au filtre de traversabilite de
    /// <see cref="Espace.Gameplay.Military.FleetRouting"/>. Declarer la guerre a qui l'on borde
    /// garantit une guerre reellement exploitable, sans verification d'itineraire.
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
            List<int> neighborEmpires = EmpireHoldings.NeighboringEmpires(empire.Id, map);
            if (neighborEmpires.Count == 0)
            {
                return;
            }

            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);
            float ownPower = EmpireHoldings.TotalPower(empire.Id, map, military);

            if (TryProposePeace(empire, neighborEmpires, ownPower, map, military, diplomacy, profile))
            {
                return;
            }

            if (profile.AggressionThreshold != null
                && TryDeclareWar(empire, neighborEmpires, ownPower, map, military, diplomacy, profile))
            {
                return;
            }

            TryProposePact(empire, neighborEmpires, diplomacy, profile);
        }

        /// <summary>Propose la paix au premier empire limitrophe en guerre dont la puissance relegue la sienne sous le seuil de lassitude de la personnalite.</summary>
        private static bool TryProposePeace(
            Empire empire, List<int> neighborEmpires, float ownPower, GalaxyMap map,
            IMilitaryService military, IDiplomacyService diplomacy, EmpirePersonalityProfileData profile)
        {
            foreach (int otherId in neighborEmpires)
            {
                if (diplomacy.GetStatus(empire.Id, otherId) != DiplomaticStatus.War)
                {
                    continue;
                }

                float enemyPower = EmpireHoldings.TotalPower(otherId, map, military);
                if (enemyPower > 0f && ownPower / enemyPower >= profile.PeacePowerRatioThreshold)
                {
                    continue;
                }

                if (diplomacy.TrySubmitProposal(empire.Id, otherId, ProposalType.PeaceTreaty, default, default, null, null, out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Declare la guerre au premier empire limitrophe en Paix simple dont la puissance totale est ecrasee par la sienne.</summary>
        private static bool TryDeclareWar(
            Empire empire, List<int> neighborEmpires, float ownPower, GalaxyMap map,
            IMilitaryService military, IDiplomacyService diplomacy, EmpirePersonalityProfileData profile)
        {
            foreach (int otherId in neighborEmpires)
            {
                if (diplomacy.GetStatus(empire.Id, otherId) != DiplomaticStatus.Peace)
                {
                    continue;
                }

                float enemyPower = EmpireHoldings.TotalPower(otherId, map, military);
                if (enemyPower > 0f && ownPower < enemyPower * profile.AggressionThreshold.Value)
                {
                    continue;
                }

                if (diplomacy.TryDeclareWar(empire.Id, otherId, out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Propose un Pacte de non-agression (ou une Alliance si un pacte est deja en vigueur et la relation excellente) au premier empire limitrophe suffisamment apprecie.</summary>
        private static bool TryProposePact(
            Empire empire, List<int> neighborEmpires, IDiplomacyService diplomacy, EmpirePersonalityProfileData profile)
        {
            foreach (int otherId in neighborEmpires)
            {
                float opinion = diplomacy.GetOpinion(empire.Id, otherId);
                if (opinion < profile.ProactivePactOpinionThreshold)
                {
                    continue;
                }

                DiplomaticStatus status = diplomacy.GetStatus(empire.Id, otherId);
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

                if (diplomacy.TrySubmitProposal(empire.Id, otherId, type, default, default, null, null, out _))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
