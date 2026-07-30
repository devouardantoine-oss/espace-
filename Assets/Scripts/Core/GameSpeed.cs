namespace Espace.Core
{
    /// <summary>
    /// Vitesse d'ecoulement du temps de jeu.
    /// <para>
    /// Quatre paliers actifs plus la pause : une simplification assumee du modele a cinq
    /// vitesses de Crusader Kings 3, pour une horloge lisible d'un coup d'oeil sur petit
    /// ecran plutot qu'une rangee de six boutons tactiles.
    /// </para>
    /// </summary>
    public enum GameSpeed
    {
        /// <summary>Temps arrete.</summary>
        Paused = 0,

        /// <summary>Vitesse de reference (x1).</summary>
        Normal = 1,

        /// <summary>Vitesse acceleree.</summary>
        Fast = 2,

        /// <summary>Vitesse tres acceleree.</summary>
        Faster = 3,

        /// <summary>Vitesse maximale.</summary>
        Fastest = 4
    }
}
