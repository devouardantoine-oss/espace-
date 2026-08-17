using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Decisions;
using Espace.Gameplay.People;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie les gouverneurs et le calcul de loyaute (Phase 24, etape 6).
    /// <para>
    /// Ce qui se joue ici n'est pas une formule mais une <b>propriete de jeu</b> : un joueur qui a
    /// mal agi une fois doit pouvoir se racheter. La memoire ne retient que trois faits, et c'est
    /// ce plafond — pas la formule — qui empeche le systeme de devenir une punition definitive.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class GovernorTests
    {
        private static readonly GovernorFactKind[] AllKinds =
            (GovernorFactKind[])Enum.GetValues(typeof(GovernorFactKind));

        private static List<GovernorFact> Facts(params GovernorFactKind[] kinds)
        {
            var facts = new List<GovernorFact>();
            foreach (GovernorFactKind kind in kinds)
            {
                facts.Add(new GovernorFact(kind, GameDate.StartOfGame));
            }

            return facts;
        }

        // --- Generation ----------------------------------------------------------

        [Test]
        public void TheSameWorld_AlwaysGetsTheSameGovernor()
        {
            // Meme garantie qu'un Amiral : aucun System.Random, donc une partie rechargee
            // retrouve les memes personnes sans qu'on ait eu a les sauvegarder.
            Governor first = Governor.Compute(42, 0);
            Governor again = Governor.Compute(42, 0);

            Assert.AreEqual(first.Name, again.Name);
            Assert.AreEqual(first.BaseLoyalty, again.BaseLoyalty, 1e-6f);
        }

        [Test]
        public void TakingAWorldFromSomeoneElse_InstallsSomeoneElse()
        {
            // Le suzerain entre dans le hachage : conquerir un monde y met un nouveau gouverneur,
            // pas celui de l'adversaire qu'on vient de chasser.
            Governor theirs = Governor.Compute(42, 3);
            Governor ours = Governor.Compute(42, 0);

            Assert.AreNotEqual(theirs.Name, ours.Name);
        }

        [Test]
        public void DifferentWorlds_GetDifferentGovernors()
        {
            var names = new List<string>();

            for (int systemId = 1; systemId <= 20; systemId++)
            {
                names.Add(Governor.Compute(systemId, 0).Name);
            }

            // Douze prenoms et douze noms donnent 144 combinaisons : quelques doublons sur vingt
            // tirages sont normaux, mais un generateur casse en produirait un seul.
            var distinct = new List<string>();
            foreach (string name in names)
            {
                if (!distinct.Contains(name))
                {
                    distinct.Add(name);
                }
            }

            Assert.Greater(distinct.Count, 10, "Les gouverneurs se ressemblent trop : le hachage ne melange plus.");
        }

        [Test]
        public void ATemperamentIsNeverExtreme()
        {
            // Ni fanatique ni traitre en puissance : c'est ce que le joueur en fait qui compte.
            for (int systemId = 1; systemId <= 60; systemId++)
            {
                float temperament = Governor.Compute(systemId, 0).BaseLoyalty;

                Assert.GreaterOrEqual(temperament, 0.5f);
                Assert.LessOrEqual(temperament, 0.7f);
                Assert.Greater(
                    temperament, LoyaltyModel.DefectionThreshold,
                    "Un gouverneur ne doit jamais arriver deja pret a partir.");
            }
        }

        // --- Memoire --------------------------------------------------------------

        [Test]
        public void OnlyTheLastThreeFactsAreKept()
        {
            var governor = new Governor("Test", 0.6f);

            governor.Remember(new GovernorFact(GovernorFactKind.Repressed, GameDate.StartOfGame));
            governor.Remember(new GovernorFact(GovernorFactKind.Conceded, GameDate.StartOfGame));
            governor.Remember(new GovernorFact(GovernorFactKind.Ignored, GameDate.StartOfGame));
            governor.Remember(new GovernorFact(GovernorFactKind.Defended, GameDate.StartOfGame));

            Assert.AreEqual(Governor.MemorySize, governor.Memory.Count);
            Assert.AreEqual(GovernorFactKind.Conceded, governor.Memory[0].Kind, "Le plus ancien doit s'effacer.");
            Assert.AreEqual(GovernorFactKind.Defended, governor.Memory[2].Kind);
        }

        [Test]
        public void APlayerCanAlwaysMakeAmends()
        {
            // La propriete la plus importante de cette etape. Trois repressions amenent le
            // gouverneur au bord du depart ; trois bonnes decisions doivent l'en ramener,
            // puisque la memoire ne retient que trois faits.
            var governor = new Governor("Test", 0.6f);

            for (int i = 0; i < 3; i++)
            {
                governor.Remember(new GovernorFact(GovernorFactKind.Repressed, GameDate.StartOfGame));
            }

            float afterCrackdowns = LoyaltyModel.Compute(governor.BaseLoyalty, governor.Memory, 0.5f);

            for (int i = 0; i < 3; i++)
            {
                governor.Remember(new GovernorFact(GovernorFactKind.Defended, GameDate.StartOfGame));
            }

            float afterMakingAmends = LoyaltyModel.Compute(governor.BaseLoyalty, governor.Memory, 0.5f);

            Assert.Greater(afterMakingAmends, afterCrackdowns, "Trois bons actes doivent effacer trois mauvais.");
            Assert.IsFalse(LoyaltyModel.WouldDefect(afterMakingAmends));
        }

        [Test]
        public void RestoringMemory_RespectsTheCap()
        {
            var governor = new Governor("Test", 0.6f);

            governor.RestoreMemory(Facts(
                GovernorFactKind.Repressed, GovernorFactKind.Conceded,
                GovernorFactKind.Ignored, GovernorFactKind.Defended,
                GovernorFactKind.Abandoned));

            Assert.AreEqual(Governor.MemorySize, governor.Memory.Count);
        }

        [Test]
        public void RestoringNothing_EmptiesTheMemory()
        {
            var governor = new Governor("Test", 0.6f);
            governor.Remember(new GovernorFact(GovernorFactKind.Repressed, GameDate.StartOfGame));

            governor.RestoreMemory(null);

            Assert.AreEqual(0, governor.Memory.Count);
        }

        [Test]
        public void EveryFactHasSomethingToSay()
        {
            // Un fait sans texte donnerait une ligne vide sur la fiche du monde.
            foreach (GovernorFactKind kind in AllKinds)
            {
                var fact = new GovernorFact(kind, GameDate.StartOfGame);

                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(fact.Describe()),
                    $"Le fait {kind} n'affiche rien.");
            }
        }

        // --- Loyaute ---------------------------------------------------------------

        [Test]
        public void RepressionCostsMoreThanAnythingElseTheGameLetsYouDo()
        {
            // Envoyer la troupe contre les siens doit rester le geste le plus couteux parmi ceux
            // que le joueur *choisit*. Perdre le monde coute davantage, mais cela ne se choisit
            // pas.
            foreach (GovernorFactKind kind in AllKinds)
            {
                if (kind == GovernorFactKind.Repressed || kind == GovernorFactKind.Abandoned)
                {
                    continue;
                }

                Assert.Greater(
                    LoyaltyModel.WeightOf(kind), LoyaltyModel.WeightOf(GovernorFactKind.Repressed),
                    $"{kind} ne doit pas couter plus cher qu'une repression.");
            }
        }

        [Test]
        public void GoodDeedsHelpAndBadOnesHurt()
        {
            Assert.Greater(LoyaltyModel.WeightOf(GovernorFactKind.Defended), 0f);
            Assert.Greater(LoyaltyModel.WeightOf(GovernorFactKind.Conceded), 0f);

            Assert.Less(LoyaltyModel.WeightOf(GovernorFactKind.Repressed), 0f);
            Assert.Less(LoyaltyModel.WeightOf(GovernorFactKind.Ignored), 0f);
            Assert.Less(LoyaltyModel.WeightOf(GovernorFactKind.Sabotaged), 0f);
            Assert.Less(LoyaltyModel.WeightOf(GovernorFactKind.Abandoned), 0f);
        }

        [Test]
        public void LoyaltyStaysBetweenZeroAndOne()
        {
            Assert.GreaterOrEqual(
                LoyaltyModel.Compute(0.6f, Facts(GovernorFactKind.Abandoned, GovernorFactKind.Abandoned, GovernorFactKind.Abandoned), 0f),
                0f);

            Assert.LessOrEqual(
                LoyaltyModel.Compute(0.7f, Facts(GovernorFactKind.Defended, GovernorFactKind.Defended, GovernorFactKind.Defended), 1f),
                1f);
        }

        [Test]
        public void AnUnstableWorld_WearsItsGovernorDown()
        {
            // On gouverne mal une population en colere, meme quand le suzerain est irreprochable.
            float calm = LoyaltyModel.Compute(0.6f, null, stability: 0.9f);
            float burning = LoyaltyModel.Compute(0.6f, null, stability: 0.1f);

            Assert.Greater(calm, burning);
        }

        [Test]
        public void StabilityAlone_NeverDrivesAGovernorOut()
        {
            // Elle tire vers elle plutot que de s'ajouter : sans faits retenus, meme un monde a
            // l'agonie ne suffit pas. Un depart doit toujours avoir une part de responsabilite
            // du joueur, sinon il est arbitraire.
            float worst = LoyaltyModel.Compute(0.50f, null, stability: 0f);

            Assert.IsFalse(
                LoyaltyModel.WouldDefect(worst),
                "Un gouverneur ne doit jamais partir sans qu'on y soit pour quelque chose.");
        }

        [Test]
        public void EnoughBadDeeds_DoDriveAGovernorOut()
        {
            float loyalty = LoyaltyModel.Compute(
                0.50f,
                Facts(GovernorFactKind.Repressed, GovernorFactKind.Repressed, GovernorFactKind.Abandoned),
                stability: 0.3f);

            Assert.IsTrue(LoyaltyModel.WouldDefect(loyalty), $"Loyaute obtenue : {loyalty}");
        }

        [Test]
        public void TheThresholdsStayInOrder()
        {
            // Si quelqu'un les intervertit, « vacille » deviendrait inatteignable et le joueur
            // ne verrait jamais venir un depart.
            Assert.Less(LoyaltyModel.DefectionThreshold, LoyaltyModel.WaveringThreshold);
            Assert.Greater(LoyaltyModel.DefectionThreshold, 0f);
            Assert.Less(LoyaltyModel.WaveringThreshold, 1f);
        }

        [Test]
        public void LoyaltyReadsAsWordsAcrossItsWholeRange()
        {
            Assert.AreEqual("sur le depart", LoyaltyModel.Describe(0.1f));
            Assert.AreEqual("vacille", LoyaltyModel.Describe(0.35f));
            Assert.AreEqual("loyal", LoyaltyModel.Describe(0.8f));
        }

        // --- Le lien avec les decisions --------------------------------------------

        [Test]
        public void EveryDecisionOption_LeavesAMemory()
        {
            // Une decision dont personne ne se souvient n'aurait aucune suite : tout l'apport de
            // cette etape est que les choix de l'etape 5 laissent une trace chez quelqu'un.
            PendingDecision decision = DecisionCatalogue.Unrest(1, null, GameDate.StartOfGame);

            foreach (DecisionOption option in decision.Options)
            {
                Assert.IsTrue(
                    option.RememberedAs.HasValue,
                    $"« {option.Label} » ne laisse aucun souvenir au gouverneur.");
            }
        }

        [Test]
        public void RepressingIsRememberedAsRepression()
        {
            PendingDecision decision = DecisionCatalogue.Unrest(1, null, GameDate.StartOfGame);

            foreach (DecisionOption option in decision.Options)
            {
                switch (option.Label)
                {
                    case "Reprimer":
                        Assert.AreEqual(GovernorFactKind.Repressed, option.RememberedAs);
                        break;
                    case "Ceder":
                        Assert.AreEqual(GovernorFactKind.Conceded, option.RememberedAs);
                        break;
                    case "Temporiser":
                        Assert.AreEqual(GovernorFactKind.Ignored, option.RememberedAs);
                        break;
                }
            }
        }
    }
}
