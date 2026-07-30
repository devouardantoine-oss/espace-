using System;

namespace Espace.Core
{
    /// <summary>
    /// Parametres d'une horloge de jeu, independants d'Unity et du
    /// <see cref="Espace.Data.GameClockConfig"/> qui les expose a l'inspecteur.
    /// <para>
    /// Meme separation que <see cref="Espace.Gameplay.Galaxy.GalaxyGenerationParameters"/> en
    /// Phase 2 : <see cref="GameClock"/> reste testable sans instancier de ScriptableObject,
    /// et <c>Espace.Core</c> n'a pas besoin de dependre de <c>Espace.Data</c> pour connaitre
    /// ces valeurs.
    /// </para>
    /// </summary>
    public readonly struct GameClockSettings
    {
        /// <summary>Date a laquelle la partie commence.</summary>
        public readonly GameDate StartDate;

        /// <summary>Secondes reelles necessaires pour qu'un jour de jeu s'ecoule a la vitesse Normale (x1).</summary>
        public readonly float SecondsPerGameDayAtNormalSpeed;

        /// <summary>Multiplicateur de la vitesse <see cref="GameSpeed.Fast"/> par rapport a Normale.</summary>
        public readonly float FastMultiplier;

        /// <summary>Multiplicateur de la vitesse <see cref="GameSpeed.Faster"/> par rapport a Normale.</summary>
        public readonly float FasterMultiplier;

        /// <summary>Multiplicateur de la vitesse <see cref="GameSpeed.Fastest"/> par rapport a Normale.</summary>
        public readonly float FastestMultiplier;

        public GameClockSettings(
            GameDate startDate,
            float secondsPerGameDayAtNormalSpeed,
            float fastMultiplier,
            float fasterMultiplier,
            float fastestMultiplier)
        {
            if (secondsPerGameDayAtNormalSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(secondsPerGameDayAtNormalSpeed), "Doit etre strictement positif.");
            }

            // Chaque palier doit accelerer strictement le precedent : sinon "vitesse
            // superieure" ne voudrait plus rien dire pour le joueur.
            if (fastMultiplier <= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(fastMultiplier), "Doit etre strictement superieur a 1 (vitesse Normale).");
            }

            if (fasterMultiplier <= fastMultiplier)
            {
                throw new ArgumentOutOfRangeException(nameof(fasterMultiplier), "Doit etre strictement superieur au multiplicateur Fast.");
            }

            if (fastestMultiplier <= fasterMultiplier)
            {
                throw new ArgumentOutOfRangeException(nameof(fastestMultiplier), "Doit etre strictement superieur au multiplicateur Faster.");
            }

            StartDate = startDate;
            SecondsPerGameDayAtNormalSpeed = secondsPerGameDayAtNormalSpeed;
            FastMultiplier = fastMultiplier;
            FasterMultiplier = fasterMultiplier;
            FastestMultiplier = fastestMultiplier;
        }

        /// <summary>Reglages raisonnables utilises si aucune configuration n'est fournie.</summary>
        public static GameClockSettings Default => new GameClockSettings(
            GameDate.StartOfGame,
            secondsPerGameDayAtNormalSpeed: 2f,
            fastMultiplier: 2f,
            fasterMultiplier: 4f,
            fastestMultiplier: 8f);

        /// <summary>Multiplicateur de vitesse reel appliqué au temps ecoule pour <paramref name="speed"/>.</summary>
        public float GetMultiplier(GameSpeed speed)
        {
            switch (speed)
            {
                case GameSpeed.Paused: return 0f;
                case GameSpeed.Normal: return 1f;
                case GameSpeed.Fast: return FastMultiplier;
                case GameSpeed.Faster: return FasterMultiplier;
                case GameSpeed.Fastest: return FastestMultiplier;
                default:
                    throw new ArgumentOutOfRangeException(nameof(speed), speed, "Vitesse inconnue.");
            }
        }
    }
}
