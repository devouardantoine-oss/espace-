using System.Collections.Generic;
using System.Linq;
using Espace.Core;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using Espace.Gameplay.People;
using Espace.Gameplay.Voies;
using Espace.Gameplay.Research;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Fiche du systeme selectionne : un en-tete permanent (identite et statistiques) et un
    /// corps qui bascule entre trois sections via des onglets places en bas (Phase 20).
    /// <para>
    /// <b>Aucune barre de defilement, nulle part.</b> L'ancienne fiche mesurait 420 unites de
    /// haut sur un ecran qui en offre environ 315 en 20:9 — elle etait structurellement plus
    /// grande que l'ecran, et ses sept boutons de recrutement alignes horizontalement dans une
    /// zone de 344 debordaient de plus du double. La fiche est desormais dimensionnee a partir
    /// de la place reellement disponible, et chaque section dispose de la pleine largeur.
    /// </para>
    /// <para>
    /// <b>Pourquoi des onglets plutot qu'un decoupage en colonnes ?</b> Avec sept champs
    /// d'identite et treize actions, aucune disposition ne fait tenir l'ensemble dans
    /// 620 × 190 sans reduire les cibles tactiles sous le seuil utilisable. Les onglets sont le
    /// seul decoupage qui laisse chaque section respirer <i>et</i> qui accueille une mecanique
    /// future sans redecouper la fenetre : ce sera un onglet de plus.
    /// </para>
    /// <para>
    /// <b>L'en-tete ne bascule jamais.</b> Nom, proprietaire et jauges restent visibles quelle
    /// que soit la section : on ne perd pas de vue quel systeme on est en train de gerer.
    /// </para>
    /// <para>
    /// <b>Disposition en <c>Rect</c> calcules, pas en <c>GUILayout</c> imbrique :</b> IMGUI ne
    /// signale pas un depassement, il le rogne silencieusement. Calculer chaque rectangle rend
    /// le debordement impossible par construction plutot que de l'esperer.
    /// </para>
    /// </summary>
    public sealed class SystemInfoPanelController : MonoBehaviour
    {
        private enum PanelTab
        {
            Overview,
            Economy,
            Military,
        }

        private const int CardWidth = 620;
        private const int MaxCardHeight = 252;
        private const int HeaderHeight = 62;
        private const int TabsHeight = 52;

        /// <summary>Hauteur de la barre d'etat du jeu (voir <c>HudController.BarHeight</c>) : la fiche se centre en dessous.</summary>
        private const int HudBarHeight = 44;

        private const int Margin = 10;
        private const int Padding = 12;
        private const int Gap = 6;

        /// <summary>Bande reservee au message de retour, retiree du corps quand il y en a un.</summary>
        private const int FeedbackHeight = 16;

        /// <summary>Meme borne que <c>EconomyService.MaxDevelopmentLevel</c>, non exposee sur l'interface.</summary>
        private const int MaxDevelopmentLevel = 5;

        /// <summary>Plafond d'unites par flotte, aligne sur <c>MilitaryService.MaxUnitsPerFleet</c>.</summary>
        private const int MaxUnitsPerFleet = 10;

        private IEventBus _eventBus;
        private GalaxyMap _map;
        private EmpireRegistry _empireRegistry;
        private IEconomyService _economy;
        private IMilitaryService _military;
        private IGovernorService _governors;
        private IVoieService _voies;
        private IDiplomacyService _diplomacy;
        private IResearchService _research;

        private StarSystemId? _selectedSystemId;
        private PanelTab _tab = PanelTab.Overview;
        private string _feedback;

        /// <summary>
        /// Flotte dont on attend la destination (Phase 20). <b>Identifiee par son numero, plus
        /// par son systeme d'origine :</b> depuis qu'un systeme peut heberger sa garnison
        /// <i>et</i> des flottes detachees, designer l'origine ne suffit plus a designer la
        /// flotte — l'ancien code deplacait systematiquement la garnison, meme apres un
        /// detachement.
        /// </summary>
        private int? _awaitingDestinationFleetId;

        /// <summary>Composition en cours de saisie, indexee par <see cref="UnitType"/>. Ouvre la feuille de creation de flotte quand elle n'est pas nulle.</summary>
        private int[] _draftComposition;

        /// <summary>Vrai quand l'onglet Armee affiche la grille de recrutement plutot que les flottes sur place.</summary>
        private bool _recruiting;

        /// <summary>Une flotte candidate a une offensive, avec ce que la planification a calcule pour elle.</summary>
        private readonly struct OffensiveCandidate
        {
            public readonly int FleetId;
            public readonly string Label;
            public readonly string Detail;
            public readonly UnitBundle Composition;
            public readonly int TravelDays;
            public readonly bool Reachable;
            public readonly float AttackModifier;

            public OffensiveCandidate(int fleetId, string label, string detail, UnitBundle composition,
                int travelDays, bool reachable, float attackModifier)
            {
                FleetId = fleetId; Label = label; Detail = detail; Composition = composition;
                TravelDays = travelDays; Reachable = reachable; AttackModifier = attackModifier;
            }
        }

        /// <summary>
        /// Candidates calculees pour la cible courante. <b>Mises en cache :</b> chaque candidate
        /// coute un Dijkstra sur cent systemes, et <c>OnGUI</c> est appele au moins deux fois par
        /// frame (mise en page puis dessin). Les recalculer a chaque appel ferait chuter la
        /// fluidite des l'ouverture de l'onglet.
        /// </summary>
        private readonly List<OffensiveCandidate> _offensiveCandidates = new List<OffensiveCandidate>();

        /// <summary>Cible pour laquelle <see cref="_offensiveCandidates"/> a ete calculee, ou <c>null</c> si le cache est vide.</summary>
        private StarSystemId? _offensiveCacheKey;

        private readonly HashSet<int> _engagedFleetIds = new HashSet<int>();
        private bool _offensiveConfirming;

        private void OnEnable()
        {
            if (ServiceLocator.TryGet(out _eventBus))
            {
                _eventBus.Subscribe<SystemSelectedEvent>(OnSystemSelected);
                _eventBus.Subscribe<SystemDeselectedEvent>(OnSystemDeselected);
            }
        }

        private void OnDisable()
        {
            if (_eventBus == null)
            {
                return;
            }

            _eventBus.Unsubscribe<SystemSelectedEvent>(OnSystemSelected);
            _eventBus.Unsubscribe<SystemDeselectedEvent>(OnSystemDeselected);
            _eventBus = null;
        }

        /// <summary>
        /// Le mode « choisir une destination » est consomme <b>avant</b> de reecrire la
        /// selection : c'est ce toucher qui declenche l'ordre, et la flotte visee ne doit pas
        /// avoir ete perdue entre-temps.
        /// </summary>
        private void OnSystemSelected(SystemSelectedEvent selectedEvent)
        {
            if (_awaitingDestinationFleetId.HasValue)
            {
                int fleetId = _awaitingDestinationFleetId.Value;
                _awaitingDestinationFleetId = null;
                ExecuteMoveOrder(fleetId, selectedEvent.SystemId);
            }

            if (!_selectedSystemId.HasValue || !_selectedSystemId.Value.Equals(selectedEvent.SystemId))
            {
                // Changer de systeme reinitialise la section : on revient a l'Apercu, qui est
                // toujours pertinent, plutot que de retomber sur un onglet vide.
                _tab = PanelTab.Overview;
                _draftComposition = null;
                _recruiting = false;
                InvalidateOffensiveCache();
            }

            _selectedSystemId = selectedEvent.SystemId;
        }

        private void OnSystemDeselected(SystemDeselectedEvent deselectedEvent)
        {
            _awaitingDestinationFleetId = null;
            _selectedSystemId = null;
            _draftComposition = null;
            _recruiting = false;
            InvalidateOffensiveCache();
        }

        private void InvalidateOffensiveCache()
        {
            _offensiveCacheKey = null;
            _offensiveCandidates.Clear();
            _engagedFleetIds.Clear();
            _offensiveConfirming = false;
        }

        /// <summary>Envoie la flotte <paramref name="fleetId"/> vers <paramref name="destinationId"/>, en revalidant qu'elle existe toujours.</summary>
        private void ExecuteMoveOrder(int fleetId, StarSystemId destinationId)
        {
            if (_military == null && !ServiceLocator.TryGet(out _military))
            {
                return;
            }

            Fleet fleet = FindPlayerFleet(fleetId);
            if (fleet == null || fleet.Status != FleetStatus.Stationed)
            {
                _feedback = "Ordre annule : cette flotte n'est plus disponible.";
                return;
            }

            if (fleet.CurrentSystemId.Equals(destinationId))
            {
                _feedback = "Ordre annule : la flotte est deja sur ce systeme.";
                return;
            }

            if (_military.TryMoveFleet(fleet, destinationId, out string error))
            {
                string destinationName = _map != null && _map.TryGetSystem(destinationId, out StarSystemState destination)
                    ? destination.Name
                    : destinationId.ToString();
                _feedback = $"{fleet.Name} fait route vers {destinationName}.";
                GameLog.Info($"[Military] {_feedback}");
                return;
            }

            _feedback = error;
            GameLog.Warning($"[Military] {error}");
        }

        private Fleet FindPlayerFleet(int fleetId)
        {
            foreach (Fleet fleet in _military.GetFleetsForEmpire(EconomyService.PlayerOwnerId))
            {
                if (fleet.Id == fleetId)
                {
                    return fleet;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ rendu

        private void OnGUI()
        {
            UITheme.BeginScaledLayout();
            try
            {
                if (!_selectedSystemId.HasValue)
                {
                    return;
                }

                if (_map == null && !ServiceLocator.TryGet(out _map))
                {
                    return;
                }

                if (!_map.TryGetSystem(_selectedSystemId.Value, out StarSystemState system))
                {
                    return;
                }

                if (_empireRegistry == null) ServiceLocator.TryGet(out _empireRegistry);
                if (_economy == null) ServiceLocator.TryGet(out _economy);
                if (_military == null) ServiceLocator.TryGet(out _military);
                if (_diplomacy == null) ServiceLocator.TryGet(out _diplomacy);
                if (_research == null) ServiceLocator.TryGet(out _research);

                DrawCard(system);
            }
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        /// <summary>
        /// Calcule le rectangle de la fiche et dessine ses trois zones.
        /// <para>
        /// La hauteur s'adapte a la place restante sous la barre d'etat plutot que d'etre fixe :
        /// un ecran tres allonge (21:9, 22:9) offre moins de 300 unites de haut, et une valeur
        /// en dur y deborderait — exactement le defaut que cette refonte corrige.
        /// </para>
        /// </summary>
        private void DrawCard(StarSystemState system)
        {
            float available = UITheme.ScreenHeight - HudBarHeight - 2 * Margin;
            float cardHeight = Mathf.Min(MaxCardHeight, available);
            float cardWidth = Mathf.Min(CardWidth, UITheme.ScreenWidth - 2 * Margin);

            float x = (UITheme.ScreenWidth - cardWidth) * 0.5f;
            float y = HudBarHeight + Mathf.Max(Margin, (available - cardHeight) * 0.5f + Margin);

            var card = new Rect(x, y, cardWidth, cardHeight);

            // Declare la fiche comme zone d'interface : IMGUI ne consomme pas les entrees du
            // nouvel Input System, et sans cela un appui sur un onglet atteignait
            // GalaxySelectionController, qui n'y trouvait aucun systeme et refermait le panneau.
            UiScreenRegions.Occupy(card, UITheme.Scale);

            GUI.Box(card, GUIContent.none, UITheme.Panel);

            var headerRect = new Rect(card.x, card.y, card.width, HeaderHeight);
            var tabsRect = new Rect(card.x, card.yMax - TabsHeight, card.width, TabsHeight);
            var bodyRect = new Rect(
                card.x + Padding,
                headerRect.yMax + Gap,
                card.width - 2 * Padding,
                tabsRect.y - headerRect.yMax - 2 * Gap);

            // Le message de retour occupe une bande reservee sous le corps plutot que de se
            // superposer a lui : ecrit par-dessus, il masquerait la derniere ligne d'une liste
            // exactement au moment ou le joueur vient d'agir dessus.
            if (!string.IsNullOrEmpty(_feedback))
            {
                bodyRect.height -= FeedbackHeight;
                GUI.Label(new Rect(bodyRect.x, bodyRect.yMax + 2, bodyRect.width, FeedbackHeight), _feedback, UITheme.MutedLabel);
            }

            // La feuille de composition prend toute la fiche : repartie dans les 125 unites du
            // corps, ses boutons « − / + » tomberaient a 22 unites de haut, moitie moins que le
            // seuil tactile utilisable. Une action de saisie merite l'ecran entier.
            if (_draftComposition != null && system.OwnerId == EconomyService.PlayerOwnerId)
            {
                DrawFleetComposer(new Rect(card.x + Padding, card.y + Padding, card.width - 2 * Padding, card.height - 2 * Padding), system);
                return;
            }

            if (_offensiveConfirming)
            {
                DrawOffensiveConfirmation(new Rect(card.x + Padding, card.y + Padding, card.width - 2 * Padding, card.height - 2 * Padding), system);
                return;
            }

            DrawHeader(headerRect, system);
            DrawBody(bodyRect, system);
            DrawTabs(tabsRect, system);
        }

        // ------------------------------------------------------------------ en-tete

        private void DrawHeader(Rect rect, StarSystemState system)
        {
            GUI.Box(rect, GUIContent.none, UITheme.Header);

            var inner = new Rect(rect.x + Padding, rect.y + 7, rect.width - 2 * Padding, rect.height - 14);

            const float identityWidth = 186f;
            GUI.Label(new Rect(inner.x, inner.y, identityWidth, UITheme.TitleHeight), system.Name, UITheme.Title);
            GUI.Label(new Rect(inner.x, inner.y + UITheme.TitleHeight, identityWidth, UITheme.CaptionHeight),
                OwnerLabel(system.OwnerId), UITheme.Caption);

            // Quatre jauges cote a cote : les statistiques qui decident d'une action se lisent
            // d'un coup d'œil, sans comparer des nombres alignes en colonne.
            float gaugesX = inner.x + identityWidth + Gap;
            float columnWidth = (inner.xMax - gaugesX - 3 * Gap) / 4f;

            DrawGauge(GaugeRect(gaugesX, inner.y, columnWidth, 0),
                "POPULATION", $"{system.Population} M", Mathf.Clamp01(system.Population / 4000f), new Color(0.56f, 0.72f, 0.96f));

            DrawGauge(GaugeRect(gaugesX, inner.y, columnWidth, 1),
                "RICHESSE", $"{system.Wealth}/100", Mathf.Clamp01(system.Wealth / 100f), new Color(0.90f, 0.80f, 0.20f));

            DrawGauge(GaugeRect(gaugesX, inner.y, columnWidth, 2),
                "STABILITE", HudFormatter.FormatPercent(system.Stability), Mathf.Clamp01(system.Stability),
                system.Stability > 0.6f ? UITheme.PositiveColor : UITheme.NegativeColor);

            // « DEVELOPPEMENT » ne tient pas dans une colonne de cette largeur : abrege plutot
            // que tronque en cours de mot, ce qui ressemblerait a un defaut d'affichage.
            DrawPipGauge(GaugeRect(gaugesX, inner.y, columnWidth, 3),
                "DEVELOP.", $"{system.DevelopmentLevel}/{MaxDevelopmentLevel}", system.DevelopmentLevel, MaxDevelopmentLevel);
        }

        private static Rect GaugeRect(float x, float y, float width, int index) =>
            new Rect(x + index * (width + Gap), y, width, UITheme.CaptionHeight + UITheme.ValueHeight + 8);

        /// <summary>Legende, valeur et barre de remplissage : la forme dit l'etat avant meme que le chiffre soit lu.</summary>
        private static void DrawGauge(Rect rect, string key, string value, float fill, Color color)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, UITheme.CaptionHeight), key, UITheme.Caption);
            GUI.Label(new Rect(rect.x, rect.y + UITheme.CaptionHeight, rect.width, UITheme.ValueHeight), value, UITheme.Value);

            var track = new Rect(rect.x, rect.y + UITheme.CaptionHeight + UITheme.ValueHeight + 3, rect.width, 4);
            GUI.DrawTexture(track, UITheme.SolidTexture(new Color(1f, 1f, 1f, 0.10f)));
            GUI.DrawTexture(new Rect(track.x, track.y, track.width * Mathf.Clamp01(fill), track.height), UITheme.SolidTexture(color));
        }

        /// <summary>
        /// Variante a pastilles pour le developpement : c'est une echelle discrete de 0 a 5, pas
        /// une proportion. Une barre continue suggererait des valeurs intermediaires inexistantes.
        /// </summary>
        private static void DrawPipGauge(Rect rect, string key, string value, int filled, int total)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, UITheme.CaptionHeight), key, UITheme.Caption);
            GUI.Label(new Rect(rect.x, rect.y + UITheme.CaptionHeight, rect.width, UITheme.ValueHeight), value, UITheme.Value);

            float pipWidth = (rect.width - (total - 1) * 3f) / total;
            float pipY = rect.y + UITheme.CaptionHeight + UITheme.ValueHeight + 3;
            for (int i = 0; i < total; i++)
            {
                var pip = new Rect(rect.x + i * (pipWidth + 3f), pipY, pipWidth, 4);
                GUI.DrawTexture(pip, UITheme.SolidTexture(i < filled
                    ? UITheme.AccentBackground
                    : new Color(1f, 1f, 1f, 0.10f)));
            }
        }

        // ------------------------------------------------------------------ onglets

        private void DrawTabs(Rect rect, StarSystemState system)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), UITheme.SolidTexture(new Color(1f, 1f, 1f, 0.08f)));

            bool ownedByPlayer = system.OwnerId == EconomyService.PlayerOwnerId;
            bool free = system.OwnerId == StarSystemState.UnownedOwnerId;

            string militaryLabel = ownedByPlayer ? "Armee" : free ? "Coloniser" : "Offensive";
            string economyBadge = ownedByPlayer && _economy != null
                ? $"{RemainingBuildingCount(system)} a construire"
                : null;
            string militaryBadge = ownedByPlayer && _military != null
                ? $"{_military.GetGarrison(system.Id, EconomyService.PlayerOwnerId).TotalCount} unites"
                : null;

            var inner = new Rect(rect.x + 8, rect.y + 5, rect.width - 16, rect.height - 10);
            float tabWidth = (inner.width - 2 * Gap) / 3f;

            DrawTab(new Rect(inner.x, inner.y, tabWidth, inner.height), PanelTab.Overview, "Apercu", null);
            DrawTab(new Rect(inner.x + tabWidth + Gap, inner.y, tabWidth, inner.height), PanelTab.Economy, "Economie", economyBadge);
            DrawTab(new Rect(inner.x + 2 * (tabWidth + Gap), inner.y, tabWidth, inner.height), PanelTab.Military, militaryLabel, militaryBadge);
        }

        private void DrawTab(Rect rect, PanelTab tab, string label, string badge)
        {
            bool active = _tab == tab;

            if (GUI.Button(rect, GUIContent.none, active ? UITheme.ActiveTabButton : UITheme.TabButton))
            {
                _tab = tab;
                _draftComposition = null;
                _recruiting = false;
                _feedback = null;
            }

            bool hasBadge = !string.IsNullOrEmpty(badge);
            float labelY = hasBadge ? rect.y + 4 : rect.y + (rect.height - UITheme.ValueHeight) * 0.5f;

            GUI.Label(new Rect(rect.x, labelY, rect.width, UITheme.ValueHeight), label, UITheme.Value);
            if (hasBadge)
            {
                GUI.Label(new Rect(rect.x, rect.y + 4 + UITheme.ValueHeight, rect.width, UITheme.CaptionHeight), badge, UITheme.Caption);
            }
        }

        private int RemainingBuildingCount(StarSystemState system)
        {
            int built = _economy.GetBuildings(system.Id).Count;
            return Mathf.Max(0, _economy.BuildingCatalog.Count - built);
        }

        // ------------------------------------------------------------------ corps

        private void DrawBody(Rect rect, StarSystemState system)
        {
            switch (_tab)
            {
                case PanelTab.Economy:
                    DrawEconomyTab(rect, system);
                    break;
                case PanelTab.Military:
                    DrawMilitaryTab(rect, system);
                    break;
                default:
                    DrawOverviewTab(rect, system);
                    break;
            }
        }

        private void DrawOverviewTab(Rect rect, StarSystemState system)
        {
            float half = (rect.width - Padding) * 0.5f;
            var left = new Rect(rect.x, rect.y, half, rect.height);
            var right = new Rect(rect.x + half + Padding, rect.y, half, rect.height);

            GUI.Label(new Rect(left.x, left.y, left.width, UITheme.CaptionHeight), "GISEMENTS", UITheme.Caption);
            GUI.Label(new Rect(left.x, left.y + 14, left.width, 18),
                system.ResourceDeposits.Length == 0 ? "Aucun" : string.Join(", ", system.ResourceDeposits),
                UITheme.Label);

            GUI.Label(new Rect(left.x, left.y + 36, left.width, UITheme.CaptionHeight), "RESEAU", UITheme.Caption);
            GUI.Label(new Rect(left.x, left.y + 50, left.width, 18),
                $"{_map.GetNeighbors(system.Id).Count} routes hyperspatiales", UITheme.Label);

            // Le detail de colonisation n'est pas repris ici : il occupe l'onglet « Coloniser »,
            // ou il est accompagne des flottes capables de s'en charger. Le dupliquer faisait
            // deborder l'Apercu de treize unites des qu'un message de retour s'affichait.

            DrawGovernor(new Rect(left.x, left.y + 72, left.width, 56), system);
            DrawSystemVoies(new Rect(left.x, left.y + 132, left.width, left.yMax - left.y - 132), system);

            GUI.Label(new Rect(right.x, right.y, right.width, UITheme.CaptionHeight), "FORCES EN PRESENCE", UITheme.Caption);

            if (_military == null)
            {
                GUI.Label(new Rect(right.x, right.y + UITheme.CaptionHeight + 2, right.width, UITheme.ValueHeight), "Inconnues", UITheme.Caption);
                return;
            }

            IReadOnlyList<Fleet> fleets = _military.GetFleetsAt(system.Id);
            if (fleets.Count == 0)
            {
                GUI.Label(new Rect(right.x, right.y + UITheme.CaptionHeight + 2, right.width, UITheme.ValueHeight), "Aucune garnison", UITheme.Caption);
                return;
            }

            float lineY = right.y + 18;
            foreach (Fleet fleet in fleets)
            {
                if (lineY + 18 > right.yMax)
                {
                    break;
                }

                GUI.Label(new Rect(right.x, lineY, right.width - 74, UITheme.ValueHeight), OwnerLabel(fleet.OwnerId), UITheme.Value);
                GUI.Label(new Rect(right.xMax - 74, lineY, 74, UITheme.ValueHeight), $"{fleet.Composition.TotalCount} u.", UITheme.Caption);
                lineY += 20;
            }
        }

        /// <summary>
        /// Le gouverneur du monde, son etat d'esprit et ce qu'il retient (Phase 24, etape 6).
        /// <para>
        /// <b>Uniquement sur ses propres systemes.</b> Savoir que le gouverneur adverse vacille
        /// serait un renseignement de premier ordre, et l'espionnage doit le faire meriter — meme
        /// regle de confidentialite que la garnison et la stabilite depuis la Phase 23.
        /// </para>
        /// <para>
        /// <b>Les mots avant le nombre, et les souvenirs sous les mots.</b> « Loyaute 0,31 »
        /// n'apprend rien a qui ignore ou se trouve le seuil ; « vacille » se lit d'un coup
        /// d'oeil, et les faits retenus expliquent <i>pourquoi</i>. Sans eux, un depart passerait
        /// pour arbitraire.
        /// </para>
        /// </summary>
        private void DrawGovernor(Rect rect, StarSystemState system)
        {
            if (system.OwnerId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            if (_governors == null)
            {
                ServiceLocator.TryGet(out _governors);
            }

            Governor governor = _governors?.GetGovernor(system.Id);
            if (governor == null)
            {
                return;
            }

            float loyalty = _governors.GetLoyalty(system.Id);

            GUI.Label(new Rect(rect.x, rect.y, rect.width, UITheme.CaptionHeight), "GOUVERNEUR", UITheme.Caption);
            GUI.Label(new Rect(rect.x, rect.y + 14, rect.width, 18), governor.Name, UITheme.Value);
            GUI.Label(
                new Rect(rect.x, rect.y + 32, rect.width, 18),
                $"{LoyaltyModel.Describe(loyalty)} ({loyalty:0.00})",
                UITheme.Caption);

            // Le fait le plus recent suffit : les trois tiennent dans la fiche complete, pas
            // dans l'apercu, et c'est le dernier qui explique le mieux l'etat present.
            if (governor.Memory.Count > 0)
            {
                GovernorFact latest = governor.Memory[governor.Memory.Count - 1];
                GUI.Label(new Rect(rect.x, rect.y + 48, rect.width, 18), latest.Describe(), UITheme.Caption);
            }
        }

        /// <summary>
        /// Les deux voies qui s'appliquent a un monde precis (Phase 24, etape 7).
        /// <para>
        /// <b>Ici et nulle part ailleurs.</b> Rendre un monde ou l'abandonner suppose de savoir
        /// <i>lequel</i> : la fiche du systeme est le seul ecran ou la question ne se pose pas.
        /// Dupliquer un selecteur de monde dans le panneau Empire aurait donne deux chemins vers
        /// le meme geste irreversible, donc deux occasions de se tromper.
        /// </para>
        /// <para>
        /// <b>Elles ne s'affichent qu'une fois le fragment IV obtenu</b>, comme dans le panneau
        /// Empire : le joueur ne doit pas savoir que ces reponses existent avant de lire l'aveu.
        /// </para>
        /// </summary>
        private void DrawSystemVoies(Rect rect, StarSystemState system)
        {
            if (rect.height < 40f || system.OwnerId != EconomyService.PlayerOwnerId)
            {
                return;
            }

            if (_voies == null)
            {
                ServiceLocator.TryGet(out _voies);
            }

            if (_voies == null || !_voies.AreUnlocked)
            {
                return;
            }

            GUI.Label(new Rect(rect.x, rect.y, rect.width, UITheme.CaptionHeight), "REPONSES A LA COURBE", UITheme.Caption);

            float buttonHeight = Mathf.Min(24f, (rect.height - 16f) * 0.5f);
            float y = rect.y + 16f;

            foreach (VoieDefinition definition in VoieCatalogue.All)
            {
                if (!definition.NeedsASystem || !definition.IsPlayable)
                {
                    continue;
                }

                if (y + buttonHeight > rect.yMax)
                {
                    break;
                }

                if (GUI.Button(new Rect(rect.x, y, rect.width, buttonHeight), definition.Name, UITheme.Button))
                {
                    _feedback = _voies.TryTake(definition.Voie, system.Id, out string error) ? null : error;
                }

                y += buttonHeight + 4f;
            }
        }

        private void DrawEconomyTab(Rect rect, StarSystemState system)
        {
            if (system.OwnerId != EconomyService.PlayerOwnerId || _economy == null)
            {
                GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.4f, rect.width, 20),
                    "Aucune action economique : ce systeme ne vous appartient pas.", UITheme.MutedLabel);
                return;
            }

            bool atMax = system.DevelopmentLevel >= MaxDevelopmentLevel;

            // Hauteur proportionnelle plutot que fixe : sur un ecran tres allonge, 46 unites en
            // dur ne laissaient que 14 unites aux tuiles de batiment, illisibles et intouchables.
            float investHeight = Mathf.Clamp(rect.height * 0.34f, 32f, 46f);
            var investRect = new Rect(rect.x, rect.y, rect.width, investHeight);

            GUI.enabled = !atMax;
            string investLabel = atMax
                ? "Developpement maximal atteint"
                : $"Investir dans le developpement   —   niveau {system.DevelopmentLevel} → {system.DevelopmentLevel + 1}   ·   {_economy.GetInvestmentCost(system.Id):0} Cr";

            if (GUI.Button(investRect, investLabel, UITheme.Button) && !atMax)
            {
                _feedback = _economy.TryInvestInDevelopment(system.Id, out string error) ? null : error;
            }
            GUI.enabled = true;

            GUI.Label(new Rect(rect.x, investRect.yMax + 4, rect.width, UITheme.CaptionHeight), "BATIMENTS", UITheme.Caption);

            // Les cinq batiments sur une seule rangee : la pleine largeur de la fiche leur laisse
            // environ 118 unites chacun, contre 60 dans l'ancienne colonne. Aucun ne deborde.
            var gridArea = new Rect(rect.x, investRect.yMax + 20, rect.width, rect.yMax - investRect.yMax - 20);
            IReadOnlyList<BuildingType> catalog = _economy.BuildingCatalog;
            int columns = Mathf.Max(1, catalog.Count);
            float tileWidth = (gridArea.width - (columns - 1) * Gap) / columns;

            for (int i = 0; i < catalog.Count; i++)
            {
                BuildingType building = catalog[i];
                if (building == null)
                {
                    continue;
                }

                var tile = new Rect(gridArea.x + i * (tileWidth + Gap), gridArea.y, tileWidth, gridArea.height);
                bool built = _economy.GetBuildings(system.Id).Any(b => b.Type == building);

                GUI.enabled = !built;
                if (GUI.Button(tile, $"{building.DisplayName}\n{(built ? "construit" : $"{building.CreditsCost:0} Cr")}", UITheme.Button) && !built)
                {
                    _feedback = _economy.TryStartConstruction(system.Id, building, out string error) ? null : error;
                }
                GUI.enabled = true;
            }
        }

        // ------------------------------------------------------------------ armee

        private void DrawMilitaryTab(Rect rect, StarSystemState system)
        {
            if (_military == null)
            {
                return;
            }

            if (system.OwnerId == EconomyService.PlayerOwnerId)
            {
                if (_draftComposition != null)
                {
                    DrawFleetComposer(rect, system);
                }
                else if (_awaitingDestinationFleetId.HasValue)
                {
                    DrawDestinationPrompt(rect);
                }
                else
                {
                    DrawOwnedMilitary(rect, system);
                }

                return;
            }

            if (system.OwnerId == StarSystemState.UnownedOwnerId)
            {
                DrawColonizationTab(rect, system);
                return;
            }

            DrawOffensiveTab(rect, system);
        }

        private void DrawOwnedMilitary(Rect rect, StarSystemState system)
        {
            const float leftWidth = 190f;
            var left = new Rect(rect.x, rect.y, leftWidth, rect.height);
            var right = new Rect(rect.x + leftWidth + Padding, rect.y, rect.width - leftWidth - Padding, rect.height);

            UnitBundle garrison = _military.GetGarrison(system.Id, EconomyService.PlayerOwnerId);

            // Etat du plafond de flottes en campagne : deduit de ce que le service expose deja
            // (nombre de flottes non stationnees, et la possibilite d'en lancer une de plus)
            // plutot que d'ajouter un membre a IMilitaryService, que cinq doublures de test
            // implementent.
            int deployed = _military.GetFleetsForEmpire(EconomyService.PlayerOwnerId).Count(f => f.Status != FleetStatus.Stationed);
            bool canDeploy = _military.CanDeployAnotherFleet(EconomyService.PlayerOwnerId);

            // Les boutons sont places en premier et gardent toujours une hauteur touchable ;
            // les lignes d'information au-dessus ne sont ecrites que si elles tiennent encore.
            // Sur un ecran tres allonge, ce sont les details qui disparaissent, jamais l'action.
            float buttonHeight = Mathf.Clamp(left.height * 0.34f, 32f, 44f);
            float contentBottom = left.yMax - buttonHeight - 6f;
            float lineY = left.y;

            WriteLine(left, ref lineY, contentBottom, UITheme.CaptionHeight, "GARNISON", UITheme.Caption);
            WriteLine(left, ref lineY, contentBottom, UITheme.TitleHeight, $"{garrison.TotalCount} unites", UITheme.Title);
            WriteLine(left, ref lineY, contentBottom, UITheme.CaptionHeight, $"puissance ~{_military.EstimatePower(garrison):0}", UITheme.Caption);
            WriteLine(left, ref lineY, contentBottom, UITheme.CaptionHeight, garrison.ToString(), UITheme.Caption);
            WriteLine(left, ref lineY, contentBottom, UITheme.CaptionHeight,
                canDeploy ? $"{deployed} flotte(s) en campagne" : $"{deployed} en campagne — plafond atteint",
                canDeploy ? UITheme.Caption : UITheme.Value);

            float buttonWidth = (left.width - Gap) * 0.5f;
            var createRect = new Rect(left.x, left.yMax - buttonHeight, buttonWidth, buttonHeight);
            var recruitRect = new Rect(left.x + buttonWidth + Gap, left.yMax - buttonHeight, buttonWidth, buttonHeight);

            GUI.enabled = garrison.TotalCount > 0;
            if (GUI.Button(createRect, "Creer\nune flotte", UITheme.Button) && garrison.TotalCount > 0)
            {
                _draftComposition = new int[System.Enum.GetValues(typeof(UnitType)).Length];
                _feedback = null;
            }
            GUI.enabled = true;

            if (GUI.Button(recruitRect, _recruiting ? "Voir les\nflottes" : "Recruter\ndes unites", UITheme.Button))
            {
                _recruiting = !_recruiting;
                _feedback = null;
            }

            if (_recruiting)
            {
                DrawRecruitGrid(right, system);
            }
            else
            {
                DrawFleetList(right, system);
            }
        }

        /// <summary>
        /// Ecrit une ligne et avance le curseur, ou ne fait rien si elle ne tient plus. Evite
        /// d'avoir a decliner chaque disposition par hauteur d'ecran.
        /// </summary>
        private static void WriteLine(Rect column, ref float y, float bottom, float height, string text, GUIStyle style)
        {
            if (y + height > bottom)
            {
                return;
            }

            GUI.Label(new Rect(column.x, y, column.width, height), text, style);
            y += height;
        }

        /// <summary>
        /// Grille de recrutement, en quatre colonnes plutot qu'en une rangee de sept.
        /// <para>
        /// C'est le debordement horizontal de l'ancienne fiche : sept boutons alignes avec leur
        /// description tenaient dans plus de 700 unites, pour une zone qui en offrait 344. Quatre
        /// colonnes sur deux rangees laissent environ 94 unites par tuile, soit une cible tactile
        /// confortable et un libelle lisible.
        /// </para>
        /// </summary>
        private void DrawRecruitGrid(Rect rect, StarSystemState system)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, UITheme.CaptionHeight), "RECRUTER", UITheme.Caption);

            IReadOnlyList<UnitTypeDefinition> catalog = _military.UnitCatalog;
            var grid = new Rect(rect.x, rect.y + 16, rect.width, rect.height - 16);

            const int columns = 4;
            int rows = Mathf.Max(1, Mathf.CeilToInt(catalog.Count / (float)columns));
            float tileWidth = (grid.width - (columns - 1) * Gap) / columns;
            float tileHeight = (grid.height - (rows - 1) * Gap) / rows;

            for (int i = 0; i < catalog.Count; i++)
            {
                UnitTypeDefinition unitType = catalog[i];
                if (unitType == null)
                {
                    continue;
                }

                var tile = new Rect(
                    grid.x + (i % columns) * (tileWidth + Gap),
                    grid.y + (i / columns) * (tileHeight + Gap),
                    tileWidth,
                    tileHeight);

                bool allowed = system.DevelopmentLevel >= unitType.MinimumDevelopmentLevel;

                GUI.enabled = allowed;
                string label = allowed
                    ? $"{unitType.DisplayName}\n{unitType.CreditsCost:0} Cr"
                    : $"{unitType.DisplayName}\ndev. {unitType.MinimumDevelopmentLevel} requis";

                if (GUI.Button(tile, label, UITheme.Button) && allowed)
                {
                    _feedback = _military.TryRecruitUnits(system.Id, unitType, 1, out string error) ? null : error;
                }
                GUI.enabled = true;
            }
        }

        /// <summary>
        /// Les flottes du joueur presentes sur ce systeme, garnison comprise. Chacune peut
        /// recevoir un ordre de destination independamment des autres.
        /// </summary>
        private void DrawFleetList(Rect rect, StarSystemState system)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, UITheme.CaptionHeight), "FLOTTES SUR PLACE", UITheme.Caption);

            var playerFleets = _military.GetFleetsAt(system.Id)
                .Where(f => f.OwnerId == EconomyService.PlayerOwnerId)
                .ToList();

            if (playerFleets.Count == 0)
            {
                GUI.Label(new Rect(rect.x, rect.y + UITheme.CaptionHeight + 2, rect.width, UITheme.ValueHeight), "Aucune", UITheme.Caption);
                return;
            }

            float rowHeight = Mathf.Min(44f, (rect.height - 20 - (playerFleets.Count - 1) * 4) / playerFleets.Count);
            float rowY = rect.y + 20;

            foreach (Fleet fleet in playerFleets)
            {
                if (rowY + rowHeight > rect.yMax)
                {
                    break;
                }

                var row = new Rect(rect.x, rowY, rect.width, rowHeight);
                GUI.DrawTexture(row, UITheme.SolidTexture(new Color(1f, 1f, 1f, 0.04f)));

                GUI.Label(new Rect(row.x + 8, row.y + 3, row.width - 110, UITheme.ValueHeight), fleet.Name, UITheme.Value);
                GUI.Label(new Rect(row.x + 6 + UITheme.ValueHeight, row.y + 3 + UITheme.ValueHeight, row.width - 110, UITheme.CaptionHeight),
                    $"{fleet.Composition.TotalCount} u. · amiral {fleet.Admiral.Name}", UITheme.Caption);

                var sendRect = new Rect(row.xMax - 94, row.y + 5, 88, rowHeight - 10);
                if (GUI.Button(sendRect, "Envoyer", UITheme.Button))
                {
                    _awaitingDestinationFleetId = fleet.Id;
                    _feedback = null;
                }

                rowY += rowHeight + 4;
            }
        }

        /// <summary>
        /// Feuille de composition : le joueur choisit quelles unites embarquent, dans la limite
        /// du plafond par flotte. C'est ce qui distingue « creer une flotte » de « envoyer toute
        /// la garnison », l'ancien comportement qui laissait systematiquement le systeme sans
        /// defense.
        /// </summary>
        private void DrawFleetComposer(Rect rect, StarSystemState system)
        {
            UnitBundle garrison = _military.GetGarrison(system.Id, EconomyService.PlayerOwnerId);
            int total = _draftComposition.Sum();

            const float titleHeight = 40f;
            const float footerHeight = 52f;

            GUI.Label(new Rect(rect.x, rect.y, rect.width - 240, UITheme.TitleHeight), "Composition de la flotte", UITheme.Title);
            GUI.Label(new Rect(rect.x, rect.y + UITheme.TitleHeight, rect.width - 240, UITheme.CaptionHeight),
                $"{total}/{MaxUnitsPerFleet} unites embarquees — le reste tient la garnison", UITheme.Caption);

            var types = (UnitType[])System.Enum.GetValues(typeof(UnitType));
            var grid = new Rect(rect.x, rect.y + titleHeight, rect.width, rect.height - titleHeight - footerHeight);

            const int columns = 4;
            int rows = Mathf.Max(1, Mathf.CeilToInt(types.Length / (float)columns));
            float tileWidth = (grid.width - (columns - 1) * Gap) / columns;
            float tileHeight = (grid.height - (rows - 1) * Gap) / rows;

            for (int i = 0; i < types.Length; i++)
            {
                UnitType type = types[i];
                int available = garrison.Get(type);
                int chosen = _draftComposition[(int)type];

                var tile = new Rect(
                    grid.x + (i % columns) * (tileWidth + Gap),
                    grid.y + (i / columns) * (tileHeight + Gap),
                    tileWidth,
                    tileHeight);

                GUI.DrawTexture(tile, UITheme.SolidTexture(new Color(1f, 1f, 1f, available > 0 ? 0.05f : 0.02f)));

                GUI.Label(new Rect(tile.x + 6, tile.y + 3, tile.width - 12, UITheme.CaptionHeight), ShortName(type), UITheme.Caption);

                // Valeur placee <b>entre</b> les deux boutons plutot qu'au-dessus : empilee, elle
                // chevauchait les boutons des que la tuile descendait sous 62 unites de haut,
                // c'est-a-dire sur tout ecran plus allonge que du 19,5:9.
                float stepperHeight = Mathf.Max(28f, tile.height - 22f);
                float stepperY = tile.yMax - stepperHeight - 4;
                float stepperWidth = (tile.width - 12f) * 0.3f;
                float valueWidth = tile.width - 12f - 2 * stepperWidth;

                var minusRect = new Rect(tile.x + 6, stepperY, stepperWidth, stepperHeight);
                var plusRect = new Rect(tile.xMax - 6 - stepperWidth, stepperY, stepperWidth, stepperHeight);
                GUI.Label(new Rect(minusRect.xMax, stepperY + (stepperHeight - UITheme.TitleHeight) * 0.5f, valueWidth, UITheme.TitleHeight),
                    $"{chosen}/{available}", UITheme.Title);

                GUI.enabled = chosen > 0;
                if (GUI.Button(minusRect, "-", UITheme.Button) && chosen > 0)
                {
                    _draftComposition[(int)type]--;
                }

                GUI.enabled = chosen < available && total < MaxUnitsPerFleet;
                if (GUI.Button(plusRect, "+", UITheme.Button) && chosen < available && total < MaxUnitsPerFleet)
                {
                    _draftComposition[(int)type]++;
                }
                GUI.enabled = true;
            }

            var cancelRect = new Rect(rect.x, rect.yMax - 44, 160, 44);
            var confirmRect = new Rect(rect.x + 168, rect.yMax - 44, rect.width - 168, 44);

            if (GUI.Button(cancelRect, "Annuler", UITheme.Button))
            {
                _draftComposition = null;
                return;
            }

            GUI.enabled = total > 0;
            if (GUI.Button(confirmRect, total > 0 ? $"Detacher {total} unite(s) en une nouvelle flotte" : "Choisissez au moins une unite", UITheme.Button)
                && total > 0)
            {
                ConfirmFleetCreation(system);
            }
            GUI.enabled = true;
        }

        private void ConfirmFleetCreation(StarSystemState system)
        {
            UnitBundle units = UnitBundle.Zero;
            var types = (UnitType[])System.Enum.GetValues(typeof(UnitType));
            foreach (UnitType type in types)
            {
                int count = _draftComposition[(int)type];
                if (count > 0)
                {
                    units += UnitBundle.Of(type, count);
                }
            }

            if (_military.TryDetachFleet(system.Id, EconomyService.PlayerOwnerId, units, out Fleet fleet, out string error))
            {
                _feedback = $"{fleet.Name} constituee ({units.TotalCount} unites). Choisissez sa destination.";
                GameLog.Info($"[Military] {_feedback}");
                _draftComposition = null;
                return;
            }

            _feedback = error;
            GameLog.Warning($"[Military] {error}");
        }

        private void DrawDestinationPrompt(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.25f, rect.width, 22),
                "Touchez le systeme de destination sur la carte.", UITheme.Title);
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.25f + 24, rect.width, 18),
                "Un systeme libre sera colonise si la flotte transporte assez d'Infanterie ; un systeme ennemi sera attaque.",
                UITheme.MutedLabel);

            if (GUI.Button(new Rect(rect.x, rect.yMax - 44, 200, 40), "Annuler l'ordre", UITheme.Button))
            {
                _awaitingDestinationFleetId = null;
            }
        }

        /// <summary>
        /// Colonisation explicite d'un systeme libre : la fiche liste les flottes du joueur
        /// capables de s'y installer, avec ce qui leur manque quand elles ne le peuvent pas.
        /// <para>
        /// L'ordre lui-meme reste un <c>TryMoveFleet</c> — le service verifie l'exigence
        /// d'Infanterie <b>au depart</b> depuis la Phase 16, et retire les unites a l'arrivee.
        /// Dupliquer cette regle ici pour un bouton dedie la ferait diverger a la premiere
        /// retouche d'equilibrage.
        /// </para>
        /// </summary>
        private void DrawColonizationTab(Rect rect, StarSystemState system)
        {
            int required = ColonizationRules.RequiredInfantry(system);
            int lost = ColonizationRules.InfantryLost(system);

            const float leftWidth = 200f;
            GUI.Label(new Rect(rect.x, rect.y, leftWidth, UITheme.CaptionHeight), "COLONISATION", UITheme.Caption);
            GUI.Label(new Rect(rect.x, rect.y + UITheme.CaptionHeight, leftWidth, UITheme.TitleHeight), $"{required} Infanterie", UITheme.Title);
            GUI.Label(new Rect(rect.x, rect.y + UITheme.CaptionHeight + UITheme.TitleHeight, leftWidth, UITheme.CaptionHeight), $"dont {lost} perdue(s) a l'installation", UITheme.Caption);
            GUI.Label(new Rect(rect.x, rect.y + 2 * UITheme.CaptionHeight + UITheme.TitleHeight, leftWidth, UITheme.CaptionHeight), $"stabilite {HudFormatter.FormatPercent(system.Stability)}", UITheme.Caption);

            var listRect = new Rect(rect.x + leftWidth + Padding, rect.y, rect.width - leftWidth - Padding, rect.height);
            GUI.Label(new Rect(listRect.x, listRect.y, listRect.width, UITheme.CaptionHeight), "FLOTTES DISPONIBLES", UITheme.Caption);

            var candidates = _military.GetFleetsForEmpire(EconomyService.PlayerOwnerId)
                .Where(f => f.Status == FleetStatus.Stationed)
                .OrderByDescending(f => f.Composition.Infantry)
                .ToList();

            if (candidates.Count == 0)
            {
                GUI.Label(new Rect(listRect.x, listRect.y + 20, listRect.width, 18),
                    "Aucune flotte stationnee. Creez-en une depuis un de vos systemes.", UITheme.MutedLabel);
                return;
            }

            float rowHeight = 40f;
            float rowY = listRect.y + 20;

            foreach (Fleet fleet in candidates)
            {
                if (rowY + rowHeight > listRect.yMax)
                {
                    break;
                }

                bool capable = fleet.Composition.Infantry >= required;
                string origin = _map.TryGetSystem(fleet.CurrentSystemId, out StarSystemState from) ? from.Name : "?";

                var row = new Rect(listRect.x, rowY, listRect.width, rowHeight);
                GUI.DrawTexture(row, UITheme.SolidTexture(new Color(1f, 1f, 1f, 0.04f)));

                GUI.Label(new Rect(row.x + 8, row.y + 2, row.width - 110, UITheme.ValueHeight), $"{fleet.Name} — {origin}", UITheme.Value);
                GUI.Label(new Rect(row.x + 8, row.y + 2 + UITheme.ValueHeight, row.width - 110, UITheme.CaptionHeight),
                    capable
                        ? $"{fleet.Composition.Infantry} Infanterie a bord"
                        : $"{fleet.Composition.Infantry}/{required} Infanterie — insuffisant",
                    capable ? UITheme.Caption : UITheme.Value);

                GUI.enabled = capable;
                if (GUI.Button(new Rect(row.xMax - 100, row.y + 4, 94, rowHeight - 8), "Coloniser", UITheme.Button) && capable)
                {
                    ExecuteMoveOrder(fleet.Id, system.Id);
                }
                GUI.enabled = true;

                rowY += rowHeight + 4;
            }
        }

        // -------------------------------------------------------------- offensive

        /// <summary>
        /// Planification d'offensive sur un systeme etranger : les flottes a portee avec leur
        /// delai, et la <b>prevision</b> de ce qui va se passer.
        /// <para>
        /// <b>Aucun pourcentage de victoire n'est affiche, parce qu'il n'y en a pas :</b>
        /// <c>CombatResolver</c> est entierement deterministe, le camp le plus puissant
        /// l'emporte sans le moindre tirage. Annoncer « 68 % » serait une invention. La fiche
        /// annonce donc l'issue reelle, les pertes prevues, et si le systeme changera de mains.
        /// </para>
        /// </summary>
        private void DrawOffensiveTab(Rect rect, StarSystemState system)
        {
            if (_diplomacy != null
                && _diplomacy.GetStatus(EconomyService.PlayerOwnerId, system.OwnerId) != DiplomaticStatus.War)
            {
                GUI.Label(new Rect(rect.x, rect.y, rect.width, UITheme.CaptionHeight), "RENSEIGNEMENT", UITheme.Caption);
                DrawIntelRow(rect, system);
                GUI.Label(new Rect(rect.x, rect.y + 90, rect.width, 18),
                    "Aucune offensive possible : vous n'etes pas en guerre avec cet empire.", UITheme.Label);
                return;
            }

            EnsureOffensiveCandidates(system);

            const float recapWidth = 200f;
            var list = new Rect(rect.x, rect.y, rect.width - recapWidth - Padding, rect.height);
            var recap = new Rect(rect.xMax - recapWidth, rect.y, recapWidth, rect.height);

            DrawOffensiveCandidates(list);
            DrawOffensiveRecap(recap, system);
        }

        private void DrawIntelRow(Rect rect, StarSystemState system)
        {
            UnitBundle garrison = _military.GetGarrison(system.Id, system.OwnerId);
            float columnWidth = (rect.width - 2 * Gap) / 3f;

            DrawIntelStat(new Rect(rect.x, rect.y + 18, columnWidth, 60), "GARNISON", $"{garrison.TotalCount}");
            DrawIntelStat(new Rect(rect.x + columnWidth + Gap, rect.y + 18, columnWidth, 60), "PUISSANCE",
                $"~{_military.EstimatePower(garrison):0}");
            DrawIntelStat(new Rect(rect.x + 2 * (columnWidth + Gap), rect.y + 18, columnWidth, 60), "TERRAIN",
                $"+{system.DevelopmentLevel * 10} %");
        }

        private static void DrawIntelStat(Rect rect, string key, string value)
        {
            GUI.DrawTexture(rect, UITheme.SolidTexture(new Color(1f, 1f, 1f, 0.04f)));
            GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, UITheme.CaptionHeight), key, UITheme.Caption);
            GUI.Label(new Rect(rect.x + 8, rect.y + 6 + UITheme.CaptionHeight, rect.width - 16, UITheme.TitleHeight), value, UITheme.Title);
        }

        /// <summary>
        /// Calcule, une seule fois par cible, l'itineraire et le delai de chaque flotte
        /// stationnee du joueur. Le meme predicat de traversee que <c>MilitaryService</c> est
        /// utilise (<see cref="FleetRouting.IsTraversableWaypoint"/>) : une flotte annoncee
        /// comme atteignant la cible doit reellement pouvoir partir.
        /// </summary>
        private void EnsureOffensiveCandidates(StarSystemState target)
        {
            if (_offensiveCacheKey.HasValue && _offensiveCacheKey.Value.Equals(target.Id))
            {
                return;
            }

            _offensiveCandidates.Clear();
            _engagedFleetIds.Clear();
            _offensiveCacheKey = target.Id;

            float logistics = 1f + (_research?.GetBonus(EconomyService.PlayerOwnerId, ResearchDomain.Logistics) ?? 0f);
            float command = CommandModifier(EconomyService.PlayerOwnerId);

            foreach (Fleet fleet in _military.GetFleetsForEmpire(EconomyService.PlayerOwnerId))
            {
                if (fleet.Status != FleetStatus.Stationed || fleet.Composition.IsEmpty)
                {
                    continue;
                }

                string origin = _map.TryGetSystem(fleet.CurrentSystemId, out StarSystemState from) ? from.Name : "?";
                float morale = from?.Stability ?? 1f;
                float attackModifier = morale * command * (1f + fleet.Admiral.AttackBonus);

                bool reachable = HyperlanePathfinder.TryFindPath(
                    _map, fleet.CurrentSystemId, target.Id,
                    waypoint => FleetRouting.IsTraversableWaypoint(waypoint, EconomyService.PlayerOwnerId),
                    out IReadOnlyList<StarSystemId> route);

                int days = reachable
                    ? FleetTravel.JourneyDays(_map, route,
                        FleetTravel.EffectiveSpeed(fleet.Composition, _military.UnitCatalog, logistics, fleet.Admiral.SpeedBonus))
                    : 0;

                _offensiveCandidates.Add(new OffensiveCandidate(
                    fleet.Id,
                    $"{fleet.Name} — {origin}",
                    reachable
                        ? $"{fleet.Composition.TotalCount} u. · {fleet.Composition.Infantry} Inf. · arrivee J+{days}"
                        : $"{fleet.Composition.TotalCount} u. · aucune route praticable",
                    fleet.Composition,
                    days,
                    reachable,
                    attackModifier));
            }

            _offensiveCandidates.Sort((a, b) =>
            {
                if (a.Reachable != b.Reachable) return a.Reachable ? -1 : 1;
                int byDays = a.TravelDays.CompareTo(b.TravelDays);
                return byDays != 0 ? byDays : a.FleetId.CompareTo(b.FleetId);
            });
        }

        /// <summary>Modificateur de commandement du joueur : personnalite de son empire et recherche en Armement.</summary>
        private float CommandModifier(int empireId)
        {
            float personality = _empireRegistry != null && _empireRegistry.TryGetEmpire(empireId, out Empire empire)
                ? EmpirePersonalityProfile.Get(empire.Personality).CommandModifier
                : 1f;

            return personality * (1f + (_research?.GetBonus(empireId, ResearchDomain.Weapons) ?? 0f));
        }

        private void DrawOffensiveCandidates(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, UITheme.CaptionHeight), "FLOTTES A PORTEE", UITheme.Caption);

            if (_offensiveCandidates.Count == 0)
            {
                GUI.Label(new Rect(rect.x, rect.y + 18, rect.width, 18),
                    "Aucune flotte stationnee. Creez-en une depuis un de vos systemes.", UITheme.MutedLabel);
                return;
            }

            float listHeight = rect.height - 18;
            float rowHeight = Mathf.Clamp((listHeight - (_offensiveCandidates.Count - 1) * 4) / _offensiveCandidates.Count, 26f, 36f);
            float rowY = rect.y + 18;

            foreach (OffensiveCandidate candidate in _offensiveCandidates)
            {
                if (rowY + rowHeight > rect.yMax)
                {
                    break;
                }

                bool engaged = _engagedFleetIds.Contains(candidate.FleetId);
                var row = new Rect(rect.x, rowY, rect.width, rowHeight);

                GUI.DrawTexture(row, UITheme.SolidTexture(engaged
                    ? new Color(UITheme.AccentBackground.r, UITheme.AccentBackground.g, UITheme.AccentBackground.b, 0.28f)
                    : new Color(1f, 1f, 1f, 0.04f)));

                GUI.enabled = candidate.Reachable;
                if (GUI.Button(row, GUIContent.none, UITheme.TabButton) && candidate.Reachable)
                {
                    if (!_engagedFleetIds.Remove(candidate.FleetId))
                    {
                        _engagedFleetIds.Add(candidate.FleetId);
                    }
                }
                GUI.enabled = true;

                GUI.Label(new Rect(row.x + 8, row.y + 2, 16, UITheme.ValueHeight), engaged ? "x" : "·", UITheme.Value);
                GUI.Label(new Rect(row.x + 26, row.y + 2, row.width - 34, UITheme.ValueHeight), candidate.Label, UITheme.Value);
                GUI.Label(new Rect(row.x + 26, row.y + 2 + UITheme.ValueHeight, row.width - 34, UITheme.CaptionHeight), candidate.Detail, UITheme.Caption);

                rowY += rowHeight + 4;
            }
        }

        private void DrawOffensiveRecap(Rect rect, StarSystemState system)
        {
            OffensiveOutcome outcome = SimulateEngagement(system);

            float statHeight = 24f;
            float half = (rect.width - Gap) * 0.5f;

            DrawRecapStat(new Rect(rect.x, rect.y, half, statHeight), "FLOTTES", $"{outcome.WaveCount}");
            DrawRecapStat(new Rect(rect.x + half + Gap, rect.y, half, statHeight), "UNITES", $"{outcome.UnitCount}");
            DrawRecapStat(new Rect(rect.x, rect.y + statHeight, half, statHeight), "PUISSANCE", $"{outcome.AttackPower:0}");
            DrawRecapStat(new Rect(rect.x + half + Gap, rect.y + statHeight, half, statHeight), "DEFENSE", $"{outcome.DefensePower:0}");

            float verdictY = rect.y + 2 * statHeight + 2;
            string verdict;
            GUIStyle verdictStyle;

            if (outcome.WaveCount == 0)
            {
                verdict = "Choisissez au moins une flotte";
                verdictStyle = UITheme.MutedLabel;
            }
            else if (outcome.SystemCaptured)
            {
                verdict = $"Systeme pris · J+{outcome.TravelDays} · {outcome.AttackerLosses} perte(s)";
                verdictStyle = UITheme.Label;
            }
            else if (outcome.WonWithoutOccupation)
            {
                verdict = $"Garnison detruite, systeme non pris — aucune Infanterie survivante";
                verdictStyle = UITheme.Label;
            }
            else
            {
                verdict = $"Offensive repoussee · {outcome.AttackerLosses} perte(s) · {outcome.SurvivingDefenders} defenseur(s) restant(s)";
                verdictStyle = UITheme.Label;
            }

            GUI.Label(new Rect(rect.x, verdictY, rect.width, 32), verdict, verdictStyle);
            GUI.Label(new Rect(rect.x, verdictY + 32, rect.width, UITheme.CaptionHeight), "prevision a effectifs constants", UITheme.Caption);

            float buttonHeight = Mathf.Clamp(rect.height * 0.34f, 32f, 44f);
            var launchRect = new Rect(rect.x, rect.yMax - buttonHeight, rect.width, buttonHeight);

            GUI.enabled = outcome.WaveCount > 0;
            if (GUI.Button(launchRect, "Planifier l'offensive", UITheme.Button) && outcome.WaveCount > 0)
            {
                _offensiveConfirming = true;
            }
            GUI.enabled = true;
        }

        private static void DrawRecapStat(Rect rect, string key, string value)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, UITheme.CaptionHeight), key, UITheme.Caption);
            GUI.Label(new Rect(rect.x, rect.y + UITheme.CaptionHeight, rect.width, UITheme.ValueHeight), value, UITheme.Value);
        }

        /// <summary>
        /// Rejoue l'offensive telle que le jeu la resoudra : une bataille par arrivee, dans
        /// l'ordre des arrivees. C'est ce qui rend visible qu'echelonner ses flottes, c'est se
        /// faire battre en detail.
        /// </summary>
        private OffensiveOutcome SimulateEngagement(StarSystemState system)
        {
            var waves = new List<OffensiveWave>(_engagedFleetIds.Count);
            foreach (OffensiveCandidate candidate in _offensiveCandidates)
            {
                if (_engagedFleetIds.Contains(candidate.FleetId))
                {
                    waves.Add(new OffensiveWave(candidate.FleetId, candidate.Composition, candidate.TravelDays, candidate.AttackModifier));
                }
            }

            return OffensivePlanner.Simulate(
                waves,
                _military.GetGarrison(system.Id, system.OwnerId),
                OffensivePlanner.VisibleDefenderModifier(system.Stability, system.DevelopmentLevel),
                _military.UnitCatalog);
        }

        private void DrawOffensiveConfirmation(Rect rect, StarSystemState system)
        {
            OffensiveOutcome outcome = SimulateEngagement(system);

            GUI.Label(new Rect(rect.x, rect.y, rect.width, UITheme.TitleHeight), $"Engager le combat sur {system.Name}", UITheme.Title);
            GUI.Label(new Rect(rect.x, rect.y + 28, rect.width, 18),
                $"{outcome.WaveCount} flotte(s), {outcome.UnitCount} unites — derniere arrivee dans {outcome.TravelDays} jour(s).",
                UITheme.Label);

            GUI.Label(new Rect(rect.x, rect.y + 52, rect.width, 18),
                outcome.SystemCaptured
                    ? $"Prevision : le systeme tombe, {outcome.AttackerLosses} unite(s) perdue(s)."
                    : outcome.WonWithoutOccupation
                        ? "Prevision : la garnison est detruite mais le systeme reste a son proprietaire."
                        : $"Prevision : l'offensive est repoussee, {outcome.AttackerLosses} unite(s) perdue(s).",
                UITheme.Label);

            GUI.Label(new Rect(rect.x, rect.y + 74, rect.width, 32),
                "Les flottes engagees seront indisponibles pendant tout le trajet. La garnison adverse "
                + "peut etre renforcee d'ici leur arrivee : cette prevision suppose des effectifs constants.",
                UITheme.MutedLabel);

            var cancelRect = new Rect(rect.x, rect.yMax - 44, 160, 44);
            var confirmRect = new Rect(rect.x + 168, rect.yMax - 44, rect.width - 168, 44);

            if (GUI.Button(cancelRect, "Annuler", UITheme.Button))
            {
                _offensiveConfirming = false;
                return;
            }

            if (GUI.Button(confirmRect, $"Lancer l'offensive — {outcome.UnitCount} unites", UITheme.Button))
            {
                LaunchOffensive(system);
            }
        }

        /// <summary>
        /// Envoie chaque flotte engagee. Les echecs sont rapportes tels quels : le plafond de
        /// flottes en campagne peut en refuser une partie, et le joueur doit savoir laquelle.
        /// </summary>
        private void LaunchOffensive(StarSystemState system)
        {
            int launched = 0;
            string lastError = null;

            foreach (int fleetId in _engagedFleetIds.OrderBy(id => id).ToList())
            {
                Fleet fleet = FindPlayerFleet(fleetId);
                if (fleet == null || fleet.Status != FleetStatus.Stationed)
                {
                    continue;
                }

                if (_military.TryMoveFleet(fleet, system.Id, out string error))
                {
                    launched++;
                    continue;
                }

                lastError = error;
                GameLog.Warning($"[Offensive] {fleet.Name} : {error}");
            }

            _offensiveConfirming = false;
            InvalidateOffensiveCache();

            _feedback = launched == 0
                ? lastError ?? "Aucune flotte n'a pu partir."
                : lastError == null
                    ? $"Offensive lancee sur {system.Name} : {launched} flotte(s) en route."
                    : $"{launched} flotte(s) en route. Les autres ont ete refusees : {lastError}";

            GameLog.Info($"[Offensive] {_feedback}");
        }

        // ------------------------------------------------------------------ divers

        /// <summary>Abreviation tenant dans une tuile de composition, ou le nom complet deborderait.</summary>
        private static string ShortName(UnitType type)
        {
            switch (type)
            {
                case UnitType.Infantry: return "Infant.";
                case UnitType.Armored: return "Blindes";
                case UnitType.SpecialForces: return "F. spec.";
                case UnitType.Fighter: return "Chass.";
                case UnitType.Frigate: return "Fregate";
                case UnitType.Cruiser: return "Croiseur";
                case UnitType.Battleship: return "Cuirasse";
                default: return type.ToString();
            }
        }

        private string OwnerLabel(int ownerId)
        {
            if (ownerId == StarSystemState.UnownedOwnerId)
            {
                return "Independant";
            }

            if (_empireRegistry != null && _empireRegistry.TryGetEmpire(ownerId, out Empire empire))
            {
                return empire.Name;
            }

            return ownerId.ToString();
        }
    }
}
