using Espace.Core;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using UnityEngine;

namespace Espace.UI
{
    /// <summary>
    /// Fenetre de decision d'une rencontre spatiale (Phase 17) : quand une flotte du joueur
    /// croise une flotte etrangere en route, elle propose les issues possibles selon le statut
    /// diplomatique.
    /// <para>
    /// <b>La mise en pause est purement cosmetique.</b> <c>GameClock.AdvanceDays</c> publie les
    /// <c>DayAdvancedEvent</c> en boucle dans une meme frame et ne teste <c>IsPaused</c> qu'a
    /// l'entree du tick ; le joueur peut de toute façon relancer le temps depuis la barre du HUD.
    /// La correction ne repose donc jamais sur la pause : les flottes concernees sont gelees dans
    /// <c>MilitaryService</c>, avec leurs jours de trajet restants memorises. La pause ne sert
    /// qu'a eviter que le monde defile pendant qu'on lit la question.
    /// </para>
    /// <para>
    /// Meme convention que <see cref="PauseMenuController"/> : on ne relache l'horloge que si
    /// c'est bien cette fenetre qui l'avait mise en pause.
    /// </para>
    /// <para>
    /// <b>Le choix remonte depuis <c>OnGUI</c>, pas depuis un gestionnaire d'evenement</b> : c'est
    /// tout l'interet de la file de rencontres cote service — la resolution se produit hors du
    /// parcours de la liste des flottes, jamais au milieu d'une mutation.
    /// </para>
    /// </summary>
    public sealed class EncounterWindowController : MonoBehaviour
    {
        private const int WindowWidth = 460;
        private const int WindowHeight = 300;

        private IEncounterService _encounters;
        private IMilitaryService _military;
        private IGameClock _gameClock;
        private GalaxyMap _map;
        private EmpireRegistry _empireRegistry;

        private bool _pausedByThisWindow;

        private void OnGUI()
        {
            UITheme.BeginScaledLayout();
            try
            {
                ResolveServices();

                if (_encounters == null)
                {
                    return;
                }

                PendingEncounter encounter = _encounters.GetPendingEncounterFor(EconomyService.PlayerOwnerId);

                if (encounter == null)
                {
                    ReleaseClock();
                    return;
                }

                HoldClock();
                DrawWindow(encounter);
            }
            finally
            {
                UITheme.EndScaledLayout();
            }
        }

        private void ResolveServices()
        {
            if (_encounters == null) ServiceLocator.TryGet(out _encounters);
            if (_military == null) ServiceLocator.TryGet(out _military);
            if (_gameClock == null) ServiceLocator.TryGet(out _gameClock);
            if (_map == null) ServiceLocator.TryGet(out _map);
            if (_empireRegistry == null) ServiceLocator.TryGet(out _empireRegistry);
        }

        private void HoldClock()
        {
            if (_gameClock != null && !_gameClock.IsPaused)
            {
                _gameClock.Pause();
                _pausedByThisWindow = true;
            }
        }

        private void ReleaseClock()
        {
            if (_gameClock != null && _pausedByThisWindow)
            {
                _gameClock.Resume();
            }

            _pausedByThisWindow = false;
        }

        private void DrawWindow(PendingEncounter encounter)
        {
            encounter.GetSides(EconomyService.PlayerOwnerId, out Fleet own, out Fleet opponent);

            var rect = new Rect((UITheme.ScreenWidth - WindowWidth) / 2f, (UITheme.ScreenHeight - WindowHeight) / 2f, WindowWidth, WindowHeight);
            GUI.Box(rect, string.Empty, UITheme.Panel);

            GUILayout.BeginArea(new Rect(rect.x + 14, rect.y + 12, WindowWidth - 28, WindowHeight - 24));

            GUILayout.Label("Rencontre spatiale", UITheme.Title);
            GUILayout.Label($"{own.Name} croise une flotte de {EmpireName(opponent.OwnerId)} entre {SystemName(encounter.LegFrom)} et {SystemName(encounter.LegTo)}.", UITheme.Label);
            GUILayout.Space(6);

            GUILayout.Label($"Votre flotte : {own.Composition.TotalCount} unites (puissance ~{Power(own):0})", UITheme.MutedLabel);
            GUILayout.Label($"En face : {opponent.Composition.TotalCount} unites (puissance ~{Power(opponent):0})", UITheme.MutedLabel);
            GUILayout.Label($"Statut diplomatique : {StatusLabel(encounter)}", UITheme.MutedLabel);

            GUILayout.Space(10);

            foreach (EncounterOption option in encounter.Options)
            {
                if (!GUILayout.Button(OptionLabel(option), UITheme.Button, GUILayout.Height(30)))
                {
                    continue;
                }

                if (!_encounters.TryResolveEncounter(encounter.Id, EconomyService.PlayerOwnerId, option, out string error))
                {
                    GameLog.Warning($"[Encounter] {error}");
                }

                ReleaseClock();
                break;
            }

            GUILayout.EndArea();
        }

        private float Power(Fleet fleet) => _military?.EstimatePower(fleet.Composition) ?? fleet.Composition.TotalCount;

        private string SystemName(StarSystemId systemId) =>
            _map != null && _map.TryGetSystem(systemId, out StarSystemState system) ? system.Name : systemId.ToString();

        private string EmpireName(int empireId) =>
            _empireRegistry != null && _empireRegistry.TryGetEmpire(empireId, out Empire empire) ? empire.Name : $"l'empire {empireId}";

        private static string StatusLabel(PendingEncounter encounter)
        {
            switch (encounter.Status)
            {
                case Gameplay.Diplomacy.DiplomaticStatus.War: return "Guerre";
                case Gameplay.Diplomacy.DiplomaticStatus.Alliance: return "Alliance";
                case Gameplay.Diplomacy.DiplomaticStatus.NonAggressionPact: return "Pacte de non-agression";
                default: return "Paix";
            }
        }

        private static string OptionLabel(EncounterOption option)
        {
            switch (option)
            {
                case EncounterOption.Fight: return "Combattre";
                case EncounterOption.Withdraw: return "Se replier";
                case EncounterOption.Negotiate: return "Negocier";
                case EncounterOption.Trade: return "Commercer";
                case EncounterOption.Piracy: return "Piraterie";
                default: return "Passer son chemin";
            }
        }
    }
}
