namespace Espace.Core
{
    /// <summary>
    /// Horloge de jeu : lecture et controle du temps, sans exposer la methode <c>Tick</c>
    /// qui fait avancer le temps.
    /// <para>
    /// <b>Pourquoi <c>Tick</c> n'est pas sur cette interface ?</b> Seul <c>GameBootstrap</c>
    /// doit faire avancer l'horloge (une fois par frame, dans son unique boucle de mise a
    /// jour) ; tout le reste du jeu (UI, economie, IA) ne doit que la <b>lire</b> ou en
    /// <b>changer la vitesse</b>. Separer les deux empeche un systeme quelconque de faire
    /// avancer le temps par erreur en resolvant <see cref="IGameClock"/> depuis le
    /// <see cref="ServiceLocator"/>.
    /// </para>
    /// </summary>
    public interface IGameClock
    {
        /// <summary>Date courante dans le calendrier du jeu.</summary>
        GameDate CurrentDate { get; }

        /// <summary>Vitesse courante (peut valoir <see cref="GameSpeed.Paused"/>).</summary>
        GameSpeed CurrentSpeed { get; }

        /// <summary>Multiplicateur de temps reellement applique a <see cref="CurrentSpeed"/> (0 si en pause).</summary>
        float CurrentMultiplier { get; }

        /// <summary>Raccourci pour <c>CurrentSpeed == GameSpeed.Paused</c>.</summary>
        bool IsPaused { get; }

        /// <summary>Met le temps en pause, en memorisant la vitesse courante pour <see cref="Resume"/>.</summary>
        void Pause();

        /// <summary>Reprend a la vitesse active avant la derniere pause (Normale si aucune vitesse memorisee).</summary>
        void Resume();

        /// <summary>Bascule entre pause et reprise.</summary>
        void TogglePause();

        /// <summary>
        /// Definit la vitesse. Passer <see cref="GameSpeed.Paused"/> equivaut a appeler
        /// <see cref="Pause"/> ; toute autre valeur reprend implicitement le temps si celui-ci
        /// etait en pause.
        /// </summary>
        void SetSpeed(GameSpeed speed);

        /// <summary>
        /// Impose directement <see cref="CurrentDate"/>, sans publier <c>DayAdvancedEvent</c>
        /// ni <c>MonthAdvancedEvent</c> pour les jours « sautes ». Reserve au chargement d'une
        /// sauvegarde (Phase 10) — meme restriction de principe que l'absence de <c>Tick</c>
        /// sur cette interface : personne d'autre que ce cas precis ne doit deplacer le temps
        /// autrement qu'en le laissant s'ecouler.
        /// </summary>
        void SetDate(GameDate date);
    }
}
