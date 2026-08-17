using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Research;

namespace Espace.Gameplay.Chronicle
{
    /// <summary>Le codex, tel que l'interface et la sauvegarde le consultent.</summary>
    public interface ICodexService
    {
        /// <summary>Numeros des fragments obtenus, dans l'ordre ou ils sont arrives.</summary>
        IReadOnlyList<int> UnlockedNumbers { get; }

        /// <summary>Vrai si ce fragment a ete obtenu.</summary>
        bool IsUnlocked(int number);

        /// <summary>Nombre de fragments obtenus, sur <see cref="CodexLibrary.Count"/>.</summary>
        int UnlockedCount { get; }

        /// <summary>Restaure l'etat sauvegarde. Ignore les numeros inconnus.</summary>
        void Restore(IEnumerable<int> unlockedNumbers);
    }

    /// <summary>
    /// Delivre les fragments du journal de l'Empire disparu (Phase 24, etape 3).
    /// <para>
    /// <b>Il lit l'etat du monde, il n'ecoute pas les mutations.</b> Un fragment se declenche
    /// sur « cette faction n'a plus aucun systeme » ou « la pression a franchi ce seuil » — des
    /// <i>etats</i>, pas des evenements. Le proprietaire d'un systeme change aujourd'hui a trois
    /// endroits du code (bataille, invasion, cession diplomatique) et rien ne garantit qu'il n'y
    /// en aura pas un quatrieme. S'abonner a ces trois-la, c'est s'engager a n'en jamais oublier
    /// un ; un oubli ne planterait pas, il rendrait un fragment simplement indelivrable. Relire
    /// l'etat chaque jour coute un balayage de cent systemes et reste vrai par construction.
    /// C'est exactement le raisonnement retenu pour <c>SystemGlyphController</c> en Phase 23.
    /// </para>
    /// <para>
    /// <b>L'obtention est definitive.</b> La pression retombe quand l'empire se contracte, mais
    /// on ne desapprend pas ce qu'on a lu : l'ensemble des fragments obtenus ne fait que
    /// grandir. Cette memoire vit ici, jamais dans <see cref="CodexFragment"/>, qui reste une
    /// fonction pure.
    /// </para>
    /// <para>
    /// <b>Consequence utile sur les vieilles sauvegardes.</b> Comme l'etat est relu en continu,
    /// une partie enregistree avant cette phase — donc sans aucun fragment — retrouve des le
    /// lendemain tous ceux que sa situation justifie. Rien a migrer.
    /// </para>
    /// </summary>
    public sealed class CodexService : ICodexService, IGameService
    {
        /// <summary>
        /// Les domaines de recherche, releves une fois. <c>Enum.GetValues</c> alloue un tableau a
        /// chaque appel : le faire chaque jour de jeu pour cinq valeurs immuables serait du
        /// gaspillage pur.
        /// </summary>
        private static readonly ResearchDomain[] AllDomains =
            (ResearchDomain[])System.Enum.GetValues(typeof(ResearchDomain));

        private readonly IEventBus _eventBus;
        private readonly GalaxyMap _map;

        private readonly List<int> _unlocked = new List<int>(CodexLibrary.Count);
        private readonly HashSet<int> _unlockedSet = new HashSet<int>();

        // Reutilises a chaque evaluation plutot que realloues : ce code tourne une fois par jour
        // de jeu, et le projet proscrit les allocations recurrentes.
        private readonly List<FactionLineage> _annihilated = new List<FactionLineage>();
        private readonly List<ResearchDomain> _mastered = new List<ResearchDomain>();

        private IEconomyService _economy;
        private IResearchService _research;
        private EmpireRegistry _empires;
        private IChronicleService _chronicle;
        private IGameClock _clock;

        public CodexService(IEventBus eventBus, GalaxyMap map)
        {
            _eventBus = eventBus;
            _map = map;
        }

        /// <inheritdoc />
        public IReadOnlyList<int> UnlockedNumbers
        {
            get { return _unlocked; }
        }

        /// <inheritdoc />
        public int UnlockedCount
        {
            get { return _unlocked.Count; }
        }

        /// <inheritdoc />
        public bool IsUnlocked(int number)
        {
            return _unlockedSet.Contains(number);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _unlocked.Clear();
            _unlockedSet.Clear();

            _eventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _eventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);

            _unlocked.Clear();
            _unlockedSet.Clear();
        }

        /// <inheritdoc />
        public void Restore(IEnumerable<int> unlockedNumbers)
        {
            _unlocked.Clear();
            _unlockedSet.Clear();

            if (unlockedNumbers == null)
            {
                return;
            }

            foreach (int number in unlockedNumbers)
            {
                // Un numero inconnu vient d'une sauvegarde ecrite par une version qui en
                // comptait davantage. L'ignorer vaut mieux que refuser de charger la partie.
                if (CodexLibrary.IsKnownNumber(number) && _unlockedSet.Add(number))
                {
                    _unlocked.Add(number);
                }
            }
        }

        private void OnDayAdvanced(DayAdvancedEvent e)
        {
            if (_unlocked.Count >= CodexLibrary.Count)
            {
                return;
            }

            CodexWorldState state = ReadWorld();

            for (int i = 0; i < CodexLibrary.All.Count; i++)
            {
                CodexFragment fragment = CodexLibrary.All[i];

                if (_unlockedSet.Contains(fragment.Number) || !fragment.IsDeliveredBy(state))
                {
                    continue;
                }

                Unlock(fragment);
            }
        }

        private void Unlock(CodexFragment fragment)
        {
            _unlockedSet.Add(fragment.Number);
            _unlocked.Add(fragment.Number);

            Chronicle()?.Log.Add(new GameNotice(
                NoticeKind.FragmentFound,
                $"Fragment {fragment.Numeral} : {fragment.Title}.",
                CurrentDate()));
        }

        /// <summary>
        /// Releve les trois seules donnees dont les regles ont besoin.
        /// <para>
        /// <b>La pression lue est celle du joueur, pas la plus forte de la galaxie.</b> Les
        /// fragments racontent ce que <i>le joueur</i> est en train de vivre ; les delivrer parce
        /// qu'une IA etouffe a l'autre bout de la carte n'aurait aucun sens pour lui.
        /// </para>
        /// </summary>
        private CodexWorldState ReadWorld()
        {
            return new CodexWorldState(
                PressureOnThePlayer(),
                AnnihilatedLineages(),
                MasteredDomains());
        }

        private float PressureOnThePlayer()
        {
            if (_economy == null)
            {
                ServiceLocator.TryGet(out _economy);
            }

            return _economy?.GetAdministrativePressure(EconomyService.PlayerOwnerId) ?? 0f;
        }

        /// <summary>
        /// Factions qui ne possedent plus un seul systeme.
        /// <para>
        /// <b>L'empire du joueur en est exclu.</b> Un joueur reduit a rien a perdu la partie ; il
        /// n'a pas mis la main sur ses propres archives.
        /// </para>
        /// </summary>
        private List<FactionLineage> AnnihilatedLineages()
        {
            _annihilated.Clear();

            if (_empires == null)
            {
                ServiceLocator.TryGet(out _empires);
            }

            if (_empires == null || _map == null)
            {
                return _annihilated;
            }

            foreach (Empire empire in _empires.Empires)
            {
                if (empire == null
                    || empire.Lineage == FactionLineage.Unknown
                    || empire.Id == EconomyService.PlayerOwnerId)
                {
                    continue;
                }

                if (!OwnsAnySystem(empire.Id))
                {
                    _annihilated.Add(empire.Lineage);
                }
            }

            return _annihilated;
        }

        private bool OwnsAnySystem(int empireId)
        {
            foreach (StarSystemState system in _map.Systems)
            {
                if (system.OwnerId == empireId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Domaines dont le joueur a acheve le dernier palier.
        /// <para>
        /// « Plus de technologie suivante » est la definition exacte de « maitrise », et elle
        /// n'exige aucune methode nouvelle sur <c>IResearchService</c> : le catalogue connait
        /// deja son propre dernier palier.
        /// </para>
        /// </summary>
        private List<ResearchDomain> MasteredDomains()
        {
            _mastered.Clear();

            if (_research == null)
            {
                ServiceLocator.TryGet(out _research);
            }

            if (_research == null)
            {
                return _mastered;
            }

            foreach (ResearchDomain domain in AllDomains)
            {
                bool started = _research.GetCompletedTierCount(EconomyService.PlayerOwnerId, domain) > 0;

                if (started && _research.GetNextTechnology(EconomyService.PlayerOwnerId, domain) == null)
                {
                    _mastered.Add(domain);
                }
            }

            return _mastered;
        }

        private IChronicleService Chronicle()
        {
            if (_chronicle == null)
            {
                ServiceLocator.TryGet(out _chronicle);
            }

            return _chronicle;
        }

        private GameDate CurrentDate()
        {
            if (_clock == null)
            {
                ServiceLocator.TryGet(out _clock);
            }

            return _clock?.CurrentDate ?? GameDate.StartOfGame;
        }
    }
}
