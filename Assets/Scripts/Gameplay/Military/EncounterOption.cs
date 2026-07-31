namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Issue possible d'une rencontre spatiale (Phase 17, point 13 du brief).
    /// <para>
    /// Les options reellement proposees dependent du statut diplomatique entre les deux empires
    /// (voir <see cref="EncounterRules.AvailableOptions"/>) : on ne pirate pas un allie, on ne
    /// commerce pas avec un empire en guerre.
    /// </para>
    /// <para>
    /// <b>« Poursuite » du brief n'est pas implementee</b> et c'est un choix assume : c'est la
    /// seule issue qui demanderait a une flotte d'abandonner son itineraire pour re-cibler un
    /// objet <i>mobile</i> sans position fixe. Combat, repli et negociation couvrent l'esprit du
    /// point 13 en guerre ; cote paix, « croisement » et « poursuite de route » designent tous
    /// deux <see cref="PassBy"/>.
    /// </para>
    /// </summary>
    public enum EncounterOption
    {
        /// <summary>Chacun poursuit sa route : aucune consequence.</summary>
        PassBy = 0,

        /// <summary>Engage le combat. Reserve a un etat de guerre declare.</summary>
        Fight = 1,

        /// <summary>Rebrousse chemin vers le systeme d'origine plutot que d'affronter l'autre flotte.</summary>
        Withdraw = 2,

        /// <summary>Echange de courtoisie : ameliore l'opinion mutuelle.</summary>
        Negotiate = 3,

        /// <summary>Echange commercial : les deux empires y gagnent des Credits et de l'opinion.</summary>
        Trade = 4,

        /// <summary>Rancon prise sur le tresor adverse : lucratif, mais l'opinion s'effondre.</summary>
        Piracy = 5
    }
}
