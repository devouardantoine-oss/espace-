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
    /// <para>
    /// <b>Choix du joueur (Phase 13) :</b> si <c>Espace.UI.FactionPickerController</c> a
    /// enregistre un <see cref="PendingGameSetup"/> avant le chargement de cette scene (bouton
    /// « Nouvelle partie »), la faction et l'emplacement de depart choisis remplacent le
    /// comportement par defaut. Sans lui (ex. « Continuer », qui ne repasse pas par l'ecran de
    /// choix), le comportement d'avant la Phase 13 est inchange.
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

            PendingGameSetup pendingSetup = ConsumePendingGameSetup();

            Empire[] empires = EmpireFactory.CreateEmpires(empireDefinitions, pendingSetup?.PlayerDefinition);
            StarSystemId[] candidateSlots = EmpirePlacement.ChooseHomeSystems(map, empires.Length);
            StarSystemId[] homeSystems = EmpirePlacement.AssignHomeSystems(candidateSlots, pendingSetup?.HomeSystemSlotIndex);

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

        /// <summary>
        /// Lit puis desenregistre le <see cref="PendingGameSetup"/> laisse par l'ecran de choix
        /// (Phase 13), s'il existe. A usage unique : une nouvelle partie qui repasse par cet
        /// ecran enregistre sa propre instance fraiche avant de recharger cette scene.
        /// </summary>
        private static PendingGameSetup ConsumePendingGameSetup()
        {
            if (!ServiceLocator.TryGet(out PendingGameSetup pendingSetup))
            {
                return null;
            }

            ServiceLocator.Unregister<PendingGameSetup>();
            return pendingSetup;
        }
    }
}
