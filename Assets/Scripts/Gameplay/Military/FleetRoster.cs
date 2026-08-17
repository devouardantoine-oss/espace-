using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Comment une flotte se presente dans la liste du panneau Flottes (Phase 24, etape 4).
    /// <para>
    /// <b>La regle du panneau : aucun chiffre absolu.</b> « Puissance ~418 » ne veut rien dire
    /// tant qu'on ignore ce que vaut le reste. L'action la plus frequente sur cet ecran est de
    /// <i>comparer deux flottes</i> — laquelle envoyer, laquelle renforcer — et une barre
    /// remplie repond a cette question sans qu'on ait a soustraire deux nombres de tete.
    /// </para>
    /// <para>
    /// <b>La reference est la flotte la plus forte du joueur</b>, pas une constante. Une echelle
    /// fixe se serait tassee en fin de partie, quand toutes les flottes depassent le maximum
    /// prevu, et aurait rendu la barre inutile exactement au moment ou l'on compare le plus.
    /// </para>
    /// <para>
    /// Fonctions pures : elles ne connaissent que des nombres et un statut, et se verifient sans
    /// scene ni service.
    /// </para>
    /// </summary>
    public static class FleetRoster
    {
        /// <summary>
        /// Remplissage de la barre, entre 0 et 1.
        /// <para>
        /// Une flotte non vide garde un trait visible meme face a une flotte dix fois plus forte :
        /// une barre vide se lit « detruite », ce qui est faux et dangereux.
        /// </para>
        /// </summary>
        public const float MinimumVisibleShare = 0.06f;

        /// <summary>
        /// Part de la barre a remplir pour une flotte de <paramref name="power"/>, la plus forte
        /// du joueur valant <paramref name="strongestPower"/>.
        /// </summary>
        public static float PowerShare(float power, float strongestPower)
        {
            if (power <= 0f)
            {
                return 0f;
            }

            if (strongestPower <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp(power / strongestPower, MinimumVisibleShare, 1f);
        }

        /// <summary>
        /// La mission de la flotte, en un mot.
        /// <para>
        /// <b>Le libelle dit ce qu'elle fait, pas ou elle est</b> : la destination vit sur la
        /// ligne en retrait, dessous. Melanger les deux donnait une phrase qu'il fallait lire
        /// jusqu'au bout pour savoir si la flotte etait disponible.
        /// </para>
        /// </summary>
        public static string MissionLabel(FleetStatus status, bool isGarrisonSized)
        {
            switch (status)
            {
                case FleetStatus.Moving:
                    return "en vol";

                case FleetStatus.AwaitingEncounter:
                    return "rencontre";

                default:
                    return isGarrisonSized ? "garnison" : "defense";
            }
        }

        /// <summary>
        /// Vrai si la flotte a perdu assez de sa composition pour qu'on le signale.
        /// <para>
        /// Une flotte decimee est encore une flotte : elle apparait dans la liste, elle occupe
        /// une place de deploiement, et l'oublier coute une bataille. Le mot est la pour qu'on ne
        /// la confonde pas avec une flotte au complet en la comparant a la barre voisine.
        /// </para>
        /// </summary>
        public static bool IsCrippled(float power, float strongestPower)
        {
            return power > 0f && strongestPower > 0f && power / strongestPower < 0.2f;
        }
    }
}
