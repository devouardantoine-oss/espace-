using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Fabrique la texture de surface d'une planete : continents, oceans, calottes polaires,
    /// entierement par code (Phase 21.1).
    /// <para>
    /// <b>Texture equirectangulaire</b> — longitude en abscisse, latitude en ordonnee. C'est la
    /// projection que Unity applique par defaut a une sphere, donc aucune correspondance
    /// d'habillage n'est a fournir : la texture se pose telle quelle.
    /// </para>
    /// <para>
    /// <b>Le relief est calcule a part de la couleur</b>, dans <see cref="CreateElevation"/> qui
    /// ne renvoie que des nombres. Cette separation n'est pas theorique : elle rend le seul
    /// defaut serieux de ce genre de generation — la couture de longitude — verifiable par un
    /// test, sans moteur graphique ni œil humain.
    /// </para>
    /// <para>
    /// <b>La couture n'existe pas, elle n'a pas a etre corrigee.</b> Un bruit de Perlin
    /// echantillonne sur <c>[0,1]</c> ne reboucle pas, et une sphere habillee ainsi porte une
    /// cicatrice verticale bien visible des qu'elle tourne. Plutot que de fondre les deux bords
    /// l'un dans l'autre, la longitude parcourt ici un <b>cercle</b> dans le plan du bruit :
    /// apres un tour complet, l'echantillon retombe exactement sur son point de depart. La
    /// latitude, elle, translate le centre de ce cercle, ce qui donne une vraie variation dans
    /// les deux directions sans rompre la periodicite.
    /// </para>
    /// <para>
    /// <b>Le fondu de bords, essaye d'abord, a ete abandonne :</b> melanger le champ avec sa
    /// copie decalee revient a moyenner deux echantillons independants, ce qui divise l'ecart
    /// type par la racine de deux au milieu de la carte. Le relief y devenait sensiblement plus
    /// plat qu'aux bords — un defaut invisible sur une image fixe, mais qui aplatissait les
    /// continents. Le pincement aux poles, lui, est laisse tel quel : les calottes de glace le
    /// recouvrent exactement.
    /// </para>
    /// </summary>
    public static class PlanetTextureFactory
    {
        /// <summary>Largeur par defaut. Le double de la hauteur, comme l'exige une projection equirectangulaire.</summary>
        public const int DefaultWidth = 256;

        /// <summary>Hauteur par defaut. 128 suffit largement : la planete occupe au plus la moitie d'un ecran de telephone.</summary>
        public const int DefaultHeight = 128;

        /// <summary>Nombre d'octaves de bruit. Trois donnent des cotes credibles ; au-dela, le detail est invisible a la taille d'affichage.</summary>
        private const int OctaveCount = 3;

        /// <summary>
        /// Rayon du cercle parcouru par la longitude dans le plan du bruit, a la premiere
        /// octave. Il fixe le nombre de motifs par tour de planete : trop petit, la planete n'a
        /// qu'un continent ; trop grand, elle est mouchetee.
        /// </summary>
        private const float BaseRadius = 1.6f;

        /// <summary>Distance parcourue d'un pole a l'autre dans le plan du bruit, a la premiere octave.</summary>
        private const float BaseLatitudeSpan = 3.2f;

        /// <summary>Facteur de frequence d'une octave a la suivante. Non entier, pour eviter que les octaves ne s'alignent en motifs reguliers.</summary>
        private const float Lacunarity = 2.2f;

        /// <summary>
        /// Etirement du contraste applique au relief brut.
        /// <para>
        /// La somme de plusieurs octaves donne une distribution en cloche tres etroite — sans
        /// correction, 90 % du relief tient entre 0,4 et 0,6, le niveau des mers devient un fil
        /// de rasoir et deux mondes tires au hasard se ressemblent. L'etirement redonne des
        /// plaines franches et des massifs francs.
        /// </para>
        /// </summary>
        private const float ContrastGain = 2.6f;

        /// <summary>Decalage maximal des coordonnees de bruit, pour que deux graines donnent deux mondes.</summary>
        private const float MaximumNoiseOffset = 4096f;

        private readonly struct SurfacePalette
        {
            public readonly Color DeepWater;
            public readonly Color ShallowWater;
            public readonly Color Lowland;
            public readonly Color Highland;
            public readonly Color Ice;

            /// <summary>Part de la surface couverte d'eau, entre 0 et 1. Le niveau des mers en est deduit pour chaque monde.</summary>
            public readonly float WaterShare;

            /// <summary>Latitude absolue (0 = equateur, 1 = pole) a partir de laquelle la glace apparait. 1 signifie « aucune calotte ».</summary>
            public readonly float IceLatitude;

            public SurfacePalette(Color deepWater, Color shallowWater, Color lowland, Color highland, Color ice, float waterShare, float iceLatitude)
            {
                DeepWater = deepWater;
                ShallowWater = shallowWater;
                Lowland = lowland;
                Highland = highland;
                Ice = ice;
                WaterShare = waterShare;
                IceLatitude = iceLatitude;
            }
        }

        /// <summary>
        /// Part de la surface couverte d'eau pour le type indique, entre 0 et 1. Zero signifie
        /// un monde entierement sec.
        /// <para>
        /// <b>Une part, pas une altitude.</b> Le premier jet fixait un niveau des mers absolu —
        /// mais la part d'ocean qu'il produit depend de la distribution exacte du bruit, et
        /// celle-ci varie d'une implementation de Perlin a l'autre. Un monde regle « aride »
        /// s'est ainsi retrouve couvert a 28 % d'eau sous Unity la ou la calibration hors-ligne
        /// en annoncait 10. Exprimer directement l'intention — « un dixieme de la surface » —
        /// donne le meme resultat quelle que soit la source de bruit.
        /// </para>
        /// </summary>
        public static float WaterShareOf(PlanetKind kind) => PaletteOf(kind).WaterShare;

        /// <summary>
        /// Altitude en dessous de laquelle se trouve exactement <paramref name="waterShare"/> de
        /// la surface.
        /// <para>
        /// Calcule par histogramme plutot que par tri : le champ compte des dizaines de milliers
        /// de valeurs, et un tri allouerait autant, a chaque changement de monde sur un ecran ou
        /// le joueur fait defiler les candidats.
        /// </para>
        /// </summary>
        public static float SeaLevelFor(float[] elevation, float waterShare)
        {
            if (elevation == null || elevation.Length == 0 || waterShare <= 0f)
            {
                return 0f;
            }

            if (waterShare >= 1f)
            {
                return 1f;
            }

            const int buckets = 256;
            var histogram = new int[buckets];
            for (int i = 0; i < elevation.Length; i++)
            {
                int bucket = Mathf.Clamp((int)(elevation[i] * buckets), 0, buckets - 1);
                histogram[bucket]++;
            }

            int target = Mathf.RoundToInt(elevation.Length * waterShare);
            int running = 0;

            for (int bucket = 0; bucket < buckets; bucket++)
            {
                running += histogram[bucket];
                if (running >= target)
                {
                    // Borne haute du seau : tout ce qui est strictement en dessous est immerge.
                    return (bucket + 1) / (float)buckets;
                }
            }

            return 1f;
        }

        /// <summary>
        /// Champ d'altitude equirectangulaire, valeurs entre 0 et 1, indexe <c>y * width + x</c>.
        /// <para>Sans couleur ni texture : c'est la partie verifiable de la generation.</para>
        /// </summary>
        /// <param name="seed">Meme graine, meme monde.</param>
        public static float[] CreateElevation(int seed, int width = DefaultWidth, int height = DefaultHeight)
        {
            width = Mathf.Max(4, width);
            height = Mathf.Max(2, height);

            var random = new System.Random(seed);
            var offsets = new Vector2[OctaveCount];
            for (int octave = 0; octave < OctaveCount; octave++)
            {
                offsets[octave] = new Vector2(
                    (float)random.NextDouble() * MaximumNoiseOffset,
                    (float)random.NextDouble() * MaximumNoiseOffset);
            }

            var elevation = new float[width * height];

            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);

                for (int x = 0; x < width; x++)
                {
                    // La longitude est un angle, pas une abscisse : c'est ce qui fait reboucler
                    // la carte sans le moindre raccord (voir la remarque de la classe).
                    float angle = 2f * Mathf.PI * x / width;
                    elevation[y * width + x] = Stretch(SampleFractal(angle, v, offsets));
                }
            }

            return elevation;
        }

        /// <summary>
        /// Texture de surface prete a habiller une sphere.
        /// </summary>
        /// <param name="seed">Meme graine, meme monde.</param>
        /// <param name="kind">Determine la palette, le niveau des mers et l'etendue des calottes.</param>
        public static Texture2D CreateSurface(int seed, PlanetKind kind, int width = DefaultWidth, int height = DefaultHeight)
        {
            width = Mathf.Max(4, width);
            height = Mathf.Max(2, height);

            float[] elevation = CreateElevation(seed, width, height);
            SurfacePalette palette = PaletteOf(kind);
            float seaLevel = SeaLevelFor(elevation, palette.WaterShare);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: true)
            {
                // Reboucle en longitude, se bloque en latitude : sans cela, le pixel du pole nord
                // se melangerait a celui du pole sud au filtrage.
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = $"Planet_{kind}_{seed}",
            };

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                // 0 a l'equateur, 1 aux poles.
                float latitude = Mathf.Abs(y / (float)(height - 1) * 2f - 1f);

                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    pixels[index] = Shade(elevation[index], seaLevel, latitude, palette);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Color32 Shade(float elevation, float seaLevel, float latitude, SurfacePalette palette)
        {
            Color color;

            if (elevation < seaLevel)
            {
                // Rapporte a la profondeur relative plutot qu'a l'altitude brute : les hauts-fonds
                // restent visibles quelle que soit la part d'ocean du type de planete.
                float depth = seaLevel > 0f ? elevation / seaLevel : 1f;
                color = Color.Lerp(palette.DeepWater, palette.ShallowWater, depth * depth);
            }
            else
            {
                float span = 1f - seaLevel;
                float height = span > 0f ? (elevation - seaLevel) / span : elevation;
                color = Color.Lerp(palette.Lowland, palette.Highland, height);
            }

            if (latitude > palette.IceLatitude && palette.IceLatitude < 1f)
            {
                float t = Mathf.InverseLerp(palette.IceLatitude, 1f, latitude);
                color = Color.Lerp(color, palette.Ice, UiSmooth(t));
            }

            color.a = 1f;
            return color;
        }

        /// <summary>
        /// Lissage local plutot que <c>Mathf.SmoothStep</c> : celui du moteur interpole
        /// <em>entre deux bornes</em> et ne remplace pas le <c>smoothstep</c> des nuanceurs.
        /// La meme confusion avait deja fausse la transparence des libelles de faction en
        /// Phase 19.
        /// </summary>
        private static float UiSmooth(float t)
        {
            float clamped = Mathf.Clamp01(t);
            return clamped * clamped * (3f - 2f * clamped);
        }

        /// <summary>
        /// Somme d'octaves echantillonnee le long d'un cercle de longitude, translate par la
        /// latitude.
        /// </summary>
        /// <param name="angle">Longitude, en radians. Un tour complet ramene au meme point du bruit.</param>
        /// <param name="v">Latitude normalisee, de 0 (pole sud) a 1 (pole nord).</param>
        private static float SampleFractal(float angle, float v, Vector2[] offsets)
        {
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            float total = 0f;
            float amplitude = 1f;
            float totalAmplitude = 0f;
            float radius = BaseRadius;
            float latitudeSpan = BaseLatitudeSpan;

            for (int octave = 0; octave < offsets.Length; octave++)
            {
                total += amplitude * Mathf.PerlinNoise(
                    offsets[octave].x + radius * cos,
                    offsets[octave].y + radius * sin + v * latitudeSpan);

                totalAmplitude += amplitude;
                amplitude *= 0.5f;
                radius *= Lacunarity;
                latitudeSpan *= Lacunarity;
            }

            return totalAmplitude > 0f ? total / totalAmplitude : 0.5f;
        }

        /// <summary>Ecarte les valeurs de la moyenne pour redonner du contraste au relief (voir <see cref="ContrastGain"/>).</summary>
        private static float Stretch(float value)
        {
            return Mathf.Clamp01(0.5f + (value - 0.5f) * ContrastGain);
        }

        private static SurfacePalette PaletteOf(PlanetKind kind)
        {
            switch (kind)
            {
                case PlanetKind.Ocean:
                    return new SurfacePalette(
                        new Color(0.02f, 0.10f, 0.26f), new Color(0.10f, 0.34f, 0.55f),
                        new Color(0.28f, 0.46f, 0.36f), new Color(0.46f, 0.55f, 0.42f),
                        new Color(0.86f, 0.92f, 0.96f), waterShare: 0.86f, iceLatitude: 0.86f);

                case PlanetKind.Arid:
                    return new SurfacePalette(
                        new Color(0.16f, 0.12f, 0.06f), new Color(0.34f, 0.24f, 0.11f),
                        new Color(0.56f, 0.37f, 0.18f), new Color(0.75f, 0.56f, 0.32f),
                        new Color(0.88f, 0.86f, 0.80f), waterShare: 0.10f, iceLatitude: 0.94f);

                case PlanetKind.Ice:
                    return new SurfacePalette(
                        new Color(0.10f, 0.20f, 0.31f), new Color(0.34f, 0.53f, 0.66f),
                        new Color(0.66f, 0.77f, 0.85f), new Color(0.88f, 0.94f, 0.98f),
                        new Color(0.95f, 0.98f, 1f), waterShare: 0.34f, iceLatitude: 0.28f);

                case PlanetKind.Toxic:
                    return new SurfacePalette(
                        new Color(0.14f, 0.17f, 0.05f), new Color(0.33f, 0.40f, 0.10f),
                        new Color(0.42f, 0.48f, 0.16f), new Color(0.58f, 0.62f, 0.28f),
                        new Color(0.72f, 0.78f, 0.60f), waterShare: 0.30f, iceLatitude: 1f);

                case PlanetKind.Barren:
                    return new SurfacePalette(
                        new Color(0.14f, 0.13f, 0.13f), new Color(0.22f, 0.21f, 0.20f),
                        new Color(0.36f, 0.34f, 0.32f), new Color(0.55f, 0.53f, 0.50f),
                        new Color(0.70f, 0.70f, 0.72f), waterShare: 0f, iceLatitude: 1f);

                default:
                    return new SurfacePalette(
                        new Color(0.03f, 0.12f, 0.28f), new Color(0.10f, 0.32f, 0.52f),
                        new Color(0.18f, 0.42f, 0.30f), new Color(0.48f, 0.46f, 0.32f),
                        new Color(0.90f, 0.94f, 0.98f), waterShare: 0.56f, iceLatitude: 0.78f);
            }
        }
    }
}
