using System.Collections.Generic;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Military;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie <see cref="EncounterRules"/> : issues proposees selon le statut diplomatique, et
    /// choix de l'IA par formule pure (aucun tirage, aucun hachage).
    /// </summary>
    [TestFixture]
    public sealed class EncounterRulesTests
    {
        private static EmpirePersonalityProfileData Profile(EmpirePersonality personality) =>
            EmpirePersonalityProfile.Get(personality);

        private static bool Offers(DiplomaticStatus status, EncounterOption option)
        {
            IReadOnlyList<EncounterOption> options = EncounterRules.AvailableOptions(status);
            foreach (EncounterOption candidate in options)
            {
                if (candidate == option)
                {
                    return true;
                }
            }

            return false;
        }

        // --- Issues proposees ---------------------------------------------------------

        [Test]
        public void AvailableOptions_War_OffersFightWithdrawNegotiate()
        {
            Assert.IsTrue(Offers(DiplomaticStatus.War, EncounterOption.Fight));
            Assert.IsTrue(Offers(DiplomaticStatus.War, EncounterOption.Withdraw));
            Assert.IsTrue(Offers(DiplomaticStatus.War, EncounterOption.Negotiate));
            Assert.IsFalse(Offers(DiplomaticStatus.War, EncounterOption.Trade), "On ne commerce pas au milieu d'une guerre.");
        }

        [Test]
        public void AvailableOptions_Peace_OffersPiracyButNotFight()
        {
            Assert.IsTrue(Offers(DiplomaticStatus.Peace, EncounterOption.Piracy));
            Assert.IsTrue(Offers(DiplomaticStatus.Peace, EncounterOption.Trade));
            Assert.IsTrue(Offers(DiplomaticStatus.Peace, EncounterOption.PassBy));
            Assert.IsFalse(Offers(DiplomaticStatus.Peace, EncounterOption.Fight), "Attaquer en paix passerait par une declaration de guerre.");
        }

        [Test]
        public void AvailableOptions_AllianceOrPact_NeverOffersPiracy()
        {
            Assert.IsFalse(Offers(DiplomaticStatus.Alliance, EncounterOption.Piracy), "On ne detrousse pas un allie.");
            Assert.IsFalse(Offers(DiplomaticStatus.NonAggressionPact, EncounterOption.Piracy));
            Assert.IsTrue(Offers(DiplomaticStatus.Alliance, EncounterOption.Trade));
        }

        // --- Choix de l'IA -------------------------------------------------------------

        [Test]
        public void ChooseForAi_WarAndOverwhelmingAdvantage_Fights()
        {
            // Militariste : AggressionThreshold = 1.1.
            EncounterOption choice = EncounterRules.ChooseForAi(
                Profile(EmpirePersonality.Militarist), DiplomaticStatus.War,
                ownPower: 100f, otherPower: 10f, opinionOfOther: 0f, hasTradeTreaty: false);

            Assert.AreEqual(EncounterOption.Fight, choice);
        }

        [Test]
        public void ChooseForAi_WarButPacifist_NeverFights()
        {
            // Pacifiste : AggressionThreshold nul = n'engage jamais le combat de son propre chef.
            EncounterOption choice = EncounterRules.ChooseForAi(
                Profile(EmpirePersonality.Pacifist), DiplomaticStatus.War,
                ownPower: 100f, otherPower: 1f, opinionOfOther: 0f, hasTradeTreaty: false);

            Assert.AreNotEqual(EncounterOption.Fight, choice);
        }

        [Test]
        public void ChooseForAi_WarAndHopelesslyOutgunned_Withdraws()
        {
            EncounterOption choice = EncounterRules.ChooseForAi(
                Profile(EmpirePersonality.Militarist), DiplomaticStatus.War,
                ownPower: 1f, otherPower: 100f, opinionOfOther: 0f, hasTradeTreaty: false);

            Assert.AreEqual(EncounterOption.Withdraw, choice);
        }

        [Test]
        public void ChooseForAi_TradeTreaty_Trades()
        {
            EncounterOption choice = EncounterRules.ChooseForAi(
                Profile(EmpirePersonality.Mercantile), DiplomaticStatus.Peace,
                ownPower: 10f, otherPower: 10f, opinionOfOther: 0f, hasTradeTreaty: true);

            Assert.AreEqual(EncounterOption.Trade, choice);
        }

        [Test]
        public void ChooseForAi_HostileAndStrongerInPeace_Pirates()
        {
            // Militariste, opinion negative, nettement en position de force.
            EncounterOption choice = EncounterRules.ChooseForAi(
                Profile(EmpirePersonality.Militarist), DiplomaticStatus.Peace,
                ownPower: 100f, otherPower: 10f, opinionOfOther: -30f, hasTradeTreaty: false);

            Assert.AreEqual(EncounterOption.Piracy, choice);
        }

        [Test]
        public void ChooseForAi_PacifistInPeace_NeverPirates()
        {
            EncounterOption choice = EncounterRules.ChooseForAi(
                Profile(EmpirePersonality.Pacifist), DiplomaticStatus.Peace,
                ownPower: 100f, otherPower: 1f, opinionOfOther: -50f, hasTradeTreaty: false);

            Assert.AreNotEqual(EncounterOption.Piracy, choice);
        }

        [Test]
        public void ChooseForAi_UnderPactNeverPirates_EvenWhenHostileAndStronger()
        {
            EncounterOption choice = EncounterRules.ChooseForAi(
                Profile(EmpirePersonality.Militarist), DiplomaticStatus.NonAggressionPact,
                ownPower: 100f, otherPower: 10f, opinionOfOther: -30f, hasTradeTreaty: false);

            Assert.AreNotEqual(EncounterOption.Piracy, choice);
        }

        [Test]
        public void ChooseForAi_IsDeterministic()
        {
            EmpirePersonalityProfileData profile = Profile(EmpirePersonality.Opportunist);

            EncounterOption first = EncounterRules.ChooseForAi(profile, DiplomaticStatus.Peace, 50f, 20f, -10f, false);
            EncounterOption second = EncounterRules.ChooseForAi(profile, DiplomaticStatus.Peace, 50f, 20f, -10f, false);

            Assert.AreEqual(first, second, "Aucun tirage : les memes entrees donnent toujours la meme issue.");
        }

        [Test]
        public void ChooseForAi_ZeroPowerOpponent_DoesNotDivideByZero()
        {
            Assert.DoesNotThrow(() => EncounterRules.ChooseForAi(
                Profile(EmpirePersonality.Militarist), DiplomaticStatus.War, 10f, 0f, 0f, false));
        }
    }
}
