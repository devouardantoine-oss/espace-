using System;
using System.Collections.Generic;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Espionage;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="EspionageDecisionMaker"/> : seuil par personnalite, mission
    /// preferee, aucune restriction d'adjacence, une seule mission par appel.
    /// </summary>
    [TestFixture]
    public sealed class EspionageDecisionMakerTests
    {
        private const int PlayerId = 0;
        private const int AiId = 1;
        private const int NeighborId = 2;
        private const int FarEmpireId = 3;

        /// <summary>
        /// Espion controlable : contrairement au vrai <see cref="EspionageService"/> (qui
        /// exigerait un GalaxyMap, une economie et resoudrait ses effets via ServiceLocator),
        /// ce faux isole la logique de decision (quel adversaire, quel seuil, quelle mission)
        /// de la mecanique de resolution deja verifiee dans <c>EspionageServiceTests</c>.
        /// </summary>
        private sealed class SpyEspionageService : IEspionageService
        {
            private readonly Dictionary<int, float> _powers = new Dictionary<int, float>();
            private readonly Dictionary<int, float> _counterPowers = new Dictionary<int, float>();

            public readonly List<(int ProposerId, int TargetId, EspionageMissionType Type)> Attempts = new List<(int, int, EspionageMissionType)>();

            public void SetPower(int empireId, float power) => _powers[empireId] = power;
            public void SetCounterPower(int empireId, float power) => _counterPowers[empireId] = power;

            public float GetEspionagePower(int empireId) => _powers.TryGetValue(empireId, out float power) ? power : 10f;
            public float GetCounterEspionagePower(int empireId, StarSystemId referenceSystemId) => _counterPowers.TryGetValue(empireId, out float power) ? power : 10f;

            public bool TryStealTechnology(int proposerId, int targetEmpireId, out string error)
            {
                Attempts.Add((proposerId, targetEmpireId, EspionageMissionType.StealTechnology));
                error = null;
                return true;
            }

            public bool TrySabotage(int proposerId, StarSystemId targetSystemId, out string error)
            {
                Attempts.Add((proposerId, OwnerOf(targetSystemId), EspionageMissionType.Sabotage));
                error = null;
                return true;
            }

            public bool TryInciteRevolt(int proposerId, StarSystemId targetSystemId, out string error)
            {
                Attempts.Add((proposerId, OwnerOf(targetSystemId), EspionageMissionType.IncitesRevolt));
                error = null;
                return true;
            }

            public bool TryInfluenceGovernment(int proposerId, int targetEmpireId, out string error)
            {
                Attempts.Add((proposerId, targetEmpireId, EspionageMissionType.InfluenceGovernment));
                error = null;
                return true;
            }

            public bool TryDiscoverArmies(int proposerId, int targetEmpireId, StarSystemId targetSystemId, out Espace.Gameplay.Military.UnitBundle discoveredGarrison, out string error)
            {
                Attempts.Add((proposerId, targetEmpireId, EspionageMissionType.DiscoverArmies));
                discoveredGarrison = Espace.Gameplay.Military.UnitBundle.Zero;
                error = null;
                return true;
            }

            // Les systemes de ces tests portent le meme identifiant numerique que leur proprietaire (voir MakeSystem) : raccourci pour retrouver l'empire cible depuis un StarSystemId.
            private static int OwnerOf(StarSystemId systemId) => systemId.Value;
        }

        private static StarSystemState MakeSystem(int ownerId, Vector2 position)
        {
            var system = new StarSystemState(new StarSystemId(ownerId), $"System{ownerId}", position, 1000, 500, 3, 1f, Array.Empty<Espace.Data.ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        private static Empire MakeEmpire(int id, EmpirePersonality personality) => new Empire(id, $"Empire{id}", Color.white, personality, isPlayerControlled: id == PlayerId);

        private static EmpireRegistry MakeRegistry(params Empire[] empires) => new EmpireRegistry(empires);

        [Test]
        public void DecideAndAct_PacifistNeverSpies_DoesNothing()
        {
            var home = MakeSystem(AiId, Vector2.zero);
            var neighbor = MakeSystem(NeighborId, new Vector2(1f, 0f));
            var map = new GalaxyMap(new[] { home, neighbor }, Array.Empty<HyperlaneLink>());
            var espionage = new SpyEspionageService();
            EmpireRegistry registry = MakeRegistry(MakeEmpire(PlayerId, EmpirePersonality.Expansionist), MakeEmpire(AiId, EmpirePersonality.Pacifist), MakeEmpire(NeighborId, EmpirePersonality.Militarist));

            EspionageDecisionMaker.DecideAndAct(registry.GetEmpire(AiId), registry, map, espionage);

            Assert.AreEqual(0, espionage.Attempts.Count);
        }

        [Test]
        public void DecideAndAct_InsufficientPower_DoesNothing()
        {
            var home = MakeSystem(AiId, Vector2.zero);
            var neighbor = MakeSystem(NeighborId, new Vector2(1f, 0f));
            var map = new GalaxyMap(new[] { home, neighbor }, Array.Empty<HyperlaneLink>());
            var espionage = new SpyEspionageService();
            espionage.SetPower(AiId, 10f);
            espionage.SetCounterPower(NeighborId, 100f); // tres largement superieur au seuil du Militariste (1.2x)
            EmpireRegistry registry = MakeRegistry(MakeEmpire(PlayerId, EmpirePersonality.Expansionist), MakeEmpire(AiId, EmpirePersonality.Militarist), MakeEmpire(NeighborId, EmpirePersonality.Pacifist));

            EspionageDecisionMaker.DecideAndAct(registry.GetEmpire(AiId), registry, map, espionage);

            Assert.AreEqual(0, espionage.Attempts.Count);
        }

        [Test]
        public void DecideAndAct_SufficientPower_ExecutesPreferredMission()
        {
            var home = MakeSystem(AiId, Vector2.zero);
            var neighbor = MakeSystem(NeighborId, new Vector2(1f, 0f));
            var map = new GalaxyMap(new[] { home, neighbor }, Array.Empty<HyperlaneLink>());
            var espionage = new SpyEspionageService();
            espionage.SetPower(AiId, 100f);
            espionage.SetCounterPower(NeighborId, 10f); // Militariste : DiscoverArmies, seuil 1.2x
            EmpireRegistry registry = MakeRegistry(MakeEmpire(PlayerId, EmpirePersonality.Expansionist), MakeEmpire(AiId, EmpirePersonality.Militarist), MakeEmpire(NeighborId, EmpirePersonality.Pacifist));

            EspionageDecisionMaker.DecideAndAct(registry.GetEmpire(AiId), registry, map, espionage);

            Assert.AreEqual(1, espionage.Attempts.Count);
            Assert.AreEqual((AiId, NeighborId, EspionageMissionType.DiscoverArmies), espionage.Attempts[0]);
        }

        [Test]
        public void DecideAndAct_DifferentPersonalities_UseTheirOwnPreferredMission()
        {
            var home = MakeSystem(AiId, Vector2.zero);
            var neighbor = MakeSystem(NeighborId, new Vector2(1f, 0f));
            var map = new GalaxyMap(new[] { home, neighbor }, Array.Empty<HyperlaneLink>());
            var espionage = new SpyEspionageService();
            espionage.SetPower(AiId, 100f);
            espionage.SetCounterPower(NeighborId, 10f);
            EmpireRegistry registry = MakeRegistry(MakeEmpire(PlayerId, EmpirePersonality.Expansionist), MakeEmpire(AiId, EmpirePersonality.Opportunist), MakeEmpire(NeighborId, EmpirePersonality.Pacifist));

            EspionageDecisionMaker.DecideAndAct(registry.GetEmpire(AiId), registry, map, espionage);

            Assert.AreEqual((AiId, NeighborId, EspionageMissionType.InfluenceGovernment), espionage.Attempts[0]);
        }

        [Test]
        public void DecideAndAct_TargetOwnsNoSystem_SkipsToNextTarget()
        {
            var home = MakeSystem(AiId, Vector2.zero);
            var reachableEnemy = MakeSystem(FarEmpireId, new Vector2(2f, 0f));
            var map = new GalaxyMap(new[] { home, reachableEnemy }, Array.Empty<HyperlaneLink>());
            var espionage = new SpyEspionageService();
            espionage.SetPower(AiId, 100f);
            espionage.SetCounterPower(FarEmpireId, 10f);
            // NeighborId n'a aucun systeme sur cette carte : doit etre ignore, pas planter.
            EmpireRegistry registry = MakeRegistry(
                MakeEmpire(PlayerId, EmpirePersonality.Expansionist),
                MakeEmpire(AiId, EmpirePersonality.Militarist),
                MakeEmpire(NeighborId, EmpirePersonality.Pacifist),
                MakeEmpire(FarEmpireId, EmpirePersonality.Pacifist));

            Assert.DoesNotThrow(() => EspionageDecisionMaker.DecideAndAct(registry.GetEmpire(AiId), registry, map, espionage));

            Assert.AreEqual(1, espionage.Attempts.Count);
            Assert.AreEqual(FarEmpireId, espionage.Attempts[0].TargetId);
        }

        [Test]
        public void DecideAndAct_NoOwnedSystemAtAll_TargetsNoOneWithoutThrowing()
        {
            var home = MakeSystem(AiId, Vector2.zero);
            var map = new GalaxyMap(new[] { home }, Array.Empty<HyperlaneLink>());
            var espionage = new SpyEspionageService();
            EmpireRegistry registry = MakeRegistry(MakeEmpire(PlayerId, EmpirePersonality.Expansionist), MakeEmpire(AiId, EmpirePersonality.Militarist));

            Assert.DoesNotThrow(() => EspionageDecisionMaker.DecideAndAct(registry.GetEmpire(AiId), registry, map, espionage));
            Assert.AreEqual(0, espionage.Attempts.Count);
        }
    }
}
