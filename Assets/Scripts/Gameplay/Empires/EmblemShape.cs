namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Silhouette de l'embleme d'une civilisation (Phase 21.3).
    /// <para>
    /// <b>Une enumeration, pas une image.</b> Le projet n'a pas d'illustrateur (voir le README) :
    /// les emblemes sont traces par <see cref="FactionEmblemFactory"/>. Passer par une valeur
    /// nommee plutot que par un <c>Sprite</c> a assigner garantit qu'une nouvelle faction aura
    /// forcement un embleme — et qui s'accordera aux cinq autres, puisqu'ils sortent tous du
    /// meme tracage.
    /// </para>
    /// <para>
    /// Le jour ou de vrais emblemes dessines existent, un champ <c>Sprite</c> facultatif sur
    /// <see cref="EmpireDefinition"/> les fera passer devant, sans rien retirer d'ici.
    /// </para>
    /// </summary>
    public enum EmblemShape
    {
        /// <summary>Cercle traverse d'une croix et d'un losange : humain, symetrique, institutionnel.</summary>
        Compass,

        /// <summary>Hexagones emboites : alveole, ruche, croissance par repetition.</summary>
        Hive,

        /// <summary>Bouclier a base large, coeur plein : masse, defense, mineral.</summary>
        Bastion,

        /// <summary>Losange etoile aux diagonales marquees : reseau, echange, carrefour.</summary>
        Ledger,

        /// <summary>Amande verticale dans un cercle : graine, feuille, croissance lente.</summary>
        Seed,

        /// <summary>Losange fractionne en facettes : cristal, artifice, geometrie froide.</summary>
        Prism
    }
}
