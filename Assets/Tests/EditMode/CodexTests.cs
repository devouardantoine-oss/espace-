using System;
using System.Collections.Generic;
using Espace.Gameplay.Chronicle;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Research;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie la table des fragments et leurs declencheurs (Phase 24, etape 3).
    /// <para>
    /// <b>Ce que ces tests protegent vraiment.</b> Un fragment mal cable ne plante pas et ne se
    /// voit pas : il devient simplement <i>indelivrable</i>, et le joueur ne saura jamais qu'il
    /// lui manque quelque chose. C'est le pire mode de defaillance possible pour du contenu
    /// narratif — silencieux, et invisible meme en jouant. D'ou une serie de tests qui verifient
    /// surtout l'<b>atteignabilite</b>.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class CodexTests
    {
        private static CodexWorldState World(
            float pressure = 0f,
            FactionLineage[] annihilated = null,
            ResearchDomain[] mastered = null)
        {
            return new CodexWorldState(
                pressure,
                annihilated ?? new FactionLineage[0],
                mastered ?? new ResearchDomain[0]);
        }

        // --- Integrite de la table ----------------------------------------------

        [Test]
        public void Fragments_AreNumberedOneToElevenWithoutAGap()
        {
            Assert.AreEqual(CodexLibrary.Count, CodexLibrary.All.Count);

            for (int i = 0; i < CodexLibrary.All.Count; i++)
            {
                Assert.AreEqual(i + 1, CodexLibrary.All[i].Number, "Les numeros doivent se suivre sans trou.");
                Assert.AreEqual(CodexLibrary.All[i].Number, CodexLibrary.ByNumber(i + 1).Number);
            }
        }

        [Test]
        public void EveryFragment_CarriesItsThreeTexts()
        {
            foreach (CodexFragment fragment in CodexLibrary.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(fragment.Numeral), $"Fragment {fragment.Number} sans chiffre romain.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(fragment.Title), $"Fragment {fragment.Number} sans titre.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(fragment.Text), $"Fragment {fragment.Number} sans texte.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(fragment.Reveal), $"Fragment {fragment.Number} sans revelation.");
            }
        }

        [Test]
        public void EveryFragment_StaysReadableOnAPhone()
        {
            // L'interface tient dans 700 x 286 unites logiques. Un texte qui demande de lire plus
            // de quarante mots d'un coup ne sera pas lu — la contrainte est posee dans la bible
            // d'univers, autant la faire respecter par un test plutot que par la vigilance.
            const int MaximumWords = 40;

            foreach (CodexFragment fragment in CodexLibrary.All)
            {
                int words = fragment.Text.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

                Assert.LessOrEqual(
                    words, MaximumWords,
                    $"Fragment {fragment.Numeral} : {words} mots, il ne sera pas lu.");
            }
        }

        // --- Atteignabilite : le vrai risque ------------------------------------

        [Test]
        public void PressureThresholds_AreStrictlyIncreasing()
        {
            // L'ordre des seuils EST la progression du recit : le fragment I doit arriver avant
            // le II, sinon le joueur lit « je » avant la note de service anonyme et tout l'effet
            // tombe.
            float previous = float.NegativeInfinity;

            foreach (CodexFragment fragment in CodexLibrary.All)
            {
                if (fragment.TriggerKind != FragmentTriggerKind.AdministrativePressure)
                {
                    continue;
                }

                Assert.Greater(
                    fragment.PressureThreshold, previous,
                    $"Le seuil du fragment {fragment.Numeral} n'est pas au-dessus du precedent.");

                previous = fragment.PressureThreshold;
            }
        }

        [Test]
        public void EveryPressureThreshold_IsBelowTheCeilingSoItCanBeReached()
        {
            // Le piege exact que ce test attrape : AdministrationModel.Pressure sature a
            // MaximumPressure, et la comparaison est stricte. Un seuil pose *a* cette valeur ne
            // serait jamais franchi, et le fragment IV — celui qui debloque les quatre voies —
            // n'arriverait jamais. Rien, en jouant, ne permettrait de s'en apercevoir.
            foreach (CodexFragment fragment in CodexLibrary.All)
            {
                if (fragment.TriggerKind != FragmentTriggerKind.AdministrativePressure)
                {
                    continue;
                }

                Assert.Less(
                    fragment.PressureThreshold, AdministrationModel.MaximumPressure,
                    $"Le seuil du fragment {fragment.Numeral} est hors d'atteinte.");
            }
        }

        [Test]
        public void ArchiveFragments_TargetFiveDistinctAndDeclaredFactions()
        {
            var seen = new List<FactionLineage>();

            foreach (CodexFragment fragment in CodexLibrary.All)
            {
                if (fragment.TriggerKind != FragmentTriggerKind.FactionAnnihilated)
                {
                    continue;
                }

                Assert.AreNotEqual(
                    FactionLineage.Unknown, fragment.Lineage,
                    $"Fragment {fragment.Numeral} : une faction inconnue ne peut jamais etre aneantie.");

                CollectionAssert.DoesNotContain(
                    seen, fragment.Lineage,
                    $"Deux fragments attendent l'aneantissement de {fragment.Lineage}.");

                seen.Add(fragment.Lineage);
            }

            Assert.AreEqual(5, seen.Count, "Cinq factions delivrent leurs archives.");
        }

        [Test]
        public void ResearchFragments_TargetDistinctDomains()
        {
            var seen = new List<ResearchDomain>();

            foreach (CodexFragment fragment in CodexLibrary.All)
            {
                if (fragment.TriggerKind != FragmentTriggerKind.ResearchMastered)
                {
                    continue;
                }

                CollectionAssert.DoesNotContain(seen, fragment.Domain);
                seen.Add(fragment.Domain);
            }

            Assert.AreEqual(2, seen.Count, "Deux domaines delivrent un fragment.");
        }

        [Test]
        public void ExactlyOneFragment_ShowsThePlayerTheirOwnLedger()
        {
            // C'est le fragment III, et le fait qu'il soit seul est tout l'effet : la lettre
            // cesse de parler d'un autre une fois, a un moment precis. Deux fragments qui
            // afficheraient les colonnes du joueur banaliseraient celui-ci.
            int withLedger = 0;

            foreach (CodexFragment fragment in CodexLibrary.All)
            {
                if (fragment.ShowsPlayerLedger)
                {
                    withLedger++;
                    Assert.AreEqual(3, fragment.Number, "Seul le fragment III montre les colonnes du joueur.");
                }
            }

            Assert.AreEqual(1, withLedger);
        }

        [Test]
        public void UnknownNumbers_AreRejectedRatherThanGuessed()
        {
            Assert.IsFalse(CodexLibrary.IsKnownNumber(0));
            Assert.IsFalse(CodexLibrary.IsKnownNumber(CodexLibrary.Count + 1));
            Assert.IsFalse(CodexLibrary.IsKnownNumber(-4));
            Assert.IsTrue(CodexLibrary.IsKnownNumber(1));
            Assert.IsTrue(CodexLibrary.IsKnownNumber(CodexLibrary.Count));
        }

        // --- Declenchement -------------------------------------------------------

        [Test]
        public void PressureFragment_ArrivesOnlyAboveItsThreshold()
        {
            CodexFragment second = CodexLibrary.ByNumber(2);

            Assert.IsFalse(second.IsDeliveredBy(World(pressure: second.PressureThreshold)),
                "Le seuil est strict : l'atteindre exactement ne suffit pas.");
            Assert.IsTrue(second.IsDeliveredBy(World(pressure: second.PressureThreshold + 0.01f)));
        }

        [Test]
        public void TheFirstFragment_NeedsAnyPressureAtAll()
        {
            // Seuil a zero : il ne doit pas arriver dans un empire qui respire, sinon il perd sa
            // fonction, qui est de coincider avec la premiere gene ressentie.
            CodexFragment first = CodexLibrary.ByNumber(1);

            Assert.IsFalse(first.IsDeliveredBy(World(pressure: 0f)));
            Assert.IsTrue(first.IsDeliveredBy(World(pressure: 0.001f)));
        }

        [Test]
        public void ArchiveFragment_NeedsItsOwnFactionGone()
        {
            CodexFragment kethra = CodexLibrary.ByNumber(9);
            Assert.AreEqual(FactionLineage.Kethra, kethra.Lineage);

            Assert.IsFalse(kethra.IsDeliveredBy(World(annihilated: new[] { FactionLineage.Oskar })),
                "Aneantir une autre faction ne livre pas ces archives-la.");
            Assert.IsTrue(kethra.IsDeliveredBy(World(annihilated: new[] { FactionLineage.Oskar, FactionLineage.Kethra })));
        }

        [Test]
        public void ResearchFragment_NeedsItsOwnDomainMastered()
        {
            CodexFragment routes = CodexLibrary.ByNumber(10);
            Assert.AreEqual(ResearchDomain.Logistics, routes.Domain);

            Assert.IsFalse(routes.IsDeliveredBy(World(mastered: new[] { ResearchDomain.Weapons })));
            Assert.IsTrue(routes.IsDeliveredBy(World(mastered: new[] { ResearchDomain.Logistics })));
        }

        [Test]
        public void AnEmptyWorld_DeliversNothing()
        {
            // Une nouvelle partie ne doit rien offrir : tout le dispositif repose sur le fait que
            // le premier fragment arrive comme une surprise, longtemps apres le debut.
            foreach (CodexFragment fragment in CodexLibrary.All)
            {
                Assert.IsFalse(
                    fragment.IsDeliveredBy(World()),
                    $"Fragment {fragment.Numeral} delivre des le premier jour.");
            }
        }

        [Test]
        public void WorldState_ToleratesNullCollections()
        {
            // CodexService passe des listes reutilisees ; un appelant plus negligent passera un
            // jour null. Mieux vaut ne rien delivrer que lever pendant le tour de jeu.
            var bare = new CodexWorldState(1f, null, null);

            Assert.DoesNotThrow(() => bare.IsAnnihilated(FactionLineage.Kethra));
            Assert.IsFalse(bare.IsAnnihilated(FactionLineage.Kethra));
            Assert.IsFalse(bare.IsMastered(ResearchDomain.Economy));
        }
    }
}
