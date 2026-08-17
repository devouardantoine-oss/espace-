using System.Collections.Generic;
using UnityEngine;

namespace Espace.Gameplay.People
{
    /// <summary>
    /// Comment se calcule la loyaute d'un gouverneur (Phase 24, etape 6).
    /// <para>
    /// <b>Fonction pure, et c'est ce qui la rend verifiable.</b> Elle ne connait que trois
    /// nombres et une liste de faits : aucun service, aucune scene. Les seuils et les poids sont
    /// des constantes nommees plutot que des nombres semes dans le code, parce que ce sont eux
    /// qu'il faudra equilibrer et non la formule.
    /// </para>
    /// <para>
    /// <b>Trois contributions, dans cet ordre d'importance.</b> Le temperament donne le point de
    /// depart ; les <b>faits retenus</b> pesent le plus, puisque ce sont les actes du joueur ; la
    /// <b>stabilite du monde</b> tire doucement, parce qu'on gouverne mal une population en
    /// colere meme quand le suzerain est irreprochable.
    /// </para>
    /// <para>
    /// <b>Le joueur peut toujours se racheter.</b> La memoire ne retient que trois faits (voir
    /// <see cref="Governor.MemorySize"/>) : trois bonnes decisions effacent trois mauvaises. Sans
    /// ce plafond, une seule repression rendrait un gouverneur definitivement perdu, et le
    /// systeme cesserait d'etre une relation pour devenir une punition.
    /// </para>
    /// </summary>
    public static class LoyaltyModel
    {
        /// <summary>Sous ce seuil, le gouverneur part. Voir <c>GovernorService</c>.</summary>
        public const float DefectionThreshold = 0.25f;

        /// <summary>Sous ce seuil, il hesite encore — et le joueur doit pouvoir le voir venir.</summary>
        public const float WaveringThreshold = 0.45f;

        /// <summary>Part de la loyaute tiree par la stabilite du monde gouverne.</summary>
        public const float StabilityWeight = 0.25f;

        /// <summary>Poids d'un fait retenu, par nature.</summary>
        public static float WeightOf(GovernorFactKind kind)
        {
            switch (kind)
            {
                // Envoyer la troupe contre les siens est le geste le plus couteux du jeu envers
                // un gouverneur. Il obeit, et il n'oublie pas.
                case GovernorFactKind.Repressed: return -0.20f;

                case GovernorFactKind.Conceded: return +0.10f;
                case GovernorFactKind.Ignored: return -0.15f;
                case GovernorFactKind.Sabotaged: return -0.05f;
                case GovernorFactKind.Defended: return +0.15f;
                case GovernorFactKind.Abandoned: return -0.25f;

                default: return 0f;
            }
        }

        /// <summary>
        /// Loyaute actuelle, entre 0 et 1.
        /// </summary>
        /// <param name="baseLoyalty">Temperament du gouverneur.</param>
        /// <param name="memory">Faits retenus. Peut etre nulle.</param>
        /// <param name="stability">Stabilite du monde gouverne, de 0 a 1.</param>
        public static float Compute(float baseLoyalty, IReadOnlyList<GovernorFact> memory, float stability)
        {
            float loyalty = baseLoyalty;

            if (memory != null)
            {
                for (int i = 0; i < memory.Count; i++)
                {
                    loyalty += WeightOf(memory[i].Kind);
                }
            }

            // La stabilite tire vers elle plutot que de s'ajouter : un monde calme rassure un
            // gouverneur inquiet, un monde en feu inquiete un gouverneur serein, et ni l'un ni
            // l'autre ne suffit a lui seul a le faire partir.
            loyalty = Mathf.Lerp(loyalty, Mathf.Clamp01(stability), StabilityWeight);

            return Mathf.Clamp01(loyalty);
        }

        /// <summary>Vrai si le gouverneur est en train de partir.</summary>
        public static bool WouldDefect(float loyalty)
        {
            return loyalty < DefectionThreshold;
        }

        /// <summary>
        /// L'etat du gouverneur en trois mots, tel que la fiche du systeme l'affiche.
        /// <para>
        /// <b>Les mots comptent autant que le nombre</b>, pour la meme raison que la vigilance de
        /// l'espionnage : « loyaute 0,31 » n'apprend rien a qui ignore ou se trouve le seuil de
        /// defection. Le joueur doit voir venir un depart, sinon la defection est une punition
        /// arbitraire plutot qu'une consequence.
        /// </para>
        /// </summary>
        public static string Describe(float loyalty)
        {
            if (loyalty < DefectionThreshold)
            {
                return "sur le depart";
            }

            return loyalty < WaveringThreshold ? "vacille" : "loyal";
        }
    }
}
