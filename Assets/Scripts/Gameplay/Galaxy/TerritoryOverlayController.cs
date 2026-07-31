using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Empires;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Halo colore par empire autour de chaque systeme controle, pour distinguer les
    /// territoires d'un coup d'œil (Phase 12).
    /// <para>
    /// <b>Mis a jour par sondage (<see cref="Update"/>), pas par abonnement a des evenements :</b>
    /// au moins deux evenements distincts peuvent changer un proprietaire
    /// (<c>SystemColonizedEvent</c>, <c>BattleResolvedEvent</c>) et rien ne garantit qu'il n'y
    /// en aura pas d'autres plus tard (echange de territoire diplomatique...). Comparer le
    /// <see cref="StarSystemState.OwnerId"/> courant de chaque systeme a une valeur mise en
    /// cache reste correct quelle que soit la cause du changement, pour un cout negligeable
    /// (une centaine de comparaisons d'entiers par frame).
    /// </para>
    /// <para>
    /// <b>Resolution paresseuse de <see cref="EmpireRegistry"/> :</b> comme tous les autres
    /// controleurs de la scene <c>GalaxyMap</c>, ce registre n'existe qu'a partir du
    /// <c>Start</c> d'<c>EmpireController</c> — les halos restent invisibles jusque-la, puis
    /// se colorent des que les empires et leurs systemes d'origine sont attribues.
    /// </para>
    /// </summary>
    public sealed class TerritoryOverlayController : MonoBehaviour
    {
        /// <summary>Rayon du halo, en multiple du rayon de base d'un marqueur de systeme.</summary>
        private const float HaloRadiusFactor = 1.7f;

        private const float HaloDepth = 0.5f;
        private const float BaseAlpha = 0.35f;
        private const float PulseAmplitude = 0.12f;
        private const float PulseSpeed = 1.2f;

        private readonly Dictionary<StarSystemId, int> _lastKnownOwnerId = new Dictionary<StarSystemId, int>();
        private readonly Dictionary<StarSystemId, SpriteRenderer> _halos = new Dictionary<StarSystemId, SpriteRenderer>();

        private GalaxyMap _map;
        private EmpireRegistry _empireRegistry;

        /// <summary>
        /// Cree un halo (initialement transparent) par systeme. Appele une seule fois par
        /// <see cref="GalaxyMapController"/> a la generation de la carte, avec les positions
        /// deja calculees par <c>BuildMarkers</c> (evite de les recalculer).
        /// </summary>
        public void Initialize(GalaxyMap map, IReadOnlyDictionary<StarSystemId, Vector3> worldPositions)
        {
            _map = map;

            var haloRoot = new GameObject("TerritoryHalos").transform;
            haloRoot.SetParent(transform, worldPositionStays: false);

            foreach (StarSystemState system in map.Systems)
            {
                if (!worldPositions.TryGetValue(system.Id, out Vector3 position))
                {
                    continue;
                }

                var haloObject = new GameObject($"Halo_{system.Name}");
                haloObject.transform.SetParent(haloRoot, worldPositionStays: false);
                haloObject.transform.position = new Vector3(position.x, position.y, HaloDepth);
                haloObject.transform.localScale = Vector3.one * (1.2f * HaloRadiusFactor);

                var renderer = haloObject.AddComponent<SpriteRenderer>();
                renderer.sprite = RuntimeSpriteFactory.GetCircleSprite();
                renderer.color = new Color(0f, 0f, 0f, 0f);

                _halos[system.Id] = renderer;
                _lastKnownOwnerId[system.Id] = StarSystemState.UnownedOwnerId;
            }
        }

        private void Update()
        {
            if (_map == null)
            {
                return;
            }

            if (_empireRegistry == null)
            {
                ServiceLocator.TryGet(out _empireRegistry);
            }

            float pulse = 1f + Mathf.Sin(Time.time * PulseSpeed) * PulseAmplitude;

            foreach (StarSystemState system in _map.Systems)
            {
                if (!_halos.TryGetValue(system.Id, out SpriteRenderer halo))
                {
                    continue;
                }

                if (_lastKnownOwnerId[system.Id] != system.OwnerId)
                {
                    _lastKnownOwnerId[system.Id] = system.OwnerId;
                    ApplyOwnerColor(halo, system.OwnerId);
                }

                if (system.OwnerId != StarSystemState.UnownedOwnerId)
                {
                    Color color = halo.color;
                    halo.color = new Color(color.r, color.g, color.b, BaseAlpha * pulse);
                }
            }
        }

        private void ApplyOwnerColor(SpriteRenderer halo, int ownerId)
        {
            if (ownerId == StarSystemState.UnownedOwnerId)
            {
                halo.color = new Color(0f, 0f, 0f, 0f);
                return;
            }

            Color empireColor = _empireRegistry != null && _empireRegistry.TryGetEmpire(ownerId, out Empire empire)
                ? empire.Color
                : Color.gray;

            halo.color = new Color(empireColor.r, empireColor.g, empireColor.b, BaseAlpha);
        }
    }
}
