namespace Espace.Gameplay.Diplomacy
{
    /// <summary>
    /// Etat diplomatique entre deux empires. Toujours symetrique : le statut de A envers B
    /// est identique a celui de B envers A (a la difference de l'opinion, voir
    /// <see cref="IDiplomacyService.GetOpinion"/>, qui est dirigee).
    /// <para>
    /// Traite commercial et embargo restent volontairement en dehors de cette enumeration
    /// (voir <see cref="IDiplomacyService.HasTradeTreaty"/>/<see cref="IDiplomacyService.IsEmbargoing"/>) :
    /// une vraie diplomatie superpose plusieurs relations independantes (on peut avoir un
    /// traite commercial tout en etant en Paix simple, ou etre embargue par un allie), les
    /// fusionner dans un seul statut empecherait ces combinaisons.
    /// </para>
    /// </summary>
    public enum DiplomaticStatus
    {
        /// <summary>Statut par defaut entre deux empires qui n'ont jamais interagi.</summary>
        Peace = 0,

        /// <summary>Combat autorise entre ces deux empires (voir le blocage de guerre en Phase 7 dans <c>MilitaryService</c>).</summary>
        War = 1,

        /// <summary>Alliance militaire : aucune attaque possible tant qu'elle dure.</summary>
        Alliance = 2,

        /// <summary>Pacte de non-agression : aucune attaque possible tant qu'il dure, sans les obligations d'une alliance.</summary>
        NonAggressionPact = 3
    }
}
