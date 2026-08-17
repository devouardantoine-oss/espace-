using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Chronicle;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Voies;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie les quatre reponses a la courbe (Phase 24, etape 7).
    /// <para>
    /// Deux proprietes portent tout : <b>aucune voie n'est gratuite</b>, comme aucune option de
    /// decision ne l'est, et <b>la Deconcentration ne doit pas se confondre avec la
    /// Compression</b>. Deux voies qui feraient la meme chose n'en feraient qu'une, et le joueur
    /// aurait trois reponses affichees pour deux reelles.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class VoieTests
    {
        private static readonly Voie[] AllVoies = (Voie[])Enum.GetValues(typeof(Voie));

        // --- Le catalogue ---------------------------------------------------------

        [Test]
        public void EveryVoieIsDescribed()
        {
            Assert.AreEqual(AllVoies.Length, VoieCatalogue.All.Count);

            foreach (Voie voie in AllVoies)
            {
                VoieDefinition definition = VoieCatalogue.Of(voie);

                Assert.AreEqual(voie, definition.Voie, "Le catalogue doit etre indexe par la valeur de l'enum.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(definition.Name));
                Assert.IsFalse(string.IsNullOrWhiteSpace(definition.Effect));
                Assert.IsFalse(string.IsNullOrWhiteSpace(definition.History));
            }
        }

        [Test]
        public void NoVoieIsFree()
        {
            // Meme regle absolue qu'a l'etape 5 : une reponse sans contrepartie serait *la*
            // reponse, et la courbe cesserait d'etre un probleme.
            foreach (Voie voie in AllVoies)
            {
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(VoieCatalogue.Of(voie).Cost),
                    $"{voie} n'annonce aucun cout.");
            }
        }

        [Test]
        public void CoercionCostsMoreThanTheAdministrationItReplaces()
        {
            // Si tenir par la force revenait moins cher qu'administrer, la Coercition serait la
            // bonne reponse en toute circonstance. C'est un echange de ruine, pas une economie.
            Assert.Greater(
                AdministrationModel.CoercionCreditsPerInfluence, 1f,
                "La Coercition doit couter plus que ce qu'elle soulage.");

            Assert.Greater(AdministrationModel.CoercionInfluenceRelief, 0f);
            Assert.Less(
                AdministrationModel.CoercionInfluenceRelief, 1f,
                "Elle ne supprime pas l'administration, elle en deplace une part.");
        }

        [Test]
        public void TransformationIsAnnouncedButNotYetPlayable()
        {
            // Elle est presentee au joueur parce que la connaitre fait partie du recit, et
            // refusee proprement parce que le reseau d'hyperroutes n'est pas mutable.
            Assert.IsFalse(VoieCatalogue.Of(Voie.Transformation).IsPlayable);
            Assert.IsFalse(string.IsNullOrWhiteSpace(VoieCatalogue.TransformationPending));

            foreach (Voie voie in AllVoies)
            {
                if (voie != Voie.Transformation)
                {
                    Assert.IsTrue(VoieCatalogue.Of(voie).IsPlayable, $"{voie} devrait etre jouable.");
                }
            }
        }

        [Test]
        public void TheUnlockingFragmentIsTheConfession()
        {
            // Le fragment IV est celui ou l'auteur dit « j'ai essaye les quatre ». C'est le seul
            // endroit du jeu ou le recit debloque une mecanique.
            Assert.AreEqual(4, VoieCatalogue.UnlockingFragment);
        }

        // --- Le service -----------------------------------------------------------

        private sealed class FakeCodex : ICodexService
        {
            public bool FourthFound;

            public IReadOnlyList<int> UnlockedNumbers => FourthFound ? new[] { 4 } : new int[0];
            public int UnlockedCount => FourthFound ? 1 : 0;
            public bool IsUnlocked(int number) => FourthFound && number == 4;
            public void Restore(IEnumerable<int> unlockedNumbers) { }
        }

        private GalaxyMap _map;
        private VoieService _voies;
        private FakeCodex _codex;

        [SetUp]
        public void SetUp()
        {
            _codex = new FakeCodex { FourthFound = true };

            _map = new GalaxyMap(
                new List<StarSystemState> { MakeSystem(1), MakeSystem(2) },
                new List<HyperlaneLink>());

            ServiceLocator.Register<ICodexService>(_codex);

            _voies = new VoieService(_map);
            _voies.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _voies.Shutdown();
            ServiceLocator.Clear();
        }

        private static StarSystemState MakeSystem(int id)
        {
            var system = new StarSystemState(
                new StarSystemId(id), $"Systeme{id}", Vector2.zero,
                population: 400, wealth: 30, developmentLevel: 4,
                stability: 0.8f, resourceDeposits: new ResourceType[0]);

            system.OwnerId = EconomyService.PlayerOwnerId;
            return system;
        }

        [Test]
        public void NothingIsAvailableBeforeTheConfessionIsRead()
        {
            _codex.FourthFound = false;

            Assert.IsFalse(_voies.AreUnlocked);
            Assert.IsFalse(_voies.TryTake(Voie.Coercition, default(StarSystemId), out string error));
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
            Assert.IsFalse(_voies.IsCoercionActive);
        }

        [Test]
        public void Deconcentration_LeavesADevelopedWorldBehind()
        {
            // Tout le cout de cette voie est la : le monde reste une prise pour qui saura s'y
            // installer.
            Assert.IsTrue(_voies.TryTake(Voie.Deconcentration, new StarSystemId(1), out string error), error);

            StarSystemState system = _map.Systems[0];

            Assert.AreEqual(StarSystemState.UnownedOwnerId, system.OwnerId);
            Assert.AreEqual(4, system.DevelopmentLevel, "La Deconcentration ne detruit rien.");
            Assert.Greater(system.Population, 0);
        }

        [Test]
        public void Compression_LeavesNothingBehind()
        {
            Assert.IsTrue(_voies.TryTake(Voie.Compression, new StarSystemId(1), out string error), error);

            StarSystemState system = _map.Systems[0];

            Assert.AreEqual(StarSystemState.UnownedOwnerId, system.OwnerId);
            Assert.AreEqual(0, system.DevelopmentLevel);
            Assert.AreEqual(0, system.Population);
        }

        [Test]
        public void TheTwoAbandonmentVoiesAreGenuinelyDifferent()
        {
            // La propriete qui empeche d'avoir ecrit deux fois la meme voie. Les deux rendent un
            // monde a personne — et c'est la seule chose qu'elles ont en commun.
            _voies.TryTake(Voie.Deconcentration, new StarSystemId(1), out string firstError);
            _voies.TryTake(Voie.Compression, new StarSystemId(2), out string secondError);

            Assert.IsNull(firstError);
            Assert.IsNull(secondError);

            StarSystemState deconcentrated = _map.Systems[0];
            StarSystemState compressed = _map.Systems[1];

            Assert.AreEqual(deconcentrated.OwnerId, compressed.OwnerId, "Les deux rendent bien le monde a personne.");
            Assert.Greater(
                deconcentrated.DevelopmentLevel, compressed.DevelopmentLevel,
                "Si les deux laissaient le meme monde derriere elles, ce serait deux fois la meme voie.");
        }

        [Test]
        public void Coercion_TakesEffectAndCostsStabilityEverywhere()
        {
            float before = _map.Systems[0].Stability;

            Assert.IsTrue(_voies.TryTake(Voie.Coercition, default(StarSystemId), out string error), error);

            Assert.IsTrue(_voies.IsCoercionActive);
            Assert.Less(_map.Systems[0].Stability, before);
            Assert.Less(_map.Systems[1].Stability, before, "Le choc porte sur tout l'empire, pas sur un monde.");
        }

        [Test]
        public void CoercionIsNotTakenTwice()
        {
            _voies.TryTake(Voie.Coercition, default(StarSystemId), out string ignored);

            Assert.IsFalse(_voies.TryTake(Voie.Coercition, default(StarSystemId), out string error));
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
        }

        [Test]
        public void AForeignWorldIsRefused()
        {
            _map.Systems[0].OwnerId = 3;

            Assert.IsFalse(_voies.TryTake(Voie.Deconcentration, new StarSystemId(1), out string error));
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
            Assert.AreEqual(3, _map.Systems[0].OwnerId, "Un refus ne doit rien modifier.");
        }

        [Test]
        public void AnUnknownWorldIsRefusedRatherThanGuessed()
        {
            Assert.IsFalse(_voies.TryTake(Voie.Compression, new StarSystemId(999), out string error));
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
        }

        [Test]
        public void TransformationIsRefusedWithAReason()
        {
            Assert.IsFalse(_voies.TryTake(Voie.Transformation, default(StarSystemId), out string error));
            Assert.AreEqual(VoieCatalogue.TransformationPending, error);
        }

        [Test]
        public void RestoringCoercion_BringsTheRegimeBack()
        {
            // Seule voie qui dure, donc seule a etre sauvegardee.
            Assert.IsFalse(_voies.IsCoercionActive);

            _voies.RestoreCoercion(true);

            Assert.IsTrue(_voies.IsCoercionActive);
        }
    }
}
