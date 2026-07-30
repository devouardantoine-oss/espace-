namespace Espace.Core
{
    /// <summary>
    /// Contrat commun a tous les services du jeu (economie, sauvegarde, bataille...).
    /// <para>
    /// Un service est un objet C# pur, sans dependance a la scene : il est cree une
    /// seule fois par <c>GameBootstrap</c>, enregistre dans le <see cref="ServiceLocator"/>
    /// puis consomme via son <b>interface</b>, jamais via son type concret.
    /// C'est ce qui permet de respecter le principe d'inversion des dependances (SOLID/DIP)
    /// et de remplacer une implementation (ex : sauvegarde JSON -> cloud) sans toucher
    /// aux appelants.
    /// </para>
    /// </summary>
    public interface IGameService
    {
        /// <summary>
        /// Appele une fois, apres l'enregistrement de tous les services.
        /// Les dependances vers d'autres services doivent etre resolues ici,
        /// et non dans le constructeur, pour eviter les problemes d'ordre d'initialisation.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Appele a la destruction du bootstrap. Doit liberer toute ressource
        /// et se desabonner de tout evenement afin d'eviter les fuites entre deux parties.
        /// </summary>
        void Shutdown();
    }
}
