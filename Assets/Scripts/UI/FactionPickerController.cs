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

        /// <summary>Part de la largeur occupee par le dossier de monde (Phase 21.4). Le reste revient a la planete.</summary>
        private const float WorldPanelFraction = 0.46f;

        /// <summary>Hauteur reservee aux deux lignes de commandes de l'ecran de monde.</summary>
        private const float WorldFooterHeight = 88f;

        /// <summary>Rayon de la planete, en fraction de la demi-hauteur visible. 0,42 la fait occuper 84 % de la hauteur.</summary>
        private const float PlanetRadiusFraction = 0.42f;

        /// <summary>Position horizontale de la planete, en fraction de la demi-largeur visible.</summary>
        private const float PlanetOffsetFraction = 0.44f;

        /// <summary>Distance de la planete devant la camera. Plus pres que le fond stellaire, qui est a 2.</summary>
        private const float PlanetDistance = 6f;
        private const string GalaxyMapSceneName = "GalaxyMap";

        private enum Step
        {
            Faction,
            System
        }

        private bool _isOpen;
        private Step _step;
        private EmpireDefinition _chosenFaction;
        private GalaxyMap _previewMap;
        private StarSystemId[] _candidateSlots;
        private int _selectedWorld;
        private PlanetVisual _planet;
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
            _candidateSlots = null;
            _selectedWorld = 0;
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

        /// <summary>
        /// Choix du monde d'origine, concept « orbite » (Phase 21.4).
        /// <para>
        /// <b>Un monde a la fois, en grand.</b> L'ancien ecran alignait six noms dans un
        /// ascenseur : le joueur choisissait au hasard, non par negligence mais parce qu'il
        /// n'avait rien pour choisir autrement. Le generateur produisait pourtant deja des
        /// systemes tres differents — cet ecran ne fabrique aucune donnee, il montre celles qui
        /// existaient sans etre lues (voir <see cref="WorldProfile"/>).
        /// </para>
        /// <para>
        /// <b>La planete vit dans la scene, le dossier dans IMGUI</b> : meme partage que partout
        /// ailleurs. Elle est accrochee a la camera pour rester cadree quelle que soit la derive
        /// du fond.
        /// </para>
        /// </summary>
        private void DrawSystemStep()
        {
            float w = UITheme.ScreenWidth;
            float h = UITheme.ScreenHeight;

            UiScreenRegions.Occupy(new Rect(0f, 0f, w * WorldPanelFraction, h), UITheme.Scale);

            // Un voile degrade seulement sur la moitie gauche : le dossier doit se lire sans
            // que la planete, a droite, soit assombrie.
            GUI.DrawTexture(
                new Rect(0f, 0f, w * WorldPanelFraction, h),
                UiTextures.Gradient(_palette.Background, new Color(_palette.Background.r, _palette.Background.g, _palette.Background.b, 0.55f)));

            StarSystemState system = SelectedSystem();
            if (system == null)
            {
                GUI.Label(new Rect(Margin, Margin, w - Margin * 2f, 24f), "Calcul des emplacements possibles...", UITheme.MutedLabel);
                return;
            }

            WorldProfile profile = WorldProfile.Describe(system, _previewMap.GetNeighbors(system.Id).Count);
            float dossierWidth = w * WorldPanelFraction - Margin * 2f;

            DrawWorldDossier(new Rect(Margin, Margin, dossierWidth, h - Margin * 2f - WorldFooterHeight), system, profile);
            DrawWorldFooter(w, h, dossierWidth, system);
        }

        private void DrawWorldDossier(Rect area, StarSystemState system, WorldProfile profile)
        {
            float y = area.y;

            // Le nom et le rang du candidat partagent la meme ligne. Un premier jet les
            // empilait, mais le dossier reclamait alors 209 unites de haut pour 158 disponibles
            // sur un telephone tres dense : trois blocs se faisaient rogner en silence.
            var title = new GUIStyle(UITheme.Title) { fontSize = 24, normal = { textColor = _palette.Text } };
            var rank = new GUIStyle(UITheme.Caption)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = _palette.Accent },
            };

            GUI.Label(new Rect(area.x, y, area.width * 0.66f, 28f), system.Name.ToUpperInvariant(), title);
            GUI.Label(
                new Rect(area.x + area.width * 0.66f, y + 8f, area.width * 0.34f, UITheme.CaptionHeight),
                $"{_selectedWorld + 1:00} / {_candidateSlots.Length:00}",
                rank);
            y += 28f;

            UITheme.DrawHairline(new Rect(area.x, y, area.width, 1f), _palette.AccentAt(0.30f));
            y += 9f;

            // Quatre faits sur une ligne : le regard les prend d'un bloc, alors qu'empiles ils
            // se liraient un par un — et ne tiendraient pas.
            float quarter = area.width / 4f;
            DrawFact(new Rect(area.x, y, quarter, 0f), "TYPE", TypeNameOf(profile.Kind));
            DrawFact(new Rect(area.x + quarter, y, quarter, 0f), "POPULATION", FormatPopulation(system.Population));
            DrawFact(new Rect(area.x + quarter * 2f, y, quarter, 0f), "SECTEUR", profile.SectorName.Replace("Secteur ", string.Empty));
            DrawFact(
                new Rect(area.x + quarter * 3f, y, quarter, 0f),
                "DIFFICULTE",
                profile.Difficulty.ToString().ToUpperInvariant(),
                DifficultyColorOf(profile.Difficulty));
            y += UITheme.CaptionHeight + UITheme.ValueHeight + 10f;

            DrawRating(area, ref y, "METAUX", profile.Metals);
            DrawRating(area, ref y, "ENERGIE", profile.Energy);
            DrawRating(area, ref y, "NOURRITURE", profile.Food);

            // Le nombre de voisins est la premiere chose sacrifiee quand la place manque : la
            // difficulte, affichee plus haut, en resume deja l'essentiel.
            if (y + UITheme.CaptionHeight + UITheme.ValueHeight <= area.yMax)
            {
                y += 6f;
                DrawFact(new Rect(area.x, y, area.width, 0f), "VOISINS DIRECTS", $"{profile.NeighbourCount:00} systemes relies");
            }
        }

        /// <summary>Un intitule et sa valeur, empiles. La hauteur du rectangle est ignoree : seule l'origine compte.</summary>
        private void DrawFact(Rect at, string label, string value, Color? valueColor = null)
        {
            var caption = new GUIStyle(UITheme.Caption) { normal = { textColor = _palette.TextMuted } };
            var body = new GUIStyle(UITheme.Value) { normal = { textColor = valueColor ?? _palette.Text } };

            GUI.Label(new Rect(at.x, at.y, at.width, UITheme.CaptionHeight), label, caption);
            GUI.Label(new Rect(at.x, at.y + UITheme.CaptionHeight, at.width, UITheme.ValueHeight), value, body);
        }

        /// <summary>
        /// Note en carres pleins plutot qu'en barre continue : cinq crans se comptent d'un coup
        /// d'œil, alors qu'une barre demande de comparer des longueurs.
        /// </summary>
        private void DrawRating(Rect area, ref float y, string label, int rating)
        {
            const float labelWidth = 82f;
            const float pipSize = 8f;
            const float pipGap = 4f;

            var caption = new GUIStyle(UITheme.Caption) { normal = { textColor = _palette.TextMuted } };
            GUI.Label(new Rect(area.x, y, labelWidth, UITheme.CaptionHeight), label, caption);

            float x = area.x + labelWidth + 6f;
            float pipY = y + (UITheme.CaptionHeight - pipSize) * 0.5f;

            for (int i = 0; i < WorldProfile.MaximumRating; i++)
            {
                Color color = i < rating ? _palette.Accent : _palette.AccentAt(0.18f);
                GUI.DrawTexture(new Rect(x + i * (pipSize + pipGap), pipY, pipSize, pipSize), UITheme.SolidTexture(color));
            }

            y += UITheme.CaptionHeight + 5f;
        }

        /// <summary>Navigation entre candidats a gauche, validation a droite.</summary>
        private void DrawWorldFooter(float w, float h, float dossierWidth, StarSystemState system)
        {
            float y = h - Margin - ConfirmHeight;

            var arrow = new GUIStyle(UITheme.Button)
            {
                fontSize = UITheme.ValueFontSize,
                normal =
                {
                    background = UITheme.SolidTexture(new Color(1f, 1f, 1f, 0.05f)),
                    textColor = _palette.Text,
                },
            };

            if (GUI.Button(new Rect(Margin, y, 34f, ConfirmHeight), "\u25C0", arrow))
            {
                StepWorld(-1);
            }

            if (GUI.Button(new Rect(Margin + 38f, y, 34f, ConfirmHeight), "\u25B6", arrow))
            {
                StepWorld(1);
            }

            // Points de position : le joueur doit savoir combien de mondes il lui reste a voir
            // avant de trancher, sinon il valide le premier par prudence.
            float dotY = y + ConfirmHeight * 0.5f - 2f;
            for (int i = 0; i < _candidateSlots.Length; i++)
            {
                bool active = i == _selectedWorld;
                GUI.DrawTexture(
                    new Rect(Margin + 82f + i * 12f, dotY, active ? 10f : 5f, 4f),
                    UITheme.SolidTexture(active ? _palette.Accent : _palette.AccentAt(0.35f)));
            }

            var back = new GUIStyle(UITheme.Button)
            {
                fontSize = UITheme.CaptionFontSize,
                normal =
                {
                    background = UITheme.SolidTexture(new Color(1f, 1f, 1f, 0.04f)),
                    textColor = _palette.TextMuted,
                },
            };

            if (GUI.Button(new Rect(Margin, y - ConfirmHeight - 8f, 96f, ConfirmHeight), "RETOUR", back))
            {
                _step = Step.Faction;
                HidePlanet();
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

            // Le bouton de validation occupe la ligne du dessus, avec « Retour » : la ligne du
            // bas est deja prise par les fleches et les points de position, et les faire
            // cohabiter sur 320 unites de large les ferait se chevaucher.
            float confirmWidth = Mathf.Min(252f, dossierWidth - 104f);
            var rect = new Rect(Margin + dossierWidth - confirmWidth, y - ConfirmHeight - 8f, confirmWidth, ConfirmHeight);

            if (GUI.Button(rect, $"ETABLIR LA CAPITALE — {system.Name.ToUpperInvariant()}", confirm))
            {
                Confirm(_selectedWorld);
            }

            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), UITheme.SolidTexture(_palette.Accent));
        }

        private void StepWorld(int delta)
        {
            if (_candidateSlots == null || _candidateSlots.Length == 0)
            {
                return;
            }

            _selectedWorld = (_selectedWorld + delta + _candidateSlots.Length) % _candidateSlots.Length;
            ShowSelectedWorld();
        }

        private StarSystemState SelectedSystem()
        {
            if (_previewMap == null || _candidateSlots == null || _candidateSlots.Length == 0)
            {
                return null;
            }

            return _previewMap.GetSystem(_candidateSlots[Mathf.Clamp(_selectedWorld, 0, _candidateSlots.Length - 1)]);
        }

        /// <summary>
        /// Cree la planete si besoin et lui donne le visage du monde selectionne.
        /// <para>
        /// <b>Accrochee a la camera</b>, pas posee dans le monde : le fond du menu derive
        /// lentement, et une planete laissee en coordonnees absolues sortirait du cadre.
        /// </para>
        /// </summary>
        private void ShowSelectedWorld()
        {
            StarSystemState system = SelectedSystem();
            if (system == null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            if (_planet == null)
            {
                var host = new GameObject("[WorldPreview]");
                host.transform.SetParent(camera.transform, worldPositionStays: false);
                _planet = host.AddComponent<PlanetVisual>();
            }

            _planet.gameObject.SetActive(true);

            WorldProfile profile = WorldProfile.Describe(system, _previewMap.GetNeighbors(system.Id).Count);

            // La graine est l'identifiant du systeme : le meme monde presente toujours le meme
            // visage, ici comme en partie.
            _planet.Show(profile.Kind, system.Id.Value, PlanetRadiusFor(camera));
            FramePlanet(camera);
        }

        private void HidePlanet()
        {
            if (_planet != null)
            {
                _planet.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// La planete est accrochee a la camera, donc detruite avec la scene ; ce nettoyage
        /// couvre le cas ou seul l'objet d'interface disparait, sans changement de scene.
        /// </summary>
        private void OnDestroy()
        {
            if (_planet != null)
            {
                Destroy(_planet.gameObject);
                _planet = null;
            }
        }

        private static float PlanetRadiusFor(Camera camera) => camera.orthographicSize * PlanetRadiusFraction;

        /// <summary>Place la planete dans la moitie droite, hors du dossier.</summary>
        private void FramePlanet(Camera camera)
        {
            if (_planet == null)
            {
                return;
            }

            float halfWidth = camera.orthographicSize * camera.aspect;
            _planet.transform.localPosition = new Vector3(halfWidth * PlanetOffsetFraction, 0f, PlanetDistance);
            _planet.SetRadius(PlanetRadiusFor(camera));
        }

        private static string TypeNameOf(PlanetKind kind)
        {
            switch (kind)
            {
                case PlanetKind.Ocean: return "Oceanique";
                case PlanetKind.Arid: return "Aride";
                case PlanetKind.Ice: return "Glace";
                case PlanetKind.Toxic: return "Toxique";
                case PlanetKind.Barren: return "Sterile";
                default: return "Tellurique";
            }
        }

        private Color DifficultyColorOf(WorldDifficulty difficulty)
        {
            switch (difficulty)
            {
                case WorldDifficulty.Facile: return new Color(0.55f, 0.85f, 0.62f, 1f);
                case WorldDifficulty.Difficile: return new Color(0.90f, 0.62f, 0.58f, 1f);
                default: return new Color(0.92f, 0.82f, 0.45f, 1f);
            }
        }

        /// <summary>Population en milliards, avec une decimale : « 2,4 Md » se lit plus vite que « 2 400 ».</summary>
        private static string FormatPopulation(int population)
        {
            return population >= 1000
                ? $"{population / 1000f:0.0} Md"
                : $"{population} M";
        }

        private void SelectFaction(EmpireDefinition definition)
        {
            _chosenFaction = definition;
            ComputeCandidates();
            _selectedWorld = 0;
            _step = Step.System;
            _scroll = Vector2.zero;
            ShowSelectedWorld();
        }

        /// <summary>
        /// Regenere une galaxie jetable (voir la remarque de la classe) et en retient les
        /// emplacements de depart candidats.
        /// <para>
        /// La carte est <b>conservee</b>, contrairement a la Phase 13 qui n'en gardait que les
        /// noms : l'ecran de monde a besoin des gisements, du developpement et du nombre de
        /// voisins pour dresser un portrait comparable.
        /// </para>
        /// </summary>
        private void ComputeCandidates()
        {
            _previewMap = GalaxyGenerator.Generate(galaxyConfig.ToGenerationParameters());
            _candidateSlots = EmpirePlacement.ChooseHomeSystems(_previewMap, empireDefinitions.Length);
        }

        private void Confirm(int homeSystemSlotIndex)
        {
            HidePlanet();
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
