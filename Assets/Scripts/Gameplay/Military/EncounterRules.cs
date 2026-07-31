using System.Collections.Generic;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Empires;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Regles d'une rencontre spatiale (Phase 17) : quelles issues sont possibles selon le statut
    /// diplomatique, et laquelle une IA choisit.
    /// <para>
    /// <b>Fonctions statiques pures</b>, comme <see cref="CombatResolver"/> et
    /// <see cref="ColonizationRules"/> : testables sans scene, sans service, sans aleatoire.
    /// </para>
    /// <para>
    /// <b>Aucun hachage, aucun tirage : une formule.</b> Le projet a deux mecanismes deterministes
    /// (formule pure façon <c>CombatResolver</c>, ou hachage façon <c>Admiral.Compute</c>) et
    /// c'est le premier qui convient ici. Un hachage devrait etre indexe sur
    /// <c>Fleet.Id</c>, qui <b>n'est pas stable d'un chargement de sauvegarde a l'autre</b> — le
    /// tirage serait « deterministe » sans etre reproductible. Et aucune variation aleatoire
    /// n'est necessaire : les seuils de personnalite deja en place
    /// (<see cref="EmpirePersonalityProfileData.AggressionThreshold"/>,
    /// <see cref="EmpirePersonalityProfileData.PeacePowerRatioThreshold"/>,
    /// <see cref="EmpirePersonalityProfileData.ProactivePactOpinionThreshold"/>) suffisent a
    /// decider, sans introduire la moindre constante nouvelle.
    /// </para>
    /// </summary>
    public static class EncounterRules
    {
        /// <summary>Part du tresor de la victime saisie lors d'une <see cref="EncounterOption.Piracy"/>.</summary>
        public const float PiracyTreasuryShare = 0.1f;

        /// <summary>Credits gagnes par chaque camp lors d'un <see cref="EncounterOption.Trade"/>.</summary>
        public const float TradeCreditsPerSide = 40f;

        /// <summary>Gain d'opinion d'une negociation reussie.</summary>
        public const float NegotiationOpinionGain = 5f;

        /// <summary>Gain d'opinion supplementaire d'un echange commercial.</summary>
        public const float TradeOpinionGain = 8f;

        /// <summary>Chute d'opinion subie par le pirate aux yeux de sa victime.</summary>
        public const float PiracyOpinionPenalty = 25f;

        /// <summary>
        /// Issues proposables entre deux empires selon leur statut diplomatique.
        /// <para>
        /// En guerre : combattre, se replier ou negocier (une treve de circonstance reste
        /// concevable). En paix : negocier, commercer, piller ou passer son chemin. Sous pacte ou
        /// alliance, la piraterie disparait — on ne detrousse pas un allie.
        /// </para>
        /// </summary>
        public static IReadOnlyList<EncounterOption> AvailableOptions(DiplomaticStatus status)
        {
            switch (status)
            {
                case DiplomaticStatus.War:
                    return new[] { EncounterOption.Fight, EncounterOption.Withdraw, EncounterOption.Negotiate };

                case DiplomaticStatus.Alliance:
                case DiplomaticStatus.NonAggressionPact:
                    return new[] { EncounterOption.Negotiate, EncounterOption.Trade, EncounterOption.PassBy };

                default:
                    return new[] { EncounterOption.Negotiate, EncounterOption.Trade, EncounterOption.Piracy, EncounterOption.PassBy };
            }
        }

        /// <summary>
        /// Issue choisie par une flotte pilotee par l'IA.
        /// <para>
        /// <b>En guerre :</b> elle combat si son avantage de puissance atteint le seuil
        /// d'agressivite de sa personnalite ; sinon elle se replie si le rapport de force lui est
        /// nettement defavorable ; sinon elle temporise en negociant. Une personnalite qui
        /// n'attaque jamais (<c>AggressionThreshold</c> nul) ne combat jamais de son propre chef.
        /// </para>
        /// <para>
        /// <b>En paix :</b> elle commerce s'il existe un traite commercial ou si l'opinion est
        /// assez bonne ; elle pille si elle est belliqueuse, en position de force et deja mal
        /// disposee ; sinon elle passe son chemin.
        /// </para>
        /// </summary>
        public static EncounterOption ChooseForAi(
            EmpirePersonalityProfileData profile, DiplomaticStatus status,
            float ownPower, float otherPower, float opinionOfOther, bool hasTradeTreaty)
        {
            // Un adversaire de puissance nulle ne represente aucune menace : evite aussi la
            // division par zero du rapport de force.
            float powerRatio = otherPower <= 0f ? float.MaxValue : ownPower / otherPower;

            if (status == DiplomaticStatus.War)
            {
                if (profile.AggressionThreshold != null && powerRatio >= profile.AggressionThreshold.Value)
                {
                    return EncounterOption.Fight;
                }

                return powerRatio < profile.PeacePowerRatioThreshold
                    ? EncounterOption.Withdraw
                    : EncounterOption.Negotiate;
            }

            bool pactOrAlliance = status == DiplomaticStatus.Alliance || status == DiplomaticStatus.NonAggressionPact;

            if (hasTradeTreaty || opinionOfOther >= profile.ProactivePactOpinionThreshold)
            {
                return EncounterOption.Trade;
            }

            if (!pactOrAlliance
                && profile.AggressionThreshold != null
                && opinionOfOther < 0f
                && powerRatio >= profile.AggressionThreshold.Value)
            {
                return EncounterOption.Piracy;
            }

            return EncounterOption.PassBy;
        }
    }
}
