using System;
using System.Collections.Generic;
using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>Une flotte engagee dans une offensive, avec son delai d'arrivee (Phase 20).</summary>
    public readonly struct OffensiveWave
    {
        public readonly int FleetId;
        public readonly UnitBundle Composition;

        /// <summary>Jours de trajet jusqu'a la cible.</summary>
        public readonly int TravelDays;

        /// <summary>Modificateur de combat de cette flotte (moral d'origine, Amiral, commandement).</summary>
        public readonly float AttackModifier;

        public OffensiveWave(int fleetId, UnitBundle composition, int travelDays, float attackModifier)
        {
            FleetId = fleetId;
            Composition = composition;
            TravelDays = travelDays;
            AttackModifier = attackModifier;
        }
    }

    /// <summary>Ce que la planification annonce au joueur avant qu'il confirme (Phase 20).</summary>
    public readonly struct OffensiveOutcome
    {
        /// <summary>Nombre de flottes engagees.</summary>
        public readonly int WaveCount;

        /// <summary>Total des unites embarquees, toutes flottes confondues.</summary>
        public readonly int UnitCount;

        /// <summary>Puissance offensive cumulee, modificateurs compris.</summary>
        public readonly float AttackPower;

        /// <summary>Puissance defensive estimee, terrain compris.</summary>
        public readonly float DefensePower;

        /// <summary>Jours jusqu'a l'arrivee de la <b>derniere</b> flotte engagee.</summary>
        public readonly int TravelDays;

        /// <summary>Vrai si la simulation aboutit a la prise du systeme.</summary>
        public readonly bool SystemCaptured;

        /// <summary>Vrai si toutes les vagues sont repoussees sans jamais entamer la garnison au point de la detruire.</summary>
        public readonly bool AllWavesRepelled;

        /// <summary>Unites perdues par l'attaquant, toutes vagues confondues.</summary>
        public readonly int AttackerLosses;

        /// <summary>Unites encore en garnison chez le defenseur a l'issue de la simulation.</summary>
        public readonly int SurvivingDefenders;

        /// <summary>
        /// Vrai si la derniere vague victorieuse n'embarquait plus d'Infanterie : la garnison est
        /// detruite mais le systeme reste a son proprietaire.
        /// </summary>
        public readonly bool WonWithoutOccupation;

        public OffensiveOutcome(
            int waveCount, int unitCount, float attackPower, float defensePower, int travelDays,
            bool systemCaptured, bool allWavesRepelled, int attackerLosses, int survivingDefenders,
            bool wonWithoutOccupation)
        {
            WaveCount = waveCount;
            UnitCount = unitCount;
            AttackPower = attackPower;
            DefensePower = defensePower;
            TravelDays = travelDays;
            SystemCaptured = systemCaptured;
            AllWavesRepelled = allWavesRepelled;
            AttackerLosses = attackerLosses;
            SurvivingDefenders = survivingDefenders;
            WonWithoutOccupation = wonWithoutOccupation;
        }
    }

    /// <summary>
    /// Simule une offensive avant son lancement, pour que le joueur sache ce qu'il engage
    /// (Phase 20).
    /// <para>
    /// <b>Pourquoi aucune « probabilite de victoire » ?</b> Parce qu'il n'y en a pas :
    /// <see cref="CombatResolver"/> est <b>entierement deterministe</b> — le camp le plus
    /// puissant l'emporte, sans le moindre tirage. Afficher « 68 % de victoire » serait une
    /// invention pure. Cette classe annonce donc ce qui va reellement se produire : l'issue,
    /// les pertes, et si le systeme change de mains. C'est a la fois plus honnete et plus utile
    /// qu'un pourcentage.
    /// </para>
    /// <para>
    /// <b>Les vagues sont simulees dans leur ordre d'arrivee, une bataille chacune.</b> C'est
    /// ce que fait le jeu : <c>MilitaryService</c> resout chaque arrivee independamment, il n'y
    /// a pas de bataille combinee. Trois flottes arrivant a trois dates differentes livrent
    /// trois batailles successives, et se font battre en detail — la simulation le montre au
    /// lieu de le cacher derriere un total flatteur, ce qui apprend au joueur a concentrer ses
    /// forces.
    /// </para>
    /// <para>
    /// <b>Estimation, pas certitude.</b> La garnison peut etre renforcee avant l'arrivee, et le
    /// commandement comme la recherche de l'adversaire ne sont pas connus du joueur — c'est
    /// precisement le role de l'espionnage. L'appelant doit presenter le resultat comme une
    /// prevision a effectifs constants.
    /// </para>
    /// </summary>
    public static class OffensivePlanner
    {
        /// <summary>Bonus defensif par niveau de developpement, aligne sur <c>MilitaryService.TerrainBonusPerDevelopmentLevel</c>.</summary>
        public const float TerrainBonusPerDevelopmentLevel = 0.1f;

        /// <summary>Modificateur defensif visible du joueur : moral du systeme et fortifications.</summary>
        public static float VisibleDefenderModifier(float systemStability, int developmentLevel)
        {
            return Mathf.Max(0f, systemStability) * (1f + Mathf.Max(0, developmentLevel) * TerrainBonusPerDevelopmentLevel);
        }

        /// <summary>
        /// Simule l'offensive. <paramref name="waves"/> peut arriver dans n'importe quel ordre :
        /// la simulation les rejoue par delai d'arrivee croissant.
        /// </summary>
        /// <param name="waves">Flottes engagees.</param>
        /// <param name="defenderComposition">Garnison connue de la cible.</param>
        /// <param name="defenderModifier">Modificateur defensif (voir <see cref="VisibleDefenderModifier"/>).</param>
        /// <param name="catalog">Catalogue d'unites, pour les puissances.</param>
        public static OffensiveOutcome Simulate(
            IReadOnlyList<OffensiveWave> waves,
            UnitBundle defenderComposition,
            float defenderModifier,
            IReadOnlyList<UnitTypeDefinition> catalog)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            if (waves.Count == 0)
            {
                return new OffensiveOutcome(0, 0, 0f, ComputeDefensePower(defenderComposition, defenderModifier, catalog),
                    0, false, false, 0, defenderComposition.TotalCount, false);
            }

            var ordered = new List<OffensiveWave>(waves);
            ordered.Sort(WaveArrivalComparer.Instance);

            UnitBundle defenders = defenderComposition;
            int unitCount = 0;
            int losses = 0;
            int travelDays = 0;
            float attackPower = 0f;
            bool captured = false;
            bool wonWithoutOccupation = false;
            bool anyWaveWon = false;

            foreach (OffensiveWave wave in ordered)
            {
                unitCount += wave.Composition.TotalCount;
                attackPower += CombatResolver.ComputePower(wave.Composition, catalog) * wave.AttackModifier;
                travelDays = Mathf.Max(travelDays, wave.TravelDays);

                if (captured)
                {
                    // Le systeme est deja pris : les vagues suivantes ne font que renforcer la
                    // garnison, elles ne livrent aucune bataille et ne perdent rien.
                    continue;
                }

                CombatResolver.BattleOutcome outcome = CombatResolver.Resolve(
                    wave.Composition, wave.AttackModifier, defenders, defenderModifier, catalog);

                losses += wave.Composition.TotalCount - outcome.AttackerSurvivors.TotalCount;

                if (!outcome.AttackerWon)
                {
                    defenders = outcome.DefenderSurvivors;
                    continue;
                }

                anyWaveWon = true;
                defenders = UnitBundle.Zero;

                // Gagner la bataille spatiale ne suffit pas : sans Infanterie survivante, la
                // garnison est detruite mais le systeme reste a son proprietaire (Phase 16).
                if (outcome.AttackerSurvivors.Infantry > 0)
                {
                    captured = true;
                    wonWithoutOccupation = false;
                }
                else
                {
                    wonWithoutOccupation = true;
                }
            }

            return new OffensiveOutcome(
                ordered.Count,
                unitCount,
                attackPower,
                ComputeDefensePower(defenderComposition, defenderModifier, catalog),
                travelDays,
                captured,
                !anyWaveWon,
                losses,
                defenders.TotalCount,
                wonWithoutOccupation && !captured);
        }

        private static float ComputeDefensePower(UnitBundle composition, float modifier, IReadOnlyList<UnitTypeDefinition> catalog)
        {
            return CombatResolver.ComputePower(composition, catalog) * modifier;
        }

        /// <summary>
        /// Tri par delai d'arrivee, puis par identifiant de flotte. Le second critere rend
        /// l'ordre <b>total</b> : deux flottes arrivant le meme jour sont toujours simulees dans
        /// le meme ordre, donc la prevision affichee ne change pas d'une frame a l'autre.
        /// </summary>
        private sealed class WaveArrivalComparer : IComparer<OffensiveWave>
        {
            public static readonly WaveArrivalComparer Instance = new WaveArrivalComparer();

            public int Compare(OffensiveWave a, OffensiveWave b)
            {
                int byDays = a.TravelDays.CompareTo(b.TravelDays);
                return byDays != 0 ? byDays : a.FleetId.CompareTo(b.FleetId);
            }
        }
    }
}
