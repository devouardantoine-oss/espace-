using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Chronicle;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using UnityEngine;

namespace Espace.Gameplay.Voies
{
    /// <summary>Les voies, telles que l'interface et la sauvegarde les consultent.</summary>
    public interface IVoieService
    {
        /// <summary>Vrai si le joueur a lu l'aveu et connait donc les quatre voies.</summary>
        bool AreUnlocked { get; }

        /// <summary>Vrai si la Coercition est en vigueur.</summary>
        bool IsCoercionActive { get; }

        /// <summary>
        /// Emprunte une voie. <paramref name="systemId"/> n'est lu que pour celles qui
        /// s'appliquent a un monde.
        /// </summary>
        /// <returns>Faux, avec un motif, si la voie n'est pas praticable en l'etat.</returns>
        bool TryTake(Voie voie, StarSystemId systemId, out string error);

        /// <summary>Restaure l'etat sauvegarde.</summary>
        void RestoreCoercion(bool active);
    }

    /// <summary>
    /// Applique les reponses a la courbe (Phase 24, etape 7).
    /// <para>
    /// <b>Ce que ces voies changent, et qui manquait.</b> La courbe d'administration existe depuis
    /// la Phase 22 : au-dela d'une certaine taille, tenir coute plus que ce qu'on produit. Le
    /// joueur pouvait la subir, jamais y repondre. Il a maintenant trois gestes possibles —
    /// rendre, abandonner, tenir par la force — et aucun n'est confortable.
    /// </para>
    /// <para>
    /// <b>Elles ne s'ouvrent qu'avec le fragment IV du codex.</b> Le joueur decouvre ces reponses
    /// en apprenant qu'un autre les a deja toutes essayees, et qu'aucune ne l'a sauve. C'est le
    /// seul endroit du jeu ou le recit debloque une mecanique, et c'est voulu : la lettre ne
    /// raconte pas seulement, elle <i>donne</i> quelque chose.
    /// </para>
    /// <para>
    /// <b>La Transformation n'est pas appliquee ici</b>, faute d'un reseau d'hyperroutes mutable —
    /// voir <see cref="VoieCatalogue.TransformationPending"/>. Elle est refusee proprement plutot
    /// que traitee a moitie : une route retracee sans garantie de connexite couperait la galaxie
    /// en deux et rendrait la partie injouable.
    /// </para>
    /// </summary>
    public sealed class VoieService : IVoieService, IGameService
    {
        /// <summary>Opinion perdue aupres de chaque empire quand un monde est abandonne.</summary>
        public const float CompressionOpinionPenalty = 12f;

        /// <summary>Stabilite perdue partout le jour ou la Coercition entre en vigueur.</summary>
        public const float CoercionStabilityShock = 0.10f;

        private readonly GalaxyMap _map;

        private ICodexService _codex;
        private IChronicleService _chronicle;
        private IDiplomacyService _diplomacy;
        private EmpireRegistry _empires;
        private IGameClock _clock;

        private bool _coercionActive;

        public VoieService(GalaxyMap map)
        {
            _map = map;
        }

        /// <inheritdoc />
        public bool IsCoercionActive
        {
            get { return _coercionActive; }
        }

        /// <inheritdoc />
        public bool AreUnlocked
        {
            get
            {
                ResolveServices();
                return _codex != null && _codex.IsUnlocked(VoieCatalogue.UnlockingFragment);
            }
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _coercionActive = false;
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _coercionActive = false;
        }

        /// <inheritdoc />
        public void RestoreCoercion(bool active)
        {
            _coercionActive = active;
        }

        /// <inheritdoc />
        public bool TryTake(Voie voie, StarSystemId systemId, out string error)
        {
            VoieDefinition definition = VoieCatalogue.Of(voie);

            if (!AreUnlocked)
            {
                error = "Vous ne connaissez pas encore cette reponse.";
                return false;
            }

            if (!definition.IsPlayable)
            {
                error = VoieCatalogue.TransformationPending;
                return false;
            }

            if (definition.NeedsASystem && !OwnedByPlayer(systemId, out StarSystemState system))
            {
                error = "Ce monde ne vous appartient pas.";
                return false;
            }

            switch (voie)
            {
                case Voie.Deconcentration:
                    Deconcentrate(system);
                    break;

                case Voie.Compression:
                    Compress(system);
                    break;

                case Voie.Coercition:
                    if (_coercionActive)
                    {
                        error = "La garnison administre deja.";
                        return false;
                    }

                    Coerce();
                    break;

                default:
                    error = VoieCatalogue.TransformationPending;
                    return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Le monde passe en autonomie : il garde tout, mais il n'est plus a vous.
        /// <para>
        /// <b>Il reste developpe, et c'est tout le cout.</b> Un monde prospere sans proprietaire
        /// est une prise pour qui saura s'y installer — la colonisation existe deja et n'a besoin
        /// de rien de nouveau pour cela. La Deconcentration soulage la courbe et fabrique un
        /// voisin.
        /// </para>
        /// </summary>
        private void Deconcentrate(StarSystemState system)
        {
            system.OwnerId = StarSystemState.UnownedOwnerId;

            Record(NoticeKind.VoieTaken, $"{system.Name} passe en autonomie.", system.Id);
        }

        /// <summary>
        /// Le monde est evacue puis abandonne.
        /// <para>
        /// <b>Rien n'y reste, mais tout le monde vous en veut.</b> C'est l'exact contraire de la
        /// Deconcentration : aucun voisin ne nait de cet abandon, seulement une reputation. Les
        /// deux voies rendent un monde a personne, et c'est la seule chose qu'elles ont en commun.
        /// </para>
        /// </summary>
        private void Compress(StarSystemState system)
        {
            system.Population = 0;
            system.DevelopmentLevel = 0;
            system.Stability = 1f;
            system.OwnerId = StarSystemState.UnownedOwnerId;

            ResolveServices();

            if (_diplomacy != null && _empires != null)
            {
                foreach (Empire empire in _empires.Empires)
                {
                    if (empire == null || empire.Id == EconomyService.PlayerOwnerId)
                    {
                        continue;
                    }

                    _diplomacy.ApplyOpinionShift(empire.Id, EconomyService.PlayerOwnerId, -CompressionOpinionPenalty);
                }
            }

            Record(NoticeKind.VoieTaken, $"{system.Name} est evacuee et abandonnee.", system.Id);
        }

        /// <summary>
        /// La garnison remplace l'administration.
        /// <para>
        /// Le basculement du cout lui-meme vit dans <c>EconomyService</c>, qui interroge
        /// <see cref="IsCoercionActive"/> a chaque bilan mensuel. Ici on ne fait qu'encaisser le
        /// choc du jour ou la decision est prise.
        /// </para>
        /// </summary>
        private void Coerce()
        {
            _coercionActive = true;

            if (_map != null)
            {
                foreach (StarSystemState system in _map.Systems)
                {
                    if (system.OwnerId == EconomyService.PlayerOwnerId)
                    {
                        system.Stability = Mathf.Clamp01(system.Stability - CoercionStabilityShock);
                    }
                }
            }

            Record(NoticeKind.VoieTaken, "La garnison administre desormais vos mondes.", null);
        }

        private bool OwnedByPlayer(StarSystemId systemId, out StarSystemState system)
        {
            system = null;

            return _map != null
                   && _map.TryGetSystem(systemId, out system)
                   && system.OwnerId == EconomyService.PlayerOwnerId;
        }

        private void Record(NoticeKind kind, string text, StarSystemId? subject)
        {
            ResolveServices();
            _chronicle?.Log.Add(new GameNotice(kind, text, Today(), subject));
        }

        private GameDate Today()
        {
            return _clock != null ? _clock.CurrentDate : GameDate.StartOfGame;
        }

        private void ResolveServices()
        {
            if (_codex == null) ServiceLocator.TryGet(out _codex);
            if (_chronicle == null) ServiceLocator.TryGet(out _chronicle);
            if (_diplomacy == null) ServiceLocator.TryGet(out _diplomacy);
            if (_empires == null) ServiceLocator.TryGet(out _empires);
            if (_clock == null) ServiceLocator.TryGet(out _clock);
        }
    }
}
