using System;
using System.Collections.Generic;
using Espace.Gameplay.Economy;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Construit les <see cref="Empire"/> d'une partie à partir de leurs
    /// <see cref="EmpireDefinition"/>.
    /// <para>
    /// Fonction pure, testable sans <c>ServiceLocator</c> ni scène — même séparation que
    /// <see cref="Espace.Gameplay.Galaxy.GalaxyGenerator"/> vis-à-vis de
    /// <see cref="Espace.Gameplay.Galaxy.GalaxyMapController"/>.
    /// </para>
    /// </summary>
    public static class EmpireFactory
    {
        /// <summary>
        /// Attribue <see cref="EconomyService.PlayerOwnerId"/> (0) à la définition marquée
        /// <see cref="EmpireDefinition.IsPlayerControlled"/>, puis 1, 2, 3... aux autres dans
        /// l'ordre du tableau.
        /// </summary>
        /// <exception cref="ArgumentException">Si <paramref name="definitions"/> est vide ou contient une entrée nulle.</exception>
        public static Empire[] CreateEmpires(IReadOnlyList<EmpireDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
            {
                throw new ArgumentException("Au moins une definition d'empire est requise.", nameof(definitions));
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] == null)
                {
                    throw new ArgumentException($"La definition d'empire a l'index {i} est nulle.", nameof(definitions));
                }
            }

            EmpireDefinition playerDefinition = FindPlayerDefinition(definitions);

            var empires = new Empire[definitions.Count];
            empires[0] = new Empire(
                EconomyService.PlayerOwnerId, playerDefinition.DisplayName, playerDefinition.Color,
                playerDefinition.Personality, isPlayerControlled: true);

            int nextAiId = EconomyService.PlayerOwnerId + 1;
            int nextSlot = 1;
            for (int i = 0; i < definitions.Count; i++)
            {
                EmpireDefinition definition = definitions[i];
                if (ReferenceEquals(definition, playerDefinition))
                {
                    continue;
                }

                empires[nextSlot] = new Empire(nextAiId, definition.DisplayName, definition.Color, definition.Personality, isPlayerControlled: false);
                nextAiId++;
                nextSlot++;
            }

            return empires;
        }

        /// <summary>
        /// Trouve la définition marquée joueur. En l'absence d'une définition marquée, ou si
        /// plusieurs le sont, retient la première du tableau et journalise un avertissement :
        /// mieux vaut une partie jouable avec un joueur déterministe qu'un blocage au
        /// démarrage pour une erreur de configuration d'asset.
        /// </summary>
        private static EmpireDefinition FindPlayerDefinition(IReadOnlyList<EmpireDefinition> definitions)
        {
            EmpireDefinition found = null;
            int playerCount = 0;

            for (int i = 0; i < definitions.Count; i++)
            {
                EmpireDefinition definition = definitions[i];
                if (definition != null && definition.IsPlayerControlled)
                {
                    playerCount++;
                    found ??= definition;
                }
            }

            if (playerCount == 1)
            {
                return found;
            }

            Espace.Core.GameLog.Warning(playerCount == 0
                ? "[EmpireFactory] Aucune EmpireDefinition marquee 'isPlayerControlled' : la premiere du tableau est utilisee par defaut."
                : $"[EmpireFactory] {playerCount} EmpireDefinition marquees 'isPlayerControlled' : une seule est attendue, la premiere est retenue.");

            return found ?? definitions[0];
        }
    }
}
