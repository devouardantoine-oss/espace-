using Espace.Gameplay.Empires;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Encodage visuel de l'etat vivant d'un systeme sur la carte (Phase 23, tranche A).
    /// <para>
    /// <b>Le probleme resolu.</b> Lire l'etat d'un systeme demandait d'ouvrir un panneau qui
    /// recouvrait la carte : le jeu avait deux modes, regarder ou gerer, et comparer deux
    /// systemes etait impossible. Ici la planete <em>porte</em> ses propres releves — anneaux de
    /// developpement, halo de stabilite, pastilles de garnison — donc un panneau n'a plus besoin
    /// de les repeter et redevient une liste d'actions.
    /// </para>
    /// <para>
    /// <b>Ce qui est visible depuis l'exterieur, et ce qui ne l'est pas.</b> C'est la regle
    /// centrale de ce modele, et elle vient du gameplay, pas de l'esthetique :
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Le developpement se voit de partout.</b> C'est de l'infrastructure
    /// visible en orbite. La carte le montrait deja par la teinte du marqueur depuis la Phase 2 ;
    /// les anneaux ne font que le rendre denombrable.</description></item>
    /// <item><description><b>La stabilite et la garnison ne se voient que chez soi.</b> Ce sont
    /// des informations interieures. Les afficher sur les systemes adverses donnerait
    /// gratuitement au joueur ce que l'espionnage doit lui faire meriter — et contredirait
    /// l'assistant d'offensive, qui annonce « garnison inconnue » tant qu'aucune operation n'a
    /// abouti.</description></item>
    /// </list>
    /// <para>
    /// <b>Fonction pure.</b> Aucun <c>MonoBehaviour</c>, aucun service : la structure se calcule
    /// a partir de valeurs simples et se verifie sans scene, comme <see cref="WorldProfile"/> ou
    /// <see cref="TerritoryPartition"/>.
    /// </para>
    /// </summary>
    public readonly struct SystemGlyph
    {
        /// <summary>Nombre maximal d'anneaux, egal au developpement maximal d'un systeme.</summary>
        public const int MaximumDevelopmentRings = 5;

        /// <summary>
        /// Nombre maximal de pastilles de garnison affichees.
        /// <para>
        /// Au-dela, <see cref="GarrisonExceedsPips"/> passe a vrai et la derniere pastille change
        /// d'aspect. Compter des points au-dela de cinq d'un coup d'oeil n'est de toute facon pas
        /// possible — et un plafond borne le nombre de sprites crees par systeme.
        /// </para>
        /// </summary>
        public const int MaximumGarrisonPips = 5;

        /// <summary>Halo d'un systeme dont la stabilite est bonne : discret, froid.</summary>
        public static readonly Color CalmHalo = new Color(0.44f, 0.66f, 0.72f);

        /// <summary>Halo au seuil de bascule : ambre.</summary>
        public static readonly Color StrainedHalo = new Color(0.78f, 0.58f, 0.29f);

        /// <summary>Halo d'un systeme au bord de la revolte : rouge brique.</summary>
        public static readonly Color TroubledHalo = new Color(0.79f, 0.44f, 0.36f);

        /// <summary>Opacite du halo quand tout va bien : presque invisible, pour ne pas encombrer.</summary>
        public const float CalmHaloOpacity = 0.22f;

        /// <summary>Opacite du halo au plus bas de la stabilite : un systeme en difficulte doit attirer l'oeil.</summary>
        public const float TroubledHaloOpacity = 0.85f;

        // --- Geometrie -------------------------------------------------------
        //
        // Ces valeurs vivent ici plutot que dans StarSystemMarker pour une raison apprise a mes
        // depens : la premiere version les gardait dans le composant, et le rendu ASCII qui
        // servait a les verifier en detenait sa propre copie. Les deux ont diverge, et j'ai
        // valide une geometrie qui n'etait pas celle du jeu. Source unique, donc — et les
        // invariants (les anneaux ne croisent rien, les pastilles restent en orbite basse)
        // deviennent verifiables par des tests.
        //
        // Toutes les echelles sont exprimees en multiples du DIAMETRE du corps du marqueur.

        /// <summary>Echelle de l'anneau decoratif de la Phase 12, quand le systeme en porte un.</summary>
        public const float DecorativeRingScale = 1.9f;

        /// <summary>Echelle du premier anneau de developpement. Au-dela de l'anneau decoratif, deliberement.</summary>
        public const float FirstDevelopmentRingScale = 2.5f;

        /// <summary>Ecart d'echelle entre deux anneaux de developpement consecutifs.</summary>
        public const float DevelopmentRingSpacing = 0.6f;

        /// <summary>Echelle du halo de stabilite.</summary>
        public const float HaloScale = 5.8f;

        /// <summary>Distance d'orbite des pastilles de garnison, en multiples du rayon du corps.</summary>
        public const float GarrisonPipOrbit = 0.78f;

        /// <summary>Echelle d'une pastille de garnison.</summary>
        public const float GarrisonPipScale = 0.16f;

        /// <summary>Arc, en degres, sur lequel les pastilles se repartissent sous le systeme.</summary>
        public const float GarrisonPipArc = 74f;

        /// <summary>Echelle de l'anneau de developpement d'indice <paramref name="index"/> (0 pour le premier).</summary>
        public static float DevelopmentRingScaleAt(int index)
        {
            return FirstDevelopmentRingScale + index * DevelopmentRingSpacing;
        }

        /// <summary>Echelle du dernier anneau possible : la borne que le halo doit depasser.</summary>
        public static float OutermostDevelopmentRingScale()
        {
            return DevelopmentRingScaleAt(MaximumDevelopmentRings - 1);
        }

        // --- Etat encode -----------------------------------------------------

        /// <summary>Anneaux de developpement, de 0 a <see cref="MaximumDevelopmentRings"/>.</summary>
        public readonly int DevelopmentRings;

        /// <summary>Vrai si l'observateur possede ce systeme, donc en connait l'interieur.</summary>
        public readonly bool RevealsInternalState;

        /// <summary>Teinte du halo de stabilite. Sans objet si <see cref="RevealsInternalState"/> est faux.</summary>
        public readonly Color HaloColor;

        /// <summary>Opacite du halo, entre <see cref="CalmHaloOpacity"/> et <see cref="TroubledHaloOpacity"/>. Zero si l'interieur n'est pas connu.</summary>
        public readonly float HaloOpacity;

        /// <summary>Pastilles de garnison a afficher, de 0 a <see cref="MaximumGarrisonPips"/>. Zero si l'interieur n'est pas connu.</summary>
        public readonly int GarrisonPips;

        /// <summary>Vrai si la garnison reelle depasse le nombre de pastilles affichables.</summary>
        public readonly bool GarrisonExceedsPips;

        private SystemGlyph(int developmentRings, bool revealsInternalState, Color haloColor, float haloOpacity, int garrisonPips, bool garrisonExceedsPips)
        {
            DevelopmentRings = developmentRings;
            RevealsInternalState = revealsInternalState;
            HaloColor = haloColor;
            HaloOpacity = haloOpacity;
            GarrisonPips = garrisonPips;
            GarrisonExceedsPips = garrisonExceedsPips;
        }

        /// <summary>
        /// Encodage d'un systeme tel que le voit <paramref name="ownedByViewer"/>.
        /// </summary>
        /// <param name="developmentLevel">Niveau de developpement, 0 a 5. Visible de tous.</param>
        /// <param name="stability">Stabilite, 0 a 1. Ignoree si le systeme n'appartient pas a l'observateur.</param>
        /// <param name="garrisonCount">Unites en garnison. Ignorees si le systeme n'appartient pas a l'observateur.</param>
        /// <param name="ownedByViewer">Vrai si l'observateur possede ce systeme.</param>
        public static SystemGlyph For(int developmentLevel, float stability, int garrisonCount, bool ownedByViewer)
        {
            int rings = Mathf.Clamp(developmentLevel, 0, MaximumDevelopmentRings);

            if (!ownedByViewer)
            {
                // Rien de l'interieur ne filtre. Voir la regle dans la documentation de la classe.
                return new SystemGlyph(rings, false, CalmHalo, 0f, 0, false);
            }

            int pips = Mathf.Clamp(garrisonCount, 0, MaximumGarrisonPips);

            return new SystemGlyph(
                rings,
                true,
                HaloColorFor(stability),
                HaloOpacityFor(stability),
                pips,
                garrisonCount > MaximumGarrisonPips);
        }

        /// <summary>
        /// Teinte du halo : froide au-dessus du seuil critique, ambre au seuil, rouge en dessous.
        /// <para>
        /// <b>Le seuil est celui d'<see cref="EmpireAssessment.CriticalStability"/></b>, celui-la
        /// meme qui fait basculer un empire IA en posture de consolidation. Le halo vire donc a
        /// l'ambre exactement au moment ou la situation devient preoccupante pour l'IA : le
        /// joueur et la machine lisent le meme seuil. Aucun nombre nouveau n'a ete invente pour
        /// ce rendu.
        /// </para>
        /// </summary>
        public static Color HaloColorFor(float stability)
        {
            float value = Mathf.Clamp01(stability);

            if (value >= EmpireAssessment.CriticalStability)
            {
                float t = Mathf.InverseLerp(EmpireAssessment.CriticalStability, 1f, value);
                return Color.Lerp(StrainedHalo, CalmHalo, t);
            }

            float below = Mathf.InverseLerp(0f, EmpireAssessment.CriticalStability, value);
            return Color.Lerp(TroubledHalo, StrainedHalo, below);
        }

        /// <summary>
        /// Opacite du halo : elle <b>monte quand la stabilite baisse</b>.
        /// <para>
        /// Un systeme sain reste discret, un systeme en difficulte s'impose. C'est ce qui permet
        /// de reperer les trois planetes qui demandent quelque chose sans lire une liste — et
        /// c'est l'inverse d'un rendu qui souligne uniformement tout ce qu'il connait.
        /// </para>
        /// </summary>
        public static float HaloOpacityFor(float stability)
        {
            return Mathf.Lerp(TroubledHaloOpacity, CalmHaloOpacity, Mathf.Clamp01(stability));
        }
    }
}
