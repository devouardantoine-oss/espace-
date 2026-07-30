using UnityEngine;

namespace Espace.Gameplay.Military
{
    /// <summary>
    /// Definition d'un type d'unite recrutable.
    /// <para>
    /// Meme pattern que <see cref="Espace.Gameplay.Economy.BuildingType"/> : le contenu du
    /// jeu (ici, les unites) vit dans des assets editables, jamais en dur dans le code.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "UnitTypeDefinition", menuName = "Espace/Military/Unit Type Definition", order = 0)]
    public sealed class UnitTypeDefinition : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private UnitType unitType;

        [Tooltip("Puissance de combat d'une unite, utilisee telle quelle par CombatResolver.")]
        [SerializeField]
        [Min(0f)]
        private float power;

        [Tooltip("Distance parcourue par jour de jeu. La vitesse d'une flotte mixte est celle de son unite la plus lente.")]
        [SerializeField]
        [Min(0.1f)]
        private float speed;

        [Tooltip("Cout en Credits, paye integralement au lancement du recrutement.")]
        [SerializeField]
        [Min(0f)]
        private float creditsCost;

        [Tooltip("Cout en Minerais, paye integralement au lancement du recrutement.")]
        [SerializeField]
        [Min(0f)]
        private float mineralsCost;

        [Tooltip("Duree de recrutement, en jours de jeu (identique quel que soit le nombre d'unites commandees dans le meme ordre).")]
        [SerializeField]
        [Range(1, 60)]
        private int recruitmentDays = 3;

        [Tooltip("Cout d'entretien en Credits par jour et par unite en service.")]
        [SerializeField]
        [Min(0f)]
        private float upkeepPerDay;

        [Tooltip("Niveau de developpement minimal du systeme requis pour recruter ce type d'unite.")]
        [SerializeField]
        [Range(0, 5)]
        private int minimumDevelopmentLevel;

        public string DisplayName => displayName;
        public UnitType UnitType => unitType;
        public float Power => power;
        public float Speed => speed;
        public float CreditsCost => creditsCost;
        public float MineralsCost => mineralsCost;
        public int RecruitmentDays => recruitmentDays;
        public float UpkeepPerDay => upkeepPerDay;
        public int MinimumDevelopmentLevel => minimumDevelopmentLevel;
    }
}
