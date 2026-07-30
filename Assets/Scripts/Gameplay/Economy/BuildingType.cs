using Espace.Data;
using UnityEngine;

namespace Espace.Gameplay.Economy
{
    /// <summary>
    /// Definition d'un type de batiment constructible sur un systeme.
    /// <para>
    /// Meme pattern que <see cref="Espace.Gameplay.Galaxy.GalaxyConfig"/> : le contenu du
    /// jeu (ici, les batiments) vit dans des assets editables, jamais en dur dans le code.
    /// </para>
    /// <para>
    /// <b>Portee Phase 4 :</b> un batiment n'a pas de niveau/amelioration — construit ou pas.
    /// Une progression multi-niveaux ajouterait des couts et une UI d'amelioration sans
    /// valeur de test immediate ; a envisager en Phase 12 (equilibrage) si besoin.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingType", menuName = "Espace/Economy/Building Type", order = 0)]
    public sealed class BuildingType : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [Tooltip("Ressource produite par ce batiment une fois construit.")]
        [SerializeField]
        private ResourceType producedResource;

        [Tooltip("Quantite produite par jour de jeu, une fois la construction terminee.")]
        [SerializeField]
        [Min(0f)]
        private float productionPerDay;

        [Tooltip("Cout en Credits, paye integralement au lancement de la construction.")]
        [SerializeField]
        [Min(0f)]
        private float creditsCost;

        [Tooltip("Duree de construction, en jours de jeu.")]
        [SerializeField]
        [Range(1, 60)]
        private int constructionDurationDays = 5;

        [Tooltip("Niveau de developpement minimal du systeme requis pour construire ce batiment.")]
        [SerializeField]
        [Range(0, 5)]
        private int minimumDevelopmentLevel;

        public string DisplayName => displayName;
        public ResourceType ProducedResource => producedResource;
        public float ProductionPerDay => productionPerDay;
        public float CreditsCost => creditsCost;
        public int ConstructionDurationDays => constructionDurationDays;
        public int MinimumDevelopmentLevel => minimumDevelopmentLevel;
    }
}
