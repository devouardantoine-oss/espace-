using System;

namespace Espace.Core.States
{
    /// <summary>
    /// Premier etat traverse au lancement du jeu.
    /// <para>
    /// Role a terme : charger la sauvegarde, precharger les donnees de factions et
    /// d'unites, puis basculer sur le menu principal. En Phase 1 il ne fait que valider
    /// que la machine a etats fonctionne, y compris la transition declenchee depuis
    /// <see cref="Enter"/>.
    /// </para>
    /// <para>
    /// <b>Choix technique :</b> l'etat ne connait pas la machine a etats ni l'etat suivant.
    /// Il recoit un simple <c>Action</c> « boot termine ». C'est une inversion de controle :
    /// l'etat reste ignorant du graphe de transitions, qui appartient au <c>GameManager</c>.
    /// </para>
    /// </summary>
    public sealed class BootState : IGameState
    {
        private readonly Action _onBootCompleted;

        /// <param name="onBootCompleted">Invoque quand l'initialisation est terminee.</param>
        public BootState(Action onBootCompleted)
        {
            _onBootCompleted = onBootCompleted ?? throw new ArgumentNullException(nameof(onBootCompleted));
        }

        /// <inheritdoc />
        public void Enter()
        {
            GameLog.Info("[FSM] Entree dans BootState");

            // Phase 1 : rien a charger, on enchaine immediatement.
            // Phases suivantes : chargement de la sauvegarde et des ScriptableObjects ici.
            _onBootCompleted.Invoke();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // Etat instantane : aucune logique par frame.
        }

        /// <inheritdoc />
        public void Exit()
        {
            GameLog.Info("[FSM] Sortie de BootState");
        }
    }
}
