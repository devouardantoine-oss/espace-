using System.Collections.Generic;
using Espace.Data;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Economie du joueur : tresor, impots, construction et investissement.
    /// <para>
    /// <b>Portee Phase 4 :</b> un seul tresor (celui du joueur, <see cref="EconomyService.PlayerOwnerId"/>).
    /// La generalisation a plusieurs empires avec IA (Phase 5) reutilisera ces memes methodes
    /// une fois qu'un identifiant d'empire remplacera la constante actuelle.
    /// </para>
    /// </summary>
    public interface IEconomyService
    {
        /// <summary>Ressources actuellement en reserve.</summary>
        ResourceBundle Treasury { get; }

        /// <summary>Taux d'imposition courant (0 a 1), applique a la production de Credits.</summary>
        float TaxRate { get; }

        /// <summary>Catalogue des types de batiments constructibles.</summary>
        IReadOnlyList<BuildingType> BuildingCatalog { get; }

        /// <summary>Definit le taux d'imposition, borne automatiquement entre 0 et 1.</summary>
        void SetTaxRate(float rate);

        /// <summary>
        /// Lance la construction de <paramref name="buildingType"/> sur <paramref name="systemId"/>
        /// si le systeme appartient au joueur, respecte le developpement minimal requis, n'a
        /// pas deja ce batiment, et si le tresor peut en couvrir le cout.
        /// </summary>
        bool TryStartConstruction(StarSystemId systemId, BuildingType buildingType, out string error);

        /// <summary>Cout en Credits du prochain niveau de developpement de <paramref name="systemId"/>.</summary>
        float GetInvestmentCost(StarSystemId systemId);

        /// <summary>
        /// Depense <see cref="GetInvestmentCost"/> pour augmenter d'un niveau le developpement
        /// de <paramref name="systemId"/>, si le systeme appartient au joueur et n'est pas deja
        /// au developpement maximal.
        /// </summary>
        bool TryInvestInDevelopment(StarSystemId systemId, out string error);

        /// <summary>Batiments (construits ou en cours) presents sur <paramref name="systemId"/>.</summary>
        IReadOnlyList<BuildingInstance> GetBuildings(StarSystemId systemId);
    }
}
