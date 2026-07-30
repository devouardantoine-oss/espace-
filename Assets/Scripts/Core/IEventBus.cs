using System;

namespace Espace.Core
{
    /// <summary>
    /// Bus d'evenements typé : permet a deux systemes de communiquer sans se connaitre.
    /// <para>
    /// Exemple concret pour la suite du MVP : l'economie publie <c>ResourcesChangedEvent</c>,
    /// le HUD s'y abonne. L'economie n'a aucune reference vers l'UI, et l'UI peut etre
    /// supprimee sans casser l'economie.
    /// </para>
    /// </summary>
    public interface IEventBus
    {
        /// <summary>Abonne <paramref name="handler"/> aux evenements de type <typeparamref name="TEvent"/>.</summary>
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;

        /// <summary>
        /// Desabonne <paramref name="handler"/>. <b>Toujours</b> appeler dans <c>OnDestroy</c>
        /// / <c>Shutdown</c> : un abonne detruit mais toujours reference fait fuir la memoire.
        /// </summary>
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;

        /// <summary>Diffuse <paramref name="gameEvent"/> a tous les abonnes de <typeparamref name="TEvent"/>.</summary>
        void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent;
    }
}
