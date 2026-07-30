using Espace.Core;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Compose les empires de la partie dans la scene <c>GalaxyMap</c> : les cree depuis
    /// <see cref="EmpireDefinition"/>, leur attribue un systeme d'origine, publie
    /// <see cref="EmpireRegistry"/>.
    /// <para>
    /// <b>Resolution dans <c>Start</c>, pas <c>Awake</c> :</b> meme raisonnement que
    /// <c>EconomyController</c> — a besoin de la <see cref="GalaxyMap"/> generee par
    /// <c>GalaxyMapController.Awake</c>, et tous les <c>Awake</c> de la frame sont garantis
    /// termines avant le premier <c>Start</c>.
    /// </para>
    /// <para>
    /// Remplace l'attribution du systeme du joueur qui vivait dans
    /// <c>EconomyController</c> depuis la Phase 4 : desormais generalisee a tous les
    /// empires via <see cref="EmpirePlacement"/>, avec le meme systeme d'origine pour le
    /// joueur (le plus proche du centre) qu'auparavant.
    /// </para>
    /// </summary>
    public sealed class EmpireController : MonoBehaviour
    {
        [Tooltip("Roster des empires de la partie. Une definition doit avoir 'isPlayerControlled' coche.")]
        [SerializeField]
        private EmpireDefinition[] empireDefinitions = System.Array.Empty<EmpireDefinition>();

        private void Start()
        {
            if (!ServiceLocator.TryGet(out GalaxyMap map))
            {
                GameLog.Error("[EmpireController] GalaxyMap indisponible : les empires ne peuvent pas etre crees.");
                return;
            }

            if (empireDefinitions.Length == 0)
            {
                GameLog.Error("[EmpireController] Aucune EmpireDefinition assignee.");
                return;
            }

            if (ServiceLocator.IsRegistered<EmpireRegistry>())
            {
                // Deja fait (rechargement de la meme GalaxyMap, par exemple).
                return;
            }

            Empire[] empires = EmpireFactory.CreateEmpires(empireDefinitions);
            StarSystemId[] homeSystems = EmpirePlacement.ChooseHomeSystems(map, empires.Length);

            for (int i = 0; i < empires.Length; i++)
            {
                StarSystemState homeSystem = map.GetSystem(homeSystems[i]);
                homeSystem.OwnerId = empires[i].Id;
                GameLog.Info($"[Empires] {empires[i].Name} ({(empires[i].IsPlayerControlled ? "joueur" : empires[i].Personality.ToString())}) : systeme d'origine {homeSystem.Name}.");
            }

            var registry = new EmpireRegistry(empires);
            ServiceLocator.Register(registry);

            GameLog.Info($"[Empires] {empires.Length} empires crees.");
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<EmpireRegistry>();
        }
    }
}
