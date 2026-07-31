using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Military
{
    /// <summary>Etat d'un <see cref="Fleet"/>.</summary>
    public enum FleetStatus
    {
        /// <summary>Stationnee sur un systeme : constitue la garnison de ce systeme pour son proprietaire.</summary>
        Stationed,

        /// <summary>En route vers <see cref="Fleet.DestinationSystemId"/>, arrivee de l'etape courante prevue a <see cref="Fleet.ArrivalDate"/>.</summary>
        Moving,

        /// <summary>
        /// Immobilisee par une rencontre spatiale en attente de resolution (Phase 17). Le trajet
        /// est gele : les jours restants sont memorises et reemis a la resolution, sans dependre
        /// d'une mise en pause de l'horloge.
        /// </summary>
        AwaitingEncounter
    }

    /// <summary>
    /// Groupe d'unites appartenant a un empire : soit stationnee (garnison d'un systeme), soit
    /// en deplacement vers un systeme adjacent.
    /// <para>
    /// <b>Au plus une flotte stationnee par (systeme, proprietaire) :</b> <see cref="Espace.Gameplay.Military.MilitaryService"/>
    /// fusionne toute arrivee dans la flotte stationnee existante plutot que d'en garder
    /// plusieurs distinctes au meme endroit — la resolution de combat n'a ainsi jamais a
    /// combiner plusieurs flottes du meme camp avant de comparer les puissances.
    /// </para>
    /// <para>
    /// <b>Identifiant attribue par <see cref="MilitaryService"/></b>, pas par un compteur
    /// statique interne a la classe : un compteur statique partagerait un etat mutable entre
    /// tests (executions non isolees), ce qu'evite deja tout le reste du projet (voir
    /// <see cref="Espace.Core.ServiceLocator"/>).
    /// </para>
    /// </summary>
    public sealed class Fleet
    {
        public int Id { get; }
        public int OwnerId { get; }

        /// <summary>
        /// Nom de la flotte (Phase 14), attribue automatiquement a la creation. Pas encore
        /// renommable par le joueur faute de besoin exprime cette phase.
        /// </summary>
        public string Name { get; }

        /// <summary>Commandant de la flotte (Phase 15), attribue automatiquement a la creation. Voir <see cref="Admiral"/>.</summary>
        public Admiral Admiral { get; }

        public UnitBundle Composition { get; private set; }
        public FleetStatus Status { get; private set; }

        /// <summary>Systeme ou la flotte est stationnee (si <see cref="Status"/> vaut <see cref="FleetStatus.Stationed"/>).</summary>
        public StarSystemId CurrentSystemId { get; private set; }

        /// <summary>Systeme quitte au depart du voyage, conserve pour permettre une retraite en cas de defaite.</summary>
        public StarSystemId OriginSystemId { get; private set; }

        /// <summary>
        /// Itineraire complet du voyage en cours, extremites incluses (Phase 17), ou <c>null</c>
        /// si la flotte est stationnee. Calcule une fois au depart par
        /// <see cref="HyperlanePathfinder"/> et jamais recalcule en vol.
        /// </summary>
        public IReadOnlyList<StarSystemId> Route { get; private set; }

        /// <summary>Indice du systeme d'ou part l'etape en cours : la flotte va de <see cref="CurrentLegFrom"/> vers <see cref="CurrentLegTo"/>.</summary>
        public int RouteIndex { get; private set; }

        /// <summary>Date de depart de l'etape en cours, pour situer la flotte sur son tronçon.</summary>
        public GameDate? DepartureDate { get; private set; }

        /// <summary>
        /// Date de depart du voyage entier. Sert de reference pour calculer chaque etape a partir
        /// de la distance <i>cumulee</i> (un seul arrondi pour tout le trajet). Decalee d'autant
        /// de jours qu'une rencontre en a fait perdre, pour que les etapes suivantes restent dans
        /// le futur.
        /// </summary>
        public GameDate? JourneyStartDate { get; private set; }

        /// <summary>Destination finale du voyage : le dernier systeme de <see cref="Route"/>.</summary>
        public StarSystemId? DestinationSystemId =>
            Route == null || Route.Count == 0 ? (StarSystemId?)null : Route[Route.Count - 1];

        /// <summary>Systeme d'ou part l'etape en cours (<c>null</c> si la flotte ne voyage pas).</summary>
        public StarSystemId? CurrentLegFrom =>
            Route == null || RouteIndex >= Route.Count ? (StarSystemId?)null : Route[RouteIndex];

        /// <summary>Systeme vers lequel se dirige l'etape en cours (<c>null</c> si la flotte ne voyage pas).</summary>
        public StarSystemId? CurrentLegTo =>
            Route == null || RouteIndex + 1 >= Route.Count ? (StarSystemId?)null : Route[RouteIndex + 1];

        /// <summary>Vrai si l'etape en cours est la derniere : son arrivee resout le voyage.</summary>
        public bool IsOnFinalLeg => Route != null && RouteIndex + 2 >= Route.Count;

        /// <summary>Date d'arrivee de l'etape en cours (si la flotte voyage).</summary>
        public GameDate? ArrivalDate { get; private set; }

        /// <summary>Vrai si ce trajet est un repli force apres une bataille perdue ou une rencontre, plutot qu'un ordre volontaire.</summary>
        public bool IsRetreating { get; private set; }

        /// <summary>Jours de trajet restants memorises pendant une rencontre (voir <see cref="FreezeForEncounter"/>).</summary>
        private int _frozenRemainingDays;

        /// <summary>
        /// <paramref name="name"/> et <paramref name="admiral"/> sont optionnels : omis (ou
        /// <c>null</c>), un nom et un Amiral sont generes automatiquement. Fournis, ils
        /// restaurent un nom/Amiral existant plutot que d'en generer un nouveau — utilise par
        /// <c>SaveService</c> au chargement d'une sauvegarde.
        /// </summary>
        public Fleet(int id, int ownerId, StarSystemId stationedAt, UnitBundle composition, string name = null, Admiral? admiral = null)
        {
            Id = id;
            OwnerId = ownerId;
            Name = string.IsNullOrEmpty(name) ? $"Flotte {id}" : name;
            Admiral = admiral ?? Admiral.Compute(id, ownerId);
            CurrentSystemId = stationedAt;
            OriginSystemId = stationedAt;
            Composition = composition;
            Status = FleetStatus.Stationed;
        }

        /// <summary>Ajoute des unites a une flotte stationnee (recrutement complete, renfort).</summary>
        public void AddUnits(UnitBundle units)
        {
            Composition += units;
        }

        /// <summary>Remplace la composition (pertes de bataille appliquees par l'appelant).</summary>
        public void SetComposition(UnitBundle composition)
        {
            Composition = composition;
        }

        /// <summary>
        /// Lance un voyage le long de <paramref name="route"/> (extremites incluses), en
        /// memorisant le systeme quitte.
        /// <para>
        /// <b><see cref="OriginSystemId"/> et <see cref="CurrentSystemId"/> ne bougent plus
        /// jusqu'a l'arrivee finale</b>, meme en franchissant des points de passage :
        /// <c>MilitaryService.ComputeAttackerModifier</c> lit le moral du <i>systeme d'origine</i>
        /// et <c>RetreatToOrigin</c> y renvoie la flotte. Les faire avancer d'etape en etape
        /// ferait varier silencieusement la puissance de combat selon le dernier systeme survole,
        /// et replierait la flotte d'un seul saut au lieu de la ramener chez elle.
        /// </para>
        /// </summary>
        public void BeginJourney(IReadOnlyList<StarSystemId> route, GameDate departureDate, GameDate legArrivalDate, bool isRetreating)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (route.Count < 2) throw new ArgumentException("Un voyage exige au moins une origine et une destination.", nameof(route));

            OriginSystemId = CurrentSystemId;
            Route = route;
            RouteIndex = 0;
            JourneyStartDate = departureDate;
            DepartureDate = departureDate;
            ArrivalDate = legArrivalDate;
            IsRetreating = isRetreating;
            Status = FleetStatus.Moving;
        }

        /// <summary>Restaure un voyage depuis une sauvegarde, etape et dates comprises (Phase 17).</summary>
        public void RestoreJourney(
            IReadOnlyList<StarSystemId> route, int routeIndex, StarSystemId originSystemId,
            GameDate journeyStartDate, GameDate departureDate, GameDate legArrivalDate, bool isRetreating)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (route.Count < 2) throw new ArgumentException("Un voyage exige au moins une origine et une destination.", nameof(route));

            OriginSystemId = originSystemId;
            Route = route;
            RouteIndex = Math.Clamp(routeIndex, 0, route.Count - 2);
            JourneyStartDate = journeyStartDate;
            DepartureDate = departureDate;
            ArrivalDate = legArrivalDate;
            IsRetreating = isRetreating;
            Status = FleetStatus.Moving;
        }

        /// <summary>
        /// Franchit un point de passage et entame l'etape suivante. Ne touche ni
        /// <see cref="OriginSystemId"/> ni <see cref="CurrentSystemId"/> (voir <see cref="BeginJourney"/>).
        /// </summary>
        public void AdvanceToNextLeg(GameDate departureDate, GameDate legArrivalDate)
        {
            if (Route == null || RouteIndex + 2 >= Route.Count)
            {
                throw new InvalidOperationException("Aucune etape suivante : ce voyage est sur sa derniere etape.");
            }

            RouteIndex++;
            DepartureDate = departureDate;
            ArrivalDate = legArrivalDate;
        }

        /// <summary>Termine le voyage : la flotte est desormais stationnee sur <paramref name="arrivedAt"/>.</summary>
        public void CompleteMove(StarSystemId arrivedAt)
        {
            CurrentSystemId = arrivedAt;
            Status = FleetStatus.Stationed;
            Route = null;
            RouteIndex = 0;
            JourneyStartDate = null;
            DepartureDate = null;
            ArrivalDate = null;
            IsRetreating = false;
            _frozenRemainingDays = 0;
        }

        /// <summary>
        /// Immobilise la flotte le temps qu'une rencontre soit tranchee, en memorisant les jours
        /// de trajet qui lui restaient. <b>Ne depend pas d'une mise en pause de l'horloge :</b>
        /// celle-ci est purement cosmetique (le compteur de jours tourne par lots dans une meme
        /// frame et le joueur peut relancer le temps depuis la barre du HUD).
        /// </summary>
        public void FreezeForEncounter(GameDate currentDate)
        {
            _frozenRemainingDays = ArrivalDate == null
                ? 1
                : Math.Max(1, ArrivalDate.Value.ToDayIndex() - currentDate.ToDayIndex());
            Status = FleetStatus.AwaitingEncounter;
        }

        /// <summary>Relache la flotte apres une rencontre : l'etape en cours reprend pour les jours qui lui restaient.</summary>
        public void ResumeAfterEncounter(GameDate currentDate)
        {
            if (Status != FleetStatus.AwaitingEncounter)
            {
                return;
            }

            GameDate newArrival = currentDate.AddDays(Math.Max(1, _frozenRemainingDays));

            // Le voyage entier glisse d'autant de jours que la rencontre en a coute, sinon les
            // etapes suivantes — calculees depuis JourneyStartDate — tomberaient dans le passe.
            if (ArrivalDate != null && JourneyStartDate != null)
            {
                int lostDays = newArrival.ToDayIndex() - ArrivalDate.Value.ToDayIndex();
                if (lostDays > 0)
                {
                    JourneyStartDate = JourneyStartDate.Value.AddDays(lostDays);
                }
            }

            DepartureDate = currentDate;
            ArrivalDate = newArrival;
            _frozenRemainingDays = 0;
            Status = FleetStatus.Moving;
        }
    }
}
