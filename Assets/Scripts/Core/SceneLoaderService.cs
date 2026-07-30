using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Espace.Core
{
    /// <summary>
    /// Implementation de <see cref="ISceneLoader"/> basee sur <c>SceneManager</c>.
    /// <para>
    /// <b>Choix technique :</b> on s'appuie sur l'evenement <c>AsyncOperation.completed</c>
    /// plutot que sur une coroutine. Consequence : ce service reste un objet C# pur, sans
    /// MonoBehaviour hote, ce qui evite d'introduire une dependance a la scene dans la
    /// couche Core. Le chargement reste asynchrone, donc sans freeze visible sur mobile.
    /// </para>
    /// </summary>
    public sealed class SceneLoaderService : ISceneLoader, IGameService
    {
        /// <inheritdoc />
        public bool IsLoading { get; private set; }

        /// <inheritdoc />
        public void Initialize()
        {
            IsLoading = false;
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            IsLoading = false;
        }

        /// <inheritdoc />
        public void LoadScene(string sceneName, Action onCompleted = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Le nom de scene ne peut pas etre vide.", nameof(sceneName));
            }

            // Deux chargements simultanes laisseraient le jeu dans un etat imprevisible :
            // on ignore la seconde demande plutot que de la mettre en file (suffisant pour le MVP).
            if (IsLoading)
            {
                GameLog.Warning($"[SceneLoader] Chargement de '{sceneName}' ignore : '{SceneManager.GetActiveScene().name}' est deja en cours de transition.");
                return;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                GameLog.Error($"[SceneLoader] Scene '{sceneName}' introuvable. Est-elle ajoutee aux Build Settings ?");
                return;
            }

            IsLoading = true;
            operation.completed += _ =>
            {
                IsLoading = false;
                onCompleted?.Invoke();
            };
        }
    }
}
