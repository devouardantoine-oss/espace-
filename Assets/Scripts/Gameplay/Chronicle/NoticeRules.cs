namespace Espace.Gameplay.Chronicle
{
    /// <summary>Niveau d'attention que reclame un avis.</summary>
    public enum NoticeTier
    {
        /// <summary>Consultable dans le journal, invisible autrement.</summary>
        Information = 0,

        /// <summary>Incremente le compteur d'alertes. N'interrompt jamais.</summary>
        Important = 1,

        /// <summary>Irreversible ou demande une decision immediate.</summary>
        Critical = 2
    }

    /// <summary>
    /// Nature d'un avis. Volontairement plus fin que le type d'evenement : une bataille gagnee
    /// et une bataille perdue viennent du meme <c>BattleResolvedEvent</c> mais n'appellent pas
    /// la meme attention.
    /// </summary>
    public enum NoticeKind
    {
        BattleWon,
        BattleLost,
        SystemLost,
        SystemTaken,
        SystemColonized,
        EncounterStarted,
        ProposalReceived,
        DiplomaticStatusChanged,

        /// <summary>Changement de statut entre deux tiers : public, mais ne nous concerne pas.</summary>
        ForeignDiplomacy,

        TechnologyResearched,
        TechnologyStolen,
        BuildingCompleted,
        RecruitmentCompleted,
        FleetDeparted,
        ResearchDomainChanged,
        EspionageFailed,
        SystemSabotaged,
        RevoltIncited,

        /// <summary>Un fragment du journal de l'Empire disparu vient d'etre obtenu (Phase 24, etape 3).</summary>
        FragmentFound,

        /// <summary>Une question attend une reponse du joueur (Phase 24, etape 5).</summary>
        DecisionRequired,

        /// <summary>Le joueur a tranche.</summary>
        DecisionAnswered,

        /// <summary>L'ardoise contractee par une decision vient de tomber.</summary>
        DecisionSettled,

        /// <summary>Un gouverneur a quitte son poste (Phase 24, etape 6).</summary>
        GovernorDefected,

        /// <summary>Le joueur a emprunte l'une des voies (Phase 24, etape 7).</summary>
        VoieTaken
    }

    /// <summary>
    /// Regles de tri des avis (Phase 24, etape 1).
    /// <para>
    /// <b>Le probleme resolu.</b> Le jeu publie trente et un types d'evenements et n'en affiche
    /// aucun. Construction terminee, planete capturee, technologie acquise, proposition
    /// diplomatique recue : tout se produit en silence, et le joueur doit deviner qu'il faut
    /// aller verifier.
    /// </para>
    /// <para>
    /// <b>L'enjeu n'est pas de notifier, c'est de ne pas noyer.</b> Un fil ou tout remonte ne
    /// vaut pas mieux qu'un fil vide : dans les deux cas le joueur cesse de le lire. D'ou trois
    /// niveaux et une regle stricte pour chacun.
    /// </para>
    /// <para>
    /// <b>La regle qui fait tenir le systeme :</b> un avis n'a le droit de reclamer l'attention
    /// que s'il a une <b>destination</b> — un endroit ou l'appui mene et ou l'on peut agir. Si
    /// appuyer dessus n'ouvre rien d'utile, il redescend au journal. Ce n'est pas un principe
    /// decoratif : c'est ce test, et pas le jugement au cas par cas, qui empeche la liste de
    /// gonfler a chaque nouvelle fonctionnalite. Il est verifie par un test.
    /// </para>
    /// </summary>
    public static class NoticeRules
    {
        /// <summary>Niveau d'attention d'un avis.</summary>
        public static NoticeTier TierOf(NoticeKind kind)
        {
            switch (kind)
            {
                // Irreversible, ou une decision est attendue tout de suite.
                case NoticeKind.EncounterStarted:
                case NoticeKind.SystemLost:
                case NoticeKind.BattleLost:

                // Le quatrieme type critique, et il est ajoute deliberement : « demande une
                // decision immediate » est la definition meme de ce niveau, et une decision qui
                // attendrait sagement dans le journal cesserait d'etre une decision.
                case NoticeKind.DecisionRequired:
                    return NoticeTier.Critical;

                // Merite d'etre vu, jamais d'interrompre.
                case NoticeKind.ProposalReceived:
                case NoticeKind.DiplomaticStatusChanged:
                case NoticeKind.TechnologyResearched:
                case NoticeKind.BuildingCompleted:
                case NoticeKind.SystemColonized:
                case NoticeKind.SystemTaken:
                case NoticeKind.SystemSabotaged:
                case NoticeKind.RevoltIncited:

                // Un fragment merite d'etre vu — c'est le fil narratif du jeu — mais jamais
                // d'interrompre : il n'y a rien a decider, seulement quelque chose a lire.
                case NoticeKind.FragmentFound:

                // L'ardoise qui tombe deux mois apres un choix : le joueur doit faire le lien
                // entre ce qu'il subit et ce qu'il a decide, sinon le differe n'apprend rien.
                case NoticeKind.DecisionSettled:

                // Un depart se voit sur la fiche du monde, et il ne se rattrape pas : il merite
                // le compteur, jamais l'interruption.
                case NoticeKind.GovernorDefected:

                // Emprunter une voie est le geste le plus lourd que le joueur puisse faire de son
                // propre chef. Il vient de le decider, donc rien ne l'interrompt — mais la trace
                // doit rester visible.
                case NoticeKind.VoieTaken:
                    return NoticeTier.Important;

                // Le reste vit dans le journal et nulle part ailleurs.
                default:
                    return NoticeTier.Information;
            }
        }

        /// <summary>
        /// Vrai si l'avis mene quelque part — un systeme a ouvrir, un ecran a consulter.
        /// <para>
        /// Un avis sans destination ne peut pas depasser <see cref="NoticeTier.Information"/> :
        /// reclamer l'attention pour ne mener nulle part est la meilleure facon d'apprendre au
        /// joueur a ignorer le compteur.
        /// </para>
        /// </summary>
        public static bool HasDestination(NoticeKind kind)
        {
            switch (kind)
            {
                // Ces trois-la n'ont rien a ouvrir : le domaine actif est deja visible dans le
                // lisere, un depart de flotte est visible sur la carte, un recrutement est deja
                // fini quand on l'apprend.
                case NoticeKind.ResearchDomainChanged:
                case NoticeKind.FleetDeparted:
                case NoticeKind.RecruitmentCompleted:

                // La question a deja ete tranchee : il n'y a plus rien a ouvrir. C'est une trace
                // pour le journal, pas un rappel.
                case NoticeKind.DecisionAnswered:
                    return false;

                default:
                    return true;
            }
        }
    }
}
