namespace Espace.Core.States
{
    /// <summary>
    /// Etat « menu principal » : point d'arrivee du demarrage.
    /// <para>
    /// En Phase 1 c'est un etat terminal qui se contente de signaler qu'il est actif.
    /// Il recevra l'ecran principal (Nouvelle partie / Charger / Parametres) en Phase 9,
    /// et la transition vers la carte galactique en Phase 2.
    /// </para>
    /// </summary>
    public sealed class MainMenuState : IGameState
    {
        /// <inheritdoc />
        public void Enter()
        {
            GameLog.Info("[FSM] Entree dans MainMenuState - le socle est operationnel.");
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // Phase 9 : animations de l'ecran titre.
        }

        /// <inheritdoc />
        public void Exit()
        {
            GameLog.Info("[FSM] Sortie de MainMenuState");
        }
    }
}
