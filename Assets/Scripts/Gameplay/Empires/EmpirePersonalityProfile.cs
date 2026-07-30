using System;
using Espace.Data;

namespace Espace.Gameplay.Empires
{
    /// <summary>Paramètres de décision associés à une <see cref="EmpirePersonality"/>.</summary>
    public readonly struct EmpirePersonalityProfileData
    {
        /// <summary>Taux d'imposition (0 à 1) que l'IA maintient sur son système.</summary>
        public readonly float PreferredTaxRate;

        /// <summary>
        /// Ordre de préférence des ressources à faire produire par un nouveau bâtiment.
        /// <c>null</c> signifie « pas d'ordre fixe » : voir <see cref="PicksCheapestAffordable"/>.
        /// </summary>
        public readonly ResourceType[] BuildPriority;

        /// <summary>
        /// Si vrai, ignore <see cref="BuildPriority"/> et construit le bâtiment finançable le
        /// moins cher — une vraie différence de comportement pour l'Opportuniste, pas
        /// seulement des nombres différents.
        /// </summary>
        public readonly bool PicksCheapestAffordable;

        /// <summary>
        /// Marge exigée avant d'investir dans le développement : le trésor doit atteindre
        /// <c>coût × InvestmentEagerness</c>. 1.0 = investit dès que finançable ; au-dessus,
        /// l'IA garde une réserve de précaution avant de dépenser.
        /// </summary>
        public readonly float InvestmentEagerness;

        public EmpirePersonalityProfileData(
            float PreferredTaxRate, ResourceType[] BuildPriority, bool PicksCheapestAffordable, float InvestmentEagerness)
        {
            this.PreferredTaxRate = PreferredTaxRate;
            this.BuildPriority = BuildPriority;
            this.PicksCheapestAffordable = PicksCheapestAffordable;
            this.InvestmentEagerness = InvestmentEagerness;
        }
    }

    /// <summary>
    /// Table statique associant chaque <see cref="EmpirePersonality"/> à ses paramètres de
    /// décision.
    /// <para>
    /// <b>Code, pas ScriptableObject :</b> une personnalité est un <i>comportement</i> (la
    /// façon dont l'IA arbitre ses décisions), pas du contenu qu'un game designer ajusterait
    /// librement sans toucher au code — à la différence de <see cref="Espace.Gameplay.Economy.BuildingType"/>
    /// ou <see cref="EmpireDefinition"/>, qui sont, eux, de vraies données de partie.
    /// </para>
    /// <para>
    /// Valeurs de depart raisonnables, explicitement destinees a etre affinees en Phase 12
    /// (equilibrage) — la logique (chaque personnalite privilegie des ressources et un
    /// rythme de depense differents) est ce qui compte pour l'instant.
    /// </para>
    /// </summary>
    public static class EmpirePersonalityProfile
    {
        /// <summary>Retourne les paramètres de décision de <paramref name="personality"/>.</summary>
        public static EmpirePersonalityProfileData Get(EmpirePersonality personality)
        {
            switch (personality)
            {
                case EmpirePersonality.Pacifist:
                    // Impots bas (population satisfaite), priorite a la Nourriture et
                    // l'Influence (stabilite et culture plutot que puissance brute), prudent.
                    return new EmpirePersonalityProfileData(
                        PreferredTaxRate: 0.20f,
                        BuildPriority: new[] { ResourceType.Food, ResourceType.Influence, ResourceType.Energy, ResourceType.Minerals, ResourceType.Credits },
                        PicksCheapestAffordable: false,
                        InvestmentEagerness: 1.5f);

                case EmpirePersonality.Expansionist:
                    // Impots bas (economise pour la croissance future), priorite au
                    // developpement de son unique systeme : investit tres volontiers.
                    return new EmpirePersonalityProfileData(
                        PreferredTaxRate: 0.20f,
                        BuildPriority: new[] { ResourceType.Influence, ResourceType.Food, ResourceType.Credits, ResourceType.Minerals, ResourceType.Energy },
                        PicksCheapestAffordable: false,
                        InvestmentEagerness: 1.1f);

                case EmpirePersonality.Mercantile:
                    // Impots eleves, priorite absolue aux Credits puis a l'Energie.
                    return new EmpirePersonalityProfileData(
                        PreferredTaxRate: 0.35f,
                        BuildPriority: new[] { ResourceType.Credits, ResourceType.Energy, ResourceType.Minerals, ResourceType.Food, ResourceType.Influence },
                        PicksCheapestAffordable: false,
                        InvestmentEagerness: 1.3f);

                case EmpirePersonality.Militarist:
                    // Impots moyens-eleves, priorite au Minerai et a l'Energie (materiel pour
                    // les futures armees de la Phase 6).
                    return new EmpirePersonalityProfileData(
                        PreferredTaxRate: 0.30f,
                        BuildPriority: new[] { ResourceType.Minerals, ResourceType.Energy, ResourceType.Credits, ResourceType.Food, ResourceType.Influence },
                        PicksCheapestAffordable: false,
                        InvestmentEagerness: 1.4f);

                case EmpirePersonality.Opportunist:
                    // Impots moyens, aucun ordre fixe : saisit ce qui est finançable au
                    // moindre cout plutot que de suivre un plan.
                    return new EmpirePersonalityProfileData(
                        PreferredTaxRate: 0.25f,
                        BuildPriority: null,
                        PicksCheapestAffordable: true,
                        InvestmentEagerness: 1.2f);

                default:
                    throw new ArgumentOutOfRangeException(nameof(personality), personality, "Personnalite inconnue.");
            }
        }
    }
}
