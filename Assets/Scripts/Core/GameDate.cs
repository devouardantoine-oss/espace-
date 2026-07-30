using System;

namespace Espace.Core
{
    /// <summary>
    /// Date dans le calendrier du jeu.
    /// <para>
    /// <b>Calendrier simplifie a dessein :</b> 12 mois de 30 jours (360 jours par an), sans
    /// annees bissextiles ni mois de longueur variable. C'est une convention courante dans
    /// les jeux de grande strategie : elle rend l'arithmetique de date triviale (voir
    /// <see cref="ToDayIndex"/>/<see cref="FromDayIndex"/>) sans jamais degrader la lisibilite
    /// pour le joueur, qui ne compare jamais ce calendrier au calendrier reel.
    /// </para>
    /// </summary>
    public readonly struct GameDate : IEquatable<GameDate>, IComparable<GameDate>
    {
        /// <summary>Nombre de jours par mois, fixe.</summary>
        public const int DaysPerMonth = 30;

        /// <summary>Nombre de mois par annee, fixe.</summary>
        public const int MonthsPerYear = 12;

        /// <summary>Nombre de jours par annee (<see cref="DaysPerMonth"/> * <see cref="MonthsPerYear"/>).</summary>
        public const int DaysPerYear = DaysPerMonth * MonthsPerYear;

        /// <summary>Annee, a partir de 1.</summary>
        public readonly int Year;

        /// <summary>Mois, de 1 a <see cref="MonthsPerYear"/>.</summary>
        public readonly int Month;

        /// <summary>Jour du mois, de 1 a <see cref="DaysPerMonth"/>.</summary>
        public readonly int Day;

        public GameDate(int year, int month, int day)
        {
            if (year < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(year), "L'annee doit etre superieure ou egale a 1.");
            }

            if (month < 1 || month > MonthsPerYear)
            {
                throw new ArgumentOutOfRangeException(nameof(month), $"Le mois doit etre compris entre 1 et {MonthsPerYear}.");
            }

            if (day < 1 || day > DaysPerMonth)
            {
                throw new ArgumentOutOfRangeException(nameof(day), $"Le jour doit etre compris entre 1 et {DaysPerMonth}.");
            }

            Year = year;
            Month = month;
            Day = day;
        }

        /// <summary>Date de fondation de l'empire : An 1, Mois 1, Jour 1.</summary>
        public static GameDate StartOfGame => new GameDate(1, 1, 1);

        /// <summary>Index absolu du jour depuis <see cref="StartOfGame"/> (0 = An 1, Mois 1, Jour 1).</summary>
        public int ToDayIndex() => (Year - 1) * DaysPerYear + (Month - 1) * DaysPerMonth + (Day - 1);

        /// <summary>Reconstruit une date depuis un index absolu de jour (voir <see cref="ToDayIndex"/>).</summary>
        public static GameDate FromDayIndex(int dayIndex)
        {
            if (dayIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dayIndex), "L'index de jour ne peut pas etre negatif.");
            }

            int year = dayIndex / DaysPerYear + 1;
            int remainder = dayIndex % DaysPerYear;
            int month = remainder / DaysPerMonth + 1;
            int day = remainder % DaysPerMonth + 1;

            return new GameDate(year, month, day);
        }

        /// <summary>Retourne la date <paramref name="days"/> jours plus tard.</summary>
        public GameDate AddDays(int days) => FromDayIndex(ToDayIndex() + days);

        public int CompareTo(GameDate other) => ToDayIndex().CompareTo(other.ToDayIndex());

        public bool Equals(GameDate other) => Year == other.Year && Month == other.Month && Day == other.Day;

        public override bool Equals(object obj) => obj is GameDate other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Year, Month, Day);

        /// <summary>Format compact et trivialement triable, ex. "0003-07-12".</summary>
        public override string ToString() => $"{Year:D4}-{Month:D2}-{Day:D2}";

        public static bool operator ==(GameDate left, GameDate right) => left.Equals(right);
        public static bool operator !=(GameDate left, GameDate right) => !left.Equals(right);
        public static bool operator <(GameDate left, GameDate right) => left.CompareTo(right) < 0;
        public static bool operator >(GameDate left, GameDate right) => left.CompareTo(right) > 0;
        public static bool operator <=(GameDate left, GameDate right) => left.CompareTo(right) <= 0;
        public static bool operator >=(GameDate left, GameDate right) => left.CompareTo(right) >= 0;
    }
}
