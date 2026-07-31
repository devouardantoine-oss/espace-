namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Les sept types d'unites recrutables.
    /// <para>
    /// Valeurs explicites et contigues, comme <see cref="Espace.Data.ResourceType"/> : servent
    /// d'index dans <see cref="UnitBundle"/> et sont ecrites telles quelles dans les
    /// sauvegardes JSON (Phase 10). La regle habituelle « ne jamais reordonner ni reutiliser
    /// une valeur » est rompue une fois, deliberement, en Phase 14 : <c>SpaceFleet = 3</c>
    /// (unite spatiale generique) devient <see cref="Fighter"/> — projet en cours sans
    /// sauvegarde a preserver, changement documente par l'incrementation de
    /// <c>GameSaveData.Version</c> (1 vers 2). A partir d'ici, la regle reprend : ne plus
    /// reordonner ni reutiliser une valeur.
    /// </para>
    /// </summary>
    public enum UnitType
    {
        /// <summary>Infanterie : peu couteuse, base de toute garnison. Indispensable pour coloniser et envahir.</summary>
        Infantry = 0,

        /// <summary>Blindes : puissance de feu terrestre superieure.</summary>
        Armored = 1,

        /// <summary>Forces speciales : rapides et precises, couteuses.</summary>
        SpecialForces = 2,

        /// <summary>Chasseurs : combat spatial et defense de flotte, l'unite spatiale la moins chere.</summary>
        Fighter = 3,

        /// <summary>Fregate : transport leger d'infanterie, vitesse superieure aux autres vaisseaux.</summary>
        Frigate = 4,

        /// <summary>Croiseur : vaisseau de combat polyvalent, entre la Fregate et le Cuirasse.</summary>
        Cruiser = 5,

        /// <summary>Cuirasse : transporte blindes, infanterie et chasseurs ; la plus forte puissance de combat.</summary>
        Battleship = 6
    }

    /// <summary>Constantes partagees liees a <see cref="UnitType"/>.</summary>
    public static class UnitTypes
    {
        /// <summary>Nombre de types d'unites. Evite un <c>Enum.GetValues</c> qui alloue a chaque appel.</summary>
        public const int Count = 7;

        /// <summary>Tous les types, dans l'ordre de l'enumeration. Tableau partage : ne pas modifier.</summary>
        public static readonly UnitType[] All =
        {
            UnitType.Infantry,
            UnitType.Armored,
            UnitType.SpecialForces,
            UnitType.Fighter,
            UnitType.Frigate,
            UnitType.Cruiser,
            UnitType.Battleship
        };
    }
}
