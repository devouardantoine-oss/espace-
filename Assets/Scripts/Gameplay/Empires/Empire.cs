using System;
using UnityEngine;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Identité d'un empire galactique : qui il est, jamais ce qu'il possède.
    /// <para>
    /// <b>Aucun territoire stocké ici (pas de <c>HomeSystemId</c>) :</b> « quels systèmes
    /// appartiennent à cet empire » reste dérivé à la volée de
    /// <see cref="Espace.Gameplay.Galaxy.StarSystemState.OwnerId"/>, la seule source de
    /// vérité déjà en place depuis la Phase 2. Dupliquer cette information ici l'exposerait
    /// à devenir périmée dès que la Phase 6 permettra de posséder plusieurs systèmes.
    /// </para>
    /// <para>
    /// Classe immuable : rien ne change jamais pour un empire une fois créé (contrairement
    /// à <see cref="Espace.Gameplay.Galaxy.StarSystemState"/>, dont les statistiques évoluent).
    /// </para>
    /// </summary>
    public sealed class Empire
    {
        /// <summary>Identifiant, identique à <see cref="Espace.Gameplay.Galaxy.StarSystemState.OwnerId"/> pour les systèmes qu'il possède.</summary>
        public int Id { get; }

        /// <summary>Nom affiché.</summary>
        public string Name { get; }

        /// <summary>Couleur d'identification (interface, cartes).</summary>
        public Color Color { get; }

        /// <summary>Personnalité pilotant les décisions autonomes. Sans effet si <see cref="IsPlayerControlled"/>.</summary>
        public EmpirePersonality Personality { get; }

        /// <summary>Vrai pour l'unique empire du joueur ; faux pour tous les empires IA.</summary>
        public bool IsPlayerControlled { get; }

        /// <summary>
        /// Filiation vis-a-vis de l'Empire disparu, recopiee de <c>EmpireDefinition</c> comme
        /// <see cref="Personality"/> (Phase 24, etape 3). Voir <see cref="FactionLineage"/>.
        /// </summary>
        public FactionLineage Lineage { get; }

        /// <param name="lineage">
        /// Optionnel : les empires construits avant la Phase 24 — et les fixtures de test qui
        /// n'ont rien a faire du codex — restent valides sans filiation declaree.
        /// </param>
        public Empire(
            int id, string name, Color color, EmpirePersonality personality, bool isPlayerControlled,
            FactionLineage lineage = FactionLineage.Unknown)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Le nom de l'empire ne peut pas etre vide.", nameof(name));
            }

            Id = id;
            Name = name;
            Color = color;
            Personality = personality;
            IsPlayerControlled = isPlayerControlled;
            Lineage = lineage;
        }

        public override string ToString() => Name;
    }
}
