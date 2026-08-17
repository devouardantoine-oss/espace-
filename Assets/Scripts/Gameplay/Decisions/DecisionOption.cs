using Espace.Gameplay.People;

namespace Espace.Gameplay.Decisions
{
    /// <summary>
    /// Une reponse possible a une decision (Phase 24, etape 5).
    /// <para>
    /// <b>La regle absolue de ce systeme : aucune option gratuite.</b> Une option sans cout est
    /// la bonne reponse, et il n'y a plus de dilemme — seulement un bouton a appuyer. Chaque
    /// option porte donc <b>un effet immediat chiffre et un effet differe</b>, et un test verifie
    /// que c'est vrai de toutes celles du catalogue. C'est ce test, et non la relecture, qui
    /// empechera la premiere option confortable d'apparaitre le jour ou quelqu'un voudra
    /// « adoucir » une decision.
    /// </para>
    /// <para>
    /// <b>Les couts sont exprimes en Credits et en parts de garnison</b>, jamais en stabilite
    /// seule. Ce n'est pas un detail : la stabilite ne produit aujourd'hui <i>aucun</i> effet
    /// economique dans ce jeu — elle ne sert qu'a la defense contre l'espionnage, au halo de la
    /// carte et a la liste d'attention. Une option qui ne couterait que de la stabilite ne
    /// couterait donc rien du tout, et la regle ci-dessus serait respectee sur le papier
    /// seulement.
    /// </para>
    /// <para>
    /// <b>Le differe est ce qui rend le choix difficile.</b> Sans lui, on compare trois prix et
    /// on prend le moins cher. Avec lui, l'option la moins chere aujourd'hui peut etre la plus
    /// chere dans deux mois, et le joueur doit parier.
    /// </para>
    /// </summary>
    public readonly struct DecisionOption
    {
        /// <summary>Libelle du bouton. Un ou deux mots.</summary>
        public readonly string Label;

        /// <summary>Ce que l'option coute tout de suite, en clair et chiffre.</summary>
        public readonly string ImmediateText;

        /// <summary>Ce qu'elle coutera plus tard, en clair.</summary>
        public readonly string DeferredText;

        /// <summary>Credits preleves immediatement. Toujours positif ou nul.</summary>
        public readonly float ImmediateCredits;

        /// <summary>
        /// Part de la garnison perdue immediatement, de 0 a 1.
        /// <para>
        /// Une fraction et non un nombre d'unites : c'est deja ainsi que le jeu fait deserter
        /// une flotte dont l'entretien n'est pas paye (<c>UnitBundle.Scale</c>). Compter en
        /// unites aurait exige d'inventer une politique de selection — quel type part en
        /// premier ? — qui n'existe nulle part ailleurs.
        /// </para>
        /// </summary>
        public readonly float ImmediateGarrisonFraction;

        /// <summary>Variation de stabilite appliquee tout de suite. Peut etre negative.</summary>
        public readonly float ImmediateStability;

        /// <summary>Delai, en jours, avant que l'effet differe ne tombe.</summary>
        public readonly int DeferredDelayDays;

        /// <summary>Credits preleves a l'echeance.</summary>
        public readonly float DeferredCredits;

        /// <summary>Part de la garnison perdue a l'echeance, de 0 a 1.</summary>
        public readonly float DeferredGarrisonFraction;

        /// <summary>Variation de stabilite a l'echeance.</summary>
        public readonly float DeferredStability;

        /// <summary>
        /// Ce que le gouverneur du monde retient de ce choix (Phase 24, etape 6).
        /// <para>
        /// La correspondance vit ici, avec le contenu, et non dans le service : c'est le
        /// catalogue qui decide que reprimer se retient comme une repression. Un choix qui ne
        /// laisserait aucun souvenir vaudrait <c>null</c> — mais aucun n'est dans ce cas, et un
        /// test le verifie : une decision dont personne ne se souvient n'aurait pas de suite.
        /// </para>
        /// </summary>
        public readonly GovernorFactKind? RememberedAs;

        public DecisionOption(
            string label, string immediateText, string deferredText,
            float immediateCredits, float immediateGarrisonFraction, float immediateStability,
            int deferredDelayDays, float deferredCredits, float deferredGarrisonFraction, float deferredStability,
            GovernorFactKind? rememberedAs = null)
        {
            Label = label;
            ImmediateText = immediateText;
            DeferredText = deferredText;
            ImmediateCredits = immediateCredits;
            ImmediateGarrisonFraction = immediateGarrisonFraction;
            ImmediateStability = immediateStability;
            DeferredDelayDays = deferredDelayDays;
            DeferredCredits = deferredCredits;
            DeferredGarrisonFraction = deferredGarrisonFraction;
            DeferredStability = deferredStability;
            RememberedAs = rememberedAs;
        }

        /// <summary>
        /// Vrai si l'option coute quelque chose <b>maintenant</b>.
        /// <para>
        /// Une perte de stabilite compte comme un cout immediat : c'est ce que paie l'option qui
        /// consiste a ne rien faire, et c'est reel puisque la stabilite declenche la decision
        /// suivante.
        /// </para>
        /// </summary>
        public bool HasImmediateCost
        {
            get { return ImmediateCredits > 0f || ImmediateGarrisonFraction > 0f || ImmediateStability < 0f; }
        }

        /// <summary>Vrai si l'option laisse une ardoise a payer plus tard.</summary>
        public bool HasDeferredConsequence
        {
            get { return DeferredCredits > 0f || DeferredGarrisonFraction > 0f || DeferredStability < 0f; }
        }

        /// <summary>
        /// Vrai si l'option respecte la regle absolue : elle coute maintenant <b>et</b> plus tard.
        /// </summary>
        public bool IsHonest
        {
            get { return HasImmediateCost && HasDeferredConsequence; }
        }
    }
}
