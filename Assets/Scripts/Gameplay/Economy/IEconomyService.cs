using System.Collections.Generic;
using Espace.Data;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Economie de tous les empires : un trésor et un taux d'imposition par empire, un
    /// catalogue de bâtiments partagé.
    /// <para>
    /// <b>Depuis la Phase 5 :</b> généralisé à plusieurs empires. <see cref="Treasury"/>,
    /// <see cref="TaxRate"/> et <see cref="SetTaxRate(float)"/> restent des raccourcis vers
    /// <see cref="EconomyService.PlayerOwnerId"/>, pour que le code écrit en Phase 4
    /// (notamment <c>EconomyDebugPanel</c>) continue de fonctionner sans modification.
    /// <see cref="TryStartConstruction"/> et <see cref="TryInvestInDevelopment"/> retrouvent
    /// déjà l'empire concerné via le propriétaire du système visé : ils fonctionnent tels
    /// quels pour n'importe quel empire, joueur ou IA.
    /// </para>
    /// </summary>
    public interface IEconomyService
    {
        /// <summary>Ressources du joueur actuellement en réserve. Raccourci pour <c>GetTreasury(EconomyService.PlayerOwnerId)</c>.</summary>
        ResourceBundle Treasury { get; }

        /// <summary>Taux d'imposition courant du joueur. Raccourci pour <c>GetTaxRate(EconomyService.PlayerOwnerId)</c>.</summary>
        float TaxRate { get; }

        /// <summary>Catalogue des types de batiments constructibles, commun a tous les empires.</summary>
        IReadOnlyList<BuildingType> BuildingCatalog { get; }

        /// <summary>Ressources de <paramref name="empireId"/> actuellement en reserve. <see cref="ResourceBundle.Zero"/> si cet empire n'a encore rien produit.</summary>
        ResourceBundle GetTreasury(int empireId);

        /// <summary>Taux d'imposition courant de <paramref name="empireId"/> (0 a 1).</summary>
        float GetTaxRate(int empireId);

        /// <summary>Definit le taux d'imposition du joueur, borne automatiquement entre 0 et 1.</summary>
        void SetTaxRate(float rate);

        /// <summary>Definit le taux d'imposition de <paramref name="empireId"/>, borne automatiquement entre 0 et 1.</summary>
        void SetTaxRate(int empireId, float rate);

        /// <summary>
        /// Deduit <paramref name="cost"/> du tresor de <paramref name="empireId"/> si celui-ci
        /// peut le couvrir. Point d'entree generique pour toute depense hors construction et
        /// investissement (recrutement et entretien militaires, Phase 6) : evite de dupliquer
        /// la verification d'affordabilite et la publication de <c>TreasuryChangedEvent</c>
        /// deja ecrites pour <see cref="TryStartConstruction"/>/<see cref="TryInvestInDevelopment"/>.
        /// </summary>
        bool TrySpend(int empireId, ResourceBundle cost, out string error);

        /// <summary>
        /// Lance la construction de <paramref name="buildingType"/> sur <paramref name="systemId"/>
        /// si le systeme a un proprietaire, respecte le developpement minimal requis, n'a
        /// pas deja ce batiment, et si le tresor de son proprietaire peut en couvrir le cout.
        /// </summary>
        bool TryStartConstruction(StarSystemId systemId, BuildingType buildingType, out string error);

        /// <summary>Cout en Credits du prochain niveau de developpement de <paramref name="systemId"/>.</summary>
        float GetInvestmentCost(StarSystemId systemId);

        /// <summary>
        /// Depense <see cref="GetInvestmentCost"/> pour augmenter d'un niveau le developpement
        /// de <paramref name="systemId"/>, si le systeme a un proprietaire et n'est pas deja
        /// au developpement maximal.
        /// </summary>
        bool TryInvestInDevelopment(StarSystemId systemId, out string error);

        /// <summary>Batiments (construits ou en cours) presents sur <paramref name="systemId"/>.</summary>
        IReadOnlyList<BuildingInstance> GetBuildings(StarSystemId systemId);
    }
}
