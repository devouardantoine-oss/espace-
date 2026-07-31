using System;
using System.Reflection;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>Verifie l'attribution des identifiants d'empire par <see cref="EmpireFactory"/>.</summary>
    [TestFixture]
    public sealed class EmpireFactoryTests
    {
        /// <summary>
        /// <see cref="EmpireDefinition"/> n'expose aucun setter public (le contenu vit dans
        /// des assets, pas dans du code) : les tests construisent une instance via
        /// reflection, meme technique que pour <c>BuildingType</c> dans
        /// <c>EconomyServiceTests</c>.
        /// </summary>
        private static EmpireDefinition MakeDefinition(string name, EmpirePersonality personality, bool isPlayer)
        {
            var definition = ScriptableObject.CreateInstance<EmpireDefinition>();
            SetPrivateField(definition, "displayName", name);
            SetPrivateField(definition, "color", Color.white);
            SetPrivateField(definition, "personality", personality);
            SetPrivateField(definition, "isPlayerControlled", isPlayer);
            return definition;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Champ introuvable : {fieldName}");
            field.SetValue(target, value);
        }

        [Test]
        public void CreateEmpires_NullOrEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(() => EmpireFactory.CreateEmpires(null));
            Assert.Throws<ArgumentException>(() => EmpireFactory.CreateEmpires(Array.Empty<EmpireDefinition>()));
        }

        [Test]
        public void CreateEmpires_NullEntry_Throws()
        {
            var definitions = new[] { MakeDefinition("Joueur", EmpirePersonality.Expansionist, true), null };

            Assert.Throws<ArgumentException>(() => EmpireFactory.CreateEmpires(definitions));
        }

        [Test]
        public void CreateEmpires_PlayerDefinition_GetsPlayerOwnerId()
        {
            EmpireDefinition player = MakeDefinition("Joueur", EmpirePersonality.Expansionist, true);
            EmpireDefinition ai = MakeDefinition("IA Un", EmpirePersonality.Militarist, false);

            Empire[] empires = EmpireFactory.CreateEmpires(new[] { ai, player });

            Empire playerEmpire = Array.Find(empires, e => e.IsPlayerControlled);
            Assert.IsNotNull(playerEmpire);
            Assert.AreEqual(EconomyService.PlayerOwnerId, playerEmpire.Id);
            Assert.AreEqual("Joueur", playerEmpire.Name);
        }

        [Test]
        public void CreateEmpires_AiDefinitions_GetSequentialIdsExcludingPlayer()
        {
            EmpireDefinition player = MakeDefinition("Joueur", EmpirePersonality.Expansionist, true);
            EmpireDefinition ai1 = MakeDefinition("IA Un", EmpirePersonality.Militarist, false);
            EmpireDefinition ai2 = MakeDefinition("IA Deux", EmpirePersonality.Pacifist, false);

            Empire[] empires = EmpireFactory.CreateEmpires(new[] { player, ai1, ai2 });

            Empire empire1 = Array.Find(empires, e => e.Name == "IA Un");
            Empire empire2 = Array.Find(empires, e => e.Name == "IA Deux");

            CollectionAssert.AreEquivalent(new[] { EconomyService.PlayerOwnerId, EconomyService.PlayerOwnerId + 1, EconomyService.PlayerOwnerId + 2 },
                Array.ConvertAll(empires, e => e.Id));
            Assert.AreNotEqual(empire1.Id, empire2.Id);
        }

        [Test]
        public void CreateEmpires_NoPlayerFlagged_FallsBackToFirstDefinition()
        {
            EmpireDefinition ai1 = MakeDefinition("IA Un", EmpirePersonality.Militarist, false);
            EmpireDefinition ai2 = MakeDefinition("IA Deux", EmpirePersonality.Pacifist, false);

            Empire[] empires = EmpireFactory.CreateEmpires(new[] { ai1, ai2 });

            Empire playerEmpire = Array.Find(empires, e => e.IsPlayerControlled);
            Assert.IsNotNull(playerEmpire);
            Assert.AreEqual("IA Un", playerEmpire.Name);
            Assert.AreEqual(EconomyService.PlayerOwnerId, playerEmpire.Id);
        }

        [Test]
        public void CreateEmpires_MultiplePlayerFlagged_UsesFirstOne()
        {
            EmpireDefinition player1 = MakeDefinition("Premier", EmpirePersonality.Expansionist, true);
            EmpireDefinition player2 = MakeDefinition("Second", EmpirePersonality.Mercantile, true);

            Empire[] empires = EmpireFactory.CreateEmpires(new[] { player1, player2 });

            Empire playerEmpire = Array.Find(empires, e => e.IsPlayerControlled);
            Assert.AreEqual("Premier", playerEmpire.Name);
        }

        [Test]
        public void CreateEmpires_ReturnsSameCountAsDefinitions()
        {
            var definitions = new[]
            {
                MakeDefinition("Joueur", EmpirePersonality.Expansionist, true),
                MakeDefinition("IA Un", EmpirePersonality.Militarist, false),
                MakeDefinition("IA Deux", EmpirePersonality.Pacifist, false),
                MakeDefinition("IA Trois", EmpirePersonality.Mercantile, false),
            };

            Empire[] empires = EmpireFactory.CreateEmpires(definitions);

            Assert.AreEqual(4, empires.Length);
        }

        // --- playerDefinitionOverride (Phase 13) ---

        [Test]
        public void CreateEmpires_OverridePresent_BecomesPlayerEvenIfAnotherIsFlagged()
        {
            EmpireDefinition flaggedPlayer = MakeDefinition("Marque joueur", EmpirePersonality.Expansionist, true);
            EmpireDefinition chosenByPicker = MakeDefinition("Choisi par le joueur", EmpirePersonality.Militarist, false);

            Empire[] empires = EmpireFactory.CreateEmpires(new[] { flaggedPlayer, chosenByPicker }, chosenByPicker);

            Empire playerEmpire = Array.Find(empires, e => e.IsPlayerControlled);
            Assert.AreEqual("Choisi par le joueur", playerEmpire.Name);
            Assert.AreEqual(EconomyService.PlayerOwnerId, playerEmpire.Id);
        }

        [Test]
        public void CreateEmpires_OverrideNull_BehavesLikeBeforePhase13()
        {
            EmpireDefinition player = MakeDefinition("Joueur", EmpirePersonality.Expansionist, true);
            EmpireDefinition ai = MakeDefinition("IA Un", EmpirePersonality.Militarist, false);

            Empire[] empires = EmpireFactory.CreateEmpires(new[] { ai, player }, playerDefinitionOverride: null);

            Empire playerEmpire = Array.Find(empires, e => e.IsPlayerControlled);
            Assert.AreEqual("Joueur", playerEmpire.Name);
        }

        [Test]
        public void CreateEmpires_OverrideNotInDefinitions_FallsBackToFlaggedDefinition()
        {
            EmpireDefinition flaggedPlayer = MakeDefinition("Marque joueur", EmpirePersonality.Expansionist, true);
            EmpireDefinition ai = MakeDefinition("IA Un", EmpirePersonality.Militarist, false);
            EmpireDefinition foreignDefinition = MakeDefinition("Hors du roster", EmpirePersonality.Pacifist, false);

            Empire[] empires = EmpireFactory.CreateEmpires(new[] { flaggedPlayer, ai }, foreignDefinition);

            Empire playerEmpire = Array.Find(empires, e => e.IsPlayerControlled);
            Assert.AreEqual("Marque joueur", playerEmpire.Name);
        }
    }
}
