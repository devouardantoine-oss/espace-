namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Nature d'un monde, qui determine sa palette et la part de sa surface couverte d'eau
    /// (Phase 21.1).
    /// <para>
    /// <b>Purement visuel pour l'instant.</b> Aucune regle de simulation n'en depend : le jeu
    /// modelise un systeme par une population, un niveau de developpement et une stabilite, pas
    /// par une geologie. Introduire ici un type qui modifierait la production reviendrait a
    /// changer l'equilibrage sous couvert d'habillage — ce sera une decision separee, si elle
    /// est prise.
    /// </para>
    /// <para>
    /// L'ordre des valeurs est celui d'une echelle d'hospitalite decroissante, ce qui permet de
    /// deduire un type plausible d'un niveau de developpement sans table de correspondance.
    /// </para>
    /// </summary>
    public enum PlanetKind
    {
        /// <summary>Continents, oceans, calottes polaires. Le monde de reference.</summary>
        Terran,

        /// <summary>Presque entierement recouvert d'eau, quelques archipels.</summary>
        Ocean,

        /// <summary>Deserts ocre, mers interieures rares.</summary>
        Arid,

        /// <summary>Glace jusqu'aux tropiques.</summary>
        Ice,

        /// <summary>Etendues acides, teintes vertes malsaines.</summary>
        Toxic,

        /// <summary>Roche nue et crateres, aucune atmosphere.</summary>
        Barren
    }
}
