using System.Collections.Generic;

namespace Espace.Gameplay.Research
{
    /// <summary>
    /// Recherche de tous les empires : generation journaliere de points investis dans un
    /// domaine actif choisi, progression et bonus cumulatifs par domaine.
    /// </summary>
    public interface IResearchService
    {
        /// <summary>Catalogue complet des paliers de recherche, tous domaines confondus.</summary>
        IReadOnlyList<TechnologyDefinition> Catalog { get; }

        /// <summary>Domaine sur lequel <paramref name="empireId"/> investit ses points de recherche journaliers, ou <c>null</c> si aucun n'a encore ete choisi.</summary>
        ResearchDomain? GetActiveDomain(int empireId);

        /// <summary>Nombre de paliers de <paramref name="domain"/> deja completes par <paramref name="empireId"/> (0 si aucun).</summary>
        int GetCompletedTierCount(int empireId, ResearchDomain domain);

        /// <summary>Points de recherche deja investis dans le prochain palier non complete de <paramref name="domain"/> pour <paramref name="empireId"/>.</summary>
        float GetProgress(int empireId, ResearchDomain domain);

        /// <summary>Prochain palier non complete de <paramref name="domain"/> pour <paramref name="empireId"/>, ou <c>null</c> si le domaine est deja au maximum.</summary>
        TechnologyDefinition GetNextTechnology(int empireId, ResearchDomain domain);

        /// <summary>Bonus cumulatif (somme des <see cref="TechnologyDefinition.EffectMagnitude"/>) de tous les paliers de <paramref name="domain"/> deja completes par <paramref name="empireId"/>. 0 si aucun.</summary>
        float GetBonus(int empireId, ResearchDomain domain);

        /// <summary>
        /// Fait de <paramref name="domain"/> le domaine actif de <paramref name="empireId"/>.
        /// Echoue si ce domaine est deja au maximum, ou s'il est deja actif.
        /// </summary>
        bool TrySetActiveDomain(int empireId, ResearchDomain domain, out string error);

        /// <summary>
        /// Complete instantanement le prochain palier de <paramref name="domain"/> pour
        /// <paramref name="empireId"/>, sans depenser de points ni verifier de cout — reservee
        /// au vol de technologie (Espionnage, Phase 9). Ne fait rien si le domaine est deja au
        /// maximum. Publie le meme <see cref="TechnologyResearchedEvent"/> qu'une completion
        /// normale : du point de vue de l'empire qui en beneficie, le resultat est identique.
        /// </summary>
        void GrantTier(int empireId, ResearchDomain domain);

        /// <summary>
        /// Impose directement le nombre de paliers completes et la progression courante de
        /// <paramref name="domain"/> pour <paramref name="empireId"/>, sans evenement publie
        /// — reserve au chargement d'une sauvegarde (Phase 10).
        /// </summary>
        void RestoreProgress(int empireId, ResearchDomain domain, int completedTiers, float progress);

        /// <summary>
        /// Impose directement le domaine actif de <paramref name="empireId"/>, sans passer par
        /// les verifications ni l'evenement de <see cref="TrySetActiveDomain"/> — reserve au
        /// chargement d'une sauvegarde (Phase 10), ou le domaine sauvegarde peut legitimement
        /// etre deja au maximum (l'empire n'avait pas encore change de focus).
        /// </summary>
        void RestoreActiveDomain(int empireId, ResearchDomain domain);
    }
}
