using System.Collections.Generic;
using Espace.Core;
using Espace.Data;
using Espace.Gameplay.Decisions;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie le systeme de decisions (Phase 24, etape 5).
    /// <para>
    /// <b>Le test qui compte est celui de la regle absolue :</b> aucune option gratuite. Une
    /// option sans cout est la bonne reponse, et le dilemme disparait — sans que rien ne plante
    /// ni ne se voie. C'est exactement le genre de regression qu'une relecture laisse passer et
    /// qu'un test attrape, surtout le jour ou quelqu'un voudra « adoucir » une decision jugee
    /// trop dure.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class DecisionTests
    {
        private static StarSystemState MakeSystem(int id, float stability, int ownerId = 0)
        {
            var system = new StarSystemState(
                new StarSystemId(id), $"Systeme{id}", Vector2.zero,
                population: 100, wealth: 10, developmentLevel: 2,
                stability: stability, resourceDeposits: new ResourceType[0]);

            system.OwnerId = ownerId;
            return system;
        }

        private static PendingDecision Unrest(float stability = 0.2f)
        {
            return DecisionCatalogue.Unrest(1, MakeSystem(1, stability), GameDate.StartOfGame);
        }

        // --- La regle absolue ----------------------------------------------------

        [Test]
        public void NoOptionIsEverFree()
        {
            // Chaque option doit couter maintenant ET plus tard. Si l'une des deux moities
            // manque, le joueur a une reponse evidente et il n'y a plus de choix a faire.
            foreach (DecisionOption option in Unrest().Options)
            {
                Assert.IsTrue(
                    option.HasImmediateCost,
                    $"« {option.Label} » ne coute rien tout de suite : c'est la bonne reponse, donc il n'y a plus de dilemme.");

                Assert.IsTrue(
                    option.HasDeferredConsequence,
                    $"« {option.Label} » ne laisse aucune ardoise : le choix se reduit a comparer trois prix.");

                Assert.IsTrue(option.IsHonest);
            }
        }

        [Test]
        public void ADecisionNeverOffersMoreThanThreeOptions()
        {
            // Au-dela de trois, on ne compare plus, on balaie.
            Assert.LessOrEqual(Unrest().Options.Count, PendingDecision.MaximumOptions);
            Assert.Greater(Unrest().Options.Count, 1, "Une seule option n'est pas une decision.");
        }

        [Test]
        public void EveryOption_ExplainsBothHalvesOfItsPrice()
        {
            // Les deux couts sont montres avant le choix : c'est la difference entre un dilemme
            // et un piege.
            foreach (DecisionOption option in Unrest().Options)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(option.Label));
                Assert.IsFalse(string.IsNullOrWhiteSpace(option.ImmediateText), $"« {option.Label} » ne dit pas ce qu'elle coute.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(option.DeferredText), $"« {option.Label} » ne dit pas ce qu'elle coutera.");
            }
        }

        [Test]
        public void EveryOption_SchedulesItsBillRatherThanLettingItFallAtOnce()
        {
            foreach (DecisionOption option in Unrest().Options)
            {
                Assert.Greater(
                    option.DeferredDelayDays, 0,
                    $"« {option.Label} » ferait tomber son ardoise le jour meme : ce n'est plus un differe.");
            }
        }

        [Test]
        public void TheOptionsAreGenuinelyDifferentFromOneAnother()
        {
            // Trois options qui couteraient la meme chose seraient trois fois la meme option.
            var seen = new List<string>();

            foreach (DecisionOption option in Unrest().Options)
            {
                CollectionAssert.DoesNotContain(seen, option.Label);
                seen.Add(option.Label);
            }

            Assert.AreEqual(3, seen.Count);
        }

        [Test]
        public void NoSingleCurrencyIsEnoughToRankTheOptions()
        {
            // La propriete qui fait tout l'interet du dilemme : les trois options ne se paient
            // pas dans la meme monnaie. Reprimer coute des hommes, ceder coute des Credits,
            // temporiser coute de la stabilite. Si l'une d'elles etait la moins chere sur
            // *toutes* les monnaies a la fois, le choix serait resolu d'avance et tout ce
            // systeme serait decoratif.
            IReadOnlyList<DecisionOption> options = Unrest().Options;

            foreach (DecisionOption option in options)
            {
                bool beatenSomewhere = false;

                foreach (DecisionOption other in options)
                {
                    if (other.Label == option.Label)
                    {
                        continue;
                    }

                    bool cheaperInCredits = other.ImmediateCredits + other.DeferredCredits
                                            < option.ImmediateCredits + option.DeferredCredits;
                    bool cheaperInMen = other.ImmediateGarrisonFraction + other.DeferredGarrisonFraction
                                        < option.ImmediateGarrisonFraction + option.DeferredGarrisonFraction;

                    if (cheaperInCredits || cheaperInMen)
                    {
                        beatenSomewhere = true;
                        break;
                    }
                }

                Assert.IsTrue(
                    beatenSomewhere,
                    $"« {option.Label} » n'est battue sur aucune monnaie : elle domine, et le dilemme disparait.");
            }
        }

        // --- Les seuils ----------------------------------------------------------

        [Test]
        public void TheQuestionArrivesAfterTheWarning_NeverBefore()
        {
            // Le panneau Empire signale un systeme des CriticalStability ; la decision n'arrive
            // qu'a UnrestThreshold. L'inverse ferait tomber une question sans qu'aucun
            // avertissement ne l'ait precedee.
            Assert.Less(
                DecisionCatalogue.UnrestThreshold, EmpireAssessment.CriticalStability,
                "Une decision doit toujours etre la consequence d'un avertissement ignore.");
        }

        [Test]
        public void TheUnrestThresholdStaysWithinItsRange()
        {
            Assert.Greater(DecisionCatalogue.UnrestThreshold, 0f);
            Assert.Less(DecisionCatalogue.UnrestThreshold, 1f);
        }

        // --- Le texte pose --------------------------------------------------------

        [Test]
        public void TheQuestionNamesTheSystemItConcerns()
        {
            PendingDecision decision = DecisionCatalogue.Unrest(7, MakeSystem(4, 0.1f), GameDate.StartOfGame);

            StringAssert.Contains("Systeme4", decision.Title);
            StringAssert.Contains("Systeme4", decision.Question);
            Assert.AreEqual(7, decision.Id);
            Assert.AreEqual(new StarSystemId(4), decision.SystemId);
        }

        [Test]
        public void ADecisionWithoutItsSystem_StillReads()
        {
            // Peut arriver en restaurant une sauvegarde dont un systeme a change de main :
            // mieux vaut une question un peu vague qu'une exception au chargement.
            Assert.DoesNotThrow(() => DecisionCatalogue.Unrest(1, null, GameDate.StartOfGame));

            PendingDecision decision = DecisionCatalogue.Unrest(1, null, GameDate.StartOfGame);
            Assert.IsFalse(string.IsNullOrWhiteSpace(decision.Title));
            Assert.AreEqual(3, decision.Options.Count);
        }
    }
}
