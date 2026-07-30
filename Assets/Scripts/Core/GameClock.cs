using System;

namespace Espace.Core
{
    /// <summary>
    /// Implementation par defaut de <see cref="IGameClock"/> : temps continu avec pause et
    /// paliers de vitesse, inspire de Crusader Kings III mais simplifie pour le mobile
    /// (voir <see cref="GameSpeed"/>).
    /// <para>
    /// <b>Fonctionnement :</b> chaque frame accumule <c>deltaTime * multiplicateur</c> dans un
    /// compteur ; des qu'il atteint <see cref="GameClockSettings.SecondsPerGameDayAtNormalSpeed"/>,
    /// un jour de jeu s'ecoule et le compteur est decremente d'autant (le reste est conserve,
    /// pas remis a zero, pour ne pas desynchroniser le rythme au fil du temps).
    /// </para>
    /// </summary>
    public sealed class GameClock : IGameClock, IGameService
    {
        /// <summary>
        /// Plafond de jours avances en une seule frame. Protege d'une boucle trop longue si
        /// un <c>deltaTime</c> extreme survient (ex. application relancee apres des heures en
        /// arriere-plan, un scenario reel sur mobile) : mieux vaut perdre quelques jours de
        /// simulation que de geler l'application ou noyer les abonnes sous des centaines
        /// d'evenements d'un coup.
        /// </summary>
        private const int MaxDaysAdvancedPerTick = 30;

        private readonly GameClockSettings _settings;
        private readonly IEventBus _eventBus;

        private float _accumulatedSeconds;
        private GameSpeed _speedBeforePause;

        /// <inheritdoc />
        public GameDate CurrentDate { get; private set; }

        /// <inheritdoc />
        public GameSpeed CurrentSpeed { get; private set; }

        /// <inheritdoc />
        public float CurrentMultiplier => _settings.GetMultiplier(CurrentSpeed);

        /// <inheritdoc />
        public bool IsPaused => CurrentSpeed == GameSpeed.Paused;

        public GameClock(GameClockSettings settings, IEventBus eventBus)
        {
            _settings = settings;
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <inheritdoc />
        public void Initialize()
        {
            CurrentDate = _settings.StartDate;
            CurrentSpeed = GameSpeed.Normal;
            _speedBeforePause = GameSpeed.Normal;
            _accumulatedSeconds = 0f;
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            // Rien a liberer : l'horloge ne detient ni abonnement ni ressource externe.
        }

        /// <summary>
        /// Fait avancer le temps de <paramref name="deltaTime"/> secondes reelles. Reserve a
        /// <c>GameBootstrap</c> (voir la remarque sur <see cref="IGameClock"/>).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (IsPaused || deltaTime <= 0f)
            {
                return;
            }

            _accumulatedSeconds += deltaTime * CurrentMultiplier;

            int daysToAdvance = 0;
            while (_accumulatedSeconds >= _settings.SecondsPerGameDayAtNormalSpeed && daysToAdvance < MaxDaysAdvancedPerTick)
            {
                _accumulatedSeconds -= _settings.SecondsPerGameDayAtNormalSpeed;
                daysToAdvance++;
            }

            if (daysToAdvance >= MaxDaysAdvancedPerTick)
            {
                _accumulatedSeconds = 0f;
                GameLog.Warning($"[GameClock] Avance plafonnee a {MaxDaysAdvancedPerTick} jours pour cette frame.");
            }

            if (daysToAdvance > 0)
            {
                AdvanceDays(daysToAdvance);
            }
        }

        /// <inheritdoc />
        public void Pause()
        {
            if (IsPaused)
            {
                return;
            }

            _speedBeforePause = CurrentSpeed;
            SetSpeedInternal(GameSpeed.Paused);
        }

        /// <inheritdoc />
        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            SetSpeedInternal(_speedBeforePause);
        }

        /// <inheritdoc />
        public void TogglePause()
        {
            if (IsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        /// <inheritdoc />
        public void SetSpeed(GameSpeed speed)
        {
            if (speed == GameSpeed.Paused)
            {
                Pause();
                return;
            }

            _speedBeforePause = speed;
            SetSpeedInternal(speed);
        }

        private void SetSpeedInternal(GameSpeed speed)
        {
            if (CurrentSpeed == speed)
            {
                return;
            }

            CurrentSpeed = speed;
            _eventBus.Publish(new GameSpeedChangedEvent(speed));
        }

        /// <summary>
        /// Avance le calendrier jour par jour (plutot que d'un bond) pour publier
        /// <see cref="DayAdvancedEvent"/> — et le cas echeant <see cref="MonthAdvancedEvent"/>,
        /// <see cref="YearAdvancedEvent"/> — une fois par jour reellement franchi.
        /// </summary>
        private void AdvanceDays(int days)
        {
            GameDate date = CurrentDate;

            for (int i = 0; i < days; i++)
            {
                date = date.AddDays(1);
                _eventBus.Publish(new DayAdvancedEvent(date));

                if (date.Day == 1)
                {
                    _eventBus.Publish(new MonthAdvancedEvent(date));

                    if (date.Month == 1)
                    {
                        _eventBus.Publish(new YearAdvancedEvent(date));
                    }
                }
            }

            CurrentDate = date;
        }
    }
}
