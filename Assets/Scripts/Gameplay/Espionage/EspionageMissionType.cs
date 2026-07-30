namespace Espace.Gameplay.Espionage
{
    /// <summary>
    /// Les cinq actions d'espionnage du brief. Valeurs explicites et contigues, comme
    /// <see cref="Espace.Gameplay.Research.ResearchDomain"/> : ne jamais reordonner ni
    /// reutiliser une valeur.
    /// </summary>
    public enum EspionageMissionType
    {
        /// <summary>Copie instantanement un palier de recherche que la cible possede et pas le proposeur.</summary>
        StealTechnology = 0,

        /// <summary>Reduit le developpement du systeme cible d'un niveau.</summary>
        Sabotage = 1,

        /// <summary>Revele la composition reelle de la garnison d'un systeme cible.</summary>
        DiscoverArmies = 2,

        /// <summary>Ameliore secretement l'opinion de la cible envers le proposeur (propagande).</summary>
        InfluenceGovernment = 3,

        /// <summary>Reduit fortement la stabilite du systeme cible.</summary>
        IncitesRevolt = 4
    }
}
