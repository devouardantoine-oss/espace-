using Espace.Core;
using UnityEngine;

namespace Espace.Data
{
    /// <summary>
    /// Reglages editables de l'horloge de jeu.
    /// <para>
    /// Meme pattern que <see cref="GameConfig"/> et
    /// <see cref="Espace.Gameplay.Galaxy.GalaxyConfig"/> (Phase 2) : les valeurs vivent dans
    /// un asset ajustable sans recompiler, converties en <see cref="GameClockSettings"/> —
    /// une structure C# pure — avant d'etre passees a <see cref="GameClock"/>.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "GameClockConfig", menuName = "Espace/Core/Game Clock Config", order = 1)]
    public sealed class GameClockConfig : ScriptableObject
    {
        [Header("Calendrier de depart")]
        [SerializeField]
        [Min(1)]
        private int startYear = 1;

        [SerializeField]
        [Range(1, GameDate.MonthsPerYear)]
        private int startMonth = 1;

        [SerializeField]
        [Range(1, GameDate.DaysPerMonth)]
        private int startDay = 1;

        [Header("Vitesse")]
        [Tooltip("Secondes reelles pour qu'un jour de jeu s'ecoule a la vitesse Normale (x1).")]
        [SerializeField]
        [Range(0.25f, 10f)]
        private float secondsPerGameDayAtNormalSpeed = 2f;

        [Tooltip("Multiplicateur de la vitesse Rapide par rapport a Normale.")]
        [SerializeField]
        [Range(1.1f, 10f)]
        private float fastMultiplier = 2f;

        [Tooltip("Multiplicateur de la vitesse Tres rapide par rapport a Normale.")]
        [SerializeField]
        [Range(1.1f, 20f)]
        private float fasterMultiplier = 4f;

        [Tooltip("Multiplicateur de la vitesse Maximale par rapport a Normale.")]
        [SerializeField]
        [Range(1.1f, 40f)]
        private float fastestMultiplier = 8f;

        /// <summary>Construit les parametres immuables consommes par <see cref="GameClock"/>.</summary>
        public GameClockSettings ToSettings()
        {
            return new GameClockSettings(
                new GameDate(startYear, startMonth, startDay),
                secondsPerGameDayAtNormalSpeed,
                fastMultiplier,
                fasterMultiplier,
                fastestMultiplier);
        }
    }
}
