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
            float seaLevel = PlanetTextureFactory.SeaLevelOf(kind);
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
        public void SeaLevel_RanksWorldsFromWettestToDriest()
        {
            Assert.Greater(PlanetTextureFactory.SeaLevelOf(PlanetKind.Ocean), PlanetTextureFactory.SeaLevelOf(PlanetKind.Terran));
            Assert.Greater(PlanetTextureFactory.SeaLevelOf(PlanetKind.Terran), PlanetTextureFactory.SeaLevelOf(PlanetKind.Arid));
            Assert.AreEqual(0f, PlanetTextureFactory.SeaLevelOf(PlanetKind.Barren), "Un monde sterile n'a pas d'eau du tout.");
        }

        [Test]
        public void WaterCoverage_FollowsTheKindOfWorld()
        {
            // Le meme relief lu avec des niveaux de mer differents doit donner des mondes
            // reconnaissables : un monde oceanique noye, un monde aride presque sec.
            float[] elevation = PlanetTextureFactory.CreateElevation(99, Width, Height);

            float ocean = WaterFraction(elevation, PlanetKind.Ocean);
            float terran = WaterFraction(elevation, PlanetKind.Terran);
            float arid = WaterFraction(elevation, PlanetKind.Arid);

            Assert.Greater(ocean, terran);
            Assert.Greater(terran, arid);
            Assert.Greater(ocean, 0.6f, "Un monde oceanique doit etre majoritairement liquide.");
            Assert.Less(arid, 0.25f, "Un monde aride ne doit garder que des mers residuelles.");
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

        [Test]
        public void SeaLevel_IsNotAKnifeEdge()
        {
            // Corollaire du test precedent, exprime du point de vue du reglage : deplacer le
            // niveau des mers de un dixieme doit changer la part d'eau de facon sensible mais
            // progressive. Si un dixieme faisait passer de 5 % a 95 %, aucun reglage de type de
            // monde ne serait tenable.
            float[] elevation = PlanetTextureFactory.CreateElevation(17, Width, Height);

            float below = WaterFractionAt(elevation, 0.45f);
            float above = WaterFractionAt(elevation, 0.55f);

            Assert.Greater(above - below, 0.05f, "Le niveau des mers doit avoir un effet perceptible.");
            Assert.Less(above - below, 0.55f, "Le niveau des mers ne doit pas basculer le monde entier d'un coup.");
        }

        private static float WaterFractionAt(float[] elevation, float seaLevel)
        {
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
    }
}
