using Espace.Data;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>Difficulte estimee d'un monde de depart.</summary>
    public enum WorldDifficulty
    {
        Facile,
        Moyenne,
        Difficile
    }

    /// <summary>
    /// Portrait lisible d'un monde candidat au depart : type, ressources, difficulte, secteur
    /// (Phase 21.4).
    /// <para>
    /// <b>Rien n'est invente ici.</b> Le generateur produit deja des systemes differencies —
    /// gisements, population, developpement, nombre de voisins — mais l'ancien ecran n'en
    /// montrait que le nom, si bien que le joueur choisissait au hasard faute d'avoir de quoi
    /// choisir autrement. Ce portrait ne fabrique pas de donnees : il traduit celles qui
    /// existent en quelque chose qu'on peut comparer d'un coup d'œil.
    /// </para>
    /// <para>
    /// <b>Structure pure, sans <c>MonoBehaviour</c> ni texture</b> : la derivation est ainsi
    /// verifiable en EditMode, ce qui compte parce qu'une note de ressource fausse ne se voit
    /// pas — elle se croit.
    /// </para>
    /// </summary>
    public readonly struct WorldProfile
    {
        /// <summary>Note maximale d'une ressource, en etoiles.</summary>
        public const int MaximumRating = 5;

        /// <summary>Type de monde, qui determine son apparence.</summary>
        public readonly PlanetKind Kind;

        /// <summary>Note en metaux, de 0 a <see cref="MaximumRating"/>.</summary>
        public readonly int Metals;

        /// <summary>Note en energie.</summary>
        public readonly int Energy;

        /// <summary>Note en nourriture.</summary>
        public readonly int Food;

        /// <summary>Difficulte estimee du depart.</summary>
        public readonly WorldDifficulty Difficulty;

        /// <summary>Nom du secteur, deduit de la position dans la galaxie.</summary>
        public readonly string SectorName;

        /// <summary>Nombre de systemes directement relies.</summary>
        public readonly int NeighbourCount;

        private WorldProfile(PlanetKind kind, int metals, int energy, int food, WorldDifficulty difficulty, string sectorName, int neighbourCount)
        {
            Kind = kind;
            Metals = metals;
            Energy = energy;
            Food = food;
            Difficulty = difficulty;
            SectorName = sectorName;
            NeighbourCount = neighbourCount;
        }

        /// <summary>Noms de secteur, choisis par l'angle du systeme autour du centre galactique.</summary>
        private static readonly string[] Sectors =
        {
            "Secteur Alpha", "Secteur Beta", "Secteur Gamma",
            "Secteur Delta", "Secteur Epsilon", "Secteur Zeta"
        };

        /// <summary>
        /// Nombre de voisins au-dela duquel un monde est juge expose : un carrefour est riche en
        /// debouches, mais c'est aussi par la que les flottes arrivent.
        /// </summary>
        private const int ExposedNeighbourCount = 4;

        /// <summary>En dessous, le monde est isole : facile a tenir, lent a developper.</summary>
        private const int ShelteredNeighbourCount = 2;

        /// <summary>Construit le portrait d'un systeme.</summary>
        /// <param name="system">Systeme candidat.</param>
        /// <param name="neighbourCount">Nombre de systemes relies, lu sur la carte.</param>
        public static WorldProfile Describe(StarSystemState system, int neighbourCount)
        {
            if (system == null)
            {
                return new WorldProfile(PlanetKind.Barren, 0, 0, 0, WorldDifficulty.Moyenne, Sectors[0], 0);
            }

            int metals = RatingFor(system, ResourceType.Minerals);
            int energy = RatingFor(system, ResourceType.Energy);
            int food = RatingFor(system, ResourceType.Food);

            return new WorldProfile(
                KindFor(system, metals, energy, food),
                metals,
                energy,
                food,
                DifficultyFor(neighbourCount),
                SectorFor(system.Position),
                neighbourCount);
        }

        /// <summary>
        /// Note d'une ressource, de 0 a 5.
        /// <para>
        /// La presence du gisement pese le plus lourd — c'est la seule information franche que
        /// porte le generateur — et le developpement du systeme n'ajoute qu'un appoint. Sans
        /// gisement, la note reste basse quel que soit le developpement : un monde peuple ne
        /// fait pas apparaitre du minerai.
        /// </para>
        /// </summary>
        private static int RatingFor(StarSystemState system, ResourceType resource)
        {
            bool hasDeposit = false;
            for (int i = 0; i < system.ResourceDeposits.Length; i++)
            {
                if (system.ResourceDeposits[i] == resource)
                {
                    hasDeposit = true;
                    break;
                }
            }

            int rating = hasDeposit ? 3 : 1;
            rating += Mathf.Clamp(system.DevelopmentLevel, 0, 5) / 2;

            return Mathf.Clamp(rating, 0, MaximumRating);
        }

        /// <summary>
        /// Type de monde deduit de son profil de ressources.
        /// <para>
        /// <b>Deterministe, sans tirage :</b> un systeme doit garder le meme visage d'une
        /// ouverture de l'ecran a l'autre, et le meme que celui qu'il aura en partie.
        /// </para>
        /// </summary>
        private static PlanetKind KindFor(StarSystemState system, int metals, int energy, int food)
        {
            if (system.Population <= 0 && system.DevelopmentLevel <= 0)
            {
                return PlanetKind.Barren;
            }

            // Depart sur l'identifiant plutot que sur un tirage : deux mondes parfaitement
            // quelconques se distinguent quand meme, et toujours de la meme facon.
            if (metals == energy && energy == food)
            {
                return (system.Id.Value & 1) == 0 ? PlanetKind.Terran : PlanetKind.Ice;
            }

            // Le type suit le profil *relatif*, pas des seuils absolus. Un premier jet comparait
            // les notes a des paliers fixes — mais le developpement releve les trois a la fois,
            // si bien qu'un monde tres developpe ne descendait jamais sous le seuil « peu de
            // nourriture » et ne pouvait plus etre desertique, quels que soient ses gisements.
            if (food >= metals && food >= energy)
            {
                // L'energie tient lieu d'ensoleillement : abondante, elle donne un monde tempere ;
                // faible, un monde d'eau sous une etoile pale.
                return energy >= 3 ? PlanetKind.Terran : PlanetKind.Ocean;
            }

            return metals > energy ? PlanetKind.Arid : PlanetKind.Toxic;
        }

        private static WorldDifficulty DifficultyFor(int neighbourCount)
        {
            if (neighbourCount >= ExposedNeighbourCount)
            {
                return WorldDifficulty.Difficile;
            }

            return neighbourCount <= ShelteredNeighbourCount ? WorldDifficulty.Facile : WorldDifficulty.Moyenne;
        }

        private static string SectorFor(Vector2 position)
        {
            if (position.sqrMagnitude < Mathf.Epsilon)
            {
                return Sectors[0];
            }

            float angle = Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            int index = Mathf.Clamp(Mathf.FloorToInt(angle / (360f / Sectors.Length)), 0, Sectors.Length - 1);
            return Sectors[index];
        }
    }
}
