using System;
using System.Collections.Generic;

namespace Espace.Core
{
    /// <summary>
    /// Registre central des services du jeu.
    /// <para>
    /// <b>Pourquoi pas un singleton par manager ?</b> Un <c>Manager.Instance</c> cree une
    /// dependance forte vers un type concret : impossible de le remplacer, de le mocker
    /// en test, ou de garantir un ordre d'initialisation. Ici, chaque service est
    /// enregistre <b>sous le type que les appelants utiliseront</b> (typiquement une
    /// interface) et resolu au runtime.
    /// </para>
    /// <example>
    /// <code>
    /// // Enregistrement (une seule fois, dans GameBootstrap) :
    /// ServiceLocator.Register&lt;IEventBus&gt;(new EventBus());
    ///
    /// // Consommation (n'importe ou) :
    /// var bus = ServiceLocator.Get&lt;IEventBus&gt;();
    /// </code>
    /// </example>
    /// <para>
    /// La classe est statique et volontairement non thread-safe : tous les acces se font
    /// depuis le thread principal Unity, ce qui evite le cout d'un verrou.
    /// </para>
    /// </summary>
    public static class ServiceLocator
    {
        /// <summary>Capacite initiale : nombre de services attendus a terme (evite les re-allocations).</summary>
        private const int InitialCapacity = 16;

        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>(InitialCapacity);

        /// <summary>Nombre de services actuellement enregistres. Utile pour les diagnostics et les tests.</summary>
        public static int Count => Services.Count;

        /// <summary>
        /// Enregistre <paramref name="service"/> sous le type <typeparamref name="TService"/>.
        /// Le type generic est la <b>cle</b> : enregistrer sous une interface pour rester decouple.
        /// </summary>
        /// <exception cref="ArgumentNullException">Si le service est null.</exception>
        /// <exception cref="InvalidOperationException">Si un service est deja enregistre sous ce type.</exception>
        public static void Register<TService>(TService service) where TService : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            Type key = typeof(TService);
            if (Services.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"ServiceLocator: un service est deja enregistre pour '{key.Name}'. " +
                    "Appelez Unregister ou Clear avant de le remplacer.");
            }

            Services.Add(key, service);
        }

        /// <summary>
        /// Recupere le service enregistre sous <typeparamref name="TService"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Si aucun service n'est enregistre : c'est une erreur de programmation
        /// (bootstrap incomplet), on echoue tot et bruyamment plutot que de propager un null.
        /// </exception>
        public static TService Get<TService>() where TService : class
        {
            if (!Services.TryGetValue(typeof(TService), out object service))
            {
                throw new InvalidOperationException(
                    $"ServiceLocator: aucun service enregistre pour '{typeof(TService).Name}'. " +
                    "Verifiez que GameBootstrap est present dans la scene et qu'il l'enregistre.");
            }

            return (TService)service;
        }

        /// <summary>
        /// Variante non levante de <see cref="Get{TService}"/>, pour les dependances optionnelles.
        /// </summary>
        /// <returns><c>true</c> si le service existe.</returns>
        public static bool TryGet<TService>(out TService service) where TService : class
        {
            if (Services.TryGetValue(typeof(TService), out object stored))
            {
                service = (TService)stored;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>Indique si un service est enregistre sous <typeparamref name="TService"/>.</summary>
        public static bool IsRegistered<TService>() where TService : class
        {
            return Services.ContainsKey(typeof(TService));
        }

        /// <summary>Retire le service enregistre sous <typeparamref name="TService"/>.</summary>
        /// <returns><c>true</c> si un service a effectivement ete retire.</returns>
        public static bool Unregister<TService>() where TService : class
        {
            return Services.Remove(typeof(TService));
        }

        /// <summary>
        /// Vide le registre. Appele au <c>Shutdown</c> du bootstrap et entre deux tests,
        /// indispensable car l'etat statique survit aux rechargements de scene en editeur.
        /// </summary>
        public static void Clear()
        {
            Services.Clear();
        }
    }
}
