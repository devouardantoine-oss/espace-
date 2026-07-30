using Espace.Core;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Research
{
    /// <summary>
    /// Compose et demarre la recherche dans la scene <c>GalaxyMap</c>.
    /// <para>
    /// <b>Resolution des dependances dans <c>Start</c>, pas <c>Update</c> :</b> comme
    /// <c>EconomyController</c>, ce composant n'a besoin que de la <see cref="GalaxyMap"/>
    /// (generee dans l'<c>Awake</c> de <c>GalaxyMapController</c>) et des services du socle
    /// (<see cref="IEventBus"/>, enregistres avant meme la scene <c>GalaxyMap</c>) — aucune
    /// dependance a un autre <c>Start</c> de cette scene, donc pas besoin du sondage par
    /// <c>Update</c> qu'exige <c>MilitaryController</c> (qui, lui, depend d'<c>EmpireRegistry</c>
    /// et d'<see cref="Espace.Gameplay.Economy.IEconomyService"/>).
    /// </para>
    /// <para>
    /// <b>Aucune dependance en retour depuis <c>ResearchService</c> :</b> les services qui
    /// consomment les bonus de recherche (<c>EconomyService</c>, <c>MilitaryService</c>,
    /// <c>DiplomacyService</c>) resolvent <see cref="IResearchService"/> paresseusement via
    /// <see cref="ServiceLocator"/> au moment ou ils en ont besoin plutot que de le recevoir
    /// au constructeur — ce composant peut donc s'initialiser sans se soucier de l'ordre des
    /// autres controleurs de la scene, et eux non plus.
    /// </para>
    /// </summary>
    public sealed class ResearchController : MonoBehaviour
    {
        [Tooltip("Catalogue des paliers de recherche, tous domaines confondus.")]
        [SerializeField]
        private TechnologyDefinition[] catalog = System.Array.Empty<TechnologyDefinition>();

        private ResearchService _researchService;

        private void Start()
        {
            if (!ServiceLocator.TryGet(out IEventBus eventBus) ||
                !ServiceLocator.TryGet(out GalaxyMap map))
            {
                GameLog.Error("[ResearchController] Dependances manquantes (IEventBus / GalaxyMap) : la recherche ne demarre pas.");
                return;
            }

            _researchService = new ResearchService(map, eventBus, catalog);
            _researchService.Initialize();

            if (!ServiceLocator.IsRegistered<IResearchService>())
            {
                ServiceLocator.Register<IResearchService>(_researchService);
            }

            GameLog.Info($"[Research] Demarree avec {catalog.Length} paliers de recherche disponibles.");
        }

        private void OnDestroy()
        {
            if (_researchService == null)
            {
                return;
            }

            _researchService.Shutdown();
            ServiceLocator.Unregister<IResearchService>();
            _researchService = null;
        }
    }
}
