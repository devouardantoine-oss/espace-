using System;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Commandant d'une <see cref="Fleet"/> : bonus/malus multiplicatifs sur l'attaque, la
    /// vitesse et la défense de la flotte qu'il commande (Phase 15).
    /// <para>
    /// <b>Généré par hachage déterministe, même technique que <c>StarSystemVisualProfile</c></b>
    /// (Phase 12, variante de la finalisation MurmurHash3 32 bits) : aucun <c>System.Random</c>
    /// ni <c>UnityEngine.Random</c>. Mélange <c>(fleetId, ownerId)</c> plutôt que la seule
    /// graine de galaxie (non exposée par <c>GalaxyMap</c> jusqu'à <c>MilitaryService</c>) —
    /// sans <c>ownerId</c>, la toute première flotte de chaque empire de chaque partie
    /// obtiendrait le même Amiral (<c>MilitaryService._nextFleetId</c> repart toujours de 1,
    /// aucun empire ne démarre avec une garnison). Limitation résiduelle documentée : deux
    /// parties différentes rejouant le même <c>(fleetId, ownerId)</c> obtiennent le même
    /// Amiral — sans conséquence, aucune comparaison entre parties séparées n'a de sens ici.
    /// </para>
    /// <para>
    /// <b>Jamais régénéré après coup :</b> <c>Fleet.Id</c> est réémis séquentiellement à
    /// chaque chargement de sauvegarde (voir <c>MilitaryService.GetOrCreateStationedFleet</c>),
    /// donc un Amiral recalculé depuis un id différent changerait silencieusement au
    /// rechargement. Généré une seule fois à la création réelle de la flotte
    /// (<see cref="Compute"/>), puis transporté tel quel par la sauvegarde via le constructeur
    /// public direct.
    /// </para>
    /// <para>
    /// <b>Un malus garanti parmi les trois statistiques :</b> une pige totalement indépendante
    /// par statistique donnerait environ 1 Amiral sur 5 sans aucun défaut (probabilité que les
    /// trois tirages tombent du bon côté de zéro) — trop fréquent face à un brief qui illustre
    /// l'Amiral comme un compromis (ex. +attaque/+vitesse <i>mais</i> -défense). Une pige
    /// supplémentaire désigne laquelle des trois statistiques est la faible.
    /// </para>
    /// </summary>
    public readonly struct Admiral : IEquatable<Admiral>
    {
        /// <summary>Borne basse d'un bonus (donc la borne d'un malus) : -15%.</summary>
        private const float MinBonus = -0.15f;

        /// <summary>Borne haute d'un bonus : +20%. Les deux bornes couvrent les trois exemples du brief (+10% attaque, +20% vitesse, -10% défense).</summary>
        private const float MaxBonus = 0.20f;

        /// <summary>Marge minimale garantissant qu'un bonus est strictement positif et qu'un malus est strictement négatif (jamais pile zéro).</summary>
        private const float BonusEpsilon = 0.01f;

        /// <summary>
        /// Prénoms 100% originaux (même contrainte que les factions, Phase 11) : aucune
        /// référence à un univers sous licence tierce.
        /// </summary>
        private static readonly string[] FirstNames =
        {
            "Kael", "Sera", "Doran", "Ilyana", "Voss", "Mira", "Corvin", "Thessaly",
            "Renn", "Adara", "Baris", "Nyx", "Talon", "Ysolde"
        };

        private static readonly string[] Surnames =
        {
            "Vashe", "Korrin", "Draeth", "Solenne", "Kirrow", "Amberlyn", "Torvane", "Skarn",
            "Feyric", "Brannigan", "Duskmere", "Ferrand", "Halcyon", "Ravenscar"
        };

        public readonly string Name;

        /// <summary>Bonus/malus multiplicatif sur la puissance d'attaque (ex. <c>0.1f</c> = +10%, <c>-0.1f</c> = -10%).</summary>
        public readonly float AttackBonus;

        /// <summary>Bonus/malus multiplicatif sur la vitesse de déplacement.</summary>
        public readonly float SpeedBonus;

        /// <summary>Bonus/malus multiplicatif sur la puissance de défense.</summary>
        public readonly float DefenseBonus;

        /// <summary>Constructeur direct : restauration depuis une sauvegarde (voir <see cref="Compute"/> pour la génération).</summary>
        public Admiral(string name, float attackBonus, float speedBonus, float defenseBonus)
        {
            Name = name;
            AttackBonus = attackBonus;
            SpeedBonus = speedBonus;
            DefenseBonus = defenseBonus;
        }

        /// <summary>Génère un nouvel Amiral, déterministe pour un couple <paramref name="fleetId"/>/<paramref name="ownerId"/> donné.</summary>
        public static Admiral Compute(int fleetId, int ownerId)
        {
            string firstName = FirstNames[Mix(fleetId, ownerId, 1) % (uint)FirstNames.Length];
            string surname = Surnames[Mix(fleetId, ownerId, 2) % (uint)Surnames.Length];
            string name = $"{firstName} {surname}";

            uint weakStatIndex = Mix(fleetId, ownerId, 3) % 3u;

            float attack = RollBonus(fleetId, ownerId, salt: 4, isMalus: weakStatIndex == 0);
            float speed = RollBonus(fleetId, ownerId, salt: 5, isMalus: weakStatIndex == 1);
            float defense = RollBonus(fleetId, ownerId, salt: 6, isMalus: weakStatIndex == 2);

            return new Admiral(name, attack, speed, defense);
        }

        private static float RollBonus(int fleetId, int ownerId, int salt, bool isMalus)
        {
            float fraction = (Mix(fleetId, ownerId, salt) % 1000u) / 1000f; // [0, 1)
            return isMalus
                ? MinBonus + fraction * (-BonusEpsilon - MinBonus)     // [MinBonus, -BonusEpsilon]
                : BonusEpsilon + fraction * (MaxBonus - BonusEpsilon); // [BonusEpsilon, MaxBonus]
        }

        /// <summary>Variante de la finalisation MurmurHash3 32 bits (même fonction que <c>StarSystemVisualProfile.Mix</c>, <c>seed</c> remplacé par <paramref name="ownerId"/>).</summary>
        private static uint Mix(int fleetId, int ownerId, int salt)
        {
            unchecked
            {
                uint h = (uint)fleetId;
                h = h * 0x9E3779B1u + (uint)ownerId;
                h ^= (uint)salt * 0x85EBCA77u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }

        public bool Equals(Admiral other) =>
            Name == other.Name && AttackBonus.Equals(other.AttackBonus)
            && SpeedBonus.Equals(other.SpeedBonus) && DefenseBonus.Equals(other.DefenseBonus);

        public override bool Equals(object obj) => obj is Admiral other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Name, AttackBonus, SpeedBonus, DefenseBonus);

        public override string ToString() =>
            $"Amiral {Name} (Attaque {AttackBonus:+0%;-0%}, Vitesse {SpeedBonus:+0%;-0%}, Defense {DefenseBonus:+0%;-0%})";

        public static bool operator ==(Admiral left, Admiral right) => left.Equals(right);
        public static bool operator !=(Admiral left, Admiral right) => !left.Equals(right);
    }
}
