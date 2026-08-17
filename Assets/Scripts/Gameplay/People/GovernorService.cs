using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Chronicle;
using Espace.Gameplay.Decisions;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Espionage;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using UnityEngine;

namespace Espace.Gameplay.People
{
    /// <summary>Les gouverneurs, tels que l'interface et la sauvegarde les consultent.</summary>
    public interface IGovernorService
    {
        /// <summary>Le gouverneur d'un systeme du joueur, ou <c>null</c> si le systeme ne lui appartient pas.</summary>
        Governor GetGovernor(StarSystemId systemId);

        /// <summary>Loyaute actuelle du gouverneur de ce systeme, de 0 a 1.</summary>
        float GetLoyalty(StarSystemId systemId);

        /// <summary>Systemes dont on tient une memoire, pour la sauvegarde.</summary>
        IEnumerable<StarSystemId> RememberedSystems { get; }

        /// <summary>Restaure la memoire d'un gouverneur.</summary>
        void RestoreMemory(StarSystemId systemId, IEnumerable<GovernorFact> facts);
    }

    /// <summary>
    /// Donne un visage aux mondes du joueur, et une suite a ses decisions (Phase 24, etape 6).
    /// <para>
    /// <b>Ce que cette etape apporte.</b> L'etape 5 a donne au joueur des choix couteux ; ils ne
    /// laissaient de trace que dans une ardoise chiffree. Un gouverneur <i>retient</i> ce qu'on a
    /// fait sur son monde, et finit par partir si c'est trop. C'est ce qui transforme une facture
    /// en relation.
    /// </para>
    /// <para>
    /// <b>Aucun gouverneur n'est stocke, seule sa memoire l'est.</b> Le nom et le temperament se
    /// recalculent par hachage de <c>(systeme, suzerain)</c>, comme un Amiral depuis la Phase 15 :
    /// une sauvegarde n'a donc a porter que les trois faits retenus. C'est aussi ce qui fait
    /// qu'une partie anterieure se charge sans rien de special — les gouverneurs reapparaissent
    /// identiques, simplement sans souvenirs.
    /// </para>
    /// <para>
    /// <b>La defection ne donne pas d'information a l'adversaire</b>, contrairement a ce que la
    /// bible d'univers prevoit. Ce serait le comportement juste, mais aucune IA de ce jeu ne
    /// possede de modele de renseignement : lui « apprendre » un systeme n'aurait aucun effet
    /// observable, et coder un effet imaginaire vaut moins que de noter le manque. Le depart
    /// coute donc ce qu'il peut reellement couter : de la stabilite, et un monde a reprendre en
    /// main.
    /// </para>
    /// </summary>
    public sealed class GovernorService : IGovernorService, IGameService
    {
        /// <summary>Stabilite perdue quand un gouverneur claque la porte.</summary>
        public const float DefectionStabilityLoss = 0.15f;

        private readonly IEventBus _eventBus;
        private readonly GalaxyMap _map;

        private readonly Dictionary<StarSystemId, Governor> _governors = new Dictionary<StarSystemId, Governor>();

        private IChronicleService _chronicle;
        private IGameClock _clock;

        public GovernorService(IEventBus eventBus, GalaxyMap map)
        {
            _eventBus = eventBus;
            _map = map;
        }

        /// <inheritdoc />
        public IEnumerable<StarSystemId> RememberedSystems
        {
            get { return _governors.Keys; }
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _governors.Clear();

            _eventBus.Subscribe<DecisionAnsweredEvent>(OnDecisionAnswered);
            _eventBus.Subscribe<SystemSabotagedEvent>(OnSabotaged);
            _eventBus.Subscribe<BattleResolvedEvent>(OnBattleResolved);
            _eventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _eventBus.Unsubscribe<DecisionAnsweredEvent>(OnDecisionAnswered);
            _eventBus.Unsubscribe<SystemSabotagedEvent>(OnSabotaged);
            _eventBus.Unsubscribe<BattleResolvedEvent>(OnBattleResolved);
            _eventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);

            _governors.Clear();
        }

        /// <inheritdoc />
        public Governor GetGovernor(StarSystemId systemId)
        {
            if (!OwnedByPlayer(systemId))
            {
                return null;
            }

            if (!_governors.TryGetValue(systemId, out Governor governor))
            {
                governor = Governor.Compute(systemId.Value, EconomyService.PlayerOwnerId);
                _governors[systemId] = governor;
            }

            return governor;
        }

        /// <inheritdoc />
        public float GetLoyalty(StarSystemId systemId)
        {
            Governor governor = GetGovernor(systemId);
            if (governor == null)
            {
                return 0f;
            }

            float stability = _map != null && _map.TryGetSystem(systemId, out StarSystemState system)
                ? system.Stability
                : 1f;

            return LoyaltyModel.Compute(governor.BaseLoyalty, governor.Memory, stability);
        }

        /// <inheritdoc />
        public void RestoreMemory(StarSystemId systemId, IEnumerable<GovernorFact> facts)
        {
            Governor governor = GetGovernor(systemId);
            if (governor != null)
            {
                governor.RestoreMemory(facts);
            }
        }

        // --- Ce qu'un gouverneur observe ---------------------------------------------------

        private void OnDecisionAnswered(DecisionAnsweredEvent e)
        {
            if (e.RememberedAs.HasValue)
            {
                Remember(e.SystemId, e.RememberedAs.Value);
            }
        }

        private void OnSabotaged(SystemSabotagedEvent e)
        {
            if (e.TargetEmpireId == EconomyService.PlayerOwnerId)
            {
                Remember(e.SystemId, GovernorFactKind.Sabotaged);
            }
        }

        /// <summary>
        /// Une bataille sur son monde : gagnee, on est venu ; perdue, il a vu tomber la place.
        /// <para>
        /// Le gouverneur ne retient que ce qui se passe <b>chez lui</b>. Une defaite a l'autre
        /// bout de la galaxie ne lui parvient pas — meme regle de confidentialite que la carte et
        /// le journal.
        /// </para>
        /// </summary>
        private void OnBattleResolved(BattleResolvedEvent e)
        {
            bool playerAttacked = e.AttackerEmpireId == EconomyService.PlayerOwnerId;
            bool playerDefended = e.DefenderEmpireId == EconomyService.PlayerOwnerId;

            if (!playerDefended && !playerAttacked)
            {
                return;
            }

            // Seul le defenseur a un gouverneur en place au moment des faits. Un monde qu'on
            // vient de prendre recevra le sien, vierge de tout souvenir.
            if (!playerDefended)
            {
                return;
            }

            bool held = !e.AttackerWon;
            Remember(e.SystemId, held ? GovernorFactKind.Defended : GovernorFactKind.Abandoned);
        }

        private void Remember(StarSystemId systemId, GovernorFactKind kind)
        {
            Governor governor = GetGovernor(systemId);
            governor?.Remember(new GovernorFact(kind, Today()));
        }

        // --- Depart ------------------------------------------------------------------------

        /// <summary>
        /// Verifie chaque jour si quelqu'un s'en va.
        /// <para>
        /// <b>Sur l'etat, pas sur un evenement</b>, comme partout ailleurs depuis la Phase 23 : la
        /// loyaute bouge aussi bien par un fait retenu que par la stabilite qui derive toute
        /// seule, et enumerer les causes reviendrait a s'engager a n'en jamais oublier une.
        /// </para>
        /// </summary>
        private void OnDayAdvanced(DayAdvancedEvent dayAdvanced)
        {
            if (_map == null)
            {
                return;
            }

            foreach (StarSystemState system in _map.Systems)
            {
                if (system.OwnerId != EconomyService.PlayerOwnerId
                    || !_governors.ContainsKey(system.Id))
                {
                    // Un systeme dont personne n'a encore reveille le gouverneur n'a aucun
                    // souvenir : sa loyaute est celle de son temperament, jamais assez basse
                    // pour partir. Inutile de la calculer pour cent mondes chaque jour.
                    continue;
                }

                if (!LoyaltyModel.WouldDefect(GetLoyalty(system.Id)))
                {
                    continue;
                }

                Defect(system);
            }
        }

        private void Defect(StarSystemState system)
        {
            Governor leaving = GetGovernor(system.Id);

            system.Stability = Mathf.Clamp01(system.Stability - DefectionStabilityLoss);

            // Le suivant arrive sans souvenirs : c'est la seule facon de repartir. Le hachage
            // donnerait le meme nom, on efface donc simplement la memoire.
            _governors.Remove(system.Id);
            Governor arriving = GetGovernor(system.Id);
            arriving?.RestoreMemory(null);

            Record(
                NoticeKind.GovernorDefected,
                $"{leaving?.Name} quitte {system.Name}.",
                system.Id);
        }

        // --- Utilitaires ---------------------------------------------------------------------

        private bool OwnedByPlayer(StarSystemId systemId)
        {
            return _map != null
                   && _map.TryGetSystem(systemId, out StarSystemState system)
                   && system.OwnerId == EconomyService.PlayerOwnerId;
        }

        private void Record(NoticeKind kind, string text, StarSystemId subject)
        {
            if (_chronicle == null)
            {
                ServiceLocator.TryGet(out _chronicle);
            }

            _chronicle?.Log.Add(new GameNotice(kind, text, Today(), subject));
        }

        private GameDate Today()
        {
            if (_clock == null)
            {
                ServiceLocator.TryGet(out _clock);
            }

            return _clock != null ? _clock.CurrentDate : GameDate.StartOfGame;
        }
    }
}
