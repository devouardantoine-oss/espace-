using UnityEngine;

namespace Espace.Gameplay.Research
{
    /// <summary>
    /// Definition d'un palier de recherche au sein d'un domaine.
    /// <para>
    /// Meme pattern que <see cref="Espace.Gameplay.Military.UnitTypeDefinition"/> : le contenu
    /// du jeu (ici, les technologies) vit dans des assets editables, jamais en dur dans le
    /// code. Les paliers d'un meme domaine se completent dans l'ordre de <see cref="Tier"/>
    /// (1, 2, 3, ...), jamais en parallele — voir <see cref="IResearchService"/>.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "TechnologyDefinition", menuName = "Espace/Research/Technology Definition", order = 0)]
    public sealed class TechnologyDefinition : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private ResearchDomain domain;

        [Tooltip("Position de ce palier dans son domaine (1 = premier a rechercher). Doit etre unique et contigu par domaine.")]
        [SerializeField]
        [Min(1)]
        private int tier = 1;

        [Tooltip("Points de recherche necessaires pour completer ce palier, une fois le palier precedent du meme domaine acquis.")]
        [SerializeField]
        [Min(1f)]
        private float researchPointCost = 100f;

        [Tooltip("Bonus additionnel apporte par ce palier une fois acquis (ex. 0.05 = +5%), cumule avec les paliers precedents du meme domaine.")]
        [SerializeField]
        [Min(0f)]
        private float effectMagnitude = 0.05f;

        public string DisplayName => displayName;
        public ResearchDomain Domain => domain;
        public int Tier => tier;
        public float ResearchPointCost => researchPointCost;
        public float EffectMagnitude => effectMagnitude;
    }
}
