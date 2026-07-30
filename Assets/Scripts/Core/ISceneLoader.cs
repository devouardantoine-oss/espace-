using System;

namespace Espace.Core
{
    /// <summary>
    /// Abstraction du chargement de scene.
    /// <para>
    /// Le MVP utilise <c>SceneManager</c> (implementation la plus simple qui fonctionne).
    /// Les scenes de contenu passeront plus tard par Addressables pour reduire la taille
    /// du build mobile : seule une nouvelle implementation de cette interface sera a ecrire,
    /// aucun appelant ne changera.
    /// </para>
    /// </summary>
    public interface ISceneLoader
    {
        /// <summary>Vrai tant qu'un chargement est en cours.</summary>
        bool IsLoading { get; }

        /// <summary>
        /// Charge <paramref name="sceneName"/> en mode Single (remplace la scene courante).
        /// </summary>
        /// <param name="sceneName">Nom de la scene, telle que declaree dans les Build Settings.</param>
        /// <param name="onCompleted">Rappel optionnel invoque une fois la scene active.</param>
        void LoadScene(string sceneName, Action onCompleted = null);
    }
}
