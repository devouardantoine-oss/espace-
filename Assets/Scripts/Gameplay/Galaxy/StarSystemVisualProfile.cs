using System;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Attributs purement visuels d'un systeme stellaire (Phase 12) : teinte du corps
    /// principal, presence d'un anneau, nombre de lunes, facteur de taille.
    /// <para>
    /// <b>Fonction statique pure, deterministe, jamais <c>UnityEngine.Random</c> :</b> memes
    /// donnees d'entree (systeme + graine) = meme profil, a chaque appel, sur n'importe quel
    /// appareil. Meme raisonnement que <see cref="EmpirePlacement"/> ou
    /// <see cref="GalaxyGenerator"/> : rend le style visuel testable (EditMode + recoupement
    /// Python) et stable d'une session a l'autre, comme le reste de la galaxie depuis la
    /// graine fixe de la Phase 10.
    /// </para>
    /// <para>
    /// <b>Melange de bits (variante de la finalisation MurmurHash3 32 bits) plutot qu'un
    /// <c>System.Random</c> par systeme :</b> instancier un <c>Random</c> par systeme pour un
    /// seul nombre serait couteux et n'apporterait rien ; une fonction de hachage pure evite
    /// tout etat mutable et se recoupe trivialement en Python (memes operations entieres non
    /// signees 32 bits).
    /// </para>
    /// </summary>
    public readonly struct StarSystemVisualProfile
    {
        /// <summary>Nombre maximal de lunes generees (0 a <see cref="MaxMoonCount"/> inclus).</summary>
        public const int MaxMoonCount = 2;

        /// <summary>Probabilite (pourcentage) qu'un systeme porte un anneau.</summary>
        private const uint RingChancePercent = 30;

        private const float MinSizeFactor = 0.8f;
        private const float MaxSizeFactor = 1.3f;

        /// <summary>
        /// Palette curatee (pas une teinte HSV continue, qui produirait facilement des couleurs
        /// ternes ou criardes) : huit couleurs de corps celestes plausibles.
        /// </summary>
        private static readonly Color[] Palette =
        {
            new Color(0.55f, 0.75f, 0.95f), // bleu glace
            new Color(0.80f, 0.35f, 0.25f), // rouge rouille
            new Color(0.85f, 0.75f, 0.45f), // jaune sable
            new Color(0.35f, 0.70f, 0.50f), // vert emeraude
            new Color(0.55f, 0.40f, 0.75f), // violet amethyste
            new Color(0.90f, 0.55f, 0.20f), // orange ambre
            new Color(0.45f, 0.85f, 0.85f), // cyan pale
            new Color(0.95f, 0.85f, 0.85f), // blanc rose
        };

        /// <summary>Couleur du corps principal.</summary>
        public readonly Color BodyColor;

        /// <summary>Vrai si le systeme doit afficher un anneau.</summary>
        public readonly bool HasRing;

        /// <summary>Nombre de lunes affichees (0 a <see cref="MaxMoonCount"/>).</summary>
        public readonly int MoonCount;

        /// <summary>Multiplicateur de taille applique en plus de l'echelle liee au developpement.</summary>
        public readonly float SizeFactor;

        private StarSystemVisualProfile(Color bodyColor, bool hasRing, int moonCount, float sizeFactor)
        {
            BodyColor = bodyColor;
            HasRing = hasRing;
            MoonCount = moonCount;
            SizeFactor = sizeFactor;
        }

        /// <summary>Calcule le profil visuel deterministe de <paramref name="system"/> pour la graine <paramref name="seed"/>.</summary>
        public static StarSystemVisualProfile Compute(StarSystemState system, int seed)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            int id = system.Id.Value;

            Color bodyColor = Palette[(int)(Mix(id, seed, 1) % (uint)Palette.Length)];
            bool hasRing = Mix(id, seed, 2) % 100u < RingChancePercent;
            int moonCount = (int)(Mix(id, seed, 3) % (MaxMoonCount + 1u));
            float sizeUnit = (Mix(id, seed, 4) % 1000u) / 1000f;
            float sizeFactor = MinSizeFactor + sizeUnit * (MaxSizeFactor - MinSizeFactor);

            return new StarSystemVisualProfile(bodyColor, hasRing, moonCount, sizeFactor);
        }

        /// <summary>
        /// Combine (<paramref name="id"/>, <paramref name="seed"/>, <paramref name="salt"/>) en
        /// un entier non signe 32 bits bien distribue (finalisation MurmurHash3 32 bits,
        /// domaine public). <paramref name="salt"/> distingue les differents attributs derives
        /// du meme systeme sans qu'ils correlent entre eux.
        /// </summary>
        private static uint Mix(int id, int seed, int salt)
        {
            unchecked
            {
                uint h = (uint)id;
                h = h * 0x9E3779B1u + (uint)seed;
                h ^= (uint)salt * 0x85EBCA77u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
