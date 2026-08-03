using Espace.Core;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Ou se trouve une flotte en vol, et dans quelle direction elle pointe (Phase 20).
    /// </summary>
    public readonly struct FleetPose
    {
        /// <summary>Position sur la carte, en unites monde.</summary>
        public readonly Vector2 Position;

        /// <summary>Cap, en degres, dans le sens trigonometrique depuis l'axe X.</summary>
        public readonly float HeadingDegrees;

        /// <summary>Avancement sur l'etape en cours, de 0 (au depart) a 1 (arrive).</summary>
        public readonly float LegProgress;

        public FleetPose(Vector2 position, float headingDegrees, float legProgress)
        {
            Position = position;
            HeadingDegrees = headingDegrees;
            LegProgress = legProgress;
        }
    }

    /// <summary>
    /// Traduit l'etat d'une flotte en voyage en une position et un cap sur la carte
    /// galactique (Phase 20).
    /// <para>
    /// <b>Fonction pure, separee du rendu</b> — meme raison que <c>TerritoryPartition</c> ou
    /// <c>ColonizationRules</c> : le calcul est verifiable en EditMode, sans scene ni camera.
    /// C'est ce qui permet de garantir qu'un vaisseau ne se retrouvera jamais derriere son
    /// systeme de depart ou au-dela de sa destination.
    /// </para>
    /// <para>
    /// <b>Le temps du jeu avance par jours entiers</b> (<see cref="GameDate"/> n'a pas de
    /// fraction, et <c>IGameClock</c> n'en expose pas). L'avancement calcule ici progresse donc
    /// par paliers d'un jour ; c'est au rendu de lisser le deplacement entre deux paliers
    /// (voir <c>FleetMapController</c>). Exposer une fraction de jour sur <c>IGameClock</c>
    /// aurait oblige a modifier ses sept doublures de test pour un besoin purement cosmetique.
    /// </para>
    /// </summary>
    public static class FleetPresentation
    {
        /// <summary>
        /// Position et cap de <paramref name="fleet"/> si elle est en voyage.
        /// </summary>
        /// <returns>
        /// <c>false</c> si la flotte est stationnee ou si son etape en cours est incomplete :
        /// aucun vaisseau ne doit alors etre dessine.
        /// </returns>
        public static bool TryComputePose(Fleet fleet, GalaxyMap map, GameDate currentDate, out FleetPose pose)
        {
            pose = default;

            if (fleet == null || map == null || fleet.Status == FleetStatus.Stationed)
            {
                return false;
            }

            if (!fleet.CurrentLegFrom.HasValue || !fleet.CurrentLegTo.HasValue
                || !fleet.DepartureDate.HasValue || !fleet.ArrivalDate.HasValue)
            {
                return false;
            }

            if (!map.TryGetSystem(fleet.CurrentLegFrom.Value, out StarSystemState from)
                || !map.TryGetSystem(fleet.CurrentLegTo.Value, out StarSystemState to))
            {
                return false;
            }

            float progress = ComputeLegProgress(fleet.DepartureDate.Value, fleet.ArrivalDate.Value, currentDate);
            Vector2 position = Vector2.Lerp(from.Position, to.Position, progress);

            pose = new FleetPose(position, HeadingDegrees(from.Position, to.Position), progress);
            return true;
        }

        /// <summary>
        /// Avancement de 0 a 1 entre <paramref name="departure"/> et <paramref name="arrival"/>.
        /// <para>
        /// <b>Toujours borne :</b> une flotte immobilisee par une rencontre spatiale
        /// (<see cref="FleetStatus.AwaitingEncounter"/>) voit la date courante depasser sa date
        /// d'arrivee, puisque son trajet est gele mais pas le calendrier. Sans la borne, son
        /// vaisseau continuerait au-dela de sa destination. Il s'arrete donc sur celle-ci —
        /// approximation assumee&nbsp;: la position exacte du gel n'est pas conservee par
        /// <see cref="Fleet"/>, et l'ecart ne dure que le temps de resoudre la rencontre.
        /// </para>
        /// <para>
        /// Une etape d'une duree nulle (depart et arrivee le meme jour) vaut 1 plutot que de
        /// diviser par zero : la flotte est deja arrivee.
        /// </para>
        /// </summary>
        public static float ComputeLegProgress(GameDate departure, GameDate arrival, GameDate current)
        {
            int total = arrival.ToDayIndex() - departure.ToDayIndex();
            if (total <= 0)
            {
                return 1f;
            }

            int elapsed = current.ToDayIndex() - departure.ToDayIndex();
            return Mathf.Clamp01((float)elapsed / total);
        }

        /// <summary>
        /// Cap en degres pour aller de <paramref name="from"/> vers <paramref name="to"/>. Deux
        /// points confondus renvoient 0 plutot qu'une valeur indefinie.
        /// </summary>
        public static float HeadingDegrees(Vector2 from, Vector2 to)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < 1e-8f)
            {
                return 0f;
            }

            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }
    }
}
