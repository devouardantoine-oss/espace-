using Espace.Data;
using Espace.Gameplay.Empires;

namespace Espace.Gameplay.Diplomacy
{
    /// <summary>
    /// Decision pure « cette proposition est-elle acceptee ? », appelee par
    /// <see cref="DiplomacyService"/> chaque fois qu'une proposition cible une IA (resolution
    /// instantanee, sans file d'attente). Meme esprit que
    /// <see cref="Espace.Gameplay.Military.CombatResolver"/> : aucune dependance a Unity ni au
    /// <c>ServiceLocator</c>, testable isolement.
    /// </summary>
    public static class ProposalEvaluator
    {
        public static bool Evaluate(
            DiplomaticProposal proposal, EmpirePersonalityProfileData targetProfile,
            float targetOpinionOfProposer, float targetPower, float proposerPower)
        {
            switch (proposal.Type)
            {
                case ProposalType.Alliance:
                case ProposalType.NonAggressionPact:
                case ProposalType.TradeTreaty:
                    return targetOpinionOfProposer >= targetProfile.MinOpinionToAcceptPact;

                case ProposalType.PeaceTreaty:
                    // Accepte si le rapport de force est defavorable, ou si la relation est
                    // deja assez bonne pour vouloir arreter les combats.
                    return IsOverpowered(targetPower, proposerPower, targetProfile.PeacePowerRatioThreshold)
                        || targetOpinionOfProposer >= targetProfile.MinOpinionToAcceptPact;

                case ProposalType.ResourceExchange:
                    // Echange « equitable » : ce que la cible recoit vaut au moins ce qu'elle cede.
                    return TotalValue(proposal.OfferedResources) >= TotalValue(proposal.RequestedResources);

                case ProposalType.TerritoryExchange:
                    // Aucune valeur objective pour un systeme : seule la confiance decide.
                    return targetOpinionOfProposer >= targetProfile.MinOpinionToAcceptPact;

                case ProposalType.Ultimatum:
                    // Paie le tribut seulement si un refus (donc la guerre) serait perdant.
                    return IsOverpowered(targetPower, proposerPower, targetProfile.PeacePowerRatioThreshold);

                default:
                    return false;
            }
        }

        /// <summary>Vrai si <paramref name="ownPower"/> est nettement inferieure a <paramref name="enemyPower"/> (rapport sous le seuil).</summary>
        private static bool IsOverpowered(float ownPower, float enemyPower, float ratioThreshold)
        {
            if (enemyPower <= 0f)
            {
                return false;
            }

            return ownPower / enemyPower < ratioThreshold;
        }

        private static float TotalValue(ResourceBundle bundle)
        {
            return bundle.Credits + bundle.Minerals + bundle.Energy + bundle.Food + bundle.Influence;
        }
    }
}
