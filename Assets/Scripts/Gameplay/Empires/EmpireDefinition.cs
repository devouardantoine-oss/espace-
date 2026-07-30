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

        public string DisplayName => displayName;
        public Color Color => color;
        public EmpirePersonality Personality => personality;
        public bool IsPlayerControlled => isPlayerControlled;
    }
}
