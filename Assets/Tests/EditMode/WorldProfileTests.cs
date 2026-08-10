using Espace.Data;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie le portrait des mondes candidats (Phase 21.4).
    /// <para>
    /// Ces notes servent a decider : un joueur qui voit « Metaux ★★★★☆ » y croit. Une
    /// derivation fausse ne se manifesterait par aucun symptome — ni plantage, ni affichage
    /// bizarre —, seulement par un choix pris sur une information mensongere.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class WorldProfileTests
    {
        private static StarSystemState NewSystem(
            int id = 1,
            int population = 500,
            int development = 2,
            Vector2? position = null,
            params ResourceType[] deposits)
        {
            return new StarSystemState(
                new StarSystemId(id),
                $"Systeme {id}",
                position ?? new Vector2(10f, 0f),
                population,
                wealth: 100,
                developmentLevel: development,
                stability: 0.5f,
                resourceDeposits: deposits ?? System.Array.Empty<ResourceType>());
        }

        [Test]
        public void NullSystem_YieldsAnInertProfileRatherThanThrowing()
        {
            // L'ecran doit pouvoir se dessiner meme si le placement n'a rien renvoye.
            WorldProfile profile = WorldProfile.Describe(null, 0);

            Assert.AreEqual(PlanetKind.Barren, profile.Kind);
            Assert.AreEqual(0, profile.Metals);
        }

        [Test]
        public void Deposit_RaisesTheMatchingRating()
        {
            WorldProfile withDeposit = WorldProfile.Describe(NewSystem(deposits: ResourceType.Minerals), 3);
            WorldProfile without = WorldProfile.Describe(NewSystem(), 3);

            Assert.Greater(withDeposit.Metals, without.Metals, "Un gisement doit se voir dans la note.");
        }

        [Test]
        public void Deposit_DoesNotRaiseTheOtherRatings()
        {
            // Un monde riche en minerai ne doit pas paraitre riche en tout.
            WorldProfile profile = WorldProfile.Describe(NewSystem(deposits: ResourceType.Minerals), 3);

            Assert.Greater(profile.Metals, profile.Energy);
            Assert.Greater(profile.Metals, profile.Food);
        }

        [Test]
        public void Development_AddsOnlyATopUp()
        {
            // Un monde peuple ne fait pas apparaitre du minerai : sans gisement, la note reste
            // basse quel que soit le developpement.
            WorldProfile undeveloped = WorldProfile.Describe(NewSystem(development: 0), 3);
            WorldProfile developed = WorldProfile.Describe(NewSystem(development: 5), 3);

            Assert.GreaterOrEqual(developed.Metals, undeveloped.Metals);
            Assert.Less(developed.Metals, WorldProfile.MaximumRating, "Sans gisement, la note ne doit jamais atteindre le maximum.");
        }

        [Test]
        public void Ratings_StayWithinTheirScale()
        {
            WorldProfile profile = WorldProfile.Describe(
                NewSystem(development: 99, deposits: new[] { ResourceType.Minerals, ResourceType.Energy, ResourceType.Food }),
                8);

            foreach (int rating in new[] { profile.Metals, profile.Energy, profile.Food })
            {
                Assert.GreaterOrEqual(rating, 0);
                Assert.LessOrEqual(rating, WorldProfile.MaximumRating);
            }
        }

        [Test]
        public void Difficulty_RisesWithExposure()
        {
            Assert.AreEqual(WorldDifficulty.Facile, WorldProfile.Describe(NewSystem(), 1).Difficulty);
            Assert.AreEqual(WorldDifficulty.Moyenne, WorldProfile.Describe(NewSystem(), 3).Difficulty);
            Assert.AreEqual(WorldDifficulty.Difficile, WorldProfile.Describe(NewSystem(), 5).Difficulty);
        }

        [Test]
        public void Kind_IsStableForAGivenSystem()
        {
            // Le monde doit garder le meme visage d'une ouverture de l'ecran a l'autre.
            StarSystemState system = NewSystem(id: 42, deposits: ResourceType.Food);

            Assert.AreEqual(
                WorldProfile.Describe(system, 3).Kind,
                WorldProfile.Describe(system, 3).Kind);
        }

        [Test]
        public void Kind_FollowsTheResourceProfile()
        {
            WorldProfile fertile = WorldProfile.Describe(NewSystem(development: 4, deposits: new[] { ResourceType.Food, ResourceType.Energy }), 3);
            Assert.AreEqual(PlanetKind.Terran, fertile.Kind, "Nourriture et energie abondantes : un monde tempere.");

            WorldProfile mineral = WorldProfile.Describe(NewSystem(development: 4, deposits: ResourceType.Minerals), 3);
            Assert.AreEqual(PlanetKind.Arid, mineral.Kind, "Beaucoup de metal, peu de nourriture : un desert.");

            WorldProfile empty = WorldProfile.Describe(NewSystem(population: 0, development: 0), 3);
            Assert.AreEqual(PlanetKind.Barren, empty.Kind, "Ni population ni developpement : un caillou.");
        }

        [Test]
        public void TwoOtherwiseIdenticalWorlds_StillDifferInAppearance()
        {
            // Depart sur l'identifiant : sans lui, tous les mondes moyens se ressembleraient.
            var a = WorldProfile.Describe(NewSystem(id: 2, development: 1), 3);
            var b = WorldProfile.Describe(NewSystem(id: 3, development: 1), 3);

            Assert.AreNotEqual(a.Kind, b.Kind);
        }

        [Test]
        public void Sector_FollowsTheAngleAroundTheCore()
        {
            string east = WorldProfile.Describe(NewSystem(position: new Vector2(10f, 0f)), 3).SectorName;
            string west = WorldProfile.Describe(NewSystem(position: new Vector2(-10f, 0f)), 3).SectorName;

            Assert.AreNotEqual(east, west, "Deux systemes opposes doivent tomber dans deux secteurs distincts.");
            Assert.IsTrue(east.Length > 0, "Un secteur doit toujours porter un nom.");
        }

        [Test]
        public void Sector_AtTheExactCentreDoesNotThrow()
        {
            Assert.DoesNotThrow(() => WorldProfile.Describe(NewSystem(position: Vector2.zero), 3));
        }

        [Test]
        public void NeighbourCount_IsCarriedThrough()
        {
            Assert.AreEqual(4, WorldProfile.Describe(NewSystem(), 4).NeighbourCount);
        }
    }
}
