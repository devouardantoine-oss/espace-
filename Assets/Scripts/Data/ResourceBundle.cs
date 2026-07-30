using System;

namespace Espace.Data
{
    /// <summary>
    /// Quantite immuable des cinq ressources, utilisee pour un tresor, un cout de
    /// construction, ou une production journaliere.
    /// <para>
    /// <b>Cinq champs nommes plutot qu'un tableau interne :</b> un <c>readonly struct</c>
    /// contenant un tableau ne serait pas reellement immuable (copier la struct ne copie que
    /// la <i>reference</i> au tableau ; une mutation via une copie modifierait l'original).
    /// Cinq champs `float` evitent totalement ce piege et rendent la structure veritablement
    /// une valeur, comparable et partageable sans effet de bord.
    /// </para>
    /// </summary>
    public readonly struct ResourceBundle : IEquatable<ResourceBundle>
    {
        public readonly float Credits;
        public readonly float Minerals;
        public readonly float Energy;
        public readonly float Food;
        public readonly float Influence;

        public ResourceBundle(float credits = 0f, float minerals = 0f, float energy = 0f, float food = 0f, float influence = 0f)
        {
            Credits = credits;
            Minerals = minerals;
            Energy = energy;
            Food = food;
            Influence = influence;
        }

        /// <summary>Aucune ressource.</summary>
        public static ResourceBundle Zero => default;

        /// <summary>Quantite de <paramref name="type"/> dans ce lot.</summary>
        public float Get(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Credits: return Credits;
                case ResourceType.Minerals: return Minerals;
                case ResourceType.Energy: return Energy;
                case ResourceType.Food: return Food;
                case ResourceType.Influence: return Influence;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "Type de ressource inconnu.");
            }
        }

        /// <summary>
        /// Vrai si chaque composante de ce lot est superieure ou egale a celle de
        /// <paramref name="other"/>. Utilise pour verifier qu'une depense est finançable.
        /// </summary>
        public bool IsGreaterOrEqualTo(ResourceBundle other)
        {
            return Credits >= other.Credits
                && Minerals >= other.Minerals
                && Energy >= other.Energy
                && Food >= other.Food
                && Influence >= other.Influence;
        }

        public static ResourceBundle operator +(ResourceBundle a, ResourceBundle b) => new ResourceBundle(
            a.Credits + b.Credits, a.Minerals + b.Minerals, a.Energy + b.Energy, a.Food + b.Food, a.Influence + b.Influence);

        public static ResourceBundle operator -(ResourceBundle a, ResourceBundle b) => new ResourceBundle(
            a.Credits - b.Credits, a.Minerals - b.Minerals, a.Energy - b.Energy, a.Food - b.Food, a.Influence - b.Influence);

        public static ResourceBundle operator *(ResourceBundle bundle, float scalar) => new ResourceBundle(
            bundle.Credits * scalar, bundle.Minerals * scalar, bundle.Energy * scalar, bundle.Food * scalar, bundle.Influence * scalar);

        public bool Equals(ResourceBundle other) =>
            Credits.Equals(other.Credits) && Minerals.Equals(other.Minerals) && Energy.Equals(other.Energy)
            && Food.Equals(other.Food) && Influence.Equals(other.Influence);

        public override bool Equals(object obj) => obj is ResourceBundle other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Credits, Minerals, Energy, Food, Influence);

        public override string ToString() =>
            $"{Credits:0.#} Cr, {Minerals:0.#} Min, {Energy:0.#} En, {Food:0.#} Ali, {Influence:0.#} Inf";

        public static bool operator ==(ResourceBundle left, ResourceBundle right) => left.Equals(right);
        public static bool operator !=(ResourceBundle left, ResourceBundle right) => !left.Equals(right);
    }
}
