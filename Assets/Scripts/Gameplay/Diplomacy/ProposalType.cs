namespace Espace.Gameplay.Diplomacy
{
    /// <summary>
    /// Types de propositions diplomatiques echangees entre deux empires, qui necessitent une
    /// acceptation de la cible (voir <see cref="DiplomaticProposal"/>).
    /// <para>
    /// <b>Declaration de guerre et embargo n'en font pas partie :</b> ce sont des actions
    /// unilaterales (voir <see cref="IDiplomacyService.TryDeclareWar"/>/<c>TrySetEmbargo</c>),
    /// aucune acceptation de la cible n'a de sens pour elles.
    /// </para>
    /// <para>
    /// <b>« Demande d'aide » du brief absorbee par <see cref="ResourceExchange"/> :</b> une
    /// demande d'aide est un echange de ressources ou l'un des deux lots est nul — pas besoin
    /// d'un type dedie.
    /// </para>
    /// </summary>
    public enum ProposalType
    {
        /// <summary>Alliance militaire mutuelle.</summary>
        Alliance = 0,

        /// <summary>Pacte de non-agression mutuel.</summary>
        NonAggressionPact = 1,

        /// <summary>Traite commercial : revenu passif mensuel partage tant qu'il est actif.</summary>
        TradeTreaty = 2,

        /// <summary>Proposition de paix, mettant fin a un etat de <see cref="DiplomaticStatus.War"/>.</summary>
        PeaceTreaty = 3,

        /// <summary>Echange (eventuellement asymetrique) de ressources entre les deux tresors.</summary>
        ResourceExchange = 4,

        /// <summary>Echange de propriete d'un systeme contre un autre (ou contre des ressources).</summary>
        TerritoryExchange = 5,

        /// <summary>
        /// Exigence unilaterale : la cible doit livrer le tribut demande ou subir une
        /// declaration de guerre automatique (voir <see cref="DiplomacyService"/>).
        /// </summary>
        Ultimatum = 6
    }
}
