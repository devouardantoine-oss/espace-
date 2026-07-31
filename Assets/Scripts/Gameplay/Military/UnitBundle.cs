using System;
using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Quantite immuable des sept types d'unites, composant une flotte ou une commande de
    /// recrutement.
    /// <para>
    /// Meme pattern que <see cref="Espace.Data.ResourceBundle"/> : sept champs nommes plutot
    /// qu'un tableau interne, pour eviter le piege d'alias d'un tableau dans une structure
    /// censee etre une valeur. Difference avec <c>ResourceBundle</c> : les quantites sont des
    /// <c>int</c> (pas de demi-unite), donc <see cref="Scale"/> arrondit vers le bas plutot que
    /// de conserver une fraction.
    /// </para>
    /// </summary>
    public readonly struct UnitBundle : IEquatable<UnitBundle>
    {
        public readonly int Infantry;
        public readonly int Armored;
        public readonly int SpecialForces;
        public readonly int Fighter;
        public readonly int Frigate;
        public readonly int Cruiser;
        public readonly int Battleship;

        public UnitBundle(
            int infantry = 0, int armored = 0, int specialForces = 0,
            int fighter = 0, int frigate = 0, int cruiser = 0, int battleship = 0)
        {
            Infantry = infantry;
            Armored = armored;
            SpecialForces = specialForces;
            Fighter = fighter;
            Frigate = frigate;
            Cruiser = cruiser;
            Battleship = battleship;
        }

        /// <summary>Aucune unite.</summary>
        public static UnitBundle Zero => default;

        /// <summary>Nombre total d'unites, tous types confondus.</summary>
        public int TotalCount => Infantry + Armored + SpecialForces + Fighter + Frigate + Cruiser + Battleship;

        /// <summary>Vrai si ce lot ne contient aucune unite.</summary>
        public bool IsEmpty => TotalCount == 0;

        /// <summary>Nombre d'unites de <paramref name="type"/> dans ce lot.</summary>
        public int Get(UnitType type)
        {
            switch (type)
            {
                case UnitType.Infantry: return Infantry;
                case UnitType.Armored: return Armored;
                case UnitType.SpecialForces: return SpecialForces;
                case UnitType.Fighter: return Fighter;
                case UnitType.Frigate: return Frigate;
                case UnitType.Cruiser: return Cruiser;
                case UnitType.Battleship: return Battleship;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "Type d'unite inconnu.");
            }
        }

        /// <summary>Un seul type d'unite, en quantite <paramref name="count"/>.</summary>
        public static UnitBundle Of(UnitType type, int count)
        {
            switch (type)
            {
                case UnitType.Infantry: return new UnitBundle(infantry: count);
                case UnitType.Armored: return new UnitBundle(armored: count);
                case UnitType.SpecialForces: return new UnitBundle(specialForces: count);
                case UnitType.Fighter: return new UnitBundle(fighter: count);
                case UnitType.Frigate: return new UnitBundle(frigate: count);
                case UnitType.Cruiser: return new UnitBundle(cruiser: count);
                case UnitType.Battleship: return new UnitBundle(battleship: count);
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "Type d'unite inconnu.");
            }
        }

        /// <summary>
        /// Vrai si chaque composante de ce lot est superieure ou egale a celle de
        /// <paramref name="other"/>. Utilise pour verifier qu'une garnison peut fournir le
        /// detachement demande.
        /// </summary>
        public bool IsGreaterOrEqualTo(UnitBundle other)
        {
            return Infantry >= other.Infantry && Armored >= other.Armored && SpecialForces >= other.SpecialForces
                && Fighter >= other.Fighter && Frigate >= other.Frigate && Cruiser >= other.Cruiser && Battleship >= other.Battleship;
        }

        /// <summary>
        /// Ce lot mis a l'echelle de <paramref name="fraction"/> (arrondi vers le bas par
        /// type, jamais negatif) : utilise pour repartir des pertes de bataille
        /// proportionnellement sur chaque type d'unite.
        /// </summary>
        public UnitBundle Scale(float fraction)
        {
            if (fraction <= 0f)
            {
                return Zero;
            }

            return new UnitBundle(
                ScaleCount(Infantry, fraction),
                ScaleCount(Armored, fraction),
                ScaleCount(SpecialForces, fraction),
                ScaleCount(Fighter, fraction),
                ScaleCount(Frigate, fraction),
                ScaleCount(Cruiser, fraction),
                ScaleCount(Battleship, fraction));
        }

        private static int ScaleCount(int count, float fraction)
        {
            return Mathf.Max(0, Mathf.FloorToInt(count * fraction));
        }

        public static UnitBundle operator +(UnitBundle a, UnitBundle b) => new UnitBundle(
            a.Infantry + b.Infantry, a.Armored + b.Armored, a.SpecialForces + b.SpecialForces,
            a.Fighter + b.Fighter, a.Frigate + b.Frigate, a.Cruiser + b.Cruiser, a.Battleship + b.Battleship);

        /// <summary>Soustraction bornee a zero par type : jamais de quantite negative.</summary>
        public static UnitBundle operator -(UnitBundle a, UnitBundle b) => new UnitBundle(
            Mathf.Max(0, a.Infantry - b.Infantry),
            Mathf.Max(0, a.Armored - b.Armored),
            Mathf.Max(0, a.SpecialForces - b.SpecialForces),
            Mathf.Max(0, a.Fighter - b.Fighter),
            Mathf.Max(0, a.Frigate - b.Frigate),
            Mathf.Max(0, a.Cruiser - b.Cruiser),
            Mathf.Max(0, a.Battleship - b.Battleship));

        public bool Equals(UnitBundle other) =>
            Infantry == other.Infantry && Armored == other.Armored && SpecialForces == other.SpecialForces
            && Fighter == other.Fighter && Frigate == other.Frigate && Cruiser == other.Cruiser && Battleship == other.Battleship;

        public override bool Equals(object obj) => obj is UnitBundle other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Infantry, Armored, SpecialForces, Fighter, Frigate, Cruiser, Battleship);

        public override string ToString() =>
            $"{Infantry} Inf, {Armored} Bli, {SpecialForces} FS, {Fighter} Cha, {Frigate} Fre, {Cruiser} Cro, {Battleship} Cui";

        public static bool operator ==(UnitBundle left, UnitBundle right) => left.Equals(right);
        public static bool operator !=(UnitBundle left, UnitBundle right) => !left.Equals(right);
    }
}
