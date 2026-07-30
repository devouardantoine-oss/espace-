using System;
using System.Collections.Generic;
using System.Text;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Genere des noms de systemes stellaires originaux (aucune reference a un univers
    /// existant), par composition de syllabes.
    /// <para>
    /// <b>Determinisme :</b> le generateur ne cree pas son propre <see cref="Random"/> ; il
    /// recoit celui de l'appelant, pour que la meme graine produise toujours les memes noms
    /// (voir <see cref="GalaxyGenerator"/>).
    /// </para>
    /// </summary>
    public static class StarSystemNameGenerator
    {
        private static readonly string[] Prefixes =
        {
            "Ax", "Bel", "Cor", "Dra", "El", "Fen", "Gor", "Hy", "Il", "Jor",
            "Kel", "Lor", "Mor", "Nyx", "Or", "Pyr", "Quor", "Rho", "Sol", "Tar",
            "Ul", "Vex", "Wren", "Xen", "Yl", "Zar"
        };

        private static readonly string[] Middles =
        {
            "an", "el", "ir", "or", "us", "ai", "yn", "ar", "eth", "ion", "ora", "esk"
        };

        private static readonly string[] Designations =
        {
            "Prime", "Minor", "Major", "Alpha", "Beta", "Gamma", "Outpost", "Reach", "Expanse", "Cluster"
        };

        /// <summary>Nombre maximal de tentatives avant de forcer l'unicite par suffixe numerique.</summary>
        private const int MaxCollisionRetries = 8;

        /// <summary>
        /// Genere un nom unique par rapport a <paramref name="usedNames"/>, puis l'y ajoute.
        /// </summary>
        /// <param name="rng">Generateur aleatoire partage, pour garantir le determinisme global.</param>
        /// <param name="usedNames">Ensemble des noms deja attribues dans cette galaxie.</param>
        public static string GenerateUnique(Random rng, HashSet<string> usedNames)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (usedNames == null) throw new ArgumentNullException(nameof(usedNames));

            for (int attempt = 0; attempt < MaxCollisionRetries; attempt++)
            {
                string candidate = Compose(rng);
                if (usedNames.Add(candidate))
                {
                    return candidate;
                }
            }

            // Espace de noms epuise par hasard (extremement rare) : on force l'unicite
            // plutot que d'echouer, quitte a produire un nom moins elegant.
            string fallback;
            int suffix = 2;
            do
            {
                fallback = $"{Compose(rng)} {suffix}";
                suffix++;
            } while (!usedNames.Add(fallback));

            return fallback;
        }

        private static string Compose(Random rng)
        {
            var builder = new StringBuilder();
            builder.Append(Prefixes[rng.Next(Prefixes.Length)]);
            builder.Append(Middles[rng.Next(Middles.Length)]);

            // 40% de chance d'ajouter une designation (Prime, Alpha, Reach...) pour varier
            // le rythme des noms plutot que d'avoir 100 noms a une seule syllabe composee.
            if (rng.NextDouble() < 0.4)
            {
                builder.Append(' ');
                builder.Append(Designations[rng.Next(Designations.Length)]);
            }

            string raw = builder.ToString();
            return char.ToUpperInvariant(raw[0]) + raw.Substring(1);
        }
    }
}
