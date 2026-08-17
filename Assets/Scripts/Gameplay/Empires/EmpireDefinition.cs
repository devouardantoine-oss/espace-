using UnityEngine;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Définition éditable d'un empire : contenu de partie, pas comportement.
    /// <para>
    /// Même pattern que <see cref="Espace.Gameplay.Economy.BuildingType"/> : le nom, la
    /// couleur et l'affectation joueur/IA d'un empire sont des données qu'un game designer
    /// doit pouvoir ajuster sans toucher au code. La façon dont une personnalité <i>décide</i>
    /// (voir <see cref="EmpirePersonalityProfile"/>) reste en revanche du comportement, donc
    /// du code — distinction volontaire entre les deux.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "EmpireDefinition", menuName = "Espace/Empires/Empire Definition", order = 0)]
    public sealed class EmpireDefinition : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private Color color = Color.white;

        [Tooltip("Sans effet si isPlayerControlled est coche : les decisions du joueur sont humaines.")]
        [SerializeField]
        private EmpirePersonality personality;

        [Tooltip("Coche sur une seule des definitions du roster : c'est l'empire du joueur.")]
        [SerializeField]
        private bool isPlayerControlled;

        [Header("Identite (Phase 21.3)")]
        [Tooltip("Espece et forme politique, en une ligne. Exemple : « Chitineux · ruche a conscience repartie ».")]
        [SerializeField]
        private string speciesLine;

        [Tooltip("Devise, affichee entre guillemets sur l'ecran de choix.")]
        [SerializeField]
        private string motto;

        [TextArea(2, 5)]
        [Tooltip("Deux a trois phrases : d'ou vient cette civilisation et comment elle joue.")]
        [SerializeField]
        private string description;

        [Tooltip("Silhouette de l'embleme, tracee par FactionEmblemFactory.")]
        [SerializeField]
        private EmblemShape emblem = EmblemShape.Compass;

        [Header("Doctrine (Phase 21.3)")]
        [Tooltip("Purement indicatif : ces barres decrivent le style de jeu, elles ne modifient aucune regle.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float expansion = 0.5f;

        [SerializeField]
        [Range(0f, 1f)]
        private float industry = 0.5f;

        [SerializeField]
        [Range(0f, 1f)]
        private float technology = 0.5f;

        [SerializeField]
        [Range(0f, 1f)]
        private float diplomacy = 0.5f;

        [Tooltip("Ce que cette civilisation fait mieux que les autres, en une ligne.")]
        [SerializeField]
        private string strengthLine;

        [Tooltip("Ce qu'elle paie en echange, en une ligne.")]
        [SerializeField]
        private string weaknessLine;

        [Header("Filiation (Phase 24, etape 3)")]
        [Tooltip("Identite de la faction pour le codex. Laissee a Unknown, elle ne delivre aucune archive.")]
        [SerializeField]
        private FactionLineage lineage = FactionLineage.Unknown;

        public string DisplayName => displayName;
        public Color Color => color;
        public EmpirePersonality Personality => personality;
        public bool IsPlayerControlled => isPlayerControlled;

        /// <summary>
        /// Filiation de cette faction vis-a-vis de l'Empire disparu. Voir
        /// <see cref="FactionLineage"/> pour la raison d'etre de cette cle, distincte de
        /// <see cref="Personality"/>.
        /// </summary>
        public FactionLineage Lineage => lineage;

        /// <summary>Espece et forme politique, en une ligne.</summary>
        public string SpeciesLine => speciesLine;

        /// <summary>Devise de la civilisation.</summary>
        public string Motto => motto;

        /// <summary>Presentation en deux ou trois phrases.</summary>
        public string Description => description;

        /// <summary>Silhouette de l'embleme.</summary>
        public EmblemShape Emblem => emblem;

        /// <summary>
        /// Les quatre axes de doctrine, entre 0 et 1, dans l'ordre d'affichage.
        /// <para>
        /// <b>Purement descriptif.</b> Ces valeurs ne sont lues par aucun service : la maniere
        /// dont une personnalite decide reste dans <see cref="EmpirePersonalityProfile"/>, du
        /// code et non de la donnee. Les brancher sur la simulation serait un changement
        /// d'equilibrage deguise en habillage — ce sera une decision separee, si elle est prise.
        /// </para>
        /// </summary>
        public float Expansion => expansion;

        /// <inheritdoc cref="Expansion"/>
        public float Industry => industry;

        /// <inheritdoc cref="Expansion"/>
        public float Technology => technology;

        /// <inheritdoc cref="Expansion"/>
        public float Diplomacy => diplomacy;

        /// <summary>Ce que cette civilisation fait mieux que les autres.</summary>
        public string StrengthLine => strengthLine;

        /// <summary>Ce qu'elle paie en echange.</summary>
        public string WeaknessLine => weaknessLine;
    }
}
