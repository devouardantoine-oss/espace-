namespace Espace.Gameplay.Voies
{
    /// <summary>
    /// Les quatre reponses que l'Empire disparu a tentees face a la courbe (Phase 24, etape 7).
    /// <para>
    /// <b>Ce ne sont pas des fins de partie.</b> Ce sont des <i>reponses</i>, debloquees par le
    /// fragment IV du codex et disponibles tant que la partie dure. Le joueur qui les decouvre
    /// apprend surtout qu'aucune n'a marche.
    /// </para>
    /// <para>
    /// <b>Et il en existe une cinquieme, qui n'est pas dans le journal : ne rien faire.</b>
    /// Continuer a conquerir, encaisser la pression, regarder les provinces se destabiliser une a
    /// une. C'est ce que l'Empire a fait pendant cinq cents ans avant d'essayer quoi que ce soit,
    /// et c'est ce que la plupart des joueurs feront. Le jeu ne la punit pas plus que les autres :
    /// elle doit simplement <b>durer</b>, et devenir de plus en plus inconfortable.
    /// </para>
    /// </summary>
    public enum Voie
    {
        /// <summary>Des mondes passent en autonomie : ils cessent de compter, et cessent d'etre a vous.</summary>
        Deconcentration = 0,

        /// <summary>Abandon volontaire de mondes pour repasser sous le plafond.</summary>
        Compression = 1,

        /// <summary>La garnison remplace l'administration : le cout bascule de l'Influence vers la tresorerie.</summary>
        Coercition = 2,

        /// <summary>La Dispersion : retracer le reseau pour rendre l'unification impossible.</summary>
        Transformation = 3
    }

    /// <summary>
    /// Ce qu'une voie fait, ce qu'elle coute, et ce que l'Empire en a fait (Phase 24, etape 7).
    /// <para>
    /// <b>Structure pure</b> : du contenu, verifiable sans scene ni service.
    /// </para>
    /// </summary>
    public readonly struct VoieDefinition
    {
        public readonly Voie Voie;

        /// <summary>Nom affiche.</summary>
        public readonly string Name;

        /// <summary>L'effet mecanique, en une phrase.</summary>
        public readonly string Effect;

        /// <summary>Ce que ca coute, en une phrase. Jamais vide : aucune voie n'est gratuite.</summary>
        public readonly string Cost;

        /// <summary>Ce que l'Empire disparu en a fait — la seule chose que le codex apporte de plus.</summary>
        public readonly string History;

        /// <summary>Vrai si la voie s'applique a un systeme choisi, faux si elle vaut pour tout l'empire.</summary>
        public readonly bool NeedsASystem;

        /// <summary>
        /// Vrai si le jeu sait reellement l'appliquer.
        /// <para>
        /// <b>La Transformation vaut <c>false</c> aujourd'hui</b>, et elle est tout de meme
        /// presentee au joueur : la connaitre fait partie du recit, et un choix qu'on sait
        /// exister mais qu'on ne peut pas encore prendre vaut mieux qu'un bouton qui ne ferait
        /// rien. Voir <see cref="VoieCatalogue"/> pour ce qu'il faudrait pour la rendre jouable.
        /// </para>
        /// </summary>
        public readonly bool IsPlayable;

        public VoieDefinition(
            Voie voie, string name, string effect, string cost, string history,
            bool needsASystem, bool isPlayable)
        {
            Voie = voie;
            Name = name;
            Effect = effect;
            Cost = cost;
            History = history;
            NeedsASystem = needsASystem;
            IsPlayable = isPlayable;
        }
    }
}
