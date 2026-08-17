using System.Collections.Generic;
using Espace.Data;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Espionage;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie les modeles purs des six panneaux (Phase 24, etape 4).
    /// <para>
    /// Trois panneaux ont recu une logique nouvelle : la liste d'attention du panneau Empire, la
    /// barre relative des flottes, la lecture de vigilance de l'espionnage. Elle vit hors des
    /// controleurs precisement pour etre verifiable ici — <c>OnGUI</c> ne se teste pas, et les
    /// trois debordements d'interface deja subis dans ce projet ont tous eu la meme cause : un
    /// calcul enferme dans un <c>MonoBehaviour</c>.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class PanelDigestTests
    {
        // --- Barre de puissance des flottes -------------------------------------

        [Test]
        public void PowerShare_FillsTheBarForTheStrongestFleet()
        {
            Assert.AreEqual(1f, FleetRoster.PowerShare(400f, 400f), 1e-4f);
        }

        [Test]
        public void PowerShare_IsProportional()
        {
            Assert.AreEqual(0.5f, FleetRoster.PowerShare(200f, 400f), 1e-4f);
        }

        [Test]
        public void ASurvivingFleet_KeepsAVisibleBar()
        {
            // Une barre vide se lit « detruite ». Une flotte affaiblie est encore une flotte :
            // elle occupe une place de deploiement, et l'oublier coute une bataille.
            float share = FleetRoster.PowerShare(1f, 100000f);

            Assert.GreaterOrEqual(share, FleetRoster.MinimumVisibleShare);
            Assert.Greater(share, 0f);
        }

        [Test]
        public void AnEmptyFleet_ShowsNothing()
        {
            Assert.AreEqual(0f, FleetRoster.PowerShare(0f, 400f), 1e-4f);
        }

        [Test]
        public void TheFirstFleetOfTheGame_DoesNotDivideByZero()
        {
            // Aucune flotte de reference : la premiere est forcement la plus forte.
            Assert.AreEqual(1f, FleetRoster.PowerShare(50f, 0f), 1e-4f);
            Assert.AreEqual(0f, FleetRoster.PowerShare(0f, 0f), 1e-4f);
        }

        [Test]
        public void MissionLabel_SeparatesWhatAFleetDoesFromWhereItIs()
        {
            Assert.AreEqual("en vol", FleetRoster.MissionLabel(FleetStatus.Moving, false));
            Assert.AreEqual("rencontre", FleetRoster.MissionLabel(FleetStatus.AwaitingEncounter, false));
            Assert.AreEqual("garnison", FleetRoster.MissionLabel(FleetStatus.Stationed, true));
            Assert.AreEqual("defense", FleetRoster.MissionLabel(FleetStatus.Stationed, false));
        }

        [Test]
        public void ACrippledFleet_IsCalledOut()
        {
            Assert.IsTrue(FleetRoster.IsCrippled(10f, 400f));
            Assert.IsFalse(FleetRoster.IsCrippled(300f, 400f));
            Assert.IsFalse(FleetRoster.IsCrippled(0f, 400f), "Une flotte inexistante n'est pas une flotte decimee.");
        }

        // --- Lecture de vigilance ------------------------------------------------

        [Test]
        public void Vigilance_ReadsAsWordsAcrossItsWholeRange()
        {
            const float Ceiling = 2f;

            Assert.AreEqual("cible tranquille", VigilanceReading.Describe(0f, Ceiling));
            Assert.AreEqual("cible sur ses gardes", VigilanceReading.Describe(1f, Ceiling));
            Assert.AreEqual("reseau grille — laisser retomber", VigilanceReading.Describe(1.8f, Ceiling));
        }

        [Test]
        public void Vigilance_ShareIsBoundedEvenBeyondTheCeiling()
        {
            Assert.AreEqual(0f, VigilanceReading.ShareOfCeiling(-1f, 2f), 1e-4f);
            Assert.AreEqual(1f, VigilanceReading.ShareOfCeiling(9f, 2f), 1e-4f);
            Assert.AreEqual(0.5f, VigilanceReading.ShareOfCeiling(1f, 2f), 1e-4f);
        }

        [Test]
        public void Vigilance_WithoutAKnownCeiling_ReadsAsCalmRatherThanCrashing()
        {
            Assert.DoesNotThrow(() => VigilanceReading.Describe(1f, 0f));
            Assert.AreEqual(0f, VigilanceReading.ShareOfCeiling(1f, 0f), 1e-4f);
        }

        [Test]
        public void Vigilance_AdvisesWaitingOnlyOnceTheNetworkIsBurned()
        {
            Assert.IsFalse(VigilanceReading.ShouldLetItCoolDown(1f, 2f));
            Assert.IsTrue(VigilanceReading.ShouldLetItCoolDown(1.8f, 2f));
        }

        [Test]
        public void Vigilance_ThresholdsStayInOrder()
        {
            // Si quelqu'un intervertit les deux seuils, « sur ses gardes » deviendrait
            // inatteignable et la phrase sauterait directement a « reseau grille ».
            Assert.Less(VigilanceReading.AlertFraction, VigilanceReading.BurnedFraction);
            Assert.Greater(VigilanceReading.AlertFraction, 0f);
            Assert.LessOrEqual(VigilanceReading.BurnedFraction, 1f);
        }

        // --- Motifs de la liste d'attention --------------------------------------

        [Test]
        public void AnUnstableSystem_ReportsItsStability()
        {
            var entry = new SystemAttention(default(Espace.Gameplay.Galaxy.StarSystemId), "Ylar Beta", SystemConcern.Unstable, 0.38f);

            StringAssert.Contains("38", entry.Describe());
        }

        [Test]
        public void AnExposedSystem_SaysWhyWithoutANumber()
        {
            var entry = new SystemAttention(default(Espace.Gameplay.Galaxy.StarSystemId), "Solel", SystemConcern.Exposed, 0.9f);

            Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Describe()));
            StringAssert.Contains("garnison", entry.Describe());
        }

        [Test]
        public void EveryConcern_HasSomethingToSay()
        {
            // Un motif ajoute sans texte donnerait une ligne vide dans le panneau : le systeme
            // apparaitrait comme demandant quelque chose, sans dire quoi.
            foreach (SystemConcern concern in (SystemConcern[])System.Enum.GetValues(typeof(SystemConcern)))
            {
                var entry = new SystemAttention(default(Espace.Gameplay.Galaxy.StarSystemId), "X", concern, 0.5f);

                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(entry.Describe()),
                    $"Le motif {concern} n'affiche rien.");
            }
        }

        [Test]
        public void TheAttentionList_ToleratesAWorldItCannotRead()
        {
            // Appele avant que la carte ne soit enregistree : il doit vider la liste, pas lever.
            var into = new List<SystemAttention>();

            Assert.DoesNotThrow(() => SystemAttentionList.Fill(into, 0, null, null, null));
            Assert.AreEqual(0, into.Count);
            Assert.DoesNotThrow(() => SystemAttentionList.Fill(null, 0, null, null, null));
        }

        /// <summary>
        /// Carte de systemes possedes, aux stabilites voulues. Militaire et diplomatie restent
        /// absents : sans eux aucun systeme n'est « expose », ce qui isole proprement la regle
        /// d'instabilite et son tri.
        /// </summary>
        private static GalaxyMap MapOwnedBy(int ownerId, params float[] stabilities)
        {
            var systems = new List<StarSystemState>();

            for (int i = 0; i < stabilities.Length; i++)
            {
                var system = new StarSystemState(
                    new StarSystemId(i + 1), $"S{i + 1}", Vector2.zero,
                    population: 100, wealth: 10, developmentLevel: 1,
                    stability: stabilities[i], resourceDeposits: new ResourceType[0]);

                system.OwnerId = ownerId;
                systems.Add(system);
            }

            return new GalaxyMap(systems, new List<HyperlaneLink>());
        }

        [Test]
        public void OnlySystemsBelowTheCriticalThreshold_AreListed()
        {
            // Le seuil est celui d'EmpireAssessment : le joueur, l'IA et le halo de la carte
            // lisent la meme limite.
            var into = new List<SystemAttention>();
            GalaxyMap map = MapOwnedBy(7, 0.90f, EmpireAssessment.CriticalStability - 0.01f, EmpireAssessment.CriticalStability);

            SystemAttentionList.Fill(into, 7, map, null, null);

            Assert.AreEqual(1, into.Count, "Seul le systeme strictement sous le seuil demande quelque chose.");
            Assert.AreEqual("S2", into[0].Name);
        }

        [Test]
        public void OtherEmpiresSystems_AreNeverListed()
        {
            var into = new List<SystemAttention>();
            GalaxyMap map = MapOwnedBy(7, 0.05f, 0.05f);

            SystemAttentionList.Fill(into, 99, map, null, null);

            Assert.AreEqual(0, into.Count, "Le panneau Empire ne parle que de l'empire du joueur.");
        }

        [Test]
        public void TheWorstSystemsComeFirst()
        {
            var into = new List<SystemAttention>();
            GalaxyMap map = MapOwnedBy(7, 0.40f, 0.10f, 0.25f);

            SystemAttentionList.Fill(into, 7, map, null, null);

            Assert.AreEqual(3, into.Count);
            Assert.AreEqual("S2", into[0].Name, "0,10 est le plus urgent.");
            Assert.AreEqual("S3", into[1].Name);
            Assert.AreEqual("S1", into[2].Name);
        }

        [Test]
        public void TheListIsCapped_AndKeepsTheWorst()
        {
            // La liste est plafonnee parce que le panneau fait 286 unites de haut. Ce qui est
            // coupe doit donc etre le moins urgent — l'inverse rendrait le plafond nuisible.
            var into = new List<SystemAttention>();
            GalaxyMap map = MapOwnedBy(7, 0.50f, 0.45f, 0.40f, 0.35f, 0.30f, 0.25f, 0.01f);

            SystemAttentionList.Fill(into, 7, map, null, null);

            Assert.AreEqual(SystemAttentionList.MaximumEntries, into.Count);
            Assert.AreEqual("S7", into[0].Name, "Le pire systeme ne doit jamais etre celui qu'on coupe.");

            foreach (SystemAttention entry in into)
            {
                Assert.AreNotEqual("S1", entry.Name, "Le moins urgent doit etre le premier sacrifie.");
            }
        }

        [Test]
        public void RefillingReplacesTheList_RatherThanGrowingIt()
        {
            // La liste est reutilisee d'un jour sur l'autre pour ne rien allouer : si Fill
            // oubliait de la vider, le panneau accumulerait les memes systemes indefiniment.
            var into = new List<SystemAttention>();
            GalaxyMap map = MapOwnedBy(7, 0.10f);

            SystemAttentionList.Fill(into, 7, map, null, null);
            SystemAttentionList.Fill(into, 7, map, null, null);

            Assert.AreEqual(1, into.Count);
        }
    }
}
