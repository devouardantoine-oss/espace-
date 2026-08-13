using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie l'encodage diegetique de l'etat d'un systeme (Phase 23, tranche A).
    /// <para>
    /// Le point le plus important n'est pas le rendu, c'est la <b>regle de confidentialite</b> :
    /// un systeme adverse ne doit rien laisser filtrer de son interieur. Une regression sur ce
    /// point ne planterait pas et ne se verrait pas — elle donnerait simplement au joueur une
    /// information que l'espionnage devait lui faire meriter.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class SystemGlyphTests
    {
        private const float Tolerance = 1e-4f;

        // --- Confidentialite ---------------------------------------------------

        [Test]
        public void ForeignSystem_RevealsNothingOfItsInterior()
        {
            SystemGlyph glyph = SystemGlyph.For(developmentLevel: 3, stability: 0.1f, garrisonCount: 9, ownedByViewer: false);

            Assert.IsFalse(glyph.RevealsInternalState);
            Assert.AreEqual(0f, glyph.HaloOpacity, Tolerance, "Un halo visible trahirait la stabilite adverse.");
            Assert.AreEqual(0, glyph.GarrisonPips, "La garnison adverse ne se lit que par l'espionnage.");
            Assert.IsFalse(glyph.GarrisonExceedsPips);
        }

        [Test]
        public void ForeignSystem_StillShowsItsDevelopment()
        {
            // Le developpement est de l'infrastructure visible depuis l'orbite, et la carte le
            // montrait deja par la teinte du marqueur depuis la Phase 2. Le masquer serait une
            // regression, pas une precaution.
            SystemGlyph glyph = SystemGlyph.For(SystemGlyph.FullyDevelopedLevel, 0.9f, 7, ownedByViewer: false);

            Assert.AreEqual(1, glyph.DevelopmentRings);
        }

        [Test]
        public void OwnedSystem_RevealsStabilityAndGarrison()
        {
            SystemGlyph glyph = SystemGlyph.For(2, 0.9f, 3, ownedByViewer: true);

            Assert.IsTrue(glyph.RevealsInternalState);
            Assert.Greater(glyph.HaloOpacity, 0f);
            Assert.AreEqual(3, glyph.GarrisonPips);
        }

        // --- Anneaux de developpement ------------------------------------------

        [Test]
        public void DevelopmentRing_AppearsOnlyOnAFullyDevelopedWorld()
        {
            // La regle tient en une phrase : l'anneau ne dit pas « ou en est ce monde », il dit
            // « ce monde est fini ». Un anneau par niveau rendait cent systemes illisibles a
            // distance de vue d'ensemble ; celui-ci reste denombrable parce qu'il est binaire.
            for (int level = 0; level < SystemGlyph.FullyDevelopedLevel; level++)
            {
                Assert.AreEqual(0, SystemGlyph.For(level, 1f, 0, true).DevelopmentRings,
                    "Un monde encore en chantier ne porte pas d'anneau.");
            }

            Assert.AreEqual(1, SystemGlyph.For(SystemGlyph.FullyDevelopedLevel, 1f, 0, true).DevelopmentRings);
        }

        [Test]
        public void DevelopmentRing_ToleratesLevelsOutsideTheExpectedRange()
        {
            Assert.AreEqual(0, SystemGlyph.For(-2, 1f, 0, true).DevelopmentRings);
            Assert.AreEqual(
                SystemGlyph.MaximumDevelopmentRings,
                SystemGlyph.For(99, 1f, 0, true).DevelopmentRings,
                "Un niveau aberrant ne doit pas faire apparaitre un second anneau.");
        }

        // --- Pastilles de garnison ---------------------------------------------

        [Test]
        public void GarrisonPips_SignalOverflowRatherThanGrowingForever()
        {
            SystemGlyph glyph = SystemGlyph.For(3, 1f, SystemGlyph.MaximumGarrisonPips + 4, true);

            Assert.AreEqual(SystemGlyph.MaximumGarrisonPips, glyph.GarrisonPips);
            Assert.IsTrue(glyph.GarrisonExceedsPips, "Au-dela du plafond, il faut le dire — sinon 5 et 20 se ressemblent.");
        }

        [Test]
        public void GarrisonPips_ExactlyAtTheCap_IsNotOverflow()
        {
            SystemGlyph glyph = SystemGlyph.For(3, 1f, SystemGlyph.MaximumGarrisonPips, true);

            Assert.AreEqual(SystemGlyph.MaximumGarrisonPips, glyph.GarrisonPips);
            Assert.IsFalse(glyph.GarrisonExceedsPips);
        }

        [Test]
        public void GarrisonPips_NegativeCountIsTreatedAsEmpty()
        {
            Assert.AreEqual(0, SystemGlyph.For(3, 1f, -1, true).GarrisonPips);
        }

        // --- Halo de stabilite --------------------------------------------------

        [Test]
        public void Halo_GetsLouderAsStabilityFalls()
        {
            // La propriete qui fait tout l'interet du halo : un systeme sain reste discret, un
            // systeme en difficulte s'impose. Sans elle, la carte souligne uniformement tout ce
            // qu'elle connait et n'aide plus a reperer quoi que ce soit.
            Assert.Greater(
                SystemGlyph.HaloOpacityFor(0.1f),
                SystemGlyph.HaloOpacityFor(0.9f));
        }

        [Test]
        public void Halo_OpacityStaysWithinItsDeclaredBounds()
        {
            for (int i = 0; i <= 20; i++)
            {
                float opacity = SystemGlyph.HaloOpacityFor(i / 20f);
                Assert.GreaterOrEqual(opacity, SystemGlyph.CalmHaloOpacity - Tolerance);
                Assert.LessOrEqual(opacity, SystemGlyph.TroubledHaloOpacity + Tolerance);
            }
        }

        [Test]
        public void Halo_TurnsAmberExactlyAtTheThresholdTheAiUses()
        {
            // Le seuil n'est pas un nombre invente pour le rendu : c'est celui qui fait basculer
            // un empire IA en consolidation. Le joueur et la machine lisent la meme limite. Si
            // quelqu'un deplace l'un sans l'autre, ce test le signale.
            AssertSameColor(SystemGlyph.StrainedHalo, SystemGlyph.HaloColorFor(EmpireAssessment.CriticalStability));
        }

        [Test]
        public void Halo_ReachesItsExtremesAtTheExtremes()
        {
            AssertSameColor(SystemGlyph.CalmHalo, SystemGlyph.HaloColorFor(1f));
            AssertSameColor(SystemGlyph.TroubledHalo, SystemGlyph.HaloColorFor(0f));
        }

        [Test]
        public void Halo_MovesContinuouslyAcrossTheThreshold()
        {
            // Deux teintes proches de part et d'autre du seuil doivent rester proches : une
            // discontinuite ferait clignoter la carte quand une stabilite oscille autour de la
            // limite.
            Color justBelow = SystemGlyph.HaloColorFor(EmpireAssessment.CriticalStability - 0.01f);
            Color justAbove = SystemGlyph.HaloColorFor(EmpireAssessment.CriticalStability + 0.01f);

            Assert.Less(Mathf.Abs(justBelow.r - justAbove.r), 0.05f);
            Assert.Less(Mathf.Abs(justBelow.g - justAbove.g), 0.05f);
            Assert.Less(Mathf.Abs(justBelow.b - justAbove.b), 0.05f);
        }

        [Test]
        public void Halo_ToleratesStabilityOutsideZeroOne()
        {
            Assert.DoesNotThrow(() => SystemGlyph.HaloColorFor(-3f));
            Assert.DoesNotThrow(() => SystemGlyph.HaloColorFor(4f));
            AssertSameColor(SystemGlyph.CalmHalo, SystemGlyph.HaloColorFor(4f));
        }

        // --- Invariants de geometrie -------------------------------------------
        //
        // Ces tests existent parce que la premiere version de la geometrie etait fausse et que
        // rien ne l'a signale : les cinq anneaux se chevauchaient et les pastilles de garnison
        // tombaient dessus. Aucun test de comportement ne pouvait le voir — il a fallu dessiner
        // la carte en ASCII pour s'en apercevoir. Les regles de non-chevauchement sont donc
        // ecrites ici, une bonne fois.

        [Test]
        public void Layout_TheDevelopmentRingNeverCrossesTheDecorativeRing()
        {
            // Les deux familles d'anneaux ne doivent jamais se croiser, sinon on ne sait plus
            // laquelle lire. Le decoratif (Phase 12) reste a l'interieur.
            Assert.Greater(SystemGlyph.DevelopmentRingScale, SystemGlyph.DecorativeRingScale);
        }

        [Test]
        public void Layout_TheDevelopmentRingClearsTheDecorativeRing()
        {
            // Le trait fin fait 8 % du rayon, soit 0,08 d'echelle de part et d'autre. Deux
            // anneaux plus proches que le double de cette epaisseur fusionneraient en une bande
            // unique — c'etait le defaut de la premiere version, qui empilait cinq anneaux avec
            // un trait epais de 40 %.
            const float ThinStrokeScale = 0.16f;

            Assert.Greater(
                SystemGlyph.DevelopmentRingScale - SystemGlyph.DecorativeRingScale, ThinStrokeScale * 2f,
                "L'anneau de developpement doit rester distinct de l'anneau decoratif.");
        }

        [Test]
        public void Layout_HaloEnclosesTheDevelopmentRing()
        {
            // Le halo est le fond du glyphe : un anneau qui en depasserait flotterait dans le
            // vide, detache du systeme.
            Assert.Greater(SystemGlyph.HaloScale, SystemGlyph.DevelopmentRingScale);
        }

        [Test]
        public void Layout_GarrisonPipsStayInLowOrbit()
        {
            // Les pastilles doivent tenir entre le corps et l'anneau. Les echelles sont en
            // diametres, les orbites en rayons : d'ou la division.
            float bodyRadius = 0.5f;
            float pipInner = SystemGlyph.GarrisonPipOrbit - SystemGlyph.GarrisonPipScale * 0.5f;
            float pipOuter = SystemGlyph.GarrisonPipOrbit + SystemGlyph.GarrisonPipScale * 0.5f;
            float ringRadius = SystemGlyph.DevelopmentRingScale * 0.5f;

            Assert.Greater(pipInner, bodyRadius, "Une pastille ne doit pas mordre sur le corps.");
            Assert.Less(pipOuter, ringRadius, "Une pastille ne doit pas toucher l'anneau de developpement.");
        }

        [Test]
        public void Layout_GarrisonPipsDoNotOverlapEachOther()
        {
            // Cinq pastilles sur un arc de 74 degres a l'orbite retenue : verifie que deux
            // voisines gardent un ecart superieur a leur diametre.
            int count = SystemGlyph.MaximumGarrisonPips;
            float stepRadians = SystemGlyph.GarrisonPipArc / (count - 1) * Mathf.Deg2Rad;
            float distanceBetweenNeighbours = 2f * SystemGlyph.GarrisonPipOrbit * Mathf.Sin(stepRadians * 0.5f);

            Assert.Greater(
                distanceBetweenNeighbours, SystemGlyph.GarrisonPipScale,
                "Deux pastilles voisines se chevaucheraient : elles cesseraient d'etre denombrables.");
        }

        /// <summary>
        /// Comparaison canal par canal avec tolerance. <c>Color.Lerp</c> calcule
        /// <c>a + (b - a) * t</c>, qui n'est pas exact a <c>t = 1</c> : une egalite stricte
        /// echouerait ici comme dans Unity.
        /// </summary>
        private static void AssertSameColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f, "canal rouge");
            Assert.AreEqual(expected.g, actual.g, 1e-3f, "canal vert");
            Assert.AreEqual(expected.b, actual.b, 1e-3f, "canal bleu");
        }
    }
}
