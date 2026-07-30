namespace Espace.Gameplay.Research
{
    /// <summary>
    /// Les sept domaines de recherche du brief. Valeurs explicites et contigues, comme
    /// <see cref="Espace.Gameplay.Military.UnitType"/> : servent d'index et seront ecrites
    /// telles quelles dans les sauvegardes JSON (Phase 10). Ne jamais reordonner ni reutiliser
    /// une valeur.
    /// <para>
    /// Chaque domaine ameliore un systeme existant precis (voir <see cref="IResearchService.GetBonus"/>
    /// et ses points d'application dans <c>EconomyService</c>, <c>MilitaryService</c> et
    /// <c>DiplomacyService</c>), sauf <see cref="Espionage"/> qui n'a pas encore de systeme a
    /// ameliorer (Phase 9) : son bonus est deja calculable et affiche, simplement inexploite
    /// pour l'instant.
    /// </para>
    /// </summary>
    public enum ResearchDomain
    {
        /// <summary>Ameliore la production de Credits.</summary>
        Economy = 0,

        /// <summary>Ameliore la production de Minerais.</summary>
        Industry = 1,

        /// <summary>Ameliore la puissance de combat des unites.</summary>
        Weapons = 2,

        /// <summary>Ameliore la production d'Energie.</summary>
        Energy = 3,

        /// <summary>Ameliore les gains d'opinion diplomatique.</summary>
        Diplomacy = 4,

        /// <summary>Reserve a l'espionnage (Phase 9) : bonus calculable des la Phase 8, sans effet avant que le systeme n'existe.</summary>
        Espionage = 5,

        /// <summary>Ameliore la vitesse de deplacement des flottes.</summary>
        Logistics = 6
    }
}
