using System;
using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Research
{
    /// <summary>
    /// Implementation par defaut de <see cref="IResearchService"/>.
    /// <para>
    /// <b>Un seul domaine actif a la fois, par empire :</b> comme le taux d'imposition
    /// (<see cref="Espace.Gameplay.Economy.IEconomyService.SetTaxRate(int,float)"/>), c'est un
    /// choix simple a exposer en IA comme en IMGUI, plutot qu'une repartition de points entre
    /// plusieurs domaines simultanes. Changer de domaine ne fait perdre aucune progression :
    /// les points deja investis dans un domaine restent acquis (<see cref="GetProgress"/>) et
    /// reprennent la ou ils en etaient si l'empire y revient plus tard.
    /// </para>
    /// <para>
    /// <b>Points perdus si le domaine actif est deja au maximum :</b> aucune redirection
    /// automatique vers un autre domaine (cela demanderait de decider a la place du joueur/de
    /// l'IA lequel choisir) — meme philosophie que l'entretien militaire impaye de la Phase 6
    /// (echec silencieux, documente comme tel plutot que traite comme un bug).
    /// </para>
    /// </summary>
    public sealed class ResearchService : IResearchService, IGameService
    {
        // Poids de la formule de generation journaliere de points de recherche, meme forme
        // que la production d'Influence dans EconomyService. Valeurs de depart raisonnables,
        // a affiner en Phase 12 (equilibrage).
        private const float ResearchPerPopulationPoint = 0.006f;
        private const float ResearchPerDevelopmentPoint = 0.3f;

        private readonly GalaxyMap _map;
        private readonly IEventBus _eventBus;
        private readonly List<TechnologyDefinition> _catalog;
        private readonly Dictionary<ResearchDomain, List<TechnologyDefinition>> _catalogByDomain = new Dictionary<ResearchDomain, List<TechnologyDefinition>>();

        private readonly Dictionary<int, ResearchDomain> _activeDomainByEmpire = new Dictionary<int, ResearchDomain>();
        private readonly Dictionary<(int, ResearchDomain), int> _completedTiers = new Dictionary<(int, ResearchDomain), int>();
        private readonly Dictionary<(int, ResearchDomain), float> _progress = new Dictionary<(int, ResearchDomain), float>();

        /// <inheritdoc />
        public IReadOnlyList<TechnologyDefinition> Catalog => _catalog;

        public ResearchService(GalaxyMap map, IEventBus eventBus, IReadOnlyList<TechnologyDefinition> catalog)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _catalog = new List<TechnologyDefinition>(catalog ?? Array.Empty<TechnologyDefinition>());
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _catalogByDomain.Clear();
            _activeDomainByEmpire.Clear();
            _completedTiers.Clear();
            _progress.Clear();

            foreach (TechnologyDefinition technology in _catalog)
            {
                if (technology == null)
                {
                    continue;
                }

                if (!_catalogByDomain.TryGetValue(technology.Domain, out List<TechnologyDefinition> tiers))
                {
                    tiers = new List<TechnologyDefinition>();
                    _catalogByDomain[technology.Domain] = tiers;
                }

                tiers.Add(technology);
            }

            foreach (List<TechnologyDefinition> tiers in _catalogByDomain.Values)
            {
                tiers.Sort((a, b) => a.Tier.CompareTo(b.Tier));
            }

            _eventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _eventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        /// <inheritdoc />
        public ResearchDomain? GetActiveDomain(int empireId)
        {
            return _activeDomainByEmpire.TryGetValue(empireId, out ResearchDomain domain) ? domain : (ResearchDomain?)null;
        }

        /// <inheritdoc />
        public int GetCompletedTierCount(int empireId, ResearchDomain domain)
        {
            return _completedTiers.TryGetValue((empireId, domain), out int count) ? count : 0;
        }

        /// <inheritdoc />
        public float GetProgress(int empireId, ResearchDomain domain)
        {
            return _progress.TryGetValue((empireId, domain), out float points) ? points : 0f;
        }

        /// <inheritdoc />
        public TechnologyDefinition GetNextTechnology(int empireId, ResearchDomain domain)
        {
            if (!_catalogByDomain.TryGetValue(domain, out List<TechnologyDefinition> tiers))
            {
                return null;
            }

            int nextTier = GetCompletedTierCount(empireId, domain) + 1;
            return tiers.Find(t => t.Tier == nextTier);
        }

        /// <inheritdoc />
        public float GetBonus(int empireId, ResearchDomain domain)
        {
            if (!_catalogByDomain.TryGetValue(domain, out List<TechnologyDefinition> tiers))
            {
                return 0f;
            }

            int completed = GetCompletedTierCount(empireId, domain);
            float total = 0f;
            foreach (TechnologyDefinition technology in tiers)
            {
                if (technology.Tier <= completed)
                {
                    total += technology.EffectMagnitude;
                }
            }

            return total;
        }

        /// <inheritdoc />
        public bool TrySetActiveDomain(int empireId, ResearchDomain domain, out string error)
        {
            if (GetNextTechnology(empireId, domain) == null)
            {
                error = "Ce domaine est deja recherche au maximum.";
                return false;
            }

            if (_activeDomainByEmpire.TryGetValue(empireId, out ResearchDomain current) && current == domain)
            {
                error = "Ce domaine est deja actif.";
                return false;
            }

            _activeDomainByEmpire[empireId] = domain;
            _eventBus.Publish(new ActiveDomainChangedEvent(empireId, domain));

            error = null;
            return true;
        }

        /// <inheritdoc />
        public void GrantTier(int empireId, ResearchDomain domain)
        {
            TechnologyDefinition next = GetNextTechnology(empireId, domain);
            if (next == null)
            {
                return;
            }

            _completedTiers[(empireId, domain)] = GetCompletedTierCount(empireId, domain) + 1;
            _eventBus.Publish(new TechnologyResearchedEvent(empireId, next));
        }

        private void OnDayAdvanced(DayAdvancedEvent dayAdvancedEvent)
        {
            var pointsByEmpire = new Dictionary<int, float>();

            foreach (StarSystemState system in _map.Systems)
            {
                if (system.OwnerId == StarSystemState.UnownedOwnerId)
                {
                    continue;
                }

                float production = (system.Population * ResearchPerPopulationPoint + system.DevelopmentLevel * ResearchPerDevelopmentPoint) * system.Stability;
                pointsByEmpire[system.OwnerId] = pointsByEmpire.TryGetValue(system.OwnerId, out float accumulated) ? accumulated + production : production;
            }

            foreach (KeyValuePair<int, float> entry in pointsByEmpire)
            {
                CreditActiveDomain(entry.Key, entry.Value);
            }
        }

        private void CreditActiveDomain(int empireId, float points)
        {
            if (!_activeDomainByEmpire.TryGetValue(empireId, out ResearchDomain domain))
            {
                return;
            }

            TechnologyDefinition next = GetNextTechnology(empireId, domain);
            if (next == null)
            {
                // Domaine deja au maximum : les points du jour ne sont pas conserves (voir commentaire de classe).
                return;
            }

            float progress = GetProgress(empireId, domain) + points;

            while (next != null && progress >= next.ResearchPointCost)
            {
                progress -= next.ResearchPointCost;
                _completedTiers[(empireId, domain)] = GetCompletedTierCount(empireId, domain) + 1;
                _eventBus.Publish(new TechnologyResearchedEvent(empireId, next));
                next = GetNextTechnology(empireId, domain);
            }

            _progress[(empireId, domain)] = next == null ? 0f : progress;
        }
    }
}
