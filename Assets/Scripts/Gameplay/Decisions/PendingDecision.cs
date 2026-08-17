using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Decisions
{
    /// <summary>Ce qui a fait naitre une decision.</summary>
    public enum DecisionKind
    {
        /// <summary>Un systeme est passe sous le seuil de troubles ouverts.</summary>
        Unrest = 0
    }

    /// <summary>
    /// Une question posee au joueur, en attente de reponse (Phase 24, etape 5).
    /// <para>
    /// <b>Trois options au maximum, jamais plus.</b> Au-dela, le joueur ne compare plus, il
    /// balaie — et une decision qu'on balaie n'est pas une decision. La limite est verifiee par
    /// un test plutot que par la discipline.
    /// </para>
    /// <para>
    /// <b>Son texte n'est pas sauvegarde, il est reconstruit.</b> La sauvegarde ne retient que
    /// l'identifiant, le genre, le systeme et la date ; <see cref="DecisionCatalogue"/> reproduit
    /// le reste a l'identique, puisqu'il est deterministe a partir de ces quatre valeurs. Ecrire
    /// les libelles dans le fichier aurait fige les textes d'une version dans les parties en
    /// cours — un joueur aurait garde un ancien libelle et un nouveau cout.
    /// </para>
    /// </summary>
    public sealed class PendingDecision
    {
        /// <summary>Nombre d'options qu'une decision a le droit de proposer.</summary>
        public const int MaximumOptions = 3;

        /// <summary>Identifiant stable, attribue a la levee et conserve en sauvegarde.</summary>
        public int Id { get; }

        public DecisionKind Kind { get; }

        /// <summary>Systeme concerne.</summary>
        public StarSystemId SystemId { get; }

        /// <summary>Titre court, affiche en en-tete.</summary>
        public string Title { get; }

        /// <summary>La question, en une ou deux phrases.</summary>
        public string Question { get; }

        /// <summary>Les options, de une a <see cref="MaximumOptions"/>.</summary>
        public IReadOnlyList<DecisionOption> Options { get; }

        /// <summary>Date a laquelle la question a ete posee.</summary>
        public GameDate RaisedOn { get; }

        public PendingDecision(
            int id, DecisionKind kind, StarSystemId systemId,
            string title, string question, IReadOnlyList<DecisionOption> options, GameDate raisedOn)
        {
            Id = id;
            Kind = kind;
            SystemId = systemId;
            Title = title;
            Question = question;
            Options = options;
            RaisedOn = raisedOn;
        }
    }

    /// <summary>
    /// Une ardoise contractee par une decision, qui tombera a son echeance.
    /// <para>
    /// <b>Elle survit a la sauvegarde, sinon le differe ne serait qu'une promesse.</b> Un joueur
    /// qui enregistrerait apres avoir choisi et rechargerait avant l'echeance echapperait au
    /// cout — et l'option la plus chere a terme deviendrait la moins chere, exactement a
    /// l'envers de ce que le systeme cherche a produire.
    /// </para>
    /// </summary>
    public sealed class ScheduledConsequence
    {
        public StarSystemId SystemId { get; }
        public GameDate DueOn { get; }
        public float Credits { get; }
        public float GarrisonFraction { get; }
        public float Stability { get; }

        /// <summary>Ce que le journal annoncera quand l'ardoise tombera.</summary>
        public string Text { get; }

        public ScheduledConsequence(
            StarSystemId systemId, GameDate dueOn,
            float credits, float garrisonFraction, float stability, string text)
        {
            SystemId = systemId;
            DueOn = dueOn;
            Credits = credits;
            GarrisonFraction = garrisonFraction;
            Stability = stability;
            Text = text;
        }
    }
}
