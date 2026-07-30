using System;
using Espace.Data;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Diplomacy
{
    /// <summary>
    /// Proposition diplomatique d'un empire vers un autre, en attente de reponse ou deja
    /// resolue.
    /// <para>
    /// <b>Une seule classe pour les 7 types plutot qu'une hierarchie polymorphe :</b> chaque
    /// type de proposition n'utilise qu'un sous-ensemble des quatre champs de charge utile
    /// (<see cref="OfferedResources"/>, <see cref="RequestedResources"/>,
    /// <see cref="OfferedSystemId"/>, <see cref="RequestedSystemId"/>) ; volontairement
    /// simple pour le perimetre du MVP plutot qu'une classe par type qui ajouterait de la
    /// ceremonie sans plus de securite reelle (aucun de ces champs n'est jamais lu hors de
    /// <see cref="DiplomacyService"/>, qui sait quel type elle traite).
    /// </para>
    /// <para>
    /// Immuable : une proposition ne change jamais une fois creee, seule son existence dans
    /// la file d'attente de <see cref="DiplomacyService"/> varie (retiree une fois resolue).
    /// </para>
    /// </summary>
    public sealed class DiplomaticProposal
    {
        public int Id { get; }
        public int ProposerId { get; }
        public int TargetId { get; }
        public ProposalType Type { get; }

        /// <summary>Ressources offertes par le proposeur (Echange de ressources) ou tribut exige (Ultimatum).</summary>
        public ResourceBundle OfferedResources { get; }

        /// <summary>Ressources demandees en retour au destinataire (Echange de ressources uniquement).</summary>
        public ResourceBundle RequestedResources { get; }

        /// <summary>Systeme offert par le proposeur (Echange de territoires).</summary>
        public StarSystemId? OfferedSystemId { get; }

        /// <summary>Systeme demande au destinataire (Echange de territoires).</summary>
        public StarSystemId? RequestedSystemId { get; }

        public DiplomaticProposal(
            int id, int proposerId, int targetId, ProposalType type,
            ResourceBundle offeredResources = default, ResourceBundle requestedResources = default,
            StarSystemId? offeredSystemId = null, StarSystemId? requestedSystemId = null)
        {
            if (proposerId == targetId)
            {
                throw new ArgumentException("Un empire ne peut pas s'adresser une proposition a lui-meme.", nameof(targetId));
            }

            Id = id;
            ProposerId = proposerId;
            TargetId = targetId;
            Type = type;
            OfferedResources = offeredResources;
            RequestedResources = requestedResources;
            OfferedSystemId = offeredSystemId;
            RequestedSystemId = requestedSystemId;
        }
    }
}
