using System;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Route hyperspatiale reliant deux systemes stellaires. Non orientee : la connexion
    /// entre A et B est identique a celle entre B et A.
    /// <para>
    /// <b>Choix technique :</b> l'egalite et le hash sont calcules sans tenir compte de
    /// l'ordre des deux identifiants (en triant systematiquement le plus petit en premier).
    /// Cela evite qu'un generateur ou un test cree par erreur deux liens A-B et B-A traites
    /// comme distincts.
    /// </para>
    /// </summary>
    public readonly struct HyperlaneLink : IEquatable<HyperlaneLink>
    {
        /// <summary>Extremite avec la plus petite valeur d'identifiant.</summary>
        public readonly StarSystemId SystemA;

        /// <summary>Extremite avec la plus grande valeur d'identifiant.</summary>
        public readonly StarSystemId SystemB;

        /// <summary>
        /// Cree un lien entre deux systemes distincts. L'ordre des arguments n'a pas
        /// d'importance : il est normalise en interne.
        /// </summary>
        /// <exception cref="ArgumentException">Si les deux identifiants sont identiques.</exception>
        public HyperlaneLink(StarSystemId systemA, StarSystemId systemB)
        {
            if (systemA == systemB)
            {
                throw new ArgumentException("Un lien hyperspatial ne peut pas relier un systeme a lui-meme.");
            }

            if (systemA.Value <= systemB.Value)
            {
                SystemA = systemA;
                SystemB = systemB;
            }
            else
            {
                SystemA = systemB;
                SystemB = systemA;
            }
        }

        /// <summary>Indique si <paramref name="systemId"/> est l'une des deux extremites du lien.</summary>
        public bool Contains(StarSystemId systemId) => SystemA == systemId || SystemB == systemId;

        public bool Equals(HyperlaneLink other) => SystemA == other.SystemA && SystemB == other.SystemB;

        public override bool Equals(object obj) => obj is HyperlaneLink other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(SystemA, SystemB);

        public override string ToString() => $"{SystemA} <-> {SystemB}";

        public static bool operator ==(HyperlaneLink left, HyperlaneLink right) => left.Equals(right);

        public static bool operator !=(HyperlaneLink left, HyperlaneLink right) => !left.Equals(right);
    }
}
