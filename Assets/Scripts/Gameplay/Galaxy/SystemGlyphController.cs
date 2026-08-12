using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Military;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Tient a jour l'encodage diegetique des marqueurs de systeme (Phase 23, tranche A).
    /// <para>
    /// <b>Pourquoi un composant separe.</b> <see cref="StarSystemMarker"/> est un applicateur :
    /// il dessine ce qu'on lui donne et ne lit aucun service. Faire s'abonner cent marqueurs a
    /// l'horloge, chacun resolvant l'armee de son cote, multiplierait par cent un travail qui se
    /// fait une fois. Ce controleur porte l'unique abonnement et la seule resolution de service.
    /// </para>
    /// <para>
    /// <b>Il vit dans <c>Galaxy</c> tout en lisant <c>Military</c></b>, comme
    /// <c>FleetMapController</c> — la dependance entre les deux espaces de noms ne va que dans ce
    /// sens, et la vue elle-meme reste ignorante des flottes.
    /// </para>
    /// <para>
    /// <b>Rafraichi chaque jour de jeu plutot que sur une liste d'evenements.</b> Le
    /// developpement, la stabilite et la garnison changent par des chemins differents —
    /// investissement, mois ecoule, recrutement, bataille, depart de flotte, sabotage. Enumerer
    /// ces declencheurs, c'est s'engager a ne jamais en oublier un lors des phases suivantes ; un
    /// oubli ne planterait pas, il laisserait simplement la carte mentir en silence. Recalculer
    /// tout chaque jour coute cent lectures d'entiers et rend l'affichage vrai par construction.
    /// </para>
    /// <para>
    /// <b>Aucun objet n'est cree ici.</b> Les anneaux et pastilles sont crees a la demande par le
    /// marqueur, puis reutilises : le cout journalier se limite a des affectations de couleur et
    /// de visibilite.
    /// </para>
    /// </summary>
    public sealed class SystemGlyphController : MonoBehaviour
    {
        private GalaxyMap _map;
        private IReadOnlyDictionary<StarSystemId, StarSystemMarker> _markers;
        private IEventBus _eventBus;
        private IMilitaryService _military;

        /// <summary>
        /// Cable le controleur sur la carte et ses marqueurs. Appele par
        /// <see cref="GalaxyMapController"/> a la construction de la scene.
        /// </summary>
        public void Initialize(GalaxyMap map, IReadOnlyDictionary<StarSystemId, StarSystemMarker> markers)
        {
            _map = map;
            _markers = markers;

            if (ServiceLocator.TryGet(out _eventBus))
            {
                _eventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
            }
            else
            {
                GameLog.Error("[SystemGlyphController] IEventBus indisponible : la carte n'affichera pas l'etat des systemes.");
            }

            // Premier rendu immediat : sans lui, la carte reste muette jusqu'au premier jour
            // ecoule, ce qui se remarque quand la partie demarre en pause.
            Refresh();
        }

        private void OnDestroy()
        {
            _eventBus?.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        private void OnDayAdvanced(DayAdvancedEvent dayAdvancedEvent)
        {
            Refresh();
        }

        /// <summary>
        /// Recalcule l'encodage de tous les systemes.
        /// <para>
        /// <b>L'armee est resolue paresseusement</b> (meme raison que dans <c>EspionageService</c>) :
        /// <c>MilitaryController</c> s'enregistre a son premier <c>Update</c>, donc apres la
        /// construction de la carte. Tant qu'elle manque, la garnison vaut zero et le reste de
        /// l'encodage fonctionne — un halo sans pastilles vaut mieux qu'une carte vide.
        /// </para>
        /// </summary>
        private void Refresh()
        {
            if (_map == null || _markers == null)
            {
                return;
            }

            if (_military == null)
            {
                ServiceLocator.TryGet(out _military);
            }

            foreach (StarSystemState system in _map.Systems)
            {
                if (!_markers.TryGetValue(system.Id, out StarSystemMarker marker) || marker == null)
                {
                    continue;
                }

                bool ownedByPlayer = system.OwnerId == EconomyService.PlayerOwnerId;
                int garrison = ownedByPlayer && _military != null
                    ? _military.GetGarrison(system.Id, EconomyService.PlayerOwnerId).TotalCount
                    : 0;

                marker.ApplyGlyph(SystemGlyph.For(system.DevelopmentLevel, system.Stability, garrison, ownedByPlayer));
            }
        }
    }
}
