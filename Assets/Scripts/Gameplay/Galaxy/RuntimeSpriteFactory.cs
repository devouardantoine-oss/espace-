using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Genere a la volee les sprites simples dont la carte galactique a besoin.
    /// <para>
    /// <b>Pourquoi generer plutot qu'importer une texture ?</b> Le MVP n'a pas encore
    /// d'artiste ni de pipeline d'import de textures, et une texture ecrite a la main en
    /// binaire ne peut pas etre verifiee sans ouvrir Unity. Un disque plein dessine par code
    /// est trivial a garantir correct et suffit a rendre la carte lisible ; il sera
    /// remplace par de vrais visuels d'artiste sans changer l'API des appelants.
    /// </para>
    /// <para>
    /// <b>Un seul sprite partage</b> par tous les marqueurs de systeme : ils utilisent donc
    /// la meme texture et le meme materiau par defaut, condition necessaire pour qu'Unity
    /// puisse les regrouper en un minimum de <i>draw calls</i> plutot que d'en payer un par
    /// systeme.
    /// </para>
    /// </summary>
    public static class RuntimeSpriteFactory
    {
        private const int TextureSize = 64;
        private const float PixelsPerUnit = 64f;

        private static Sprite _cachedCircleSprite;

        /// <summary>
        /// Sprite d'un disque plein avec anti-aliasing simple sur le contour, centre sur son pivot.
        /// La meme instance est retournee a chaque appel.
        /// </summary>
        public static Sprite GetCircleSprite()
        {
            if (_cachedCircleSprite != null)
            {
                return _cachedCircleSprite;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, mipChain: false)
            {
                name = "GeneratedCircle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float center = (TextureSize - 1) * 0.5f;
            float radius = TextureSize * 0.5f;
            var pixels = new Color32[TextureSize * TextureSize];

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                    // Contour adouci sur les deux derniers pixels de rayon : evite un disque
                    // en dents de scie visible a fort zoom, sans recourir a un shader dedie.
                    float alpha = Mathf.Clamp01(radius - distance);
                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _cachedCircleSprite = Sprite.Create(
                texture,
                new Rect(0, 0, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
            _cachedCircleSprite.name = "GeneratedCircleSprite";

            return _cachedCircleSprite;
        }
    }
}
