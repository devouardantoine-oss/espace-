using System;

namespace Espace.Core
{
    /// <summary>
    /// Machine a etats finis minimaliste pilotant le flux global du jeu.
    /// <para>
    /// <b>Reentrance :</b> un etat a souvent besoin de declencher la transition suivante
    /// depuis son propre <c>Enter()</c> (ex : <c>BootState</c> qui enchaine sur le menu une
    /// fois l'initialisation terminee). Un simple garde-fou booleen rejetterait cette
    /// demande. On met donc la transition demandee <b>en attente</b> et on la traite dans la
    /// boucle courante : la pile d'appel ne grandit pas, et aucune transition n'est perdue.
    /// </para>
    /// </summary>
    public sealed class GameStateMachine
    {
        private IGameState _pendingState;
        private bool _isTransitioning;

        /// <summary>Etat actif, ou <c>null</c> tant qu'aucune transition n'a eu lieu.</summary>
        public IGameState CurrentState { get; private set; }

        /// <summary>
        /// Emis apres chaque transition effective. Arguments : (etat precedent, nouvel etat).
        /// L'etat precedent peut etre <c>null</c> lors de la toute premiere transition.
        /// </summary>
        public event Action<IGameState, IGameState> StateChanged;

        /// <summary>
        /// Bascule vers <paramref name="nextState"/>. Sequence garantie :
        /// <c>Exit()</c> de l'ancien, puis <c>Enter()</c> du nouveau, puis <see cref="StateChanged"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Si l'etat cible est null.</exception>
        public void ChangeState(IGameState nextState)
        {
            if (nextState == null)
            {
                throw new ArgumentNullException(nameof(nextState));
            }

            // Appel reentrant (declenche depuis Enter/Exit) : on empile et on laisse la
            // boucle englobante traiter la demande.
            if (_isTransitioning)
            {
                _pendingState = nextState;
                return;
            }

            _isTransitioning = true;
            try
            {
                IGameState target = nextState;
                while (target != null)
                {
                    _pendingState = null;

                    if (!ReferenceEquals(target, CurrentState))
                    {
                        IGameState previous = CurrentState;

                        previous?.Exit();
                        CurrentState = target;
                        target.Enter();

                        StateChanged?.Invoke(previous, target);
                    }

                    // Un Enter()/Exit() a-t-il demande une nouvelle transition ?
                    target = _pendingState;
                }
            }
            finally
            {
                _isTransitioning = false;
                _pendingState = null;
            }
        }

        /// <summary>Fait avancer l'etat courant d'une frame.</summary>
        public void Tick(float deltaTime)
        {
            CurrentState?.Tick(deltaTime);
        }

        /// <summary>
        /// Sort de l'etat courant sans en entrer un nouveau. Appele a l'arret du jeu
        /// pour garantir que chaque <c>Enter()</c> a bien son <c>Exit()</c>.
        /// </summary>
        public void Stop()
        {
            if (CurrentState == null)
            {
                return;
            }

            IGameState previous = CurrentState;
            CurrentState = null;
            previous.Exit();
        }
    }
}
