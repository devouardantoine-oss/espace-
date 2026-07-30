namespace Espace.Core
{
    /// <summary>
    /// Interface marqueur des messages transitant par l'<see cref="EventBus"/>.
    /// <para>
    /// <b>Convention :</b> implementer <c>IGameEvent</c> sur des <c>readonly struct</c>
    /// immuables. Une struct evite une allocation par publication (donc pas de pression
    /// sur le GC, critique sur mobile) et l'immuabilite garantit qu'un abonne ne peut pas
    /// modifier l'evenement pour les suivants.
    /// </para>
    /// <example>
    /// <code>
    /// public readonly struct PlanetCapturedEvent : IGameEvent
    /// {
    ///     public readonly int PlanetId;
    ///     public PlanetCapturedEvent(int planetId) =&gt; PlanetId = planetId;
    /// }
    /// </code>
    /// </example>
    /// <para>
    /// Note : le bus est generique sur le type concret de l'evenement, il n'y a donc
    /// <b>pas de boxing</b> lors de la publication d'une struct.
    /// </para>
    /// </summary>
    public interface IGameEvent
    {
    }
}
