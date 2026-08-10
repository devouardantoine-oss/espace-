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

        /// <summary>Rayon interieur de l'anneau, en fraction du rayon exterieur (voir <see cref="GetRingSprite"/>).</summary>
        private const float RingInnerRadiusFraction = 0.6f;

        private static Sprite _cachedCircleSprite;
        private static Sprite _cachedRingSprite;

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

        /// <summary>
        /// Sprite d'un anneau plein (disque avec un trou), centre sur son pivot. Destine a etre
        /// place autour d'un marqueur de systeme (Phase 12) avec une echelle non uniforme
        /// (X != Y) pour donner l'illusion d'une ellipse vue en perspective, sans texture
        /// d'ellipse dediee.
        /// </summary>
        public static Sprite GetRingSprite()
        {
            if (_cachedRingSprite != null)
            {
                return _cachedRingSprite;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, mipChain: false)
            {
                name = "GeneratedRing",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float center = (TextureSize - 1) * 0.5f;
            float outerRadius = TextureSize * 0.5f;
            float innerRadius = outerRadius * RingInnerRadiusFraction;
            var pixels = new Color32[TextureSize * TextureSize];

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                    // Meme contour adouci que GetCircleSprite, applique sur les deux bords
                    // (interieur et exterieur) de la bande.
                    float outerAlpha = Mathf.Clamp01(outerRadius - distance);
                    float innerAlpha = Mathf.Clamp01(distance - innerRadius);
                    float alpha = Mathf.Min(outerAlpha, innerAlpha);

                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _cachedRingSprite = Sprite.Create(
                texture,
                new Rect(0, 0, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
            _cachedRingSprite.name = "GeneratedRingSprite";

            return _cachedRingSprite;
        }

        /// <summary>
        /// Sprite d'une lune : le meme disque plein que <see cref="GetCircleSprite"/>, reutilise
        /// tel quel (une lune n'est qu'un petit systeme mis a l'echelle et deplace) plutot que
        /// de dupliquer une texture identique sous un autre nom.
        /// </summary>
        public static Sprite GetMoonSprite() => GetCircleSprite();

        /// <summary>
        /// Contour du vaisseau, en coordonnees normalisees centrees sur le pivot, <b>pointe
        /// vers +X</b> : une rotation de <c>Z</c> egale au cap suffit alors a l'orienter dans le
        /// sens du trajet, sans decalage a compenser.
        /// <para>
        /// La forme est une pointe de fleche echancree a l'arriere : c'est l'echancrure qui la
        /// rend lisible comme un vaisseau plutot que comme un triangle, et qui indique l'avant
        /// meme a quelques pixels de haut.
        /// </para>
        /// </summary>
        private static readonly Vector2[] ShipHull =
        {
            new Vector2( 0.47f,  0.00f),
            new Vector2(-0.30f,  0.33f),
            new Vector2(-0.13f,  0.00f),
            new Vector2(-0.30f, -0.33f),
        };

        /// <summary>Cote de la grille de sur-echantillonnage utilisee pour l'anti-aliasing du vaisseau.</summary>
        private const int ShipSupersample = 4;

        private static Sprite _cachedShipSprite;

        /// <summary>
        /// Sprite d'un vaisseau vu de dessus, pointe vers +X, centre sur son pivot (Phase 20).
        /// <para>
        /// <b>Anti-aliasing par sur-echantillonnage plutot que par distance au contour :</b> la
        /// forme est concave, une distance signee au bord demanderait de gerer chaque arete
        /// separement. Compter les sous-pixels a l'interieur du polygone donne le meme resultat
        /// pour quelques lignes de code, et le cout est paye une seule fois — la texture est
        /// mise en cache comme toutes les autres de cette fabrique.
        /// </para>
        /// </summary>
        public static Sprite GetShipSprite()
        {
            if (_cachedShipSprite != null)
            {
                return _cachedShipSprite;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, mipChain: false)
            {
                name = "GeneratedShip",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[TextureSize * TextureSize];
            const float step = 1f / ShipSupersample;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    int inside = 0;

                    for (int sy = 0; sy < ShipSupersample; sy++)
                    {
                        for (int sx = 0; sx < ShipSupersample; sx++)
                        {
                            float u = (x + (sx + 0.5f) * step) / TextureSize - 0.5f;
                            float v = (y + (sy + 0.5f) * step) / TextureSize - 0.5f;

                            if (IsInsidePolygon(ShipHull, u, v))
                            {
                                inside++;
                            }
                        }
                    }

                    float alpha = (float)inside / (ShipSupersample * ShipSupersample);
                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _cachedShipSprite = Sprite.Create(
                texture,
                new Rect(0, 0, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
            _cachedShipSprite.name = "GeneratedShipSprite";

            return _cachedShipSprite;
        }

        /// <summary>
        /// Test d'appartenance a un polygone quelconque, par lancer de rayon horizontal (regle
        /// pair-impair). Vaut aussi pour un polygone concave, contrairement a un test par
        /// demi-plans.
        /// </summary>
        private static bool IsInsidePolygon(Vector2[] polygon, float x, float y)
        {
            bool inside = false;

            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];

                if (a.y > y == b.y > y)
                {
                    continue;
                }

                float crossingX = (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x;
                if (x < crossingX)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Cote des textures de planete auxiliaires : plus fin que le disque, leurs degrades doivent rester lisses en grand.</summary>
        private const int PlanetOverlaySize = 128;

        /// <summary>Rayon de la sphere dans la texture de halo, en fraction du demi-cote. Le reste est l'epaisseur de l'atmosphere.</summary>
        private const float AtmosphereCoreFraction = 0.68f;

        private static Sprite _cachedAtmosphereSprite;
        private static Sprite _cachedTerminatorSprite;

        /// <summary>
        /// Halo atmospherique : un anneau flou qui s'allume au bord du globe et s'eteint vers
        /// l'exterieur (Phase 21.1). A placer derriere la sphere, legerement plus grand qu'elle.
        /// <para>
        /// <b>Un sprite, pas un second maillage.</b> Une coquille spherique transparente autour
        /// de la planete demanderait d'inverser ses faces, donc un nuanceur dedie. La camera du
        /// jeu est orthographique et fixe : un disque toujours face a l'objectif donne
        /// exactement la meme image, pour un cout nul.
        /// </para>
        /// <para>
        /// Le sprite est blanc ; c'est le <c>SpriteRenderer</c> qui le teinte, ce qui permet a
        /// chaque type de monde d'avoir son atmosphere sans regenerer la texture.
        /// </para>
        /// </summary>
        public static Sprite GetAtmosphereSprite()
        {
            if (_cachedAtmosphereSprite != null)
            {
                return _cachedAtmosphereSprite;
            }

            var texture = new Texture2D(PlanetOverlaySize, PlanetOverlaySize, TextureFormat.RGBA32, mipChain: false)
            {
                name = "GeneratedAtmosphere",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float center = (PlanetOverlaySize - 1) * 0.5f;
            float outerRadius = PlanetOverlaySize * 0.5f;
            float coreRadius = outerRadius * AtmosphereCoreFraction;

            var pixels = new Color32[PlanetOverlaySize * PlanetOverlaySize];

            for (int y = 0; y < PlanetOverlaySize; y++)
            {
                for (int x = 0; x < PlanetOverlaySize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                    // Au plus lumineux juste au-dessus de la surface, puis extinction rapide :
                    // c'est ce gradient asymetrique qui se lit comme une atmosphere plutot que
                    // comme un contour lumineux.
                    float alpha;
                    if (distance <= coreRadius)
                    {
                        float t = distance / coreRadius;
                        alpha = t * t * t * 0.55f;
                    }
                    else
                    {
                        float t = Mathf.Clamp01((distance - coreRadius) / (outerRadius - coreRadius));
                        float falloff = 1f - t;
                        alpha = 0.55f * falloff * falloff;
                    }

                    pixels[y * PlanetOverlaySize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _cachedAtmosphereSprite = Sprite.Create(
                texture,
                new Rect(0, 0, PlanetOverlaySize, PlanetOverlaySize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
            _cachedAtmosphereSprite.name = "GeneratedAtmosphereSprite";

            return _cachedAtmosphereSprite;
        }

        /// <summary>
        /// Terminateur : le disque d'ombre pose <b>devant</b> la planete, transparent du cote
        /// eclaire et opaque du cote nuit (Phase 21.1).
        /// <para>
        /// <b>Pourquoi une ombre peinte plutot qu'une vraie lumiere ?</b> Eclairer la sphere
        /// demanderait un materiau <i>Lit</i> et une lumiere directionnelle dans une scene qui
        /// n'en contient aucune — donc un nuanceur a la merci de la configuration du pipeline de
        /// rendu, pour un resultat identique. Ici l'ombre reste fixe pendant que la planete
        /// tourne dessous, ce qui est exactement le comportement recherche : c'est l'etoile qui
        /// ne bouge pas, pas la surface.
        /// </para>
        /// <para>
        /// Le degrade est calcule le long de l'axe X et decoupe par le disque, si bien que le
        /// bord de l'ombre epouse la silhouette du globe au lieu de la couper au carre.
        /// </para>
        /// </summary>
        public static Sprite GetTerminatorSprite()
        {
            if (_cachedTerminatorSprite != null)
            {
                return _cachedTerminatorSprite;
            }

            var texture = new Texture2D(PlanetOverlaySize, PlanetOverlaySize, TextureFormat.RGBA32, mipChain: false)
            {
                name = "GeneratedTerminator",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float center = (PlanetOverlaySize - 1) * 0.5f;
            float radius = PlanetOverlaySize * 0.5f;
            var pixels = new Color32[PlanetOverlaySize * PlanetOverlaySize];

            for (int y = 0; y < PlanetOverlaySize; y++)
            {
                for (int x = 0; x < PlanetOverlaySize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float insideDisc = Mathf.Clamp01(radius - distance);

                    // 0 au bord gauche (plein jour), 1 au bord droit (pleine nuit).
                    float acrossDisc = x / (float)(PlanetOverlaySize - 1);

                    // Le jour occupe une bonne moitie du globe et la transition reste courte :
                    // un degrade etale sur toute la largeur donnerait une planete uniformement
                    // grisee plutot qu'un monde eclaire de cote.
                    float night = Mathf.Clamp01((acrossDisc - 0.34f) / 0.52f);
                    night = night * night * (3f - 2f * night);

                    pixels[y * PlanetOverlaySize + x] = new Color(0f, 0f, 0f, night * 0.88f * insideDisc);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _cachedTerminatorSprite = Sprite.Create(
                texture,
                new Rect(0, 0, PlanetOverlaySize, PlanetOverlaySize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
            _cachedTerminatorSprite.name = "GeneratedTerminatorSprite";

            return _cachedTerminatorSprite;
        }
    }
}
