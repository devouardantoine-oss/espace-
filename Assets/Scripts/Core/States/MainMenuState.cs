namespace Espace.Core.States
{
    /// <summary>
    /// Etat « menu principal » : point d'arrivee du demarrage.
    /// <para>
    /// Etat terminal qui se contente de signaler qu'il est actif : l'ecran reellement affiche
    /// (Nouvelle partie / Continuer / Quitter) est un <c>MonoBehaviour</c> IMGUI independant
    /// (<c>Espace.UI.MainMenuController</c>, Phase 11) place directement dans la scene
    /// <c>Bootstrap</c>, pas piloté par cet etat — voir sa remarque sur <c>ISceneLoader</c>.
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
