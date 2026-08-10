using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Amortissement et courbes d'animation partages par les ecrans (Phase 21.1).
    /// <para>
    /// <b>Amortissement exponentiel, pas d'interpolation lineaire par frame.</b> Ecrire
    /// <c>current = Mathf.Lerp(current, target, 0.1f)</c> est le reflexe habituel, et il est
    /// faux : la vitesse depend alors du nombre d'images par seconde, donc une transition
    /// dure deux fois plus longtemps a 30 FPS qu'a 60. La formule employee ici,
    /// <c>1 - e^(-dt / tau)</c>, donne le meme temps de reponse quelle que soit la cadence.
    /// C'est celle deja retenue pour le lissage des marqueurs de flotte (Phase 20).
    /// </para>
    /// <para>
    /// <b>Toujours en secondes reelles.</b> Ces animations sont de l'interface : elles ne
    /// doivent ni accelerer avec la vitesse de jeu, ni se figer quand le joueur met en pause.
    /// Les appelants passent <c>Time.unscaledDeltaTime</c>.
    /// </para>
    /// </summary>
    public static class UiEasing
    {
        /// <summary>
        /// Duree de reponse de reference : le temps au bout duquel une valeur amortie a couvert
        /// environ 63 % du chemin. Assez court pour paraitre reactif, assez long pour se voir.
        /// </summary>
        public const float StandardSmoothing = 0.12f;

        /// <summary>Duree de reponse d'un basculement de palette de faction : volontairement plus lente, c'est un changement d'identite, pas un survol.</summary>
        public const float PaletteSmoothing = 0.22f;

        /// <summary>
        /// Fraction du chemin a parcourir cette frame pour un amortissement de constante
        /// <paramref name="smoothingSeconds"/>.
        /// </summary>
        /// <param name="smoothingSeconds">Constante de temps. Zero ou moins signifie « immediat ».</param>
        /// <param name="deltaTime">Duree de la frame, en secondes reelles.</param>
        public static float Blend(float smoothingSeconds, float deltaTime)
        {
            if (smoothingSeconds <= 0f || deltaTime <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-deltaTime / smoothingSeconds);
        }

        /// <summary>Rapproche <paramref name="current"/> de <paramref name="target"/> d'une frame.</summary>
        public static float Approach(float current, float target, float smoothingSeconds, float deltaTime)
        {
            return Mathf.Lerp(current, target, Blend(smoothingSeconds, deltaTime));
        }

        /// <summary>
        /// Version couleur de <see cref="Approach(float,float,float,float)"/>.
        /// <para>
        /// L'interpolation se fait canal par canal en RVB plutot qu'en teinte-saturation-valeur :
        /// sur une transition de 0,22 s entre deux couleurs de faction, la difference est
        /// invisible, alors qu'un passage par la teinte traverserait tout le cercle chromatique
        /// pour aller du bleu a l'orange — un arc-en-ciel bien visible, lui.
        /// </para>
        /// </summary>
        public static Color Approach(Color current, Color target, float smoothingSeconds, float deltaTime)
        {
            return Color.Lerp(current, target, Blend(smoothingSeconds, deltaTime));
        }

        /// <summary>
        /// Depart franc, arrivee douce. La courbe des apparitions de panneau : l'element est
        /// deja presque en place quand l'œil le trouve, ce qui donne une impression de vivacite
        /// qu'une interpolation lineaire n'a pas.
        /// </summary>
        public static float EaseOutCubic(float t)
        {
            float clamped = Mathf.Clamp01(t);
            float inverse = 1f - clamped;
            return 1f - inverse * inverse * inverse;
        }

        /// <summary>Depart et arrivee doux. Pour ce qui va et vient — respiration d'un halo, pulsation d'un marqueur.</summary>
        public static float SmoothStep01(float t)
        {
            float clamped = Mathf.Clamp01(t);
            return clamped * clamped * (3f - 2f * clamped);
        }

        /// <summary>
        /// Progression d'un element apparaissant en cascade : l'element <paramref name="index"/>
        /// commence <paramref name="stagger"/> secondes apres le premier.
        /// </summary>
        /// <returns>Une valeur adoucie entre 0 (pas encore apparu) et 1 (en place).</returns>
        public static float StaggeredReveal(float elapsedSeconds, int index, float stagger, float duration)
        {
            if (duration <= 0f)
            {
                return 1f;
            }

            float local = (elapsedSeconds - index * Mathf.Max(0f, stagger)) / duration;
            return EaseOutCubic(local);
        }

        /// <summary>
        /// Oscillation entre 0 et 1 de periode <paramref name="periodSeconds"/>, adoucie aux deux
        /// extremites. Utilisee pour la respiration des halos.
        /// </summary>
        public static float Pulse(float timeSeconds, float periodSeconds)
        {
            if (periodSeconds <= 0f)
            {
                return 0f;
            }

            // 0.5 - 0.5·cos donne deja une oscillation douce et, contrairement a un
            // PingPong adouci, elle n'a aucune discontinuite de vitesse aux extremes.
            return 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * timeSeconds / periodSeconds);
        }
    }
}
