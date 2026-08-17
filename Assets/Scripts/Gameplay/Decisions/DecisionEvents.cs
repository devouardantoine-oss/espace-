using Espace.Core;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.People;

namespace Espace.Gameplay.Decisions
{
    /// <summary>
    /// Publie quand le joueur tranche une decision (Phase 24, etape 6).
    /// <para>
    /// <b>Pourquoi un evenement plutot qu'un appel direct au service des gouverneurs.</b> Les
    /// decisions n'ont pas a savoir qui s'interesse a leurs suites : demain, la diplomatie ou
    /// l'IA voudront peut-etre reagir a une repression, et rien ne devra changer ici. C'est la
    /// convention du projet depuis la Phase 1, et les trente et un evenements deja publies s'en
    /// portent bien.
    /// </para>
    /// </summary>
    public readonly struct DecisionAnsweredEvent : IGameEvent
    {
        /// <summary>Systeme concerne par la decision.</summary>
        public readonly StarSystemId SystemId;

        /// <summary>Ce que le gouverneur du monde doit en retenir, s'il doit en retenir quelque chose.</summary>
        public readonly GovernorFactKind? RememberedAs;

        public DecisionAnsweredEvent(StarSystemId systemId, GovernorFactKind? rememberedAs)
        {
            SystemId = systemId;
            RememberedAs = rememberedAs;
        }
    }
}
