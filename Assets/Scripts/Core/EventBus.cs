using System;
using System.Collections.Generic;

namespace Espace.Core
{
    /// <summary>
    /// Implementation par defaut de <see cref="IEventBus"/>.
    /// <para>
    /// <b>Choix technique :</b> un <c>Dictionary&lt;Type, Delegate&gt;</c> ou chaque entree
    /// est un delegue <i>multicast</i> (<c>Action&lt;TEvent&gt;</c>). La publication est un
    /// simple <c>Invoke</c> sur ce multicast : aucune liste temporaire, aucun boxing,
    /// donc <b>zero allocation</b> sur le chemin chaud — important sur mobile ou chaque
    /// allocation rapproche un pic de GC.
    /// </para>
    /// <para>
    /// <b>Limite assumee pour le MVP :</b> si un abonne leve une exception, les abonnes
    /// suivants ne sont pas appeles. Isoler chaque appel imposerait un
    /// <c>GetInvocationList()</c> qui alloue a chaque publication. On preferera corriger
    /// les abonnes fautifs plutot que payer ce cout en permanence.
    /// </para>
    /// </summary>
    public sealed class EventBus : IEventBus, IGameService
    {
        private const int InitialCapacity = 32;

        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>(InitialCapacity);

        /// <inheritdoc />
        public void Initialize()
        {
            // Rien a initialiser : le bus est operationnel des sa construction.
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _handlers.Clear();
        }

        /// <inheritdoc />
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Type key = typeof(TEvent);
            _handlers[key] = _handlers.TryGetValue(key, out Delegate existing)
                ? Delegate.Combine(existing, handler)
                : handler;
        }

        /// <inheritdoc />
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null)
            {
                return;
            }

            Type key = typeof(TEvent);
            if (!_handlers.TryGetValue(key, out Delegate existing))
            {
                return;
            }

            Delegate remaining = Delegate.Remove(existing, handler);
            if (remaining == null)
            {
                // Plus aucun abonne : on retire l'entree pour ne pas garder de cle morte.
                _handlers.Remove(key);
            }
            else
            {
                _handlers[key] = remaining;
            }
        }

        /// <inheritdoc />
        public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
        {
            if (_handlers.TryGetValue(typeof(TEvent), out Delegate stored) && stored is Action<TEvent> callback)
            {
                callback.Invoke(gameEvent);
            }
        }

        /// <summary>
        /// Nombre d'abonnes pour <typeparamref name="TEvent"/>.
        /// Reserve aux tests et au diagnostic : cette methode alloue (<c>GetInvocationList</c>).
        /// </summary>
        public int GetSubscriberCount<TEvent>() where TEvent : IGameEvent
        {
            return _handlers.TryGetValue(typeof(TEvent), out Delegate stored)
                ? stored.GetInvocationList().Length
                : 0;
        }
    }
}
