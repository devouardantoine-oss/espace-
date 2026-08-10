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

        /// <summary>Marge de l'ecran de faction, qui occupe toute la surface (Phase 21.3).</summary>
        private const float Margin = 20f;

        private const float TabHeight = 28f;
        private const float TabGap = 4f;
        private const float ConfirmHeight = 34f;
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

        /// <summary>Faction mise en avant a l'ecran. Distincte de <see cref="_chosenFaction"/>, qui n'est renseignee qu'a la validation.</summary>
        private int _highlighted;

        /// <summary>Palette affichee, qui rejoint progressivement celle de la faction mise en avant.</summary>
        private FactionPalette _palette = FactionPalette.Neutral;

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
            _highlighted = DefaultHighlightedIndex();
            _palette = PaletteOf(_highlighted);
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

                // L'ecran de faction occupe tout l'ecran et peint son propre fond (Phase 21.3) ;
                // l'etape systeme garde le panneau centre a ascenseur jusqu'a sa refonte.
                if (_step == Step.Faction)
                {
                    DrawFactionStep();
                    return;
                }

                var rect = new Rect((UITheme.ScreenWidth - PanelWidth) / 2f, (UITheme.ScreenHeight - PanelHeight) / 2f, PanelWidth, PanelHeight);
                UiScreenRegions.Occupy(rect, UITheme.Scale);
                GUI.Box(rect, string.Empty, UITheme.Panel);

                GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 10, PanelWidth - 24, PanelHeight - 20));
                _scroll = GUILayout.BeginScrollView(_scroll);

                DrawSystemStep();

                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        /// <summary>
        /// Choix de la civilisation, concept « prise de controle » (Phase 21.3).
        /// <para>
        /// <b>Toute la palette de l'ecran bascule</b> avec l'onglet : fond, traits, barres,
        /// bouton, embleme. C'est ce basculement complet — et non un portrait — qui produit la
        /// sensation de changer de civilisation. Il a l'avantage decisif d'etre entierement
        /// derivable d'une seule couleur (voir <see cref="FactionPalette"/>), donc de valoir
        /// aussi pour la septieme faction que personne n'a encore ecrite.
        /// </para>
        /// <para>
        /// <b>Dispose en rectangles calcules, pas en <c>GUILayout</c> :</b> l'ecran doit tenir
        /// sur 700 x 286 unites dans le pire cas, et <c>GUILayout</c> deborde sans rien
        /// signaler. Meme lecon que la barre d'etat en Phase 20.
        /// </para>
        /// </summary>
        private void DrawFactionStep()
        {
            float w = UITheme.ScreenWidth;
            float h = UITheme.ScreenHeight;

            // Le fond teinte occupe tout l'ecran : c'est lui qui porte l'essentiel de l'effet.
            GUI.DrawTexture(new Rect(0f, 0f, w, h), UITheme.SolidTexture(_palette.Background));
            UiScreenRegions.Occupy(new Rect(0f, 0f, w, h), UITheme.Scale);

            DrawEmblemWatermark(w, h);
            DrawFactionTabs(w);

            float contentTop = Margin + TabHeight + 16f;
            float confirmTop = h - Margin - ConfirmHeight;
            float contentBottom = confirmTop - 12f;

            EmpireDefinition definition = HighlightedDefinition();
            if (definition != null)
            {
                float columnGap = 24f;
                float leftWidth = (w - Margin * 2f - columnGap) * 0.46f;

                DrawIdentity(new Rect(Margin, contentTop, leftWidth, contentBottom - contentTop), definition);
                DrawDoctrine(
                    new Rect(Margin + leftWidth + columnGap, contentTop, w - Margin * 2f - leftWidth - columnGap, contentBottom - contentTop),
                    definition);
            }

            DrawFooter(w, confirmTop, definition);
        }

        /// <summary>
        /// Embleme geant en filigrane, cale a gauche et deborde volontairement du cadre : un
        /// motif entier et centre se lirait comme un logo, un motif recadre se lit comme une
        /// texture de fond.
        /// </summary>
        private void DrawEmblemWatermark(float w, float h)
        {
            EmpireDefinition definition = HighlightedDefinition();
            if (definition == null)
            {
                return;
            }

            float size = h * 1.15f;
            var rect = new Rect(-size * 0.18f, h * 0.5f - size * 0.5f, size, size);

            Color previous = GUI.color;
            GUI.color = _palette.AccentAt(0.10f);
            GUI.DrawTexture(rect, FactionEmblemFactory.GetTexture(definition.Emblem), ScaleMode.ScaleToFit, alphaBlend: true);
            GUI.color = previous;
        }

        /// <summary>
        /// Les six civilisations en onglets, chacune soulignee de sa propre couleur.
        /// <para>
        /// <b>Toutes visibles en permanence</b> plutot qu'une liste a derouler : c'est ce qui
        /// permet de comparer en tapotant, et le seul moyen de faire sentir qu'on choisit
        /// <em>parmi</em> des civilisations plutot qu'on en configure une.
        /// </para>
        /// </summary>
        private void DrawFactionTabs(float w)
        {
            int count = empireDefinitions.Length;
            if (count == 0)
            {
                return;
            }

            float available = w - Margin * 2f - (count - 1) * TabGap;
            float tabWidth = available / count;

            for (int i = 0; i < count; i++)
            {
                EmpireDefinition definition = empireDefinitions[i];
                if (definition == null)
                {
                    continue;
                }

                var rect = new Rect(Margin + i * (tabWidth + TabGap), Margin, tabWidth, TabHeight);
                bool active = i == _highlighted;
                FactionPalette own = FactionPalette.FromAccent(definition.Color);

                var style = new GUIStyle(UITheme.Button)
                {
                    fontSize = UITheme.CaptionFontSize,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(4, 4, 0, 0),
                    normal =
                    {
                        background = UITheme.SolidTexture(active ? own.AccentAt(0.20f) : new Color(1f, 1f, 1f, 0.03f)),
                        textColor = active ? own.Text : own.TextMuted,
                    },
                };

                if (GUI.Button(rect, ShortNameOf(definition), style))
                {
                    _highlighted = i;
                }

                // Le souligne garde sa couleur meme sur un onglet inactif : les six teintes
                // restent lisibles d'un coup d'oeil, ce qui est la moitie de l'information.
                GUI.DrawTexture(
                    new Rect(rect.x, rect.yMax - 2f, rect.width, 2f),
                    UITheme.SolidTexture(active ? own.Accent : own.AccentAt(0.45f)));
            }
        }

        /// <summary>Espece, nom, devise, description.</summary>
        private void DrawIdentity(Rect area, EmpireDefinition definition)
        {
            float y = area.y;

            var species = new GUIStyle(UITheme.Caption) { normal = { textColor = _palette.Accent } };
            GUI.Label(new Rect(area.x, y, area.width, UITheme.CaptionHeight), definition.SpeciesLine.ToUpperInvariant(), species);
            y += UITheme.CaptionHeight + 4f;

            var title = new GUIStyle(UITheme.Title)
            {
                fontSize = 24,
                normal = { textColor = _palette.Text },
            };
            GUI.Label(new Rect(area.x, y, area.width, 28f), definition.DisplayName.ToUpperInvariant(), title);
            y += 32f;

            if (!string.IsNullOrEmpty(definition.Motto))
            {
                var motto = new GUIStyle(UITheme.MutedLabel)
                {
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = _palette.TextMuted },
                };

                float height = motto.CalcHeight(new GUIContent($"« {definition.Motto} »"), area.width - 12f);
                GUI.DrawTexture(new Rect(area.x, y, 2f, height), UITheme.SolidTexture(_palette.AccentAt(0.55f)));
                GUI.Label(new Rect(area.x + 12f, y, area.width - 12f, height), $"« {definition.Motto} »", motto);
                y += height + 12f;
            }

            // La description est le premier element sacrifie quand la place manque : elle est
            // agreable a lire, mais la doctrine et les bonus sont ce sur quoi on decide.
            if (!string.IsNullOrEmpty(definition.Description) && y < area.yMax - 24f)
            {
                var body = new GUIStyle(UITheme.MutedLabel) { normal = { textColor = _palette.TextMuted } };
                GUI.Label(new Rect(area.x, y, area.width, area.yMax - y), definition.Description, body);
            }
        }

        /// <summary>Les quatre axes de doctrine, puis la force et la faiblesse.</summary>
        private void DrawDoctrine(Rect area, EmpireDefinition definition)
        {
            float y = area.y;

            var caption = new GUIStyle(UITheme.Caption) { normal = { textColor = _palette.Accent } };
            GUI.Label(new Rect(area.x, y, area.width, UITheme.CaptionHeight), "DOCTRINE", caption);
            y += UITheme.CaptionHeight + 4f;

            DrawDoctrineBar(area, ref y, "EXPANSION", definition.Expansion);
            DrawDoctrineBar(area, ref y, "INDUSTRIE", definition.Industry);
            DrawDoctrineBar(area, ref y, "TECHNOLOGIE", definition.Technology);
            DrawDoctrineBar(area, ref y, "DIPLOMATIE", definition.Diplomacy);

            y += 4f;
            UITheme.DrawHairline(new Rect(area.x, y, area.width, 1f), _palette.AccentAt(0.30f));
            y += 8f;

            DrawTrait(area, ref y, "+", definition.StrengthLine, _palette.Text);
            DrawTrait(area, ref y, "\u2212", definition.WeaknessLine, new Color(0.90f, 0.62f, 0.58f, 1f));
        }

        private void DrawDoctrineBar(Rect area, ref float y, string label, float value)
        {
            const float labelWidth = 84f;
            const float barHeight = 4f;

            var caption = new GUIStyle(UITheme.Caption) { normal = { textColor = _palette.TextMuted } };
            GUI.Label(new Rect(area.x, y, labelWidth, UITheme.CaptionHeight), label, caption);

            float barX = area.x + labelWidth + 8f;
            float barWidth = Mathf.Max(20f, area.xMax - barX);
            float barY = y + (UITheme.CaptionHeight - barHeight) * 0.5f;

            GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), UITheme.SolidTexture(_palette.AccentAt(0.16f)));
            GUI.DrawTexture(new Rect(barX, barY, barWidth * Mathf.Clamp01(value), barHeight), UITheme.SolidTexture(_palette.Accent));

            y += UITheme.CaptionHeight + 5f;
        }

        /// <summary>
        /// Atout ou contrepartie, sur une seule ligne prefixee.
        /// <para>
        /// <b>Un signe plutot qu'un intitule.</b> Un « ATOUT » / « CONTREPARTIE » au-dessus de
        /// chaque ligne coutait trente unites de hauteur, et sur l'ecran le plus court que
        /// l'interface garantisse la contrepartie se retrouvait rognee. Le signe et la couleur
        /// portent la meme information pour douze unites de large.
        /// </para>
        /// </summary>
        private void DrawTrait(Rect area, ref float y, string sign, string text, Color textColor)
        {
            if (string.IsNullOrEmpty(text) || y >= area.yMax - 8f)
            {
                return;
            }

            const float signWidth = 12f;

            var marker = new GUIStyle(UITheme.Value)
            {
                fontSize = UITheme.CaptionFontSize + 1,
                normal = { textColor = textColor },
            };
            GUI.Label(new Rect(area.x, y, signWidth, UITheme.CaptionHeight), sign, marker);

            var body = new GUIStyle(UITheme.Label)
            {
                fontSize = UITheme.CaptionFontSize + 1,
                normal = { textColor = textColor },
            };

            float width = area.width - signWidth;
            float height = Mathf.Min(body.CalcHeight(new GUIContent(text), width), area.yMax - y);
            GUI.Label(new Rect(area.x + signWidth, y, width, height), text, body);
            y += height + 6f;
        }

        /// <summary>« Retour » a gauche, validation a droite : le geste de sortie ne doit jamais tomber sous le pouce qui valide.</summary>
        private void DrawFooter(float w, float y, EmpireDefinition definition)
        {
            var back = new GUIStyle(UITheme.Button)
            {
                fontSize = UITheme.CaptionFontSize,
                normal =
                {
                    background = UITheme.SolidTexture(new Color(1f, 1f, 1f, 0.04f)),
                    textColor = _palette.TextMuted,
                },
            };

            if (GUI.Button(new Rect(Margin, y, 96f, ConfirmHeight), "RETOUR", back))
            {
                _isOpen = false;
            }

            if (definition == null)
            {
                return;
            }

            var confirm = new GUIStyle(UITheme.Button)
            {
                fontSize = UITheme.ValueFontSize,
                normal =
                {
                    background = UITheme.SolidTexture(_palette.AccentAt(0.24f)),
                    textColor = _palette.Text,
                },
            };

            const float confirmWidth = 236f;
            var rect = new Rect(w - Margin - confirmWidth, y, confirmWidth, ConfirmHeight);

            if (GUI.Button(rect, $"REJOINDRE — {ShortNameOf(definition).ToUpperInvariant()}", confirm))
            {
                SelectFaction(definition);
            }

            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), UITheme.SolidTexture(_palette.Accent));
        }

        /// <summary>
        /// Premier mot significatif du nom, pour les onglets : six noms complets ne tiennent pas
        /// sur 700 unites, et « Essaim » identifie aussi surement que « Essaim de Kethra ».
        /// </summary>
        private static string ShortNameOf(EmpireDefinition definition)
        {
            string name = definition.DisplayName;
            if (string.IsNullOrEmpty(name))
            {
                return "?";
            }

            int space = name.IndexOf(' ');
            return space > 0 ? name.Substring(0, space) : name;
        }

        private EmpireDefinition HighlightedDefinition()
        {
            return _highlighted >= 0 && _highlighted < empireDefinitions.Length
                ? empireDefinitions[_highlighted]
                : null;
        }

        private FactionPalette PaletteOf(int index)
        {
            EmpireDefinition definition = index >= 0 && index < empireDefinitions.Length ? empireDefinitions[index] : null;
            return definition != null ? FactionPalette.FromAccent(definition.Color) : FactionPalette.Neutral;
        }

        /// <summary>
        /// Ouvre sur la faction marquee « joueur » plutot que sur la premiere du tableau : c'est
        /// celle que le roster designe comme le point de depart attendu.
        /// </summary>
        private int DefaultHighlightedIndex()
        {
            for (int i = 0; i < empireDefinitions.Length; i++)
            {
                if (empireDefinitions[i] != null && empireDefinitions[i].IsPlayerControlled)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// Fait glisser la palette vers celle de la faction mise en avant.
        /// <para>
        /// <b>Dans <c>Update</c>, pas dans <c>OnGUI</c> :</b> IMGUI appelle <c>OnGUI</c>
        /// plusieurs fois par frame (mise en page puis dessin), et une animation avancee la
        /// tournerait deux fois plus vite que prevu.
        /// </para>
        /// </summary>
        private void Update()
        {
            if (!_isOpen)
            {
                return;
            }

            _palette = _palette.Approach(PaletteOf(_highlighted), UiEasing.PaletteSmoothing, Time.unscaledDeltaTime);
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
