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
    /// </summary>
    public static class ResearchDecisionMaker
    {
        public static void DecideAndAct(Empire empire, IResearchService research)
        {
            ResearchDomain? active = research.GetActiveDomain(empire.Id);
            if (active != null && research.GetNextTechnology(empire.Id, active.Value) != null)
            {
                return;
            }

            EmpirePersonalityProfileData profile = EmpirePersonalityProfile.Get(empire.Personality);
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
    }
}
