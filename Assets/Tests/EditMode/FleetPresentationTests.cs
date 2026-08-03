using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Couverture du placement des vaisseaux sur la carte (Phase 20).
    /// <para>
    /// La propriete qui compte : <b>un vaisseau ne sort jamais de son tronçon</b>. Un
    /// avancement non borne enverrait une flotte immobilisee par une rencontre spatiale
    /// au-dela de sa destination, sur une carte ou plus rien ne la ramenerait.
    /// </para>
    /// </summary>
    [TestFixture]
    public class FleetPresentationTests
    {
        private static StarSystemState MakeSystem(int id, float x, float y) =>
            new StarSystemState(new StarSystemId(id), $"S{id}", new Vector2(x, y), 100, 50, 2, 0.7f,
                System.Array.Empty<ResourceType>());

        /// <summary>Galaxie a trois systemes alignes, relies en chaine : 0 —— 1 —— 2.</summary>
        private static GalaxyMap MakeMap()
        {
            var systems = new List<StarSystemState>
            {
                MakeSystem(0, 0f, 0f),
                MakeSystem(1, 10f, 0f),
                MakeSystem(2, 10f, 10f),
            };

            var links = new List<HyperlaneLink>
            {
                new HyperlaneLink(new StarSystemId(0), new StarSystemId(1)),
                new HyperlaneLink(new StarSystemId(1), new StarSystemId(2)),
            };

            return new GalaxyMap(systems, links);
        }

        private static Fleet MakeTravellingFleet(GameDate departure, GameDate arrival)
        {
            var fleet = new Fleet(1, 0, new StarSystemId(0), UnitBundle.Of(UnitType.Infantry, 4));
            var route = new List<StarSystemId> { new StarSystemId(0), new StarSystemId(1), new StarSystemId(2) };
            fleet.BeginJourney(route, departure, arrival, isRetreating: false);
            return fleet;
        }

        // --------------------------------------------------------- avancement

        [Test]
        public void ComputeLegProgress_AtDeparture_IsZero()
        {
            Assert.AreEqual(0f, FleetPresentation.ComputeLegProgress(
                new GameDate(1, 1, 1), new GameDate(1, 1, 11), new GameDate(1, 1, 1)), 1e-4f);
        }

        [Test]
        public void ComputeLegProgress_Midway_IsHalf()
        {
            Assert.AreEqual(0.5f, FleetPresentation.ComputeLegProgress(
                new GameDate(1, 1, 1), new GameDate(1, 1, 11), new GameDate(1, 1, 6)), 1e-4f);
        }

        [Test]
        public void ComputeLegProgress_PastArrival_IsClampedToOne()
        {
            // Cas d'une rencontre spatiale : le trajet est gele, mais le calendrier avance.
            Assert.AreEqual(1f, FleetPresentation.ComputeLegProgress(
                new GameDate(1, 1, 1), new GameDate(1, 1, 11), new GameDate(1, 3, 20)), 1e-4f);
        }

        [Test]
        public void ComputeLegProgress_BeforeDeparture_IsClampedToZero()
        {
            Assert.AreEqual(0f, FleetPresentation.ComputeLegProgress(
                new GameDate(1, 2, 1), new GameDate(1, 2, 11), new GameDate(1, 1, 20)), 1e-4f);
        }

        [Test]
        public void ComputeLegProgress_ZeroLengthLeg_IsOneRatherThanDividingByZero()
        {
            var same = new GameDate(1, 1, 5);
            Assert.AreEqual(1f, FleetPresentation.ComputeLegProgress(same, same, same), 1e-4f);
        }

        // --------------------------------------------------------------- cap

        [Test]
        public void HeadingDegrees_FollowsTheDirectionOfTravel()
        {
            Assert.AreEqual(0f, FleetPresentation.HeadingDegrees(Vector2.zero, new Vector2(5f, 0f)), 1e-3f);
            Assert.AreEqual(90f, FleetPresentation.HeadingDegrees(Vector2.zero, new Vector2(0f, 5f)), 1e-3f);
            Assert.AreEqual(180f, Mathf.Abs(FleetPresentation.HeadingDegrees(Vector2.zero, new Vector2(-5f, 0f))), 1e-3f);
        }

        [Test]
        public void HeadingDegrees_IdenticalPoints_IsZeroRatherThanUndefined()
        {
            Assert.AreEqual(0f, FleetPresentation.HeadingDegrees(Vector2.one, Vector2.one), 1e-4f);
        }

        // ------------------------------------------------------------- pose

        [Test]
        public void TryComputePose_StationedFleet_DrawsNothing()
        {
            var fleet = new Fleet(1, 0, new StarSystemId(0), UnitBundle.Of(UnitType.Infantry, 3));

            Assert.IsFalse(FleetPresentation.TryComputePose(fleet, MakeMap(), new GameDate(1, 1, 1), out _),
                "Une flotte stationnee est la garnison de son systeme : elle n'a pas de vaisseau.");
        }

        [Test]
        public void TryComputePose_NullFleetOrMap_DrawsNothing()
        {
            Assert.IsFalse(FleetPresentation.TryComputePose(null, MakeMap(), new GameDate(1, 1, 1), out _));

            Fleet fleet = MakeTravellingFleet(new GameDate(1, 1, 1), new GameDate(1, 1, 6));
            Assert.IsFalse(FleetPresentation.TryComputePose(fleet, null, new GameDate(1, 1, 1), out _));
        }

        [Test]
        public void TryComputePose_MidLeg_SitsBetweenTheTwoSystems()
        {
            Fleet fleet = MakeTravellingFleet(new GameDate(1, 1, 1), new GameDate(1, 1, 11));

            Assert.IsTrue(FleetPresentation.TryComputePose(fleet, MakeMap(), new GameDate(1, 1, 6), out FleetPose pose));

            // Premiere etape : de (0,0) vers (10,0), a mi-parcours.
            Assert.AreEqual(5f, pose.Position.x, 1e-3f);
            Assert.AreEqual(0f, pose.Position.y, 1e-3f);
            Assert.AreEqual(0f, pose.HeadingDegrees, 1e-3f);
            Assert.AreEqual(0.5f, pose.LegProgress, 1e-3f);
        }

        [Test]
        public void TryComputePose_NeverLeavesItsLeg()
        {
            GalaxyMap map = MakeMap();
            Fleet fleet = MakeTravellingFleet(new GameDate(1, 1, 1), new GameDate(1, 1, 11));

            // Balaye largement au-dela des deux extremites du tronçon.
            for (int day = -40; day < 120; day++)
            {
                GameDate date = new GameDate(1, 1, 1).AddDays(day);
                Assert.IsTrue(FleetPresentation.TryComputePose(fleet, map, date, out FleetPose pose));

                Assert.GreaterOrEqual(pose.Position.x, -1e-3f, $"Vaisseau derriere son depart au jour {day}.");
                Assert.LessOrEqual(pose.Position.x, 10f + 1e-3f, $"Vaisseau au-dela de sa destination au jour {day}.");
                Assert.GreaterOrEqual(pose.LegProgress, 0f);
                Assert.LessOrEqual(pose.LegProgress, 1f);
            }
        }
    }
}
