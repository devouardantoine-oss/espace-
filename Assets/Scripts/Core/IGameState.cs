namespace Espace.Core
{
    /// <summary>
    /// Un etat macro du jeu (demarrage, menu principal, carte galactique, bataille, fin de partie).
    /// <para>
    /// Chaque etat encapsule ce qui est actif a un instant donne. Cela evite le classique
    /// <c>if (isInBattle) ... else if (isInMenu) ...</c> dispersé dans tout le code, et
    /// respecte le principe ouvert/ferme : ajouter un etat n'oblige pas a modifier les autres.
    /// </para>
    /// </summary>
    public interface IGameState
    {
        /// <summary>Appele une fois a l'entree dans l'etat (chargement de scene, abonnements, UI).</summary>
        void Enter();

        /// <summary>
        /// Appele a chaque frame tant que l'etat est actif.
        /// <paramref name="deltaTime"/> est passe en parametre plutot que lu depuis
        /// <c>Time.deltaTime</c> : l'etat reste du C# pur, donc testable hors Play Mode.
        /// </summary>
        void Tick(float deltaTime);

        /// <summary>Appele une fois a la sortie de l'etat (desabonnements, liberation des ressources).</summary>
        void Exit();
    }
}
