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
        RevoltIncited
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
                    return false;

                default:
                    return true;
            }
        }
    }
}
