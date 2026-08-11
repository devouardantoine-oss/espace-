using Espace.Gameplay.Empires;

namespace Espace.Gameplay.Research
{
    /// <summary>
    /// Decision de recherche autonome d'un empire IA pour un tour de reflexion (un mois de
    /// jeu, voir <c>AIController</c>).
    /// <para>
    /// Fonction statique pure vis-a-vis de Unity, meme esprit que
    /// <see cref="Espace.Gameplay.Military.MilitaryDecisionMaker"/> et
    /// <see cref="Espace.Gameplay.Diplomacy.DiplomacyDecisionMaker"/> : aucune dependance a
    /// <c>ServiceLocator</c> ni a un <c>MonoBehaviour</c>, testable sans scene.
    /// </para>
    /// <para>
    /// <b>Une seule action par appel, et seulement si necessaire :</b> si l'empire est deja
    /// concentre sur un domaine non encore maximal, ce module ne fait rien — changer de
    /// domaine ne coute rien (voir <see cref="ResearchService"/>) mais n'apporte rien non plus
    /// tant que le domaine courant progresse encore, donc autant laisser l'IA s'y tenir
    /// plutot que d'osciller sans raison entre plusieurs domaines de tete de liste.
    /// </para>
    /// <para>
    /// <b>Une urgence peut desormais interrompre le domaine en cours (Phase 22, P7).</b>
    /// Jusqu'ici, un empire menace d'invasion continuait a chercher la Diplomatie pendant vingt
    /// ans parce que sa personnalite l'avait mise en tete de liste : la recherche etait le seul
    /// module totalement sourd a la situation. Une posture urgente promeut maintenant un domaine
    /// (voir <see cref="DomainCalledForBy"/>) devant la liste de personnalite.
    /// </para>
    /// <para>
    /// <b>L'interruption est gratuite, et c'est ce qui la rend acceptable.</b>
    /// <see cref="ResearchService"/> conserve les points investis domaine par domaine : un
    /// empire qui abandonne l'Economie pour l'Armement retrouvera sa progression economique
    /// intacte au retour. Une posture qui oscillerait d'un mois sur l'autre ferait donc perdre
    /// du temps, jamais du travail.
    /// </para>
    /// <para>
    /// <b>La personnalite n'est pas effacee.</b> En posture <c>Expanding</c> — le cas courant,
    /// et de loin — aucun domaine n'est promu et l'ordre de personnalite decide seul, exactement
    /// comme avant. La promotion est une exception justifiee par une urgence, pas la nouvelle
    /// regle generale.
    /// </para>
    /// </summary>
    public static class ResearchDecisionMaker
    {
        public static void DecideAndAct(Empire empire, IResearchService research, EmpireAssessment assessment)
        {
            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);
            ResearchDomain? active = research.GetActiveDomain(empire.Id);

            ResearchDomain? urgent = DomainCalledForBy(assessment.Posture);
            if (urgent != null && active != urgent && research.GetNextTechnology(empire.Id, urgent.Value) != null)
            {
                // L'urgence prime, mais seulement si le domaine promu a encore quelque chose a
                // offrir : reorienter vers un domaine deja au maximum ferait perdre chaque point
                // produit (voir ResearchService, « points perdus si le domaine actif est deja au
                // maximum »).
                if (research.TrySetActiveDomain(empire.Id, urgent.Value, out _))
                {
                    return;
                }
            }

            if (active != null && research.GetNextTechnology(empire.Id, active.Value) != null)
            {
                return;
            }

            foreach (ResearchDomain domain in profile.ResearchPriority)
            {
                if (research.GetNextTechnology(empire.Id, domain) == null)
                {
                    continue;
                }

                if (research.TrySetActiveDomain(empire.Id, domain, out _))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Domaine que la situation impose, ou <c>null</c> si rien ne presse et que la
        /// personnalite doit decider seule.
        /// <para>
        /// Chaque association repond a un manque precis, pas a une preference :
        /// </para>
        /// <list type="bullet">
        /// <item><description><b>Consolidating</b> → <see cref="ResearchDomain.Economy"/> : la
        /// posture se declenche sur une tresorerie exsangue ou des provinces agitees, et
        /// l'Economie est le seul domaine qui agisse sur le probleme (plus de Credits produits,
        /// donc moins besoin de serrer la vis fiscale, donc moins de mecontentement).</description></item>
        /// <item><description><b>Defending</b> → <see cref="ResearchDomain.Weapons"/> : ce qui
        /// manque est mesure par l'indicateur meme qui a declenche la posture — la puissance de
        /// combat face au voisin le plus fort.</description></item>
        /// <item><description><b>Aggressive</b> → <see cref="ResearchDomain.Logistics"/> : la
        /// puissance est deja acquise (rapport de forces au-dela de 1,6), ce qui limite une
        /// offensive est desormais la portee. Deliberement different de <c>Defending</c> : les
        /// deux postures militaires ne doivent pas chercher la meme chose, sinon la distinction
        /// entre se defendre et attaquer ne se lit nulle part.</description></item>
        /// <item><description><b>Expanding</b> → rien : la personnalite decide, comme avant.</description></item>
        /// </list>
        /// </summary>
        public static ResearchDomain? DomainCalledForBy(StrategicPosture posture)
        {
            switch (posture)
            {
                case StrategicPosture.Consolidating:
                    return ResearchDomain.Economy;

                case StrategicPosture.Defending:
                    return ResearchDomain.Weapons;

                case StrategicPosture.Aggressive:
                    return ResearchDomain.Logistics;

                default:
                    return null;
            }
        }
    }
}
