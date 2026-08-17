using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.People;

namespace Espace.Gameplay.Decisions
{
    /// <summary>
    /// Les decisions que le jeu sait poser, et leurs options (Phase 24, etape 5).
    /// <para>
    /// <b>Contenu, pas comportement</b> — meme separation que <c>CodexLibrary</c> face a
    /// <c>CodexService</c>. Les textes et les couts vivent ici, la mecanique dans
    /// <see cref="DecisionOption"/>, la memoire et l'echeancier dans <see cref="DecisionService"/>.
    /// </para>
    /// <para>
    /// <b>Le seuil de troubles est plus bas que celui de l'alerte, deliberement.</b> Le panneau
    /// Empire signale un systeme des <see cref="EmpireAssessment.CriticalStability"/> (0,55) ;
    /// la decision n'arrive qu'a <see cref="UnrestThreshold"/>. Le joueur est donc <b>prevenu
    /// avant d'etre force de choisir</b>, et une decision qui tombe est toujours la consequence
    /// d'un avertissement ignore — jamais une surprise.
    /// </para>
    /// </summary>
    public static class DecisionCatalogue
    {
        /// <summary>
        /// Stabilite sous laquelle un systeme pose la question.
        /// <para>
        /// Volontairement sous <see cref="EmpireAssessment.CriticalStability"/> : voir la
        /// documentation de la classe. Un test verifie que l'ordre des deux seuils tient.
        /// </para>
        /// </summary>
        public const float UnrestThreshold = 0.30f;

        /// <summary>Delai avant que l'ardoise d'une option ne tombe. Deux mois de jeu.</summary>
        public const int DeferredDelayDays = 60;

        /// <summary>
        /// La decision de troubles, avec ses trois options.
        /// <para>
        /// <b>Aucune n'est confortable, et c'est le sujet.</b> Reprimer coute des hommes tout de
        /// suite et laisse une rancune ; ceder coute cher tout de suite et cree un precedent que
        /// les voisins reclameront ; temporiser ne coute rien en tresorerie mais laisse la
        /// situation pourrir, ce qui est le plus cher des trois si l'on se trompe.
        /// </para>
        /// </summary>
        public static PendingDecision Unrest(int id, StarSystemState system, GameDate raisedOn)
        {
            var options = new List<DecisionOption>
            {
                new DecisionOption(
                    "Reprimer",
                    "un quart de la garnison engage, stabilite +25 %",
                    "la rancune coute 15 % de stabilite dans deux mois",
                    immediateCredits: 0f,
                    immediateGarrisonFraction: 0.25f,
                    immediateStability: 0.25f,
                    deferredDelayDays: DeferredDelayDays,
                    deferredCredits: 0f,
                    deferredGarrisonFraction: 0f,
                    deferredStability: -0.15f,
                    rememberedAs: GovernorFactKind.Repressed),

                new DecisionOption(
                    "Ceder",
                    "600 Credits de concessions, stabilite +30 %",
                    "les voisins reclament autant : 900 Credits dans deux mois",
                    immediateCredits: 600f,
                    immediateGarrisonFraction: 0f,
                    immediateStability: 0.30f,
                    deferredDelayDays: DeferredDelayDays,
                    deferredCredits: 900f,
                    deferredGarrisonFraction: 0f,
                    deferredStability: 0f,
                    rememberedAs: GovernorFactKind.Conceded),

                new DecisionOption(
                    "Temporiser",
                    "rien n'est fait, stabilite -10 %",
                    "la garnison deserte : un cinquieme perdu dans deux mois",
                    immediateCredits: 0f,
                    immediateGarrisonFraction: 0f,
                    immediateStability: -0.10f,
                    deferredDelayDays: DeferredDelayDays,
                    deferredCredits: 0f,
                    deferredGarrisonFraction: 0.20f,
                    deferredStability: -0.05f,
                    rememberedAs: GovernorFactKind.Ignored)
            };

            string name = system != null ? system.Name : "un systeme";

            return new PendingDecision(
                id,
                DecisionKind.Unrest,
                system != null ? system.Id : default(StarSystemId),
                $"Troubles a {name}",
                $"La population de {name} ne repond plus a l'administration. "
                + "Le gouverneur attend des instructions, et il n'y a pas de bonne reponse.",
                options,
                raisedOn);
        }
    }
}
