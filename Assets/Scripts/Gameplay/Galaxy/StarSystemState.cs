using Espace.Data;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Etat d'un systeme stellaire : identite et statistiques descriptives.
    /// <para>
    /// <b>Portee de la Phase 2 :</b> ces valeurs sont generees une fois par
    /// <see cref="GalaxyGenerator"/> pour peupler la carte et permettre la selection. Aucune
    /// simulation ne les fait encore evoluer (l'economie arrive en Phase 4, les empires en
    /// Phase 5). Les proprietes restent volontairement modifiables directement : les
    /// contraindre derriere des methodes de mutation n'a de sens qu'une fois qu'un systeme
    /// (economie, IA) a effectivement besoin de les changer en cours de partie.
    /// </para>
    /// </summary>
    public sealed class StarSystemState
    {
        /// <summary>Valeur de <see cref="OwnerId"/> pour un systeme sans empire proprietaire.</summary>
        public const int UnownedOwnerId = -1;

        /// <summary>Identifiant unique et stable du systeme.</summary>
        public StarSystemId Id { get; }

        /// <summary>Nom affiche, genere par <see cref="StarSystemNameGenerator"/>.</summary>
        public string Name { get; }

        /// <summary>Position sur la carte galactique (unites monde).</summary>
        public Vector2 Position { get; }

        /// <summary>Population, en millions d'habitants. 0 pour un systeme inhabite.</summary>
        public int Population { get; set; }

        /// <summary>Richesse relative du systeme, sur une echelle de 0 a 100.</summary>
        public int Wealth { get; set; }

        /// <summary>Niveau de developpement des infrastructures, de 0 (brut) a 5 (avance).</summary>
        public int DevelopmentLevel { get; set; }

        /// <summary>Stabilite politique et sociale, de 0 (revolte imminente) a 1 (stable).</summary>
        public float Stability { get; set; }

        /// <summary>Identifiant de l'empire proprietaire, ou <see cref="UnownedOwnerId"/> si aucun.</summary>
        public int OwnerId { get; set; }

        /// <summary>
        /// Types de ressources dont le systeme dispose de gisements (0 a 2 en general).
        /// Determine les bonus de production une fois l'economie implementee (Phase 4).
        /// </summary>
        public ResourceType[] ResourceDeposits { get; }

        public StarSystemState(
            StarSystemId id,
            string name,
            Vector2 position,
            int population,
            int wealth,
            int developmentLevel,
            float stability,
            ResourceType[] resourceDeposits)
        {
            Id = id;
            Name = name;
            Position = position;
            Population = population;
            Wealth = wealth;
            DevelopmentLevel = developmentLevel;
            Stability = stability;
            OwnerId = UnownedOwnerId;
            ResourceDeposits = resourceDeposits ?? System.Array.Empty<ResourceType>();
        }
    }
}
