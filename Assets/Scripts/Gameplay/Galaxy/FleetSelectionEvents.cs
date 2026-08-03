using Espace.Core;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Publie quand le joueur touche un vaisseau sur la carte galactique (Phase 20).
    /// <para>
    /// Ne transporte que l'identifiant de la flotte, comme <see cref="SystemSelectedEvent"/> ne
    /// transporte que celui du systeme : les abonnes qui ont besoin de la composition ou de
    /// l'itineraire interrogent <c>IMilitaryService</c>, qu'ils resolvent deja.
    /// </para>
    /// <para>
    /// <b>Selectionner un vaisseau deselectionne le systeme</b>, et inversement : les deux
    /// selections sont exclusives, et c'est <see cref="GalaxySelectionController"/> qui publie
    /// l'une ou l'autre a chaque appui. Deux panneaux contextuels ouverts en meme temps se
    /// disputeraient le bas de l'ecran.
    /// </para>
    /// </summary>
    public readonly struct FleetSelectedEvent : IGameEvent
    {
        public readonly int FleetId;

        public FleetSelectedEvent(int fleetId)
        {
            FleetId = fleetId;
        }
    }

    /// <summary>Publie quand la selection de flotte est abandonnee (appui ailleurs, ou flotte disparue).</summary>
    public readonly struct FleetDeselectedEvent : IGameEvent
    {
    }
}
