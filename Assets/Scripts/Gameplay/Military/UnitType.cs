namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Les quatre types d'unites recrutables.
    /// <para>
    /// Valeurs explicites et contigues, comme <see cref="Espace.Data.ResourceType"/> : servent
    /// d'index dans <see cref="UnitBundle"/> et seront ecrites telles quelles dans les
    /// sauvegardes JSON (Phase 10). Ne jamais reordonner ni reutiliser une valeur.
    /// </para>
    /// </summary>
    public enum UnitType
    {
        /// <summary>Infanterie : peu couteuse, base de toute garnison.</summary>
        Infantry = 0,

        /// <summary>Blindes : puissance de feu terrestre superieure.</summary>
        Armored = 1,

        /// <summary>Forces speciales : rapides et precises, couteuses.</summary>
        SpecialForces = 2,

        /// <summary>Flotte spatiale : la plus puissante et la plus rapide, seule unite vraiment interstellaire.</summary>
        SpaceFleet = 3
    }

    /// <summary>Constantes partagees liees a <see cref="UnitType"/>.</summary>
    public static class UnitTypes
    {
        /// <summary>Nombre de types d'unites. Evite un <c>Enum.GetValues</c> qui alloue a chaque appel.</summary>
        public const int Count = 4;

        /// <summary>Tous les types, dans l'ordre de l'enumeration. Tableau partage : ne pas modifier.</summary>
        public static readonly UnitType[] All =
        {
            UnitType.Infantry,
            UnitType.Armored,
            UnitType.SpecialForces,
            UnitType.SpaceFleet
        };
    }
}
