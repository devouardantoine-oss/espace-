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
        /// Vigilance acquise par <paramref name="targetEmpireId"/>, de 0 au plafond
        /// (Phase 24, etape 4).
        /// <para>
        /// Elle monte a chaque tentative subie, reussie ou non, et retombe lentement chaque mois.
        /// La valeur etait deja calculee et deja appliquee a la defense ; elle n'etait lisible de
        /// nulle part, alors que <b>c'est le vrai sujet du panneau Espionnage</b> : savoir sur
        /// quelle cible on peut encore frapper.
        /// </para>
        /// </summary>
        float GetVigilance(int targetEmpireId);

        /// <summary>Plafond de <see cref="GetVigilance"/> : une cible sur ses gardes ne devient jamais imprenable.</summary>
        float MaximumVigilance { get; }

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
