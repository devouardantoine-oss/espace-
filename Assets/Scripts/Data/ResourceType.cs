namespace Espace.Data
{
    /// <summary>
    /// Les cinq ressources de l'empire.
    /// <para>
    /// Les valeurs sont explicites et contiguës : elles servent d'index dans
    /// <see cref="ResourceBundle"/> et seront écrites telles quelles dans les sauvegardes JSON.
    /// <b>Ne jamais réordonner ni réutiliser une valeur</b>, sous peine de casser les parties
    /// existantes ; les nouvelles ressources s'ajoutent à la fin.
    /// </para>
    /// </summary>
    public enum ResourceType
    {
        /// <summary>Monnaie : impôts, commerce, entretien des armées.</summary>
        Credits = 0,

        /// <summary>Minerais : construction et production militaire.</summary>
        Minerals = 1,

        /// <summary>Énergie : alimente bâtiments et flottes.</summary>
        Energy = 2,

        /// <summary>Nourriture : croissance et stabilité de la population.</summary>
        Food = 3,

        /// <summary>Influence : diplomatie, espionnage, décisions politiques.</summary>
        Influence = 4
    }

    /// <summary>Constantes partagées liées à <see cref="ResourceType"/>.</summary>
    public static class ResourceTypes
    {
        /// <summary>Nombre de ressources. Évite un <c>Enum.GetValues</c> qui alloue à chaque appel.</summary>
        public const int Count = 5;

        /// <summary>Toutes les ressources, dans l'ordre de l'énumération. Tableau partagé : ne pas modifier.</summary>
        public static readonly ResourceType[] All =
        {
            ResourceType.Credits,
            ResourceType.Minerals,
            ResourceType.Energy,
            ResourceType.Food,
            ResourceType.Influence
        };
    }
}
