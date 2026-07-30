using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Reglages editables de la generation de galaxie.
    /// <para>
    /// Suit le meme pattern que <see cref="Espace.Data.GameConfig"/> : les valeurs vivent
    /// dans un asset ajustable sans recompiler. Le contenu est converti en
    /// <see cref="GalaxyGenerationParameters"/>, une structure C# pure, avant d'etre passe
    /// a <see cref="GalaxyGenerator"/> — qui reste ainsi testable sans instancier de
    /// ScriptableObject.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "GalaxyConfig", menuName = "Espace/Galaxy/Galaxy Config", order = 0)]
    public sealed class GalaxyConfig : ScriptableObject
    {
        [Header("Graine")]
        [Tooltip("0 = graine aleatoire tiree au demarrage. Toute autre valeur reproduit exactement la meme galaxie.")]
        [SerializeField]
        private int seed;

        [Header("Systemes")]
        [Tooltip("Nombre de systemes stellaires de la galaxie.")]
        [SerializeField]
        [Range(10, 200)]
        private int systemCount = 100;

        [Tooltip("Rayon du disque galactique dans lequel les systemes sont repartis.")]
        [SerializeField]
        [Range(10f, 200f)]
        private float galaxyRadius = 45f;

        [Tooltip("Distance minimale entre deux systemes, pour eviter les amas illisibles au zoom minimal.")]
        [SerializeField]
        [Range(0.5f, 20f)]
        private float minSystemDistance = 3f;

        [Tooltip("Tentatives de placement par systeme avant d'accepter le meilleur emplacement trouve.")]
        [SerializeField]
        [Range(5, 100)]
        private int maxPlacementAttempts = 30;

        [Header("Routes hyperspatiales")]
        [Tooltip("Nombre moyen de routes par systeme vise, au-dela de l'arbre couvrant minimal qui garantit la connexite.")]
        [SerializeField]
        [Range(2f, 6f)]
        private float targetAverageDegree = 2.5f;

        /// <summary>
        /// Construit les parametres de generation. Si <see cref="seed"/> vaut 0, une graine
        /// aleatoire est tiree : chaque nouvelle partie propose alors une galaxie differente.
        /// </summary>
        public GalaxyGenerationParameters ToGenerationParameters()
        {
            int effectiveSeed = seed != 0 ? seed : System.Guid.NewGuid().GetHashCode();

            return new GalaxyGenerationParameters(
                effectiveSeed,
                systemCount,
                galaxyRadius,
                minSystemDistance,
                maxPlacementAttempts,
                targetAverageDegree);
        }

        /// <summary>Rayon du disque galactique, utilise pour cadrer la camera (voir <see cref="GalaxyCameraController"/>).</summary>
        public float GalaxyRadius => galaxyRadius;
    }
}
