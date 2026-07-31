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

        /// <summary>
        /// Cree directement <paramref name="amount"/> de <b>chaque</b> ressource dans le tresor
        /// de <paramref name="empireId"/>.
        /// <para>
        /// <b>Un octroi direct, pas une journee de production simulee.</b> L'ancienne version
        /// gonflait la richesse du systeme et publiait un jour de jeu, ce qui ne produisait que
        /// des <i>credits</i> — alors qu'une unite coute aussi des minerais, eux issus de la
        /// population (1000 habitants = 10 minerais par jour, soit exactement de quoi recruter
        /// une seule unite). Tout test recrutant deux unites ou plus echouait donc sur
        /// « Ressources insuffisantes », pour une raison sans aucun rapport avec ce qu'il
        /// verifiait. <c>Grant</c> est deterministe et ne depend d'aucun reglage d'economie.
        /// </para>
        /// </summary>
        private static void GiveResources(IEconomyService economy, int empireId, float amount)
        {
            economy.Grant(empireId, new ResourceBundle(
                credits: amount, minerals: amount, energy: amount, food: amount, influence: amount));
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
            GiveResources(economy, AiEmpireId, 1000f);
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
            GiveResources(economy, NeighborEmpireId, 1000f);
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
            GiveResources(economy, AiEmpireId, 1000f);
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
            GiveResources(economy, NeighborEmpireId, 1000f);
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
            GiveResources(economy, NeighborEmpireId, 1000f);
            // Cible libre Pop 1000 / Dev 3 -> 3 Infanterie requises (Phase 16), plus les 2 gardees a domicile.
            RecruitAndComplete(military, home, infantry, 5);
            Empire pacifist = _empireRegistry.GetEmpire(NeighborEmpireId);

            MilitaryDecisionMaker.DecideAndAct(pacifist, map, economy, military, _diplomacy);

            // Les 3 Infanterie requises ont ete detachees et envoyees : la garnison d'origine diminue
            // immediatement, mais la colonisation elle-meme n'a pas encore eu lieu (le trajet prend au moins un jour).
            Assert.AreEqual(new UnitBundle(infantry: 2), military.GetGarrison(home.Id, NeighborEmpireId));
            Assert.AreEqual(StarSystemState.UnownedOwnerId, unowned.OwnerId);

            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(1)));

            Assert.AreEqual(NeighborEmpireId, unowned.OwnerId, "Le systeme doit etre colonise a l'arrivee.");
            Assert.AreEqual(
                new UnitBundle(infantry: 2), military.GetGarrison(unowned.Id, NeighborEmpireId),
                "3 Infanterie engagees - 1 perdue a l'installation = 2 en garnison sur la colonie.");
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
            GiveResources(economy, NeighborEmpireId, 1000f);
            // 2 Infanterie : il en faudrait 3 pour coloniser plus 2 a garder a domicile (Phase 16).
            RecruitAndComplete(military, home, infantry, 2);
            Empire pacifist = _empireRegistry.GetEmpire(NeighborEmpireId);

            MilitaryDecisionMaker.DecideAndAct(pacifist, map, economy, military, _diplomacy);

            Assert.AreEqual(StarSystemState.UnownedOwnerId, unowned.OwnerId, "Pas assez d'Infanterie pour tenter la colonisation.");
            Assert.AreEqual(new UnitBundle(infantry: 2), military.GetGarrison(home.Id, NeighborEmpireId), "Aucune unite detachee.");
        }

        [Test]
        public void DecideAndAct_UnownedNeighborPresent_RecruitsBeyondPersonalityTarget()
        {
            // Sans cette regle (Phase 16), le Pacifiste (cible de garnison 2) ne pourrait jamais
            // coloniser quoi que ce soit : coloniser demande 2 unites gardees + l'exigence du systeme vise.
            StarSystemState home = MakeSystem(0, Vector2.zero, NeighborEmpireId);
            StarSystemState unowned = MakeSystem(1, new Vector2(1f, 0f), StarSystemState.UnownedOwnerId);
            var map = new GalaxyMap(new[] { home, unowned }, new[] { new HyperlaneLink(home.Id, unowned.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveResources(economy, NeighborEmpireId, 1000f);
            RecruitAndComplete(military, home, infantry, 2); // deja a la cible de personnalite
            Empire pacifist = _empireRegistry.GetEmpire(NeighborEmpireId);

            MilitaryDecisionMaker.DecideAndAct(pacifist, map, economy, military, _diplomacy);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(infantry.RecruitmentDays)));

            Assert.AreEqual(
                3, military.GetGarrison(home.Id, NeighborEmpireId).TotalCount,
                "La cible effective monte a 2 (reserve) + 3 (exigence du voisin) : le recrutement continue.");
        }

        [Test]
        public void DecideAndAct_ChoosesLeastDemandingUnownedNeighbor()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, NeighborEmpireId);
            StarSystemState expensive = MakeSystem(1, new Vector2(1f, 0f), StarSystemState.UnownedOwnerId, developmentLevel: 5);
            StarSystemState cheap = MakeSystem(2, new Vector2(-1f, 0f), StarSystemState.UnownedOwnerId, developmentLevel: 0);
            expensive.Population = 4000; // exigence 6
            cheap.Population = 0;        // exigence 1
            var map = new GalaxyMap(
                new[] { home, expensive, cheap },
                new[] { new HyperlaneLink(home.Id, expensive.Id), new HyperlaneLink(home.Id, cheap.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            military.RestoreGarrison(home.Id, NeighborEmpireId, new UnitBundle(infantry: 3));
            Empire pacifist = _empireRegistry.GetEmpire(NeighborEmpireId);

            MilitaryDecisionMaker.DecideAndAct(pacifist, map, economy, military, _diplomacy);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(1)));

            Assert.AreEqual(NeighborEmpireId, cheap.OwnerId, "L'IA doit viser le voisin le moins exigeant.");
            Assert.AreEqual(StarSystemState.UnownedOwnerId, expensive.OwnerId);
        }

        [Test]
        public void DecideAndAct_AtWarWithoutInfantry_RecruitsInfantryFirst()
        {
            // Le Militariste recrute normalement l'unite la plus puissante : sans cette regle
            // (Phase 16) il n'aurait jamais d'Infanterie, donc ne pourrait jamais occuper un
            // systeme conquis.
            StarSystemState home = MakeSystem(0, Vector2.zero, AiEmpireId, developmentLevel: 5);
            StarSystemState enemy = MakeSystem(1, new Vector2(1f, 0f), NeighborEmpireId);
            var map = new GalaxyMap(new[] { home, enemy }, new[] { new HyperlaneLink(home.Id, enemy.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, power: 10f, creditsCost: 20f, mineralsCost: 0f);
            UnitTypeDefinition battleship = MakeUnitType(UnitType.Battleship, power: 90f, creditsCost: 30f, mineralsCost: 0f);
            MilitaryService military = MakeMilitary(map, economy, infantry, battleship);
            GiveResources(economy, AiEmpireId, 1000f);
            military.RestoreGarrison(home.Id, AiEmpireId, new UnitBundle(battleship: 2)); // aucune Infanterie
            _diplomacy.SetStatus(AiEmpireId, NeighborEmpireId, DiplomaticStatus.War);
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(infantry.RecruitmentDays)));

            Assert.AreEqual(
                1, military.GetGarrison(home.Id, AiEmpireId).Infantry,
                "En guerre sans Infanterie, le Militariste doit en recruter malgre sa preference pour la puissance.");
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
            GiveResources(economy, NeighborEmpireId, 1000f);
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
            GiveResources(economy, AiEmpireId, 1000f);
            GiveResources(economy, NeighborEmpireId, 1000f);
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
            // garde l'Infanterie en priorite (elle apparait en premier dans UnitTypes.All) et
            // envoie le reste, quels que soient les types presents — mais depuis la Phase 16 la
            // reserve ne prend jamais la derniere Infanterie, sinon la force d'attaque ne
            // pourrait occuper aucun systeme conquis.
            StarSystemState home = MakeSystem(0, Vector2.zero, AiEmpireId);
            StarSystemState enemy = MakeSystem(1, new Vector2(1f, 0f), NeighborEmpireId);
            var map = new GalaxyMap(new[] { home, enemy }, new[] { new HyperlaneLink(home.Id, enemy.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveResources(economy, AiEmpireId, 1000f);
            GiveResources(economy, NeighborEmpireId, 1000f);
            // Garnison mixte restauree directement (au-dela du plafond de recrutement de 10, Phase 14).
            // Total (9) >= TargetGarrisonSize du Militariste (8) : le recrutement ne doit pas primer sur l'attaque.
            military.RestoreGarrison(home.Id, AiEmpireId, new UnitBundle(infantry: 2, fighter: 4, cruiser: 3));
            RecruitAndComplete(military, enemy, infantry, 1);
            _diplomacy.SetStatus(AiEmpireId, NeighborEmpireId, DiplomaticStatus.War);
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);

            Assert.IsTrue(military.TryGetStationedFleet(home.Id, AiEmpireId, out Fleet remainingGarrison));
            Assert.AreEqual(
                1, remainingGarrison.Composition.Infantry,
                "Une seule Infanterie reste en reserve : la seconde part avec la force d'attaque pour pouvoir occuper.");
            Assert.AreEqual(
                1, remainingGarrison.Composition.Fighter,
                "La reserve de 2 unites est completee par le type suivant dans UnitTypes.All.");
            Assert.AreEqual(0, remainingGarrison.Composition.Cruiser, "Les unites les plus fortes partent toutes a l'attaque.");
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
            GiveResources(economy, AiEmpireId, 1000f);
            GiveResources(economy, NeighborEmpireId, 1000f);
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
            GiveResources(economy, AiEmpireId, 1000f);
            // Garnison encore sous la cible du Militariste (8) : le recrutement doit primer.
            RecruitAndComplete(military, home, infantry, 1);
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);

            Assert.AreEqual(StarSystemState.UnownedOwnerId, unowned.OwnerId, "Pas de colonisation ce tour : le recrutement a eu lieu.");
            Assert.AreEqual(NeighborEmpireId, enemy.OwnerId, "Pas d'attaque ce tour non plus.");
        }

        // --- Multi-systeme et longue distance (Phase 18) ---------------------------------

        [Test]
        public void DecideAndAct_RecruitsOnTheSystemFurthestBelowItsTarget()
        {
            // Capitale (developpement 3, garnison pleine) et colonie vide. Avant la Phase 18,
            // l'IA recrutait toujours sur le premier systeme trouve et la colonie restait
            // eternellement sans garnison.
            StarSystemState capital = MakeSystem(0, Vector2.zero, AiEmpireId, developmentLevel: 3);
            StarSystemState colony = MakeSystem(1, new Vector2(1f, 0f), AiEmpireId, developmentLevel: 0);
            var map = new GalaxyMap(new[] { capital, colony }, new[] { new HyperlaneLink(capital.Id, colony.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveResources(economy, AiEmpireId, 1000f);
            military.RestoreGarrison(capital.Id, AiEmpireId, new UnitBundle(infantry: 8));
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);
            _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(infantry.RecruitmentDays)));

            Assert.AreEqual(1, military.GetGarrison(colony.Id, AiEmpireId).TotalCount, "La colonie vide doit etre renforcee en priorite.");
            Assert.AreEqual(8, military.GetGarrison(capital.Id, AiEmpireId).TotalCount, "La capitale etait deja a sa cible.");
        }

        [Test]
        public void DecideAndAct_ColonizesBeyondDirectNeighbours()
        {
            // home - relais(libre) - cible(libre) : la cible est a deux sauts. Avant la Phase 18
            // l'IA ne regardait que ses voisins directs et n'aurait vise que le relais.
            StarSystemState home = MakeSystem(0, Vector2.zero, AiEmpireId);
            // Relais tres developpe (exigence 4) et cible vierge (exigence 1) : l'exigence
            // passe avant la distance, donc la cible lointaine doit l'emporter.
            StarSystemState relay = MakeSystem(1, new Vector2(1f, 0f), StarSystemState.UnownedOwnerId, developmentLevel: 5);
            StarSystemState target = MakeSystem(2, new Vector2(2f, 0f), StarSystemState.UnownedOwnerId, developmentLevel: 0);
            var map = new GalaxyMap(
                new[] { home, relay, target },
                new[] { new HyperlaneLink(home.Id, relay.Id), new HyperlaneLink(relay.Id, target.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveResources(economy, AiEmpireId, 2000f);
            military.RestoreGarrison(home.Id, AiEmpireId, new UnitBundle(infantry: 10));
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);

            IReadOnlyList<Fleet> inTransit = military.GetFleetsInTransit();
            Assert.AreEqual(1, inTransit.Count, "Une flotte de colonisation doit etre partie.");
            Assert.AreEqual(3, inTransit[0].Route.Count, "L'itineraire doit compter les deux etapes, relais inclus.");
        }

        [Test]
        public void DecideAndAct_WaypointIsNeverColonized()
        {
            // Le relais traverse en chemin doit rester libre : seule la destination finale
            // declenche une resolution d'arrivee (regle etablie en Phase 17).
            StarSystemState home = MakeSystem(0, Vector2.zero, AiEmpireId);
            // Relais tres developpe (exigence 4) et cible vierge (exigence 1) : l'exigence
            // passe avant la distance, donc la cible lointaine doit l'emporter.
            StarSystemState relay = MakeSystem(1, new Vector2(1f, 0f), StarSystemState.UnownedOwnerId, developmentLevel: 5);
            StarSystemState target = MakeSystem(2, new Vector2(2f, 0f), StarSystemState.UnownedOwnerId, developmentLevel: 0);
            var map = new GalaxyMap(
                new[] { home, relay, target },
                new[] { new HyperlaneLink(home.Id, relay.Id), new HyperlaneLink(relay.Id, target.Id) });
            EconomyService economy = MakeEconomy(map);
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 100f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveResources(economy, AiEmpireId, 2000f);
            military.RestoreGarrison(home.Id, AiEmpireId, new UnitBundle(infantry: 10));
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);
            for (int day = 0; day < 10; day++)
            {
                _eventBus.Publish(new DayAdvancedEvent(_clock.CurrentDate.AddDays(day + 1)));
            }

            Assert.AreEqual(StarSystemState.UnownedOwnerId, relay.OwnerId, "Un point de passage n'est jamais colonise.");
            Assert.AreEqual(AiEmpireId, target.OwnerId, "La destination finale, elle, est bien colonisee.");
        }

        [Test]
        public void DecideAndAct_FleetCapReached_DetachesNothing()
        {
            // Le defaut le plus couteux corrige par la Phase 18 : TryDetachFleet n'a pas
            // d'inverse, donc detacher puis se voir refuser le depart laissait une flotte
            // orpheline stationnee a cote de la garnison, qui se fragmentait chaque mois.
            StarSystemState home = MakeSystem(0, Vector2.zero, AiEmpireId);
            StarSystemState freeA = MakeSystem(1, new Vector2(1f, 0f), StarSystemState.UnownedOwnerId);
            StarSystemState freeB = MakeSystem(2, new Vector2(0f, 1f), StarSystemState.UnownedOwnerId);
            StarSystemState freeC = MakeSystem(3, new Vector2(-1f, 0f), StarSystemState.UnownedOwnerId);
            var map = new GalaxyMap(
                new[] { home, freeA, freeB, freeC },
                new[]
                {
                    new HyperlaneLink(home.Id, freeA.Id),
                    new HyperlaneLink(home.Id, freeB.Id),
                    new HyperlaneLink(home.Id, freeC.Id),
                });
            EconomyService economy = MakeEconomy(map);
            // Vitesse tres faible : les flottes envoyees restent en vol pendant tout le test.
            UnitTypeDefinition infantry = MakeUnitType(UnitType.Infantry, speed: 0.01f);
            MilitaryService military = MakeMilitary(map, economy, infantry);
            GiveResources(economy, AiEmpireId, 3000f);
            // Bien au-dessus de la cible du Militariste (8) : le recrutement ne doit jamais
            // primer sur la colonisation dans ce scenario, meme apres deux departs.
            military.RestoreGarrison(home.Id, AiEmpireId, new UnitBundle(infantry: 20));
            Empire militarist = _empireRegistry.GetEmpire(AiEmpireId);

            // Deux colonisations consecutives saturent le plafond de base (2 flottes).
            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);
            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);
            Assert.AreEqual(2, military.GetFleetsInTransit().Count, "Le plafond de base doit etre atteint.");

            int fleetsBefore = military.GetFleetsForEmpire(AiEmpireId).Count;
            MilitaryDecisionMaker.DecideAndAct(militarist, map, economy, military, _diplomacy);

            Assert.AreEqual(
                fleetsBefore, military.GetFleetsForEmpire(AiEmpireId).Count,
                "Plafond atteint : aucune flotte supplementaire ne doit etre detachee, meme immobile.");
        }
    }
}
