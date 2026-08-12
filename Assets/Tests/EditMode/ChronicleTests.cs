using System;
using Espace.Core;
using Espace.Gameplay.Chronicle;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie le journal et les regles de tri des avis (Phase 24, etape 1).
    /// <para>
    /// L'enjeu d'un systeme de notifications n'est pas de notifier : c'est de <b>ne pas
    /// noyer</b>. Un fil ou tout remonte ne vaut pas mieux qu'un fil vide, puisque dans les deux
    /// cas le joueur cesse de le lire. Ces tests protegent donc surtout les <i>refus</i> — ce
    /// qui n'a pas le droit de reclamer l'attention.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ChronicleTests
    {
        private static readonly NoticeKind[] AllKinds = (NoticeKind[])Enum.GetValues(typeof(NoticeKind));

        private static GameNotice Notice(NoticeKind kind, int day = 1)
        {
            return new GameNotice(kind, kind.ToString(), GameDate.StartOfGame.AddDays(day), new StarSystemId(1));
        }

        // --- La regle publiee ---------------------------------------------------

        [Test]
        public void EveryDemandingNotice_LeadsSomewhere()
        {
            // La regle qui fait tenir le systeme : un avis n'a le droit de reclamer l'attention
            // que s'il mene quelque part. Sans ce test, la liste des avis importants gonflerait
            // a chaque fonctionnalite ajoutee, et le compteur cesserait de vouloir dire quelque
            // chose. C'est un test de conception, pas de code.
            foreach (NoticeKind kind in AllKinds)
            {
                if (NoticeRules.TierOf(kind) >= NoticeTier.Important)
                {
                    Assert.IsTrue(
                        NoticeRules.HasDestination(kind),
                        $"{kind} reclame l'attention sans mener nulle part : elle doit redescendre au journal.");
                }
            }
        }

        [Test]
        public void CriticalNotices_StayVeryFew()
        {
            // Trois au maximum. Un quatrieme type critique devrait etre une decision
            // deliberee, pas un ajout de plus.
            int critical = 0;
            foreach (NoticeKind kind in AllKinds)
            {
                if (NoticeRules.TierOf(kind) == NoticeTier.Critical)
                {
                    critical++;
                }
            }

            Assert.LessOrEqual(critical, 3, "Trop d'avis critiques : plus rien n'est critique.");
        }

        [Test]
        public void LosingSomething_IsCriticalWhileGainingIsNot()
        {
            // Une perte est irreversible et appelle une reaction ; un gain se constate plus tard
            // sans rien couter. L'asymetrie est voulue.
            Assert.AreEqual(NoticeTier.Critical, NoticeRules.TierOf(NoticeKind.BattleLost));
            Assert.AreEqual(NoticeTier.Critical, NoticeRules.TierOf(NoticeKind.SystemLost));

            Assert.Less(
                (int)NoticeRules.TierOf(NoticeKind.BattleWon), (int)NoticeRules.TierOf(NoticeKind.BattleLost),
                "Une victoire ne doit jamais reclamer autant d'attention qu'une defaite.");
            Assert.Less(
                (int)NoticeRules.TierOf(NoticeKind.SystemTaken), (int)NoticeRules.TierOf(NoticeKind.SystemLost),
                "Prendre un systeme se constate plus tard ; en perdre un appelle une reaction.");
        }

        [Test]
        public void RoutineEvents_NeverDemandAttention()
        {
            // Un depart de flotte est deja visible sur la carte, un recrutement est fini quand on
            // l'apprend, un changement de domaine vient du joueur lui-meme.
            Assert.AreEqual(NoticeTier.Information, NoticeRules.TierOf(NoticeKind.FleetDeparted));
            Assert.AreEqual(NoticeTier.Information, NoticeRules.TierOf(NoticeKind.RecruitmentCompleted));
            Assert.AreEqual(NoticeTier.Information, NoticeRules.TierOf(NoticeKind.ResearchDomainChanged));
            Assert.AreEqual(NoticeTier.Information, NoticeRules.TierOf(NoticeKind.ForeignDiplomacy));
        }

        // --- Le journal ---------------------------------------------------------

        [Test]
        public void Log_KeepsTheNewestFirst()
        {
            var log = new NoticeLog();
            log.Add(Notice(NoticeKind.BattleWon, day: 1));
            log.Add(Notice(NoticeKind.SystemColonized, day: 2));

            Assert.AreEqual(NoticeKind.SystemColonized, log.Notices[0].Kind);
            Assert.AreEqual(NoticeKind.BattleWon, log.Notices[1].Kind);
        }

        [Test]
        public void Log_ForgetsTheOldestBeyondCapacity()
        {
            var log = new NoticeLog();
            for (int i = 0; i < NoticeLog.Capacity + 15; i++)
            {
                log.Add(Notice(NoticeKind.FleetDeparted, day: i + 1));
            }

            Assert.AreEqual(NoticeLog.Capacity, log.Notices.Count);
        }

        [Test]
        public void UnreadCount_IgnoresInformation()
        {
            // Le compteur ne doit signaler que ce qui merite un detour. S'il montait a chaque
            // depart de flotte, il afficherait un grand nombre en permanence et ne dirait plus
            // rien.
            var log = new NoticeLog();
            log.Add(Notice(NoticeKind.FleetDeparted));
            log.Add(Notice(NoticeKind.RecruitmentCompleted));

            Assert.AreEqual(0, log.UnreadCount);

            log.Add(Notice(NoticeKind.ProposalReceived));

            Assert.AreEqual(1, log.UnreadCount);
        }

        [Test]
        public void UnreadCritical_IsFlaggedSeparately()
        {
            var log = new NoticeLog();
            log.Add(Notice(NoticeKind.BuildingCompleted));

            Assert.IsFalse(log.HasUnreadCritical);

            log.Add(Notice(NoticeKind.SystemLost));

            Assert.IsTrue(log.HasUnreadCritical);
        }

        [Test]
        public void MarkAllRead_ClearsBothSignals()
        {
            var log = new NoticeLog();
            log.Add(Notice(NoticeKind.SystemLost));
            log.MarkAllRead();

            Assert.AreEqual(0, log.UnreadCount);
            Assert.IsFalse(log.HasUnreadCritical);
        }

        [Test]
        public void MarkAllRead_KeepsTheHistory()
        {
            // Lire n'est pas effacer : le journal reste consultable, c'est tout son interet.
            var log = new NoticeLog();
            log.Add(Notice(NoticeKind.BattleWon));
            log.MarkAllRead();

            Assert.AreEqual(1, log.Notices.Count);
        }

        [Test]
        public void MostRecent_FiltersByTierAndCount()
        {
            var log = new NoticeLog();
            log.Add(Notice(NoticeKind.FleetDeparted));
            log.Add(Notice(NoticeKind.ProposalReceived));
            log.Add(Notice(NoticeKind.FleetDeparted));
            log.Add(Notice(NoticeKind.SystemLost));

            Assert.AreEqual(2, log.MostRecent(10, NoticeTier.Important).Count);
            Assert.AreEqual(1, log.MostRecent(1, NoticeTier.Important).Count);
            Assert.AreEqual(4, log.MostRecent(10).Count);
        }

        [Test]
        public void Clear_EmptiesEverything()
        {
            var log = new NoticeLog();
            log.Add(Notice(NoticeKind.SystemLost));
            log.Clear();

            Assert.AreEqual(0, log.Notices.Count);
            Assert.AreEqual(0, log.UnreadCount);
            Assert.IsFalse(log.HasUnreadCritical);
        }

        [Test]
        public void Notice_DerivesItsTierFromItsKind()
        {
            // Le niveau n'est jamais passe a la main : il se deduit, donc il ne peut pas
            // diverger des regles.
            foreach (NoticeKind kind in AllKinds)
            {
                Assert.AreEqual(NoticeRules.TierOf(kind), Notice(kind).Tier);
            }
        }
    }
}
