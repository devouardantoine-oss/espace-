using Espace.Core;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Publie via l'<see cref="IEventBus"/> quand le joueur selectionne un systeme au toucher.
    /// <para>
    /// Ne transporte que l'identifiant : les abonnes qui ont besoin des details (nom,
    /// population...) interrogent la <see cref="GalaxyMap"/> qu'ils possedent deja plutot
    /// que de dupliquer ces donnees dans l'evenement.
    /// </para>
    /// </summary>
    public readonly struct SystemSelectedEvent : IGameEvent
    {
        public readonly StarSystemId SystemId;

        public SystemSelectedEvent(StarSystemId systemId)
        {
            SystemId = systemId;
        }
    }

    /// <summary>
    /// Publie quand le joueur touche le fond de la carte (hors de tout systeme),
    /// desactivant la selection courante.
    /// </summary>
    public readonly struct SystemDeselectedEvent : IGameEvent
    {
    }
}
