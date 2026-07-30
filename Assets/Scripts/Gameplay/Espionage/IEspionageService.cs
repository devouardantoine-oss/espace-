using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;

namespace Espace.Gameplay.Espionage
{
    /// <summary>
    /// Espionnage de tous les empires : cinq missions distantes (pas besoin d'adjacence, a la
    /// difference des flottes) resolues de facon deterministe selon un rapport de puissance,
    /// comme <see cref="Espace.Gameplay.Military.CombatResolver"/>.
    /// </summary>
    public interface IEspionageService
    {
        /// <summary>Puissance d'espionnage de <paramref name="empireId"/> (base modulee par son bonus de recherche Espionnage).</summary>
        float GetEspionagePower(int empireId);

        /// <summary>
        /// Puissance de contre-espionnage de <paramref name="empireId"/>, evaluee via la
        /// stabilite de <paramref name="referenceSystemId"/> (un systeme plus stable se defend
        /// mieux) et son propre bonus de recherche Espionnage.
        /// </summary>
        float GetCounterEspionagePower(int empireId, StarSystemId referenceSystemId);

        /// <summary>
        /// Copie instantanement, pour <paramref name="proposerId"/>, le palier de recherche le
        /// plus avantageux que <paramref name="targetEmpireId"/> possede et que le proposeur
        /// n'a pas encore. Echoue si la cible n'a aucune avance technologique exploitable, ou
        /// si le contre-espionnage de la cible dejoue la tentative (payante que la mission
        /// reussisse ou non).
        /// </summary>
        bool TryStealTechnology(int proposerId, int targetEmpireId, out string error);

        /// <summary>Reduit d'un niveau le developpement du systeme <paramref name="targetSystemId"/> si la mission reussit.</summary>
        bool TrySabotage(int proposerId, StarSystemId targetSystemId, out string error);

        /// <summary>Reduit fortement la stabilite du systeme <paramref name="targetSystemId"/> si la mission reussit.</summary>
        bool TryInciteRevolt(int proposerId, StarSystemId targetSystemId, out string error);

        /// <summary>Ameliore secretement l'opinion de <paramref name="targetEmpireId"/> envers <paramref name="proposerId"/> si la mission reussit.</summary>
        bool TryInfluenceGovernment(int proposerId, int targetEmpireId, out string error);

        /// <summary>Revele, si la mission reussit, la garnison reelle de <paramref name="targetEmpireId"/> sur <paramref name="targetSystemId"/>.</summary>
        bool TryDiscoverArmies(int proposerId, int targetEmpireId, StarSystemId targetSystemId, out UnitBundle discoveredGarrison, out string error);
    }
}
