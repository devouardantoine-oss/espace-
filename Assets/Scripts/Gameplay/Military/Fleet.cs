using Espace.Core;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Military
{
    /// <summary>Etat d'un <see cref="Fleet"/>.</summary>
    public enum FleetStatus
    {
        /// <summary>Stationnee sur un systeme : constitue la garnison de ce systeme pour son proprietaire.</summary>
        Stationed,

        /// <summary>En route vers <see cref="Fleet.DestinationSystemId"/>, arrivee prevue a <see cref="Fleet.ArrivalDate"/>.</summary>
        Moving
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
        /// renommable par le joueur faute de besoin exprime cette phase ; pas d'Amiral non
        /// plus (voir Phase 15).
        /// </summary>
        public string Name { get; }

        public UnitBundle Composition { get; private set; }
        public FleetStatus Status { get; private set; }

        /// <summary>Systeme ou la flotte est stationnee (si <see cref="Status"/> vaut <see cref="FleetStatus.Stationed"/>).</summary>
        public StarSystemId CurrentSystemId { get; private set; }

        /// <summary>Systeme quitte lors du dernier depart, conserve pour permettre une retraite en cas de defaite.</summary>
        public StarSystemId OriginSystemId { get; private set; }

        /// <summary>Destination en cours (si <see cref="Status"/> vaut <see cref="FleetStatus.Moving"/>).</summary>
        public StarSystemId? DestinationSystemId { get; private set; }

        /// <summary>Date d'arrivee prevue (si <see cref="Status"/> vaut <see cref="FleetStatus.Moving"/>).</summary>
        public GameDate? ArrivalDate { get; private set; }

        /// <summary>Vrai si ce trajet est un repli force apres une bataille perdue, plutot qu'un ordre volontaire.</summary>
        public bool IsRetreating { get; private set; }

        public Fleet(int id, int ownerId, StarSystemId stationedAt, UnitBundle composition)
            : this(id, ownerId, stationedAt, composition, $"Flotte {id}")
        {
        }

        /// <summary>Utilise par <c>SaveService</c> pour restaurer un nom de flotte existant plutot que d'en generer un nouveau.</summary>
        public Fleet(int id, int ownerId, StarSystemId stationedAt, UnitBundle composition, string name)
        {
            Id = id;
            OwnerId = ownerId;
            Name = string.IsNullOrEmpty(name) ? $"Flotte {id}" : name;
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

        /// <summary>Lance un deplacement vers <paramref name="destination"/>, en memorisant le systeme quitte.</summary>
        public void BeginMove(StarSystemId destination, GameDate arrivalDate, bool isRetreating)
        {
            OriginSystemId = CurrentSystemId;
            DestinationSystemId = destination;
            ArrivalDate = arrivalDate;
            IsRetreating = isRetreating;
            Status = FleetStatus.Moving;
        }

        /// <summary>Termine le deplacement : la flotte est desormais stationnee sur <paramref name="arrivedAt"/>.</summary>
        public void CompleteMove(StarSystemId arrivedAt)
        {
            CurrentSystemId = arrivedAt;
            Status = FleetStatus.Stationed;
            DestinationSystemId = null;
            ArrivalDate = null;
            IsRetreating = false;
        }
    }
}
