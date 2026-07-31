using System;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie le placement deterministe des systemes d'origine (farthest-point sampling).</summary>
    [TestFixture]
    public sealed class EmpirePlacementTests
    {
        private static StarSystemState MakeSystem(int id, Vector2 position)
        {
            return new StarSystemState(new StarSystemId(id), $"System{id}", position, 100, 50, 2, 1f, Array.Empty<Espace.Data.ResourceType>());
        }

        private static GalaxyMap MakeMap(params StarSystemState[] systems)
        {
            return new GalaxyMap(systems, Array.Empty<HyperlaneLink>());
        }

        [Test]
        public void ChooseHomeSystems_NullMap_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => EmpirePlacement.ChooseHomeSystems(null, 1));
        }

        [Test]
        public void ChooseHomeSystems_NegativeCount_Throws()
        {
            GalaxyMap map = MakeMap(MakeSystem(0, Vector2.zero));

            Assert.Throws<ArgumentOutOfRangeException>(() => EmpirePlacement.ChooseHomeSystems(map, -1));
        }

        [Test]
        public void ChooseHomeSystems_CountExceedsSystemCount_Throws()
        {
            GalaxyMap map = MakeMap(MakeSystem(0, Vector2.zero));

            Assert.Throws<ArgumentOutOfRangeException>(() => EmpirePlacement.ChooseHomeSystems(map, 2));
        }

        [Test]
        public void ChooseHomeSystems_ZeroCount_ReturnsEmpty()
        {
            GalaxyMap map = MakeMap(MakeSystem(0, Vector2.zero));

            StarSystemId[] result = EmpirePlacement.ChooseHomeSystems(map, 0);

            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void ChooseHomeSystems_FirstResult_IsClosestToCenter()
        {
            StarSystemState near = MakeSystem(0, new Vector2(1f, 0f));
            StarSystemState mid = MakeSystem(1, new Vector2(10f, 0f));
            StarSystemState far = MakeSystem(2, new Vector2(50f, 0f));
            GalaxyMap map = MakeMap(far, mid, near); // ordre volontairement different de la distance

            StarSystemId[] result = EmpirePlacement.ChooseHomeSystems(map, 1);

            Assert.AreEqual(near.Id, result[0]);
        }

        [Test]
        public void ChooseHomeSystems_ReturnsDistinctSystems()
        {
            var systems = new StarSystemState[10];
            for (int i = 0; i < 10; i++)
            {
                systems[i] = MakeSystem(i, new Vector2(i * 5f, 0f));
            }
            GalaxyMap map = MakeMap(systems);

            StarSystemId[] result = EmpirePlacement.ChooseHomeSystems(map, 6);

            Assert.AreEqual(6, result.Length);
            CollectionAssert.AllItemsAreUnique(result);
        }

        [Test]
        public void ChooseHomeSystems_SecondResult_IsFarthestFromFirst()
        {
            // Trois systemes alignes : 0 au centre, 1 proche, 2 loin. Le premier choisi est
            // le plus proche du centre (id 0, en 0,0). Le second doit maximiser sa distance
            // au premier : ce doit etre id 2 (le plus eloigne), pas id 1.
            StarSystemState center = MakeSystem(0, Vector2.zero);
            StarSystemState near = MakeSystem(1, new Vector2(5f, 0f));
            StarSystemState far = MakeSystem(2, new Vector2(40f, 0f));
            GalaxyMap map = MakeMap(center, near, far);

            StarSystemId[] result = EmpirePlacement.ChooseHomeSystems(map, 2);

            Assert.AreEqual(center.Id, result[0]);
            Assert.AreEqual(far.Id, result[1]);
        }

        [Test]
        public void ChooseHomeSystems_IsDeterministic()
        {
            var systems = new StarSystemState[12];
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2f / 12f;
                systems[i] = MakeSystem(i, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (10f + i));
            }
            GalaxyMap map = MakeMap(systems);

            StarSystemId[] resultA = EmpirePlacement.ChooseHomeSystems(map, 6);
            StarSystemId[] resultB = EmpirePlacement.ChooseHomeSystems(map, 6);

            CollectionAssert.AreEqual(resultA, resultB);
        }

        // --- AssignHomeSystems (Phase 13) ---

        private static StarSystemId[] MakeSlots(params int[] ids)
        {
            var slots = new StarSystemId[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                slots[i] = new StarSystemId(ids[i]);
            }
            return slots;
        }

        [Test]
        public void AssignHomeSystems_NullCandidateSlots_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => EmpirePlacement.AssignHomeSystems(null, 0));
        }

        [Test]
        public void AssignHomeSystems_NullPreferredIndex_ReturnsSlotsUnchanged()
        {
            StarSystemId[] slots = MakeSlots(10, 20, 30);

            StarSystemId[] result = EmpirePlacement.AssignHomeSystems(slots, null);

            CollectionAssert.AreEqual(slots, result);
        }

        [Test]
        public void AssignHomeSystems_IndexOutOfRange_ReturnsSlotsUnchanged()
        {
            StarSystemId[] slots = MakeSlots(10, 20, 30);

            CollectionAssert.AreEqual(slots, EmpirePlacement.AssignHomeSystems(slots, -1));
            CollectionAssert.AreEqual(slots, EmpirePlacement.AssignHomeSystems(slots, 3));
        }

        [Test]
        public void AssignHomeSystems_ValidIndex_MovesChosenSlotToFront()
        {
            StarSystemId[] slots = MakeSlots(10, 20, 30, 40);

            StarSystemId[] result = EmpirePlacement.AssignHomeSystems(slots, 2);

            Assert.AreEqual(new StarSystemId(30), result[0]);
        }

        [Test]
        public void AssignHomeSystems_ValidIndex_KeepsRelativeOrderOfTheRest()
        {
            StarSystemId[] slots = MakeSlots(10, 20, 30, 40);

            StarSystemId[] result = EmpirePlacement.AssignHomeSystems(slots, 2);

            CollectionAssert.AreEqual(MakeSlots(30, 10, 20, 40), result);
        }

        [Test]
        public void AssignHomeSystems_ValidIndexZero_IsEquivalentToUnchanged()
        {
            StarSystemId[] slots = MakeSlots(10, 20, 30);

            StarSystemId[] result = EmpirePlacement.AssignHomeSystems(slots, 0);

            CollectionAssert.AreEqual(slots, result);
        }

        [Test]
        public void AssignHomeSystems_OutputLength_AlwaysMatchesInput()
        {
            StarSystemId[] slots = MakeSlots(10, 20, 30, 40, 50, 60);

            for (int i = 0; i < slots.Length; i++)
            {
                Assert.AreEqual(slots.Length, EmpirePlacement.AssignHomeSystems(slots, i).Length);
            }
        }
    }
}
