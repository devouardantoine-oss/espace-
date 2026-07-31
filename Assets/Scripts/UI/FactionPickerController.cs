using Espace.Core;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Save;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Ecran de choix affiche entre « Nouvelle partie » et le chargement de la galaxie
    /// (Phase 13) : le joueur choisit sa faction, puis son systeme de depart parmi les
    /// emplacements que l'algorithme de placement lui proposerait.
    /// <para>
    /// <b>Meme pattern de coordination que la Phase 11</b> (<c>HudController</c> bascule
    /// <c>ManagementWindowController</c>/<c>PauseMenuController</c> via <c>GetComponent</c> sur
    /// le meme GameObject) : ce composant vit sur le meme GameObject <c>[UI]</c> que
    /// <see cref="MainMenuController"/> dans la scene <c>Bootstrap</c>, qui se tait tant que
    /// <see cref="IsOpen"/> est vrai.
    /// </para>
    /// <para>
    /// <b>La galaxie « apercu » est regeneree ici, puis jetee :</b> grace a la graine fixe
    /// (<see cref="GalaxyConfig"/>, Phase 10), regenerer avec les memes parametres produit une
    /// galaxie strictement identique a celle que <c>GalaxyMapController.Awake</c> generera pour
    /// de vrai dans la scene <c>GalaxyMap</c> — inutile de faire transiter la carte elle-meme
    /// entre les deux scenes, seul le choix du joueur traverse (voir <see cref="PendingGameSetup"/>).
    /// </para>
    /// </summary>
    public sealed class FactionPickerController : MonoBehaviour
    {
        [Tooltip("Meme roster que le GameObject [Empires] de la scene GalaxyMap.")]
        [SerializeField]
        private EmpireDefinition[] empireDefinitions = System.Array.Empty<EmpireDefinition>();

        [Tooltip("Meme asset que le champ 'config' de GalaxyMapController : necessaire pour lister les emplacements de depart possibles.")]
        [SerializeField]
        private GalaxyConfig galaxyConfig;

        private const int PanelWidth = 420;
        private const int PanelHeight = 440;
        private const string GalaxyMapSceneName = "GalaxyMap";

        private enum Step
        {
            Faction,
            System
        }

        private bool _isOpen;
        private Step _step;
        private EmpireDefinition _chosenFaction;
        private string[] _candidateSystemNames;
        private Vector2 _scroll;

        /// <summary>Vrai tant que cet ecran doit s'afficher (et que <see cref="MainMenuController"/> doit se taire).</summary>
        public bool IsOpen => _isOpen;

        /// <summary>Ouvre l'ecran sur la premiere etape (choix de faction). Appele par <see cref="MainMenuController"/>.</summary>
        public void Open()
        {
            if (empireDefinitions.Length == 0 || galaxyConfig == null)
            {
                GameLog.Error("[FactionPicker] EmpireDefinitions ou GalaxyConfig non assignes : ecran indisponible.");
                return;
            }

            _isOpen = true;
            _step = Step.Faction;
            _chosenFaction = null;
            _candidateSystemNames = null;
            _scroll = Vector2.zero;
        }

        private void OnGUI()
        {
            UITheme.BeginScaledLayout();
            try
            {
                if (!_isOpen)
                {
                    return;
                }

                var rect = new Rect((UITheme.ScreenWidth - PanelWidth) / 2f, (UITheme.ScreenHeight - PanelHeight) / 2f, PanelWidth, PanelHeight);
                GUI.Box(rect, string.Empty, UITheme.Panel);

                GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 10, PanelWidth - 24, PanelHeight - 20));
                _scroll = GUILayout.BeginScrollView(_scroll);

                switch (_step)
                {
                    case Step.Faction:
                        DrawFactionStep();
                        break;
                    case Step.System:
                        DrawSystemStep();
                        break;
                }

                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        private void DrawFactionStep()
        {
            GUILayout.Label("Choisissez votre faction", UITheme.Title);
            GUILayout.Space(8);

            foreach (EmpireDefinition definition in empireDefinitions)
            {
                if (definition == null)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Box(UITheme.SolidTexture(definition.Color), GUILayout.Width(28), GUILayout.Height(28));
                if (GUILayout.Button($"{definition.DisplayName}   —   {definition.Personality}", UITheme.Button, GUILayout.Height(32)))
                {
                    SelectFaction(definition);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(12);
            if (GUILayout.Button("Retour", UITheme.Button, GUILayout.Height(28)))
            {
                _isOpen = false;
            }
        }

        private void DrawSystemStep()
        {
            GUILayout.Label($"Systeme de depart de {_chosenFaction.DisplayName}", UITheme.Title);
            GUILayout.Space(8);

            if (_candidateSystemNames == null)
            {
                GUILayout.Label("Calcul des emplacements possibles...", UITheme.MutedLabel);
            }
            else
            {
                for (int i = 0; i < _candidateSystemNames.Length; i++)
                {
                    if (GUILayout.Button(_candidateSystemNames[i], UITheme.Button, GUILayout.Height(32)))
                    {
                        Confirm(i);
                        return;
                    }
                }
            }

            GUILayout.Space(12);
            if (GUILayout.Button("Retour", UITheme.Button, GUILayout.Height(28)))
            {
                _step = Step.Faction;
            }
        }

        private void SelectFaction(EmpireDefinition definition)
        {
            _chosenFaction = definition;
            _candidateSystemNames = ComputeCandidateSystemNames();
            _step = Step.System;
            _scroll = Vector2.zero;
        }

        /// <summary>
        /// Regenere une galaxie jetable (voir la remarque de la classe) uniquement pour en lire
        /// les noms des emplacements de depart candidats.
        /// </summary>
        private string[] ComputeCandidateSystemNames()
        {
            GalaxyMap previewMap = GalaxyGenerator.Generate(galaxyConfig.ToGenerationParameters());
            StarSystemId[] candidateSlots = EmpirePlacement.ChooseHomeSystems(previewMap, empireDefinitions.Length);

            var names = new string[candidateSlots.Length];
            for (int i = 0; i < candidateSlots.Length; i++)
            {
                names[i] = previewMap.GetSystem(candidateSlots[i]).Name;
            }

            return names;
        }

        private void Confirm(int homeSystemSlotIndex)
        {
            SaveFileLocator.DeleteIfExists();

            if (ServiceLocator.TryGet(out IGameClock gameClock))
            {
                gameClock.ResetToStart();
            }

            if (ServiceLocator.IsRegistered<PendingGameSetup>())
            {
                ServiceLocator.Unregister<PendingGameSetup>();
            }
            ServiceLocator.Register(new PendingGameSetup(_chosenFaction, homeSystemSlotIndex));

            _isOpen = false;

            if (!ServiceLocator.TryGet(out ISceneLoader sceneLoader))
            {
                GameLog.Error("[FactionPicker] ISceneLoader indisponible : impossible de charger la galaxie.");
                return;
            }

            sceneLoader.LoadScene(GalaxyMapSceneName);
        }
    }
}
