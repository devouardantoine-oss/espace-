using System;
using System.Collections.Generic;
using System.Reflection;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="MilitaryDecisionMaker"/> : recrutement jusqu'a la cible de garnison,
    /// preference d'unite selon la personnalite, colonisation d'un voisin libre, seuil
    /// d'agressivite avant d'attaquer, une seule action par appel.
    /// </summary>
    [TestFixture]
    public sealed class MilitaryDecisionMakerTests
    {
        private const int AiEmpireId = 3;
        private const int NeighborEmpireId = 4;

        private sealed class FakeGameClock : IGameClock
        {
            public GameDate CurrentDate { get; set; } = GameDate.StartOfGame;
            public GameSpeed CurrentSpeed => GameSpeed.Normal;
            public float CurrentMultiplier => 1f;
            public bool IsPaused => false;
            public void Pause() { }
            public void Resume() { }
            public void TogglePause() { }
            public void SetSpeed(GameSpeed speed) { }
            public void SetDate(GameDate date) => CurrentDate = date;
            public void ResetToStart() { }
        }

        /// <summary>Meme role que dans <c>MilitaryServiceTests</c> : seul le statut guerre/paix compte ici, directement pilotable.</summary>
        private sealed class FakeDiplomacyService : IDiplomacyService
        {
            private readonly Dictionary<(int, int), DiplomaticStatus> _statuses = new Dictionary<(int, int), DiplomaticStatus>();

            private static (int, int) Key(int a, int b) => a <= b ? (a, b) : (b, a);

            public void SetStatus(int empireAId, int empireBId, DiplomaticStatus status) => _statuses[Key(empireAId, empireBId)] = status;

            public DiplomaticStatus GetStatus(int empireAId, int empireBId) =>
                _statuses.TryGetValue(Key(empireAId, empireBId), out DiplomaticStatus status) ? status : DiplomaticStatus.Peace;

            public float GetOpinion(int observerId, int targetId) => 0f;
            public bool HasTradeTreaty(int empireAId, int empireBId) => false;
            public bool IsEmbargoing(int fromEmpireId, int toEmpireId) => false;
            public IReadOnlyList<DiplomaticProposal> GetPendingProposalsFor(int empireId) => Array.Empty<DiplomaticProposal>();

            public bool TryDeclareWar(int declarerId, int targetId, out string error)
            {
                SetStatus(declarerId, targetId, DiplomaticStatus.War);
                error = null;
                return true;
            }

            public bool TrySetEmbargo(int fromEmpireId, int toEmpireId, bool active, out string error)
            {
                error = "Non supporte par ce faux service.";
                return false;
            }

            public bool TryBreakPact(int fromEmpireId, int toEmpireId, out string error)
            {
                SetStatus(fromEmpireId, toEmpireId, DiplomaticStatus.Peace);
                error = null;
                return true;
            }

            public bool TrySubmitProposal(
                int proposerId, int targetId, ProposalType type, ResourceBundle offeredResources, ResourceBundle requestedResources,
                StarSystemId? offeredSystemId, StarSystemId? requestedSystemId, out string error)
            {
                error = "Non supporte par ce faux service.";
                return false;
            }

            public bool TryRespondToProposal(int proposalId, bool accept, out string error)
            {
                error = "Non supporte par ce faux service.";
                return false;
            }

            public void ApplyOpinionShift(int observerId, int targetId, float delta) { }
            public void RestoreRelations(int empireAId, int empireBId, DiplomaticStatus status, bool hasTradeTreaty) { }
            public void RestoreOpinion(int observerId, int targetId, float value) { }
            public void RestoreEmbargo(int fromEmpireId, int toEmpireId) { }
        }

        private EventBus _eventBus;
        private FakeGameClock _clock;
        private EmpireRegistry _empireRegistry;
        private FakeDiplomacyService _diplomacy;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _eventBus.Initialize();
            _clock = new FakeGameClock();
            _diplomacy = new FakeDiplomacyService();
            _empireRegistry = new EmpireRegistry(new[]
            {
                new Empire(EconomyService.PlayerOwnerId, "Joueur", Color.blue, EmpirePersonality.Expansionist, isPlayerControlled: true),
                new Empire(AiEmpireId, "IA", Color.red, EmpirePersonality.Militarist, isPlayerControlled: false),
                new Empire(NeighborEmpireId, "Voisin", Color.green, EmpirePersonality.Pacifist, isPlayerControlled: false),
            });
        }

        [TearDown]
        public void TearDown()
        {
            _eventBus.Shutdown();
        }

        private static StarSystemState MakeSystem(int id, Vector2 position, int ownerId, int developmentLevel = 3)
        {
            var system = new StarSystemState(new StarSystemId(id), $"System{id}", position, 1000, 500, developmentLevel, 1f, Array.Empty<ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        private static UnitTypeDefinition MakeUnitType(
            UnitType type, float power = 10f, float speed = 100f, float creditsCost = 20f, float mineralsCost = 10f, int minDevelopment = 0)
        {
            var unitType = ScriptableObject.CreateInstance<UnitTypeDefinition>();
            SetPrivateField(unitType, "displayName", type.ToString());
            SetPrivateField(unitType, "unitType", type);
            SetPrivateField(unitType, "power", power);
            SetPrivateField(unitType, "speed", speed);
            SetPrivateField(unitType, "creditsCost", creditsCost);
            SetPrivateField(unitType, "mineralsCost", mineralsCost);
            SetPrivateField(unitType, "recruitmentDays", 1);
            SetPrivateField(unitType, "upkeepPerDay", 0f);
            SetPrivateField(unitType, "minimumDevelopmentLevel", minDevelopment);
            return unitType;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Champ introuvable : {fieldName}");
            field.SetValue(target, value);
        }

        private EconomyService MakeEconomy(GalaxyMap map)
        {
            var economy = new EconomyService(map, _clock, _eventBus, Array.Empty<BuildingType>());
            economy.Initialize();
            return economy;
        }

        private MilitaryService MakeMilitary(GalaxyMap map, IEconomyService economy, params UnitTypeDefinition[] catalog)
        {
            var military = new MilitaryService(map, _clock, _eventBus, economy, _diplomacy, _empireRegistry, catalog);
            military.Initialize();
            return military;
        }

        private void GiveCredits(EconomyService economy, StarSystemState system, int empireId, float minimumAmount)
        {
            int originalWealth = system.Wealth;
            float originalTax = economy.GetTaxRate(empireId);

            system.Wealth = Mathf.CeilToInt(minimumAmount / 0.05f) + 1;
            economy.SetTaxRate(empireId, 1f);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(1)));

            system.Wealth = originalWealth;
            economy.SetTaxRate(empireId, originalTax);
        }

        private void RecruitAndComplete(MilitaryService military, StarSystemState system, UnitTypeDefinition unitType, int count)
        {
            military.TryRecruitUnits(system.Id, unitType, count, out string error);
            Assert.IsNull(error, $"Recrutement prealable au test echoue : {error}");
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(unitType.RecruitmentDays)));
        }

        // --- Aucun systeme ------------------------------------------------------------

        [Test]
        public void DecideAndAct_EmpireOwnsNoSystem_DoesNothing()
        {
            StarSystemState unowned = MakeSystem(0, Vector2.zero, StarSystemState.UnownedOwnerId);
            var map = new GalaxyMap(new[] { unowned }, Array.Empty<HyperlaneLink>());
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            Assert.DoesNotThrow(() => MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy));
            Assert.AreEqual(UnitBundle.Zero, military.GetGarrison(unowned.Id, AiEmpireId));
        }

        // --- Recrutement jusqu'a la cible ------------------------------------------------

        [Test]
        public void DecideAndAct_GarrisonBelowTarget_RecruitsExactlyOneUnit()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiEmpireId);
            var map = new GalaxyMap(new[] { home }, Array.Empty<HyperlaneLink>());
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, AiEmpireId, 1000f);
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId); // TargetGarrisonSize = 8

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(infantry.RecruitmentDays)));

            Assert.AreEqual(new UnitBundle(infantry: 1), military.GetGarrison(home.Id, AiEmpireId));
        }

        [Test]
        public void DecideAndAct_GarrisonAtOrAboveTarget_DoesNotRecruit()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, NeighborEmpireId, developmentLevel: 0);
            var map = new GalaxyMap(new[] { home }, Array.Empty<HyperlaneLink>());
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, NeighborEmpireId, 1000f);
            RecruitAndComplete(military, home, infantry, 2); // Pacifist : TargetGarrisonSize = 2, deja atteinte
            Empire pacifist = _empireRegistry.GetEmpire(NeighborEmpireId);

            MilitaryDecisionMaker.DecideAndAct(pacifist, map, economy, military, _diplomacy);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(infantry.RecruitmentDays)));

            Assert.AreEqual(new UnitBundle(infantry: 2), military.GetGarrison(home.Id, NeighborEmpireId), "Ne doit pas depasser sa cible.");
        }

        [Test]
        public void DecideAndAct_Militarist_PrefersStrongestAffordableUnit()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiEmpireId, developmentLevel: 5);
            var map = new GalaxyMap(new[] { home }, Array.Empty<HyperlaneLink>());
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, power: 10f, creditsCost: 20f);
            UnitTypeDefinition specialForces = MakeUnitType(UnitType.SpecialForces, power: 40f, creditsCost: 30f);
            MilitaryService military = MakeMilitary(map, economy, infantry, specialForces);
            GiveCredits(economy, home, AiEmpireId, 1000f);
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId); // PrefersStrongestUnit = true

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(specialForces.RecruitmentDays)));

            Assert.AreEqual(new UnitBundle(specialForces: 1), military.GetGarrison(home.Id, AiEmpireId));
        }

        [Test]
        public void DecideAndAct_NonMilitarist_PrefersCheapestAffordableUnit()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, NeighborEmpireId, developmentLevel: 5);
            var map = new GalaxyMap(new[] { home }, Array.Empty<HyperlaneLink>());
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, power: 10f, creditsCost: 20f);
            UnitTypeDefinition specialForces = MakeUnitType(UnitType.SpecialForces, power: 40f, creditsCost: 30f);
            MilitaryService military = MakeMilitary(map, economy, infantry, specialForces);
            GiveCredits(economy, home, NeighborEmpireId, 1000f);
            Empire pacifist = _empireRegistry.GetEmpire(NeighborEmpireId); // PrefersStrongestUnit = false

            MilitaryDecisionMaker.DecideAndAct(pacifist, map, economy, military, _diplomacy);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(infantry.RecruitmentDays)));

            Assert.AreEqual(new UnitBundle(infantry: 1), military.GetGarrison(home.Id, NeighborEmpireId));
        }

        // --- Colonisation ------------------------------------------------------------

        [Test]
        public void DecideAndAct_GarrisonAtTarget_ColonizesAdjacentUnowned()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, NeighborEmpireId); // Pacifist, cible = 2
            StarSystemState unowned = MakeSystem(1, new Vector2(1f, 0f), StarSystemState.UnownedOwnerId);
            var map = new GalaxyMap(new[] { home, unowned }, new[] { new HyperlaneLink(home.Id, unowned.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, NeighborEmpireId, 1000f);
            RecruitAndComplete(military, home, infantry, 3); // au-dessus de la cible (2) : peut se permettre d'en detacher un
            Empire pacifist = _empireRegistry.GetEmpire(NeighborEmpireId);

            MilitaryDecisionMaker.DecideAndAct(pacifist, map, economy, military, _diplomacy);

            // Une unite a ete detachee et envoyee : la garnison d'origine diminue immediatement,
            // mais la colonisation elle-meme n'a pas encore eu lieu (le trajet prend au moins un jour).
            Assert.AreEqual(new UnitBundle(infantry: 2), military.GetGarrison(home.Id, NeighborEmpireId));
            Assert.AreEqual(StarSystemState.UnownedOwnerId, unowned.OwnerId);

            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(1)));

            Assert.AreEqual(NeighborEmpireId, unowned.OwnerId, "Le systeme doit etre colonise a l'arrivee.");
        }

        [Test]
        public void DecideAndAct_NotEnoughSpareUnits_DoesNotColonize()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, NeighborEmpireId);
            StarSystemState unowned = MakeSystem(1, new Vector2(1f, 0f), StarSystemState.UnownedOwnerId);
            var map = new GalaxyMap(new[] { home, unowned }, new[] { new HyperlaneLink(home.Id, unowned.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, NeighborEmpireId, 1000f);
            RecruitAndComplete(military, home, infantry, 2); // exactement la cible du Pacifiste : rien a epargner
            Empire pacifist = _empireRegistry.GetEmpire(NeighborEmpireId);

            MilitaryDecisionMaker.DecideAndAct(pacifist, map, economy, military, _diplomacy);

            Assert.AreEqual(StarSystemState.UnownedOwnerId, unowned.OwnerId);
            Assert.AreEqual(new UnitBundle(infantry: 2), military.GetGarrison(home.Id, NeighborEmpireId));
        }

        // --- Agressivite -----------------------------------------------------------------

        [Test]
        public void DecideAndAct_Pacifist_NeverAttacksEvenWithOverwhelmingForce()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, NeighborEmpireId);
            StarSystemState enemy = MakeSystem(1, new Vector2(1f, 0f), AiEmpireId);
            var map = new GalaxyMap(new[] { home, enemy }, new[] { new HyperlaneLink(home.Id, enemy.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, NeighborEmpireId, 1000f);
            // Plafond de 10 unites par flotte (Phase 14) : garnison ecrasante restauree directement.
            military.RestoreGarrison(home.Id, NeighborEmpireId, new UnitBundle(infantry: 50));
            RecruitAndComplete(military, enemy, infantry, 1); // adversaire quasi sans defense
            Empire pacifist = _empireRegistry.GetEmpire(NeighborEmpireId);

            MilitaryDecisionMaker.DecideAndAct(pacifist, map, economy, military, _diplomacy);

            Assert.AreEqual(AiEmpireId, enemy.OwnerId, "Le Pacifiste ne doit jamais attaquer.");
            Assert.AreEqual(new UnitBundle(infantry: 50), military.GetGarrison(home.Id, NeighborEmpireId), "Garnison intacte : aucune flotte detachee.");
        }

        [Test]
        public void DecideAndAct_Militarist_AttacksNeighborAlreadyAtWar()
        {
            // Depuis la Phase 7, le choix "qui attaquer" (seuil d'agressivite, rapport de
            // puissance) revient a DiplomacyDecisionMaker : celui-ci n'a plus qu'a exploiter
            // une guerre deja declaree.
            StarSystemState home = MakeSystem(0, Vector2.zero, AiEmpireId);
            StarSystemState enemy = MakeSystem(1, new Vector2(1f, 0f), NeighborEmpireId);
            var map = new GalaxyMap(new[] { home, enemy }, new[] { new HyperlaneLink(home.Id, enemy.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, AiEmpireId, 1000f);
            GiveCredits(economy, enemy, NeighborEmpireId, 1000f);
            // Plafond de 10 unites par flotte (Phase 14) : garnison ecrasante restauree directement.
            military.RestoreGarrison(home.Id, AiEmpireId, new UnitBundle(infantry: 20));
            RecruitAndComplete(military, enemy, infantry, 1);
            _diplomacy.SetStatus(AiEmpireId, NeighborEmpireId, DiplomaticStatus.War);
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);

            Assert.IsTrue(military.TryGetStationedFleet(home.Id, AiEmpireId, out Fleet remainingGarrison));
            Assert.Less(remainingGarrison.Composition.TotalCount, 20, "Une partie de la garnison doit avoir ete detachee pour attaquer.");
        }

        [Test]
        public void DecideAndAct_Militarist_ReservesInfantryFirstAcrossAllSevenTypes()
        {
            // Garnison mixte incluant les nouveaux types de vaisseaux (Phase 14) : SplitAttackForce
            // doit toujours garder l'Infanterie en priorite (elle apparait en premier dans
            // UnitTypes.All) et envoyer le reste, quels que soient les types presents.
            StarSystemState home = MakeSystem(0, Vector2.zero, AiEmpireId);
            StarSystemState enemy = MakeSystem(1, new Vector2(1f, 0f), NeighborEmpireId);
            var map = new GalaxyMap(new[] { home, enemy }, new[] { new HyperlaneLink(home.Id, enemy.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, AiEmpireId, 1000f);
            GiveCredits(economy, enemy, NeighborEmpireId, 1000f);
            // Garnison mixte restauree directement (au-dela du plafond de recrutement de 10, Phase 14).
            // Total (9) >= TargetGarrisonSize du Militariste (8) : le recrutement ne doit pas primer sur l'attaque.
            military.RestoreGarrison(home.Id, AiEmpireId, new UnitBundle(infantry: 2, fighter: 4, cruiser: 3));
            RecruitAndComplete(military, enemy, infantry, 1);
            _diplomacy.SetStatus(AiEmpireId, NeighborEmpireId, DiplomaticStatus.War);
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);

            Assert.IsTrue(military.TryGetStationedFleet(home.Id, AiEmpireId, out Fleet remainingGarrison));
            Assert.AreEqual(2, remainingGarrison.Composition.Infantry, "Les 2 unites d'Infanterie doivent rester en reserve (MinimumGarrisonToKeep = 2).");
            Assert.AreEqual(0, remainingGarrison.Composition.Fighter + remainingGarrison.Composition.Cruiser, "Le reste part attaquer.");
        }

        [Test]
        public void DecideAndAct_Militarist_DoesNotAttackWithoutDeclaredWar()
        {
            // Meme rapport de force ecrasant qu'un scenario d'attaque classique, mais aucune
            // guerre n'a ete declaree : depuis la Phase 7, TryMoveFleet la refuserait de toute
            // facon, donc MilitaryDecisionMaker ne doit meme pas tenter le detachement.
            StarSystemState home = MakeSystem(0, Vector2.zero, AiEmpireId);
            StarSystemState enemy = MakeSystem(1, new Vector2(1f, 0f), NeighborEmpireId);
            var map = new GalaxyMap(new[] { home, enemy }, new[] { new HyperlaneLink(home.Id, enemy.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, AiEmpireId, 1000f);
            GiveCredits(economy, enemy, NeighborEmpireId, 1000f);
            // Plafond de 10 unites par flotte (Phase 14) : garnison ecrasante restauree directement.
            military.RestoreGarrison(home.Id, AiEmpireId, new UnitBundle(infantry: 20));
            RecruitAndComplete(military, enemy, infantry, 1);
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);

            Assert.AreEqual(NeighborEmpireId, enemy.OwnerId);
            Assert.AreEqual(new UnitBundle(infantry: 20), military.GetGarrison(home.Id, AiEmpireId), "Aucune attaque sans guerre declaree : garnison intacte.");
        }

        // --- Une seule action par appel --------------------------------------------------

        [Test]
        public void DecideAndAct_RecruitmentTakesPriorityOverColonizationAndAttack()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiEmpireId);
            StarSystemState unowned = MakeSystem(1, new Vector2(1f, 0f), StarSystemState.UnownedOwnerId);
            StarSystemState enemy = MakeSystem(2, new Vector2(-1f, 0f), NeighborEmpireId);
            var map = new GalaxyMap(
                new[] { home, unowned, enemy },
                new[] { new HyperlaneLink(home.Id, unowned.Id), new HyperlaneLink(home.Id, enemy.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveCredits(economy, home, AiEmpireId, 1000f);
            // Garnison encore sous la cible du Militariste (8) : le recrutement doit primer.
            RecruitAndComplete(military, home, infantry, 1);
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);

            Assert.AreEqual(StarSystemState.UnownedOwnerId, unowned.OwnerId, "Pas de colonisation ce tour : le recrutement a eu lieu.");
            Assert.AreEqual(NeighborEmpireId, enemy.OwnerId, "Pas d'attaque ce tour non plus.");
        }
    }
}
