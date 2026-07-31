using System;
using System.Collections.Generic;
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
    /// Verifie <see cref="DiplomacyDecisionMaker"/> : propose la paix quand le rapport de
    /// force devient defavorable en guerre, declare la guerre a un voisin ecrase (personnalite
    /// agressive uniquement), propose un pacte de non-agression (ou une alliance) a un voisin
    /// apprecie, une seule action par appel.
    /// </summary>
    [TestFixture]
    public sealed class DiplomacyDecisionMakerTests
    {
        private const int PlayerId = EconomyService.PlayerOwnerId;
        private const int AiId = 1;
        private const int NeighborId = 2;

        /// <summary>Puissance controlee : chaque empire a une garnison fixe, puissance = nombre d'unites.</summary>
        private sealed class FakePowerMilitaryService : IMilitaryService
        {
            private readonly Dictionary<int, UnitBundle> _garrisonsByEmpire = new Dictionary<int, UnitBundle>();

            public void SetGarrison(int empireId, int unitCount) => _garrisonsByEmpire[empireId] = new UnitBundle(infantry: unitCount);

            public IReadOnlyList<UnitTypeDefinition> UnitCatalog => Array.Empty<UnitTypeDefinition>();
            public bool TryGetStationedFleet(StarSystemId systemId, int empireId, out Fleet fleet) { fleet = null; return false; }
            public IReadOnlyList<Fleet> GetFleetsAt(StarSystemId systemId) => Array.Empty<Fleet>();
            public IReadOnlyList<Fleet> GetFleetsForEmpire(int empireId) => Array.Empty<Fleet>();
            public UnitBundle GetGarrison(StarSystemId systemId, int empireId) => _garrisonsByEmpire.TryGetValue(empireId, out UnitBundle bundle) ? bundle : UnitBundle.Zero;
            public float EstimatePower(UnitBundle composition) => composition.TotalCount;
            public bool TryRecruitUnits(StarSystemId systemId, UnitTypeDefinition unitType, int count, out string error) { error = "n/a"; return false; }
            public bool TryMoveFleet(Fleet fleet, StarSystemId destinationSystemId, out string error) { error = "n/a"; return false; }
            public bool TryDetachFleet(StarSystemId systemId, int empireId, UnitBundle unitsToDetach, out Fleet detachedFleet, out string error) { detachedFleet = null; error = "n/a"; return false; }
            public void RestoreGarrison(StarSystemId systemId, int empireId, UnitBundle composition, string fleetName = null, Admiral? admiral = null) { }
            public bool CanDeployAnotherFleet(int empireId) => true;
            public IReadOnlyList<Fleet> GetFleetsInTransit() => Array.Empty<Fleet>();
            public void ClearFleetsInTransit() { }
            public void RestoreFleetInTransit(
                int empireId, UnitBundle composition, string fleetName, Admiral? admiral,
                IReadOnlyList<StarSystemId> route, int routeIndex, StarSystemId originSystemId,
                GameDate journeyStartDate, GameDate departureDate, GameDate legArrivalDate, bool isRetreating) { }
        }

        /// <summary>
        /// Espion controlable : contrairement a <c>MilitaryDecisionMakerTests</c> (qui reutilise
        /// le vrai <c>MilitaryService</c>, facile a preparer par recrutement), l'opinion de
        /// <see cref="DiplomacyService"/> ne peut normalement evoluer que par des actions
        /// simulees — un espion avec des setters directs est la facon la plus simple d'isoler
        /// chaque scenario de decision sans enchainer des propositions juste pour amener
        /// l'opinion a la valeur voulue.
        /// </summary>
        private sealed class SpyDiplomacyService : IDiplomacyService
        {
            private readonly Dictionary<(int, int), DiplomaticStatus> _statuses = new Dictionary<(int, int), DiplomaticStatus>();
            private readonly Dictionary<(int, int), float> _opinions = new Dictionary<(int, int), float>();

            public readonly List<(int DeclarerId, int TargetId)> DeclaredWars = new List<(int, int)>();
            public readonly List<(int ProposerId, int TargetId, ProposalType Type)> SubmittedProposals = new List<(int, int, ProposalType)>();

            private static (int, int) StatusKey(int a, int b) => a <= b ? (a, b) : (b, a);

            public void SetStatus(int empireAId, int empireBId, DiplomaticStatus status) => _statuses[StatusKey(empireAId, empireBId)] = status;
            public void SetOpinion(int observerId, int targetId, float opinion) => _opinions[(observerId, targetId)] = opinion;

            public DiplomaticStatus GetStatus(int empireAId, int empireBId) =>
                _statuses.TryGetValue(StatusKey(empireAId, empireBId), out DiplomaticStatus status) ? status : DiplomaticStatus.Peace;

            public float GetOpinion(int observerId, int targetId) => _opinions.TryGetValue((observerId, targetId), out float value) ? value : 0f;
            public bool HasTradeTreaty(int empireAId, int empireBId) => false;
            public bool IsEmbargoing(int fromEmpireId, int toEmpireId) => false;
            public IReadOnlyList<DiplomaticProposal> GetPendingProposalsFor(int empireId) => Array.Empty<DiplomaticProposal>();

            public bool TryDeclareWar(int declarerId, int targetId, out string error)
            {
                if (GetStatus(declarerId, targetId) == DiplomaticStatus.War)
                {
                    error = "Deja en guerre.";
                    return false;
                }

                DeclaredWars.Add((declarerId, targetId));
                SetStatus(declarerId, targetId, DiplomaticStatus.War);
                error = null;
                return true;
            }

            public bool TrySetEmbargo(int fromEmpireId, int toEmpireId, bool active, out string error) { error = "n/a"; return false; }

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
                SubmittedProposals.Add((proposerId, targetId, type));
                error = null;
                return true;
            }

            public bool TryRespondToProposal(int proposalId, bool accept, out string error) { error = "n/a"; return false; }
            public void ApplyOpinionShift(int observerId, int targetId, float delta) { }
            public void RestoreRelations(int empireAId, int empireBId, DiplomaticStatus status, bool hasTradeTreaty) { }
            public void RestoreOpinion(int observerId, int targetId, float value) { }
            public void RestoreEmbargo(int fromEmpireId, int toEmpireId) { }
        }

        private EmpireRegistry _empireRegistry;
        private FakePowerMilitaryService _military;
        private SpyDiplomacyService _diplomacy;

        [SetUp]
        public void SetUp()
        {
            _military = new FakePowerMilitaryService();
            _diplomacy = new SpyDiplomacyService();
            _empireRegistry = new EmpireRegistry(new[]
            {
                new Empire(PlayerId, "Joueur", Color.blue, EmpirePersonality.Expansionist, isPlayerControlled: true),
                new Empire(AiId, "IA", Color.red, EmpirePersonality.Militarist, isPlayerControlled: false),
                new Empire(NeighborId, "Voisin", Color.green, EmpirePersonality.Militarist, isPlayerControlled: false),
            });
        }

        private static StarSystemState MakeSystem(int id, Vector2 position, int ownerId)
        {
            var system = new StarSystemState(new StarSystemId(id), $"System{id}", position, 1000, 500, developmentLevel: 3, stability: 1f, resourceDeposits: Array.Empty<ResourceType>());
            system.OwnerId = ownerId;
            return system;
        }

        private Empire MakeEmpire(int id, EmpirePersonality personality) => new Empire(id, $"Empire{id}", Color.white, personality, isPlayerControlled: id == PlayerId);

        [Test]
        public void DecideAndAct_NoOwnedSystem_DoesNothing()
        {
            StarSystemState unowned = MakeSystem(0, Vector2.zero, StarSystemState.UnownedOwnerId);
            var map = new GalaxyMap(new[] { unowned }, Array.Empty<HyperlaneLink>());
            Empire ai = _empireRegistry.GetEmpire(AiId);

            Assert.DoesNotThrow(() => DiplomacyDecisionMaker.DecideAndAct(ai, map, _military, _diplomacy));
            Assert.AreEqual(0, _diplomacy.SubmittedProposals.Count);
            Assert.AreEqual(0, _diplomacy.DeclaredWars.Count);
        }

        [Test]
        public void DecideAndAct_AtWar_Overpowered_ProposesPeace()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState enemy = MakeSystem(1, new Vector2(1f, 0f), NeighborId);
            var map = new GalaxyMap(new[] { home, enemy }, new[] { new HyperlaneLink(home.Id, enemy.Id) });
            _military.SetGarrison(AiId, 1);
            _military.SetGarrison(NeighborId, 100);
            _diplomacy.SetStatus(AiId, NeighborId, DiplomaticStatus.War);
            Empire pacifist = MakeEmpire(AiId, EmpirePersonality.Pacifist); // PeacePowerRatioThreshold = 1.5

            DiplomacyDecisionMaker.DecideAndAct(pacifist, map, _military, _diplomacy);

            Assert.Contains((AiId, NeighborId, ProposalType.PeaceTreaty), _diplomacy.SubmittedProposals);
        }

        [Test]
        public void DecideAndAct_AtWar_NotOverpowered_DoesNotProposePeace()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState enemy = MakeSystem(1, new Vector2(1f, 0f), NeighborId);
            var map = new GalaxyMap(new[] { home, enemy }, new[] { new HyperlaneLink(home.Id, enemy.Id) });
            _military.SetGarrison(AiId, 100);
            _military.SetGarrison(NeighborId, 1);
            _diplomacy.SetStatus(AiId, NeighborId, DiplomaticStatus.War);
            Empire pacifist = MakeEmpire(AiId, EmpirePersonality.Pacifist);

            DiplomacyDecisionMaker.DecideAndAct(pacifist, map, _military, _diplomacy);

            Assert.AreEqual(0, _diplomacy.SubmittedProposals.Count, "Largement superieur : pas de raison de demander la paix.");
        }

        [Test]
        public void DecideAndAct_Peace_Overwhelming_DeclaresWar()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState enemy = MakeSystem(1, new Vector2(1f, 0f), NeighborId);
            var map = new GalaxyMap(new[] { home, enemy }, new[] { new HyperlaneLink(home.Id, enemy.Id) });
            _military.SetGarrison(AiId, 100);
            _military.SetGarrison(NeighborId, 1);
            Empire militarist = MakeEmpire(AiId, EmpirePersonality.Militarist); // AggressionThreshold = 1.1

            DiplomacyDecisionMaker.DecideAndAct(militarist, map, _military, _diplomacy);

            Assert.Contains((AiId, NeighborId), _diplomacy.DeclaredWars);
        }

        [Test]
        public void DecideAndAct_Peace_NotOverwhelming_DoesNotDeclareWarNorPropose()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState enemy = MakeSystem(1, new Vector2(1f, 0f), NeighborId);
            var map = new GalaxyMap(new[] { home, enemy }, new[] { new HyperlaneLink(home.Id, enemy.Id) });
            _military.SetGarrison(AiId, 10);
            _military.SetGarrison(NeighborId, 10); // force egale : sous le seuil Militariste (1.1x)
            Empire militarist = MakeEmpire(AiId, EmpirePersonality.Militarist);

            DiplomacyDecisionMaker.DecideAndAct(militarist, map, _military, _diplomacy);

            Assert.AreEqual(0, _diplomacy.DeclaredWars.Count);
            Assert.AreEqual(0, _diplomacy.SubmittedProposals.Count, "Opinion neutre (0), sous le seuil de proposition spontanee du Militariste (60).");
        }

        [Test]
        public void DecideAndAct_Pacifist_NeverDeclaresWarEvenWithOverwhelmingForce()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState enemy = MakeSystem(1, new Vector2(1f, 0f), NeighborId);
            var map = new GalaxyMap(new[] { home, enemy }, new[] { new HyperlaneLink(home.Id, enemy.Id) });
            _military.SetGarrison(AiId, 1000);
            _military.SetGarrison(NeighborId, 1);
            Empire pacifist = MakeEmpire(AiId, EmpirePersonality.Pacifist); // AggressionThreshold = null

            DiplomacyDecisionMaker.DecideAndAct(pacifist, map, _military, _diplomacy);

            Assert.AreEqual(0, _diplomacy.DeclaredWars.Count);
        }

        [Test]
        public void DecideAndAct_Peace_HighOpinion_ProposesNonAggressionPact()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState neighbor = MakeSystem(1, new Vector2(1f, 0f), NeighborId);
            var map = new GalaxyMap(new[] { home, neighbor }, new[] { new HyperlaneLink(home.Id, neighbor.Id) });
            Empire mercantile = MakeEmpire(AiId, EmpirePersonality.Mercantile); // ProactivePactOpinionThreshold = 20, AggressionThreshold = null
            _diplomacy.SetOpinion(AiId, NeighborId, 50f);

            DiplomacyDecisionMaker.DecideAndAct(mercantile, map, _military, _diplomacy);

            Assert.Contains((AiId, NeighborId, ProposalType.NonAggressionPact), _diplomacy.SubmittedProposals);
        }

        [Test]
        public void DecideAndAct_ExistingPact_VeryHighOpinion_ProposesAllianceUpgrade()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState neighbor = MakeSystem(1, new Vector2(1f, 0f), NeighborId);
            var map = new GalaxyMap(new[] { home, neighbor }, new[] { new HyperlaneLink(home.Id, neighbor.Id) });
            Empire mercantile = MakeEmpire(AiId, EmpirePersonality.Mercantile); // seuil 20 + bonus alliance 25 = 45
            _diplomacy.SetStatus(AiId, NeighborId, DiplomaticStatus.NonAggressionPact);
            _diplomacy.SetOpinion(AiId, NeighborId, 50f);

            DiplomacyDecisionMaker.DecideAndAct(mercantile, map, _military, _diplomacy);

            Assert.Contains((AiId, NeighborId, ProposalType.Alliance), _diplomacy.SubmittedProposals);
        }

        [Test]
        public void DecideAndAct_LowOpinion_DoesNotProposeAnyPact()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState neighbor = MakeSystem(1, new Vector2(1f, 0f), NeighborId);
            var map = new GalaxyMap(new[] { home, neighbor }, new[] { new HyperlaneLink(home.Id, neighbor.Id) });
            Empire mercantile = MakeEmpire(AiId, EmpirePersonality.Mercantile);
            _diplomacy.SetOpinion(AiId, NeighborId, 5f); // sous le seuil (20)

            DiplomacyDecisionMaker.DecideAndAct(mercantile, map, _military, _diplomacy);

            Assert.AreEqual(0, _diplomacy.SubmittedProposals.Count);
        }

        [Test]
        public void DecideAndAct_PeaceProposalTakesPriorityOverWarDeclaration()
        {
            StarSystemState home = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState atWarNeighbor = MakeSystem(1, new Vector2(1f, 0f), NeighborId);
            const int thirdEmpireId = 3;
            StarSystemState atPeaceNeighbor = MakeSystem(2, new Vector2(-1f, 0f), thirdEmpireId);
            var map = new GalaxyMap(
                new[] { home, atWarNeighbor, atPeaceNeighbor },
                new[] { new HyperlaneLink(home.Id, atWarNeighbor.Id), new HyperlaneLink(home.Id, atPeaceNeighbor.Id) });

            _military.SetGarrison(AiId, 1);
            _military.SetGarrison(NeighborId, 100); // en guerre, ecrasant : doit demander la paix
            _military.SetGarrison(thirdEmpireId, 1); // en paix, faible : serait autrement attaquable
            _diplomacy.SetStatus(AiId, NeighborId, DiplomaticStatus.War);
            Empire militarist = MakeEmpire(AiId, EmpirePersonality.Militarist);

            DiplomacyDecisionMaker.DecideAndAct(militarist, map, _military, _diplomacy);

            Assert.Contains((AiId, NeighborId, ProposalType.PeaceTreaty), _diplomacy.SubmittedProposals);
            Assert.AreEqual(0, _diplomacy.DeclaredWars.Count, "Une seule action par appel : la demande de paix a deja eu lieu.");
        }

        // --- Multi-systeme (Phase 18) -----------------------------------------------------

        [Test]
        public void DecideAndAct_RivalBorderingOnlyAColony_IsStillConsidered()
        {
            // capitale(0) - colonie(1) - rival(2). Avant la Phase 18, la diplomatie ne
            // regardait que les voisins d'un seul systeme : ce rival n'existait pas a ses yeux,
            // aucune guerre ni aucun pacte n'etait possible avec lui.
            StarSystemState capital = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState colony = MakeSystem(1, new Vector2(1f, 0f), AiId);
            StarSystemState rival = MakeSystem(2, new Vector2(2f, 0f), NeighborId);
            var map = new GalaxyMap(
                new[] { capital, colony, rival },
                new[] { new HyperlaneLink(capital.Id, colony.Id), new HyperlaneLink(colony.Id, rival.Id) });

            _military.SetGarrison(AiId, 100);
            _military.SetGarrison(NeighborId, 1);
            Empire militarist = MakeEmpire(AiId, EmpirePersonality.Militarist);

            DiplomacyDecisionMaker.DecideAndAct(militarist, map, _military, _diplomacy);

            Assert.Contains((AiId, NeighborId), _diplomacy.DeclaredWars);
        }

        [Test]
        public void DecideAndAct_PowerIsSummedOverEveryOwnedSystem()
        {
            // La doublure renvoie la meme garnison pour chaque systeme d'un empire : l'IA a
            // deux systemes de 6 unites (total 12), le rival un seul de 10. Le Militariste
            // exige 1.1x, soit 11 : la guerre n'est declarable qu'en sommant les deux systemes.
            StarSystemState capital = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState colony = MakeSystem(1, new Vector2(1f, 0f), AiId);
            StarSystemState rival = MakeSystem(2, new Vector2(2f, 0f), NeighborId);
            var map = new GalaxyMap(
                new[] { capital, colony, rival },
                new[] { new HyperlaneLink(capital.Id, colony.Id), new HyperlaneLink(colony.Id, rival.Id) });

            _military.SetGarrison(AiId, 6);
            _military.SetGarrison(NeighborId, 10);
            Empire militarist = MakeEmpire(AiId, EmpirePersonality.Militarist);

            DiplomacyDecisionMaker.DecideAndAct(militarist, map, _military, _diplomacy);

            Assert.Contains((AiId, NeighborId), _diplomacy.DeclaredWars,
                "12 (deux systemes) contre 10 x 1.1 = 11 : la guerre passe. 6 seul ne l'aurait pas permis.");
        }

        [Test]
        public void DecideAndAct_NoNeighboringEmpire_DoesNothing()
        {
            StarSystemState capital = MakeSystem(0, Vector2.zero, AiId);
            StarSystemState free = MakeSystem(1, new Vector2(1f, 0f), StarSystemState.UnownedOwnerId);
            var map = new GalaxyMap(new[] { capital, free }, new[] { new HyperlaneLink(capital.Id, free.Id) });

            _military.SetGarrison(AiId, 100);
            Empire militarist = MakeEmpire(AiId, EmpirePersonality.Militarist);

            DiplomacyDecisionMaker.DecideAndAct(militarist, map, _military, _diplomacy);

            Assert.AreEqual(0, _diplomacy.DeclaredWars.Count);
            Assert.AreEqual(0, _diplomacy.SubmittedProposals.Count);
        }
    }
}
