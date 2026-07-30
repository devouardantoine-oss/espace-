namespace Espace.Core
{
    /// <summary>
    /// Publie par <see cref="GameClock"/> chaque fois qu'un jour de jeu s'ecoule.
    /// <para>
    /// Publie exactement une fois par jour ecoule, meme si plusieurs jours s'ecoulent dans
    /// la meme frame (vitesse elevee, ou reprise apres une longue pause de l'application) :
    /// un systeme qui doit produire des ressources chaque jour (Phase 4) ne doit jamais en
    /// sauter un.
    /// </para>
    /// </summary>
    public readonly struct DayAdvancedEvent : IGameEvent
    {
        public readonly GameDate Date;

        public DayAdvancedEvent(GameDate date)
        {
            Date = date;
        }
    }

    /// <summary>Publie en plus de <see cref="DayAdvancedEvent"/> le premier jour de chaque mois.</summary>
    public readonly struct MonthAdvancedEvent : IGameEvent
    {
        public readonly GameDate Date;

        public MonthAdvancedEvent(GameDate date)
        {
            Date = date;
        }
    }

    /// <summary>Publie en plus de <see cref="MonthAdvancedEvent"/> le premier jour de chaque annee.</summary>
    public readonly struct YearAdvancedEvent : IGameEvent
    {
        public readonly GameDate Date;

        public YearAdvancedEvent(GameDate date)
        {
            Date = date;
        }
    }

    /// <summary>
    /// Publie quand la vitesse de l'horloge change, y compris la mise en pause
    /// (<see cref="GameSpeed.Paused"/>) et la reprise.
    /// </summary>
    public readonly struct GameSpeedChangedEvent : IGameEvent
    {
        public readonly GameSpeed NewSpeed;

        public GameSpeedChangedEvent(GameSpeed newSpeed)
        {
            NewSpeed = newSpeed;
        }
    }
}
