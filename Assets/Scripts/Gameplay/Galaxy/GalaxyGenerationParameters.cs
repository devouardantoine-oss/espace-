using System;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Parametres d'une generation de galaxie, independants d'Unity et du <see cref="Espace.Data.GalaxyConfig"/>
    /// qui les expose a l'inspecteur.
    /// <para>
    /// Separer cette structure du ScriptableObject permet de faire tourner et tester
    /// <see cref="GalaxyGenerator"/> sans instancier d'asset Unity : les tests EditMode
    /// utilisent de petites valeurs (10 systemes) pour rester rapides, la partie reelle
    /// utilise les 100 systemes definis dans l'asset.
    /// </para>
    /// </summary>
    public readonly struct GalaxyGenerationParameters
    {
        /// <summary>Graine du generateur aleatoire. Meme graine = meme galaxie.</summary>
        public readonly int Seed;

        /// <summary>Nombre de systemes stellaires a generer.</summary>
        public readonly int SystemCount;

        /// <summary>Rayon du disque galactique (unites monde) dans lequel les systemes sont places.</summary>
        public readonly float GalaxyRadius;

        /// <summary>Distance minimale imposee entre deux systemes, pour eviter les amas illisibles.</summary>
        public readonly float MinSystemDistance;

        /// <summary>Tentatives de placement autorisees par systeme avant d'accepter le meilleur candidat trouve.</summary>
        public readonly int MaxPlacementAttempts;

        /// <summary>
        /// Degre moyen vise pour le reseau de routes hyperspatiales (nombre moyen de routes
        /// par systeme), au-dela de l'arbre couvrant minimal qui garantit la connexite.
        /// </summary>
        public readonly float TargetAverageDegree;

        public GalaxyGenerationParameters(
            int seed,
            int systemCount,
            float galaxyRadius,
            float minSystemDistance,
            int maxPlacementAttempts,
            float targetAverageDegree)
        {
            if (systemCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(systemCount), "Il faut au moins un systeme.");
            }

            if (galaxyRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(galaxyRadius), "Le rayon de la galaxie doit etre positif.");
            }

            if (minSystemDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minSystemDistance), "La distance minimale ne peut pas etre negative.");
            }

            if (maxPlacementAttempts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPlacementAttempts), "Il faut au moins une tentative de placement.");
            }

            if (targetAverageDegree < 2f)
            {
                // En-dessous de 2, le reseau se limiterait quasiment a l'arbre couvrant minimal :
                // techniquement valide, mais on avertit car ce n'est presque surement pas voulu.
                throw new ArgumentOutOfRangeException(nameof(targetAverageDegree), "Le degre moyen vise doit etre d'au moins 2 pour former un reseau (pas seulement un arbre).");
            }

            Seed = seed;
            SystemCount = systemCount;
            GalaxyRadius = galaxyRadius;
            MinSystemDistance = minSystemDistance;
            MaxPlacementAttempts = maxPlacementAttempts;
            TargetAverageDegree = targetAverageDegree;
        }

        /// <summary>Parametres raisonnables pour une galaxie de 100 systemes, utilises si aucune config n'est fournie.</summary>
        public static GalaxyGenerationParameters Default => new GalaxyGenerationParameters(
            seed: 0,
            systemCount: 100,
            galaxyRadius: 45f,
            minSystemDistance: 3f,
            maxPlacementAttempts: 30,
            targetAverageDegree: 2.5f);
    }
}
