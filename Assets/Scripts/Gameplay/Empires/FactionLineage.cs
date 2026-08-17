namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Filiation d'une faction vis-a-vis de l'Empire disparu (Phase 24, etape 3).
    /// <para>
    /// <b>Pourquoi une cle dediee plutot que la personnalite.</b> Le codex doit savoir
    /// <i>laquelle</i> des six factions vient d'etre aneantie, pour delivrer ses archives. La
    /// tentation etait d'utiliser <see cref="EmpirePersonality"/>, deja presente sur chaque
    /// definition — mais elle ne compte que cinq valeurs pour six factions, et la Federation de
    /// l'Aube porte deja <c>Expansionist</c>, exactement comme l'Essaim de Kethra. Aneantir
    /// l'Aube aurait delivre les archives de l'Essaim.
    /// </para>
    /// <para>
    /// <b>Et pourquoi pas le nom affiche.</b> Parce qu'il est du contenu : un game designer doit
    /// pouvoir renommer une faction sans casser le codex, et une traduction ne doit pas modifier
    /// une regle de jeu.
    /// </para>
    /// <para>
    /// La valeur par defaut est <see cref="Unknown"/>, ce qui rend l'ajout sur une definition
    /// existante inoffensif : une faction sans filiation declaree ne delivre simplement aucune
    /// archive.
    /// </para>
    /// </summary>
    public enum FactionLineage
    {
        /// <summary>Filiation non renseignee. Ne declenche aucun fragment.</summary>
        Unknown = 0,

        /// <summary>Federation de l'Aube — descendants d'un convoi d'evacuation de la Dispersion.</summary>
        Aube = 1,

        /// <summary>Sanctuaire de Vharin — population civile, seule chronologie continue.</summary>
        Vharin = 2,

        /// <summary>Bastion de Drathmoor — descendants des garnisons restees en poste.</summary>
        Drathmoor = 3,

        /// <summary>Cartel des Confins — heritiers des Archivistes deserteurs.</summary>
        Confins = 4,

        /// <summary>Ligue Marchande d'Oskar — produit de l'Empire, non de sa population.</summary>
        Oskar = 5,

        /// <summary>Essaim de Kethra — l'etrangere, celle que l'Empire s'est ruine a contenir.</summary>
        Kethra = 6
    }
}
