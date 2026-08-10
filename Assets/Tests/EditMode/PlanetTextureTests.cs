using Espace.Gameplay.Galaxy;
using NUnit.Framework;
using UnityEngine;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Verifie la generation du relief des planetes (Phase 21.1).
    /// <para>
    /// Les tests portent sur <see cref="PlanetTextureFactory.CreateElevation"/>, qui ne renvoie
    /// que des nombres : aucune texture n'est creee, donc rien ne depend du moteur graphique.
    /// C'est ce qui permet de verifier <see cref="Elevation_WrapsAroundTheLongitudeSeam"/> —
    /// le seul defaut vraiment penible de ce genre de generation, et le plus difficile a
    /// reperer a l'œil sur une sphere en rotation lente.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class PlanetTextureTests
    {
        private const int Width = 128;
        private const int Height = 64;

        private static float WaterFraction(float[] elevation, PlanetKind kind)
        {
            float seaLevel = PlanetTextureFactory.SeaLevelFor(elevation, PlanetTextureFactory.WaterShareOf(kind));
            int submerged = 0;

            for (int i = 0; i < elevation.Length; i++)
            {
                if (elevation[i] < seaLevel)
                {
                    submerged++;
                }
            }

            return submerged / (float)elevation.Length;
        }

        [Test]
        public void Elevation_HasTheRequestedDimensions()
        {
            float[] elevation = PlanetTextureFactory.CreateElevation(7, Width, Height);
            Assert.AreEqual(Width * Height, elevation.Length);
        }

        [Test]
        public void Elevation_StaysWithinUnitRange()
        {
            float[] elevation = PlanetTextureFactory.CreateElevation(11, Width, Height);

            for (int i = 0; i < elevation.Length; i++)
            {
                Assert.GreaterOrEqual(elevation[i], 0f);
                Assert.LessOrEqual(elevation[i], 1f);
            }
        }

        [Test]
        public void Elevation_IsDeterministicForAGivenSeed()
        {
            // Un systeme doit garder le meme visage d'une ouverture de l'ecran a l'autre.
            float[] first = PlanetTextureFactory.CreateElevation(42, Width, Height);
            float[] second = PlanetTextureFactory.CreateElevation(42, Width, Height);

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Elevation_DiffersBetweenSeeds()
        {
            float[] first = PlanetTextureFactory.CreateElevation(1, Width, Height);
            float[] second = PlanetTextureFactory.CreateElevation(2, Width, Height);

            int differences = 0;
            for (int i = 0; i < first.Length; i++)
            {
                if (!Mathf.Approximately(first[i], second[i]))
                {
                    differences++;
                }
            }

            Assert.Greater(differences, first.Length / 2, "Deux graines doivent donner deux mondes nettement distincts.");
        }

        [Test]
        public void Elevation_WrapsAroundTheLongitudeSeam()
        {
            // Sans fondu de couture, la sphere porte une cicatrice verticale visible a chaque
            // tour. Le test compare la derniere colonne a la premiere : l'ecart doit rester du
            // meme ordre que celui entre deux colonnes voisines quelconques.
            float[] elevation = PlanetTextureFactory.CreateElevation(2387, Width, Height);

            float seamGap = 0f;
            float ordinaryGap = 0f;

            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                seamGap += Mathf.Abs(elevation[row + Width - 1] - elevation[row]);
                ordinaryGap += Mathf.Abs(elevation[row + Width / 2] - elevation[row + Width / 2 - 1]);
            }

            seamGap /= Height;
            ordinaryGap /= Height;

            Assert.Less(
                seamGap, ordinaryGap * 3f + 0.02f,
                $"La couture ({seamGap:F4}) doit rester du meme ordre qu'une transition ordinaire ({ordinaryGap:F4}).");
        }

        [Test]
        public void Elevation_ToleratesDegenerateDimensions()
        {
            // Les tailles sont bornees plutot que rejetees : un ecran mal configure doit donner
            // une planete laide, pas une exception au chargement.
            Assert.DoesNotThrow(() => PlanetTextureFactory.CreateElevation(3, 0, 0));
            Assert.DoesNotThrow(() => PlanetTextureFactory.CreateElevation(3, 1, 1));
        }

        [Test]
        public void WaterShare_RanksWorldsFromWettestToDriest()
        {
            Assert.Greater(PlanetTextureFactory.WaterShareOf(PlanetKind.Ocean), PlanetTextureFactory.WaterShareOf(PlanetKind.Terran));
            Assert.Greater(PlanetTextureFactory.WaterShareOf(PlanetKind.Terran), PlanetTextureFactory.WaterShareOf(PlanetKind.Arid));
            Assert.AreEqual(0f, PlanetTextureFactory.WaterShareOf(PlanetKind.Barren), "Un monde sterile n'a pas d'eau du tout.");
        }

        [Test]
        public void WaterCoverage_MatchesWhatEachKindAsksFor()
        {
            // Ce test est le garde-fou contre la divergence qui a fait echouer sa version
            // precedente : elle comparait la part d'eau a des bornes en dur, calibrees sur une
            // implementation de Perlin donnee. Un monde regle « aride » s'est retrouve couvert a
            // 28 % d'eau sous Unity la ou la calibration hors-ligne en annoncait 10. Le niveau
            // des mers etant desormais deduit du champ lui-meme, la part obtenue est celle
            // demandee — quelle que soit la source de bruit.
            float[] elevation = PlanetTextureFactory.CreateElevation(99, Width, Height);

            foreach (PlanetKind kind in new[] { PlanetKind.Ocean, PlanetKind.Terran, PlanetKind.Arid, PlanetKind.Ice, PlanetKind.Toxic })
            {
                float requested = PlanetTextureFactory.WaterShareOf(kind);
                float achieved = WaterFraction(elevation, kind);

                // La tolerance est celle du pas de l'histogramme (1/256), plus la marge d'un
                // seau entier : on ne cherche pas la precision au pixel, mais l'absence de
                // derive systematique.
                Assert.AreEqual(requested, achieved, 0.05f, $"{kind} : {achieved:P0} d'eau pour {requested:P0} demandes.");
            }
        }

        [Test]
        public void WaterCoverage_KeepsTheWorldsRecognisable()
        {
            float[] elevation = PlanetTextureFactory.CreateElevation(99, Width, Height);

            Assert.Greater(WaterFraction(elevation, PlanetKind.Ocean), WaterFraction(elevation, PlanetKind.Terran));
            Assert.Greater(WaterFraction(elevation, PlanetKind.Terran), WaterFraction(elevation, PlanetKind.Arid));
            Assert.AreEqual(0f, WaterFraction(elevation, PlanetKind.Barren), "Un monde sterile ne doit avoir aucune mer.");
        }

        [Test]
        public void SeaLevel_HandlesTheExtremes()
        {
            float[] elevation = PlanetTextureFactory.CreateElevation(7, Width, Height);

            Assert.AreEqual(0f, PlanetTextureFactory.SeaLevelFor(elevation, 0f), "Aucune eau demandee : aucune mer.");
            Assert.AreEqual(1f, PlanetTextureFactory.SeaLevelFor(elevation, 1f), "Monde entierement noye : tout est sous le niveau.");
            Assert.AreEqual(0f, PlanetTextureFactory.SeaLevelFor(null, 0.5f), "Un champ absent ne doit pas faire echouer le rendu.");
            Assert.AreEqual(0f, PlanetTextureFactory.SeaLevelFor(new float[0], 0.5f));
        }

        [Test]
        public void Elevation_SpreadsAcrossItsWholeRange()
        {
            // Garde-fou contre le defaut le plus insidieux de ce genre de generation : la somme
            // de plusieurs octaves donne naturellement une cloche tres etroite, ou 90 % du
            // relief tient entre 0,4 et 0,6. Rien ne plante, la carte n'est pas plate — mais le
            // niveau des mers devient un fil de rasoir (deux centiemes separent un monde sec
            // d'un monde noye) et toutes les planetes se ressemblent. Comparer simplement le
            // minimum au maximum ne l'aurait pas vu : quelques pixels extremes suffisent a
            // couvrir toute l'echelle.
            float[] elevation = PlanetTextureFactory.CreateElevation(5, Width, Height);

            int low = 0;
            int high = 0;
            for (int i = 0; i < elevation.Length; i++)
            {
                if (elevation[i] < 0.3f)
                {
                    low++;
                }
                else if (elevation[i] > 0.7f)
                {
                    high++;
                }
            }

            float lowShare = low / (float)elevation.Length;
            float highShare = high / (float)elevation.Length;

            Assert.Greater(lowShare, 0.08f, $"Trop peu de creux marques ({lowShare:P0}) : le relief manque de contraste.");
            Assert.Greater(highShare, 0.08f, $"Trop peu de sommets marques ({highShare:P0}) : le relief manque de contraste.");
        }

    }
}
