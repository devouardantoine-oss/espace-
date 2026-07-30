using Espace.Core;
using Espace.Core.States;

namespace Espace.Managers
{
    /// <summary>
    /// Service proprietaire du flux de jeu : il possede la <see cref="GameStateMachine"/>
    /// et le graphe des transitions entre etats.
    /// <para>
    /// <b>Responsabilite unique (SRP) :</b> ce manager ne fait <i>que</i> orchestrer les
    /// etats. Il ne connait ni l'economie, ni la bataille, ni l'UI — ces systemes seront
    /// des services distincts qui communiqueront via l'<see cref="IEventBus"/>.
    /// </para>
    /// <para>
    /// C'est un objet C# pur (pas un MonoBehaviour) : il est pilote par <c>GameBootstrap</c>,
    /// ce qui le rend testable sans lancer le Play Mode.
    /// </para>
    /// </summary>
    public sealed class GameManager : IGameService
    {
        private readonly GameStateMachine _stateMachine = new GameStateMachine();

        private BootState _bootState;
        private MainMenuState _mainMenuState;

        /// <summary>Machine a etats sous-jacente (lecture seule pour les consommateurs).</summary>
        public GameStateMachine StateMachine => _stateMachine;

        /// <summary>Etat « menu principal », cible des retours au menu.</summary>
        public IGameState MainMenu => _mainMenuState;

        /// <inheritdoc />
        public void Initialize()
        {
            // Les etats sont crees une fois puis reutilises : aucune allocation lors des
            // transitions ulterieures, ce qui evite de solliciter le GC en cours de partie.
            _mainMenuState = new MainMenuState();
            _bootState = new BootState(GoToMainMenu);

            _stateMachine.ChangeState(_bootState);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _stateMachine.Stop();
            _bootState = null;
            _mainMenuState = null;
        }

        /// <summary>Fait avancer l'etat courant. Appele par <c>GameBootstrap.Update</c>.</summary>
        public void Tick(float deltaTime)
        {
            _stateMachine.Tick(deltaTime);
        }

        /// <summary>Bascule vers un etat arbitraire.</summary>
        public void ChangeState(IGameState nextState)
        {
            _stateMachine.ChangeState(nextState);
        }

        /// <summary>Retourne au menu principal.</summary>
        public void GoToMainMenu()
        {
            _stateMachine.ChangeState(_mainMenuState);
        }
    }
}
