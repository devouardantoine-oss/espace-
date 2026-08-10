using System.Collections.Generic;
using UnityEngine;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Trace les emblemes de civilisation par code (Phase 21.3).
    /// <para>
    /// <b>Des traits, pas des aplats.</b> Un embleme dessine en surfaces pleines demanderait un
    /// vrai graphiste pour ne pas paraitre pauvre. Un embleme au trait, lui, appartient a un
    /// registre — celui de l'affichage tete haute, du plan technique — ou la simplicite se lit
    /// comme un parti pris et non comme un manque. C'est le meme raisonnement qui a fait
    /// retenir cette direction artistique plutot qu'une autre.
    /// </para>
    /// <para>
    /// <b>Textures blanches, teintees a l'affichage.</b> Une texture par embleme suffit pour les
    /// six factions : c'est l'appelant qui applique la couleur. Sans cela, il faudrait
    /// regenerer une texture a chaque frame d'un basculement de palette.
    /// </para>
    /// <para>
    /// <b>Anti-crenelage par sur-echantillonnage</b>, comme la coque des vaisseaux
    /// (<c>RuntimeSpriteFactory</c>, Phase 20) : les formes sont concaves et faites de segments,
    /// une distance signee au contour demanderait de traiter chaque arete a part.
    /// </para>
    /// </summary>
    public static class FactionEmblemFactory
    {
        private const int TextureSize = 128;

        /// <summary>Cote de la grille de sur-echantillonnage. Quatre suffit a lisser un trait de deux pixels.</summary>
        private const int Supersample = 4;

        /// <summary>Demi-epaisseur d'un trait, en coordonnees normalisees (le motif tient dans [-0,5 ; 0,5]).</summary>
        private const float StrokeHalfWidth = 0.026f;

        private static readonly Dictionary<EmblemShape, Texture2D> Cache = new Dictionary<EmblemShape, Texture2D>(6);

        /// <summary>
        /// Texture blanche de l'embleme, sur fond transparent, mise en cache. A teinter par
        /// l'appelant (<c>GUI.color</c> ou la couleur du <c>SpriteRenderer</c>).
        /// </summary>
        public static Texture2D GetTexture(EmblemShape shape)
        {
            if (Cache.TryGetValue(shape, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Vector2[][] strokes = StrokesOf(shape);

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, mipChain: false)
            {
                name = $"Emblem_{shape}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[TextureSize * TextureSize];
            const float step = 1f / Supersample;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    int covered = 0;

                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            var point = new Vector2(
                                (x + (sx + 0.5f) * step) / TextureSize - 0.5f,
                                (y + (sy + 0.5f) * step) / TextureSize - 0.5f);

                            if (IsOnAnyStroke(strokes, point))
                            {
                                covered++;
                            }
                        }
                    }

                    float alpha = covered / (float)(Supersample * Supersample);
                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            Cache[shape] = texture;
            return texture;
        }

        private static bool IsOnAnyStroke(Vector2[][] strokes, Vector2 point)
        {
            for (int i = 0; i < strokes.Length; i++)
            {
                Vector2[] path = strokes[i];
                for (int j = 0; j < path.Length - 1; j++)
                {
                    if (DistanceToSegment(point, path[j], path[j + 1]) <= StrokeHalfWidth)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Distance d'un point au segment <c>[a, b]</c>, extremites comprises.</summary>
        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;

            // Segment degenere en un point : la projection n'a pas de sens, on mesure au sommet.
            if (lengthSquared <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, a);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
            return Vector2.Distance(point, a + ab * t);
        }

        /// <summary>Sommets d'un polygone regulier a <paramref name="sides"/> cotes, referme sur lui-meme.</summary>
        private static Vector2[] Polygon(int sides, float radius, float rotationDegrees = 0f)
        {
            var points = new Vector2[sides + 1];
            float offset = rotationDegrees * Mathf.Deg2Rad;

            for (int i = 0; i < sides; i++)
            {
                float angle = offset + 2f * Mathf.PI * i / sides;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            points[sides] = points[0];
            return points;
        }

        /// <summary>Cercle approche par un polygone a trente-deux cotes : indiscernable d'un cercle a cette resolution.</summary>
        private static Vector2[] Circle(float radius) => Polygon(32, radius);

        private static Vector2[][] StrokesOf(EmblemShape shape)
        {
            switch (shape)
            {
                case EmblemShape.Hive:
                    return new[]
                    {
                        Polygon(6, 0.42f, 90f),
                        Polygon(6, 0.24f, 90f),
                        Polygon(6, 0.09f, 90f),
                    };

                case EmblemShape.Bastion:
                    return new[]
                    {
                        new[] { new Vector2(-0.36f, 0.18f), new Vector2(0f, 0.44f), new Vector2(0.36f, 0.18f), new Vector2(0.36f, -0.22f), new Vector2(0f, -0.46f), new Vector2(-0.36f, -0.22f), new Vector2(-0.36f, 0.18f) },
                        new[] { new Vector2(-0.17f, 0.10f), new Vector2(0.17f, 0.10f), new Vector2(0.17f, -0.16f), new Vector2(-0.17f, -0.16f), new Vector2(-0.17f, 0.10f) },
                        new[] { new Vector2(0f, 0.44f), new Vector2(0f, 0.10f) },
                    };

                case EmblemShape.Ledger:
                    return new[]
                    {
                        Polygon(6, 0.42f, 90f),
                        new[] { new Vector2(-0.36f, -0.21f), new Vector2(0.36f, 0.21f) },
                        new[] { new Vector2(0.36f, -0.21f), new Vector2(-0.36f, 0.21f) },
                        new[] { new Vector2(0f, 0.42f), new Vector2(0f, -0.42f) },
                        Circle(0.13f),
                    };

                case EmblemShape.Seed:
                    return new[]
                    {
                        Circle(0.42f),
                        // Amande : deux arcs opposes, approches par des lignes brisees. La forme
                        // est asymetrique en hauteur — une graine n'est pas une lentille.
                        new[] { new Vector2(0f, 0.40f), new Vector2(0.20f, 0.14f), new Vector2(0.22f, -0.10f), new Vector2(0f, -0.38f) },
                        new[] { new Vector2(0f, 0.40f), new Vector2(-0.20f, 0.14f), new Vector2(-0.22f, -0.10f), new Vector2(0f, -0.38f) },
                        new[] { new Vector2(0f, 0.30f), new Vector2(0f, -0.30f) },
                    };

                case EmblemShape.Prism:
                    return new[]
                    {
                        Polygon(4, 0.44f, 90f),
                        Polygon(4, 0.24f, 90f),
                        new[] { new Vector2(-0.44f, 0f), new Vector2(0.44f, 0f) },
                        new[] { new Vector2(0f, 0.44f), new Vector2(0f, -0.44f) },
                    };

                default:
                    return new[]
                    {
                        Circle(0.42f),
                        Circle(0.30f),
                        new[] { new Vector2(0f, 0.46f), new Vector2(0f, -0.46f) },
                        new[] { new Vector2(-0.46f, 0f), new Vector2(0.46f, 0f) },
                        Polygon(4, 0.20f, 90f),
                    };
            }
        }
    }
}
