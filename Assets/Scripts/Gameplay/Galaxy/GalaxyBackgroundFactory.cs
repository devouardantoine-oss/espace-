using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Construit le fond spatial de la carte galactique (Phase 12) : etoiles eparses et
    /// nebuleuses, sur une seule texture generee par code.
    /// <para>
    /// <b>Procedural et deterministe, comme <see cref="RuntimeSpriteFactory"/> :</b> aucune
    /// texture importee (voir sa remarque sur l'absence de pipeline d'import dans cet
    /// environnement sans editeur). Le fond depend uniquement de
    /// <see cref="GalaxyGenerationParameters.Seed"/> : meme graine, meme fond, coherent avec
    /// la galaxie elle-meme et avec le style visuel des systemes
    /// (<see cref="StarSystemVisualProfile"/>) depuis la graine fixe de la Phase 10.
    /// </para>
    /// <para>
    /// <b>Nebuleuses = bruit de Perlin (<see cref="Mathf.PerlinNoise(float,float)"/>) module par
    /// des taches radiales douces</b>, pas un simple degrade : un bruit de Perlin pur remplirait
    /// tout le cadre de texture (peu lisible), une tache radiale pure serait trop reguliere pour
    /// evoquer un nuage. Combiner les deux donne un aspect « vapeur » convaincant avec un cout de
    /// calcul minime (une fonction native du moteur, pas d'implementation maison).
    /// </para>
    /// <para>
    /// <b>Etoiles = hachage par pixel</b>, meme technique de melange de bits que
    /// <see cref="StarSystemVisualProfile"/> (pas de <c>Texture2D</c> par etoile, un seul pixel
    /// suffit) : chaque pixel decide independamment s'il est une etoile et, si oui, sa
    /// luminosite, sans etat partage ni tableau de positions a generer a part.
    /// </para>
    /// </summary>
    public static class GalaxyBackgroundFactory
    {
        private const int TextureSize = 512;
        private const int NebulaBlobCount = 4;
        private const float NebulaNoiseFrequency = 3.5f;

        /// <summary>
        /// Profondeur Z du fond : plus eloigne de la camera que les halos de territoire (0.5),
        /// les routes hyperspatiales (0.1) et les marqueurs de systeme (0).
        /// </summary>
        private const float BackgroundDepth = 2f;

        /// <summary>Multiplicateur applique au rayon de la galaxie pour garantir une couverture au-dela du zoom arriere maximal.</summary>
        private const float CoverageMultiplier = 2.5f;

        /// <summary>Fraction de pixels testes comme etoiles potentielles (avant filtrage par densite).</summary>
        private const uint StarDensityPerMille = 6;

        private static readonly Color BaseSpaceColor = new Color(0.015f, 0.018f, 0.035f, 1f);

        private static readonly Color[] NebulaPalette =
        {
            new Color(0.45f, 0.15f, 0.55f), // violet
            new Color(0.15f, 0.25f, 0.55f), // bleu profond
            new Color(0.10f, 0.45f, 0.45f), // sarcelle
            new Color(0.55f, 0.25f, 0.10f), // ambre
        };

        private readonly struct NebulaBlob
        {
            public readonly Vector2 CenterUv;
            public readonly float Radius;
            public readonly Color Color;
            public readonly Vector2 NoiseOffset;

            public NebulaBlob(Vector2 centerUv, float radius, Color color, Vector2 noiseOffset)
            {
                CenterUv = centerUv;
                Radius = radius;
                Color = color;
                NoiseOffset = noiseOffset;
            }
        }

        /// <summary>
        /// Cree un GameObject enfant de <paramref name="parent"/> portant le fond spatial,
        /// couvrant largement au-dela du rayon de la galaxie <paramref name="parameters"/>,
        /// place derriere les routes et les marqueurs de systeme.
        /// </summary>
        public static void Build(GameObject parent, GalaxyGenerationParameters parameters)
        {
            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(parent.transform, worldPositionStays: false);
            backgroundObject.transform.position = new Vector3(0f, 0f, BackgroundDepth);

            float coverageRadius = parameters.GalaxyRadius * CoverageMultiplier;
            Sprite sprite = BuildSprite(parameters.Seed, coverageRadius);

            var renderer = backgroundObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
        }

        private static Sprite BuildSprite(int seed, float coverageRadius)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, mipChain: false)
            {
                name = "GeneratedGalaxyBackground",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            NebulaBlob[] blobs = BuildNebulaBlobs(seed);
            var pixels = new Color32[TextureSize * TextureSize];

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    var uv = new Vector2((x + 0.5f) / TextureSize, (y + 0.5f) / TextureSize);
                    Color color = BaseSpaceColor;

                    foreach (NebulaBlob blob in blobs)
                    {
                        color = BlendNebula(color, blob, uv, x, y);
                    }

                    color = BlendStar(color, x, y, seed);

                    pixels[y * TextureSize + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            // pixelsPerUnit choisi pour que la texture couvre exactement un carre de cote
            // 2*coverageRadius en unites monde, quelle que soit la taille de la galaxie.
            float worldDiameter = Mathf.Max(coverageRadius, 1f) * 2f;
            float pixelsPerUnit = TextureSize / worldDiameter;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
            sprite.name = "GeneratedGalaxyBackgroundSprite";

            return sprite;
        }

        private static Color BlendNebula(Color color, NebulaBlob blob, Vector2 uv, int x, int y)
        {
            float distance = Vector2.Distance(uv, blob.CenterUv);
            float radialFalloff = Mathf.Clamp01(1f - distance / blob.Radius);
            if (radialFalloff <= 0f)
            {
                return color;
            }

            float noise = Mathf.PerlinNoise(
                x / (float)TextureSize * NebulaNoiseFrequency + blob.NoiseOffset.x,
                y / (float)TextureSize * NebulaNoiseFrequency + blob.NoiseOffset.y);

            // Le bruit adoucit le contour et cree des filaments plutot qu'un disque uniforme ;
            // eleve au carre pour concentrer l'intensite au centre de la tache.
            float intensity = radialFalloff * radialFalloff * noise;

            return Color.Lerp(color, blob.Color, intensity * 0.6f);
        }

        private static Color BlendStar(Color color, int x, int y, int seed)
        {
            uint presenceHash = Mix(x, y, seed, 1);
            if (presenceHash % 1000u >= StarDensityPerMille)
            {
                return color;
            }

            float brightness = 0.5f + (Mix(x, y, seed, 2) % 1000u) / 1000f * 0.5f;
            return Color.Lerp(color, Color.white, brightness);
        }

        private static NebulaBlob[] BuildNebulaBlobs(int seed)
        {
            var blobs = new NebulaBlob[NebulaBlobCount];

            for (int i = 0; i < NebulaBlobCount; i++)
            {
                float u = (Mix(i, 0, seed, 10) % 1000u) / 1000f;
                float v = (Mix(i, 0, seed, 11) % 1000u) / 1000f;
                float radius = 0.25f + (Mix(i, 0, seed, 12) % 1000u) / 1000f * 0.35f;
                Color paletteColor = NebulaPalette[(int)(Mix(i, 0, seed, 13) % (uint)NebulaPalette.Length)];
                var noiseOffset = new Vector2(
                    (Mix(i, 0, seed, 14) % 1000u) / 1000f * 100f,
                    (Mix(i, 0, seed, 15) % 1000u) / 1000f * 100f);

                blobs[i] = new NebulaBlob(new Vector2(u, v), radius, paletteColor, noiseOffset);
            }

            return blobs;
        }

        /// <summary>
        /// Melange de bits deterministe (variante de la finalisation MurmurHash3 32 bits, comme
        /// <see cref="StarSystemVisualProfile"/>) : <paramref name="salt"/> distingue les
        /// differents attributs derives d'un meme (<paramref name="a"/>, <paramref name="b"/>,
        /// <paramref name="seed"/>) sans qu'ils correlent entre eux (ex. presence vs luminosite
        /// d'une etoile au meme pixel, ou les differents attributs d'une meme tache de nebuleuse).
        /// </summary>
        private static uint Mix(int a, int b, int seed, int salt)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u;
                h ^= (uint)b * 0x85EBCA77u;
                h ^= (uint)seed * 0xC2B2AE3Du;
                h ^= (uint)salt * 0x27D4EB2Fu;
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
