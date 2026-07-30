using System;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Identifiant fort d'un systeme stellaire.
    /// <para>
    /// <b>Pourquoi pas un simple <c>int</c> ?</b> Un wrapper de type distingue au niveau du
    /// compilateur un identifiant de systeme d'un simple entier (index de tableau, quantite
    /// de ressource...). Cela evite une classe entiere de bugs ou l'on passerait par erreur
    /// un mauvais entier a une methode attendant un <see cref="StarSystemId"/>.
    /// </para>
    /// <para>
    /// Structure immuable, comparable par valeur : deux identifiants portant le meme entier
    /// sont interchangeables, ce qui permet de l'utiliser comme cle de <c>Dictionary</c>.
    /// </para>
    /// </summary>
    public readonly struct StarSystemId : IEquatable<StarSystemId>
    {
        /// <summary>Valeur brute de l'identifiant.</summary>
        public readonly int Value;

        public StarSystemId(int value)
        {
            Value = value;
        }

        public bool Equals(StarSystemId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is StarSystemId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => $"System#{Value}";

        public static bool operator ==(StarSystemId left, StarSystemId right) => left.Equals(right);

        public static bool operator !=(StarSystemId left, StarSystemId right) => !left.Equals(right);
    }
}
