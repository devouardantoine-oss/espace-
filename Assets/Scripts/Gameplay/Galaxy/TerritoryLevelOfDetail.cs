using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>Palier de lecture de la carte, deduit du zoom courant (Phase 19).</summary>
    public enum TerritoryDetailTier
    {
        /// <summary>Vue d'ensemble : on lit la geopolitique, pas les systemes.</summary>
        Galactic = 0,

        /// <summary>Transition : les territoires restent lisibles, les systemes majeurs emergent.</summary>
        Sector = 1,

        /// <summary>Navigation : on cherche une destination, on trace une route.</summary>
        System = 2,

        /// <summary>Decision : coloniser, construire, recruter sur un systeme precis.</summary>
        Orbital = 3,
    }

    /// <summary>
    /// L'echelle de lecture de la carte galactique (Phase 19) : <b>plus on s'eloigne, plus
    /// l'information devient politique ; plus on s'approche, plus elle devient locale.</b>
    /// <para>
    /// Les territoires s'affirment au zoom eloigne et s'effacent au zoom rapproche. Ce n'est pas
    /// une coquetterie : des zones pleines a intensite constante rendraient un systeme
    /// illisible sous sa propre couleur au moment ou le joueur doit justement en lire les
    /// statistiques. Le nom de faction suit la courbe inverse de celle des noms de systeme, si
    /// bien que les deux ne se disputent jamais l'ecran.
    /// </para>
    /// <para>
    /// <b>Classe statique pure, seuils exprimes en fraction de zoom (0 = le plus eloigne,
    /// 1 = le plus proche) :</b> meme raisonnement que <see cref="SystemLabelController"/> —
    /// des valeurs relatives restent correctes quelle que soit la taille de galaxie
    /// configuree, la ou des tailles orthographiques absolues ne conviendraient qu'a un seul
    /// <see cref="GalaxyConfig"/>. Etre pur la rend aussi entierement verifiable en EditMode.
    /// </para>
    /// </summary>
    public static class TerritoryLevelOfDetail
    {
        /// <summary>Fraction de zoom a partir de laquelle on quitte la vue galactique.</summary>
        public const float SectorThreshold = 0.30f;

        /// <summary>Fraction de zoom a partir de laquelle on lit les systemes plutot que les secteurs.</summary>
        public const float SystemThreshold = 0.50f;

        /// <summary>Fraction de zoom a partir de laquelle on est en vue orbitale.</summary>
        public const float OrbitalThreshold = 0.75f;

        /// <summary>Opacite du remplissage des territoires au zoom le plus eloigne.</summary>
        private const float FillAlphaFar = 0.30f;

        /// <summary>Opacite du remplissage des territoires au zoom le plus proche.</summary>
        private const float FillAlphaNear = 0.08f;

        /// <summary>Attenuation maximale des frontieres au zoom le plus proche (elles restent plus presentes que le remplissage).</summary>
        private const float BorderAlphaFalloff = 0.55f;

        /// <summary>Debut du fondu sortant du nom de faction.</summary>
        private const float FactionFadeStart = 0.26f;

        /// <summary>Fin du fondu sortant du nom de faction.</summary>
        private const float FactionFadeEnd = 0.46f;

        /// <summary>
        /// Largeur de trait des frontieres, en unites monde, pour chaque palier — indexee par
        /// <see cref="TerritoryDetailTier"/>.
        /// <para>
        /// <b>Pourquoi une largeur par palier plutot qu'une largeur continue ?</b> Une frontiere
        /// doit garder une epaisseur a peu pres constante <i>a l'ecran</i>, sinon elle est en
        /// dents de scie au zoom arriere et devient un ruban au zoom avant. La suivre en continu
        /// obligerait a reconstruire le maillage a chaque frame de pincement ; l'indexer sur le
        /// palier limite les reconstructions a trois au maximum pour un pincement complet, pour
        /// un ecart d'epaisseur imperceptible a l'interieur d'un palier. Les valeurs visent
        /// environ 2,5 pixels sur un ecran de 1080 de haut au milieu de chaque palier.
        /// </para>
        /// </summary>
        private static readonly float[] BorderWidths = { 0.22f, 0.16f, 0.11f, 0.05f };

        /// <summary>Multiplicateur de largeur du halo dessine sous une frontiere contestee.</summary>
        public const float ContestedGlowWidthFactor = 4f;

        /// <summary>Palier de lecture correspondant a <paramref name="zoomInFraction"/>.</summary>
        public static TerritoryDetailTier TierFor(float zoomInFraction)
        {
            if (zoomInFraction < SectorThreshold) return TerritoryDetailTier.Galactic;
            if (zoomInFraction < SystemThreshold) return TerritoryDetailTier.Sector;
            if (zoomInFraction < OrbitalThreshold) return TerritoryDetailTier.System;
            return TerritoryDetailTier.Orbital;
        }

        /// <summary>
        /// Opacite globale du remplissage des territoires. Decroit continument avec le zoom :
        /// contrairement a la largeur des frontieres, l'opacite se pilote par la teinte du
        /// materiau, sans toucher au maillage — la faire varier a chaque frame ne coute rien.
        /// </summary>
        public static float FillAlphaFor(float zoomInFraction)
        {
            return Mathf.Lerp(FillAlphaFar, FillAlphaNear, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(zoomInFraction)));
        }

        /// <summary>Opacite globale des frontieres, sur la meme courbe que le remplissage mais bien moins attenuee.</summary>
        public static float BorderAlphaFor(float zoomInFraction)
        {
            return 1f - BorderAlphaFalloff * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(zoomInFraction));
        }

        /// <summary>
        /// Opacite du nom de faction : pleine en vue galactique, eteinte des que les noms de
        /// systeme prennent le relais.
        /// </summary>
        public static float FactionLabelAlphaFor(float zoomInFraction)
        {
            return 1f - SmoothBetween(FactionFadeStart, FactionFadeEnd, zoomInFraction);
        }

        /// <summary>
        /// Transition douce de 0 a 1 lorsque <paramref name="value"/> parcourt
        /// [<paramref name="edge0"/>, <paramref name="edge1"/>].
        /// <para>
        /// <b>A ne pas confondre avec <see cref="Mathf.SmoothStep"/> :</b> celui d'Unity
        /// interpole <i>entre ses deux premiers arguments</i> en prenant le troisieme comme
        /// facteur, alors qu'on cherche ici a savoir ou l'on se situe <i>entre deux seuils</i>.
        /// Les deux ont la meme signature et des noms voisins, mais des sens opposes.
        /// </para>
        /// </summary>
        private static float SmoothBetween(float edge0, float edge1, float value)
        {
            if (Mathf.Approximately(edge0, edge1))
            {
                return value < edge0 ? 0f : 1f;
            }

            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Largeur de trait des frontieres pour <paramref name="tier"/>, en unites monde.</summary>
        public static float BorderWidthFor(TerritoryDetailTier tier)
        {
            int index = Mathf.Clamp((int)tier, 0, BorderWidths.Length - 1);
            return BorderWidths[index];
        }

        /// <summary>
        /// Fraction de zoom (0 = le plus eloigne, 1 = le plus proche) pour une camera
        /// orthographique bornee. Regroupee ici plutot que dupliquee dans chaque controleur qui
        /// en a besoin.
        /// </summary>
        public static float ZoomInFraction(float orthographicSize, float minOrthographicSize, float maxOrthographicSize)
        {
            return Mathf.InverseLerp(maxOrthographicSize, minOrthographicSize, orthographicSize);
        }
    }
}
