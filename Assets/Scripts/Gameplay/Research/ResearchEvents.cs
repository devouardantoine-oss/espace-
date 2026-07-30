using Espace.Core;

namespace Espace.Gameplay.Research
{
    /// <summary>Publie quand un empire complete un palier de recherche.</summary>
    public readonly struct TechnologyResearchedEvent : IGameEvent
    {
        public readonly int EmpireId;
        public readonly TechnologyDefinition Technology;

        public TechnologyResearchedEvent(int empireId, TechnologyDefinition technology)
        {
            EmpireId = empireId;
            Technology = technology;
        }
    }

    /// <summary>Publie quand un empire change le domaine sur lequel ses points de recherche journaliers sont investis.</summary>
    public readonly struct ActiveDomainChangedEvent : IGameEvent
    {
        public readonly int EmpireId;
        public readonly ResearchDomain Domain;

        public ActiveDomainChangedEvent(int empireId, ResearchDomain domain)
        {
            EmpireId = empireId;
            Domain = domain;
        }
    }
}
