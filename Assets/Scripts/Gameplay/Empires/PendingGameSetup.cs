using System;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Choix du joueur (faction, emplacement de depart) fait dans l'ecran de selection
    /// (Phase 13, <c>Espace.UI.FactionPickerController</c>), transporte de la scene
    /// <c>Bootstrap</c> vers la scene <c>GalaxyMap</c> via le <see cref="Core.ServiceLocator"/>.
    /// <para>
    /// <b>Pourquoi le <c>ServiceLocator</c> plutot qu'un nouveau mecanisme de transport
    /// inter-scenes ?</b> Il n'est vide qu'une seule fois, au tout premier <c>Awake</c> de
    /// <c>GameBootstrap</c> (marque <c>DontDestroyOnLoad</c>) — un objet enregistre juste
    /// avant <c>ISceneLoader.LoadScene</c> reste donc lisible par <c>EmpireController.Start</c>
    /// dans la nouvelle scene sans rien inventer de plus.
    /// </para>
    /// <para>
    /// <b>Consomme une seule fois :</b> <c>EmpireController</c> le desenregistre juste apres
    /// lecture — chaque nouvelle partie passee par l'ecran de choix ecrit sa propre instance
    /// fraiche ; « Continuer » (qui ne passe pas par cet ecran) ne le trouve simplement jamais
    /// enregistre, et <c>EmpireController</c> retombe alors sur le comportement par defaut.
    /// </para>
    /// </summary>
    public sealed class PendingGameSetup
    {
        /// <summary>Definition choisie par le joueur pour l'incarner (n'importe laquelle du roster, pas forcement celle marquee <see cref="EmpireDefinition.IsPlayerControlled"/>).</summary>
        public EmpireDefinition PlayerDefinition { get; }

        /// <summary>
        /// Index (parmi les emplacements candidats retournes par <see cref="EmpirePlacement.ChooseHomeSystems"/>)
        /// que le joueur a choisi comme systeme de depart.
        /// </summary>
        public int HomeSystemSlotIndex { get; }

        public PendingGameSetup(EmpireDefinition playerDefinition, int homeSystemSlotIndex)
        {
            PlayerDefinition = playerDefinition ?? throw new ArgumentNullException(nameof(playerDefinition));
            HomeSystemSlotIndex = homeSystemSlotIndex;
        }
    }
}
