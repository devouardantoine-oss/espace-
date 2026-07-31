using System;
using Espace.Data;
using Espace.Gameplay.Espionage;
using Espace.Gameplay.Research;

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

        /// <summary>
        /// Nombre total d'unités que l'IA cherche à maintenir en garnison avant d'envisager
        /// autre chose (colonisation, attaque). Sert aussi de réserve minimale à conserver
        /// avant de détacher une flotte.
        /// </summary>
        public readonly int TargetGarrisonSize;

        /// <summary>
        /// Si vrai, recrute le type d'unité le plus puissant qu'elle peut se permettre ; sinon
        /// le moins cher (garnison nombreuse mais peu spécialisée).
        /// </summary>
        public readonly bool PrefersStrongestUnit;

        /// <summary>
        /// Multiplicateur d'avantage de puissance exigé avant d'attaquer un système voisin
        /// (puissance propre ≥ puissance adverse estimée × ce seuil). <c>null</c> = n'attaque
        /// jamais — pas de mécanisme de guerre/paix avant la Phase 7 (Diplomatie), donc une
        /// personnalité pacifique ou commerçante ne doit avoir aucun moyen de déclencher un
        /// combat.
        /// </summary>
        public readonly float? AggressionThreshold;

        /// <summary>
        /// Multiplicateur de puissance de combat lié au commandement de cette personnalité
        /// (1 = neutre). Appliqué par <see cref="Espace.Gameplay.Military.CombatResolver"/>
        /// aux deux camps d'une bataille, attaquant comme défenseur.
        /// </summary>
        public readonly float CommandModifier;

        /// <summary>
        /// Opinion minimale (voir <c>IDiplomacyService.GetOpinion</c>) que cette personnalité
        /// exige du proposeur pour accepter une Alliance, un Pacte de non-agression ou un
        /// Traité commercial reçu. Peut être négative : une personnalité très conciliante
        /// accepte même d'un empire qu'elle n'apprécie pas encore.
        /// </summary>
        public readonly float MinOpinionToAcceptPact;

        /// <summary>
        /// Opinion au-delà de laquelle cette personnalité propose spontanément un Pacte de
        /// non-agression à un voisin en Paix simple (voir <c>DiplomacyDecisionMaker</c>).
        /// </summary>
        public readonly float ProactivePactOpinionThreshold;

        /// <summary>
        /// Rapport de puissance (la sienne divisée par celle de l'adversaire) en dessous
        /// duquel cette personnalité cherche à sortir d'un conflit : propose la paix si déjà
        /// en guerre, ou accepte le tribut d'un ultimatum plutôt que risquer la guerre sinon.
        /// Même logique que <see cref="AggressionThreshold"/> mais dans l'autre sens — capituler
        /// plutôt qu'attaquer.
        /// </summary>
        public readonly float PeacePowerRatioThreshold;

        /// <summary>
        /// Ordre de préférence des sept domaines de recherche (voir <c>ResearchDecisionMaker</c>) :
        /// l'IA active toujours le premier domaine de cette liste qui n'est pas encore
        /// recherché au maximum. Même pattern que <see cref="BuildPriority"/> — un ordre fixe
        /// plutôt qu'une répartition, pour une différence de comportement lisible entre
        /// personnalités.
        /// </summary>
        public readonly ResearchDomain[] ResearchPriority;

        /// <summary>
        /// Multiplicateur de puissance d'espionnage exigé avant de lancer une mission contre
        /// un adversaire (puissance propre ≥ contre-espionnage de la cible × ce seuil). Même
        /// convention que <see cref="AggressionThreshold"/>. <c>null</c> = n'espionne jamais.
        /// </summary>
        public readonly float? EspionageThreshold;

        /// <summary>Mission que cette personnalité tente en priorité quand l'espionnage est jugé favorable. Sans effet si <see cref="EspionageThreshold"/> est <c>null</c>.</summary>
        public readonly EspionageMissionType PreferredEspionageMission;

        /// <summary>
        /// Si vrai, concentre construction et investissement sur la capitale ; sinon rattrape
        /// systématiquement le système possédé le moins développé (Phase 18).
        /// <para>
        /// Le rattrapage est le défaut parce que c'est aussi la stratégie la moins chère —
        /// <c>GetInvestmentCost = (niveau + 1) × coût</c>, donc développer un système en retard
        /// coûte toujours moins que pousser plus haut celui qui est déjà en tête. Le Militariste
        /// fait exception : ses meilleures unités exigent un <c>MinimumDevelopmentLevel</c> de 4,
        /// il lui faut un bastion très développé plutôt que cinq systèmes médiocres.
        /// </para>
        /// </summary>
        public readonly bool DevelopsCapitalFirst;

        /// <summary>
        /// Distance maximale, en sauts hyperspatiaux, à laquelle cette personnalité envoie une
        /// flotte de colonisation ou d'attaque (Phase 18).
        /// <para>
        /// La différence de comportement la plus visible de la phase : l'Expansionniste essaime
        /// loin, le Pacifiste ne quitte pas ses abords immédiats. Borne aussi le parcours de
        /// <see cref="Espace.Gameplay.Military.FleetRouting.HopDistances"/>, donc le coût CPU.
        /// </para>
        /// </summary>
        public readonly int ExpansionRange;

        public EmpirePersonalityProfileData(
            float PreferredTaxRate, ResourceType[] BuildPriority, bool PicksCheapestAffordable, float InvestmentEagerness,
            int TargetGarrisonSize, bool PrefersStrongestUnit, float? AggressionThreshold, float CommandModifier,
            float MinOpinionToAcceptPact, float ProactivePactOpinionThreshold, float PeacePowerRatioThreshold,
            ResearchDomain[] ResearchPriority, float? EspionageThreshold, EspionageMissionType PreferredEspionageMission,
            bool DevelopsCapitalFirst, int ExpansionRange)
        {
            this.DevelopsCapitalFirst = DevelopsCapitalFirst;
            this.ExpansionRange = ExpansionRange;
            this.PreferredTaxRate = PreferredTaxRate;
            this.BuildPriority = BuildPriority;
            this.PicksCheapestAffordable = PicksCheapestAffordable;
            this.InvestmentEagerness = InvestmentEagerness;
            this.TargetGarrisonSize = TargetGarrisonSize;
            this.PrefersStrongestUnit = PrefersStrongestUnit;
            this.AggressionThreshold = AggressionThreshold;
            this.CommandModifier = CommandModifier;
            this.MinOpinionToAcceptPact = MinOpinionToAcceptPact;
            this.ProactivePactOpinionThreshold = ProactivePactOpinionThreshold;
            this.PeacePowerRatioThreshold = PeacePowerRatioThreshold;
            this.ResearchPriority = ResearchPriority;
            this.EspionageThreshold = EspionageThreshold;
            this.PreferredEspionageMission = PreferredEspionageMission;
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
    /// <para>
    /// <b>Agressivité (Phase 6) :</b> sans état de guerre/paix (Phase 7), une personnalité
    /// belliqueuse attaquerait un voisin sans justification narrative. Seuls le Militariste et
    /// l'Opportuniste ont un <see cref="EmpirePersonalityProfileData.AggressionThreshold"/>
    /// non nul ; Pacifiste et Commerçante n'attaquent jamais ; l'Expansionniste privilégie très
    /// largement la colonisation (seuil élevé, quasi jamais atteint).
    /// </para>
    /// <para>
    /// <b>Diplomatie (Phase 7) :</b> le Pacifiste et la Commerçante font confiance facilement
    /// (opinion minimale basse ou négative, seuil de proposition spontanée bas) et sortent
    /// vite d'une guerre qui tourne mal ; le Militariste se méfie (opinion minimale haute,
    /// propose rarement un pacte) et ne cède qu'écrasé ; l'Expansionniste et l'Opportuniste
    /// se situent entre les deux.
    /// </para>
    /// <para>
    /// <b>Recherche (Phase 8) :</b> l'ordre de <see cref="EmpirePersonalityProfileData.ResearchPriority"/>
    /// reflète la même logique que les autres priorités — le Militariste vise l'Armement puis
    /// la Logistique, la Commerçante l'Économie puis la Diplomatie, le Pacifiste la Diplomatie
    /// en premier, l'Expansionniste la Logistique (expansion plus rapide), l'Opportuniste
    /// l'Espionnage (encore sans effet avant la Phase 9, mais déjà accumulé).
    /// </para>
    /// <para>
    /// <b>Espionnage (Phase 9) :</b> le Pacifiste n'espionne jamais (<see cref="EmpirePersonalityProfileData.EspionageThreshold"/>
    /// nul), cohérent avec son refus de toute confrontation même déguisée. Les quatre autres
    /// personnalités espionnent avec une mission qui leur correspond : la Commerçante vole des
    /// technologies (avantage économique honnête... presque), le Militariste découvre les
    /// armées adverses (renseignement avant la bataille), l'Expansionniste sabote (affaiblir
    /// avant d'envahir), l'Opportuniste influence les gouvernements (le moyen le plus
    /// discret, cohérent avec son seuil de risque le plus bas).
    /// </para>
    /// <para>
    /// <b>Territoire (Phase 18) :</b> l'IA gérant enfin l'ensemble de ses systèmes, deux
    /// paramètres décident de sa façon de s'étendre. Le rayon d'expansion range les
    /// personnalités du casanier au conquérant — Pacifiste 2 sauts, Commerçante 3,
    /// Militariste et Opportuniste 4, Expansionniste 6 —, et seul le Militariste concentre
    /// son développement sur sa capitale (ses Cuirassés exigent un développement 4 ; les
    /// autres rattrapent leurs colonies, ce qui est aussi le moins cher).
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
                    // Petite garnison defensive uniquement, n'attaque jamais.
                    return new EmpirePersonalityProfileData(
                        PreferredTaxRate: 0.20f,
                        BuildPriority: new[] { ResourceType.Food, ResourceType.Influence, ResourceType.Energy, ResourceType.Minerals, ResourceType.Credits },
                        PicksCheapestAffordable: false,
                        InvestmentEagerness: 1.5f,
                        TargetGarrisonSize: 2,
                        PrefersStrongestUnit: false,
                        AggressionThreshold: null,
                        CommandModifier: 1.0f,
                        MinOpinionToAcceptPact: -20f,
                        ProactivePactOpinionThreshold: 10f,
                        PeacePowerRatioThreshold: 1.5f,
                        ResearchPriority: new[]
                        {
                            ResearchDomain.Diplomacy, ResearchDomain.Economy, ResearchDomain.Energy,
                            ResearchDomain.Industry, ResearchDomain.Logistics, ResearchDomain.Espionage, ResearchDomain.Weapons
                        },
                        EspionageThreshold: null,
                        PreferredEspionageMission: EspionageMissionType.StealTechnology,
                        DevelopsCapitalFirst: false,
                        ExpansionRange: 2);

                case EmpirePersonality.Expansionist:
                    // Impots bas (economise pour la croissance future), priorite au
                    // developpement de son unique systeme : investit tres volontiers.
                    // Garnison moderee ; preferre coloniser, n'attaque qu'avec un avantage
                    // ecrasant (2.5x) en dernier recours.
                    return new EmpirePersonalityProfileData(
                        PreferredTaxRate: 0.20f,
                        BuildPriority: new[] { ResourceType.Influence, ResourceType.Food, ResourceType.Credits, ResourceType.Minerals, ResourceType.Energy },
                        PicksCheapestAffordable: false,
                        InvestmentEagerness: 1.1f,
                        TargetGarrisonSize: 4,
                        PrefersStrongestUnit: false,
                        AggressionThreshold: 2.5f,
                        CommandModifier: 1.0f,
                        MinOpinionToAcceptPact: 10f,
                        ProactivePactOpinionThreshold: 30f,
                        PeacePowerRatioThreshold: 0.8f,
                        ResearchPriority: new[]
                        {
                            ResearchDomain.Logistics, ResearchDomain.Industry, ResearchDomain.Economy,
                            ResearchDomain.Energy, ResearchDomain.Diplomacy, ResearchDomain.Weapons, ResearchDomain.Espionage
                        },
                        EspionageThreshold: 1.5f,
                        PreferredEspionageMission: EspionageMissionType.Sabotage,
                        DevelopsCapitalFirst: false,
                        ExpansionRange: 6);

                case EmpirePersonality.Mercantile:
                    // Impots eleves, priorite absolue aux Credits puis a l'Energie. Garnison
                    // minimale (protege son commerce, ne cherche pas la confrontation),
                    // n'attaque jamais.
                    return new EmpirePersonalityProfileData(
                        PreferredTaxRate: 0.35f,
                        BuildPriority: new[] { ResourceType.Credits, ResourceType.Energy, ResourceType.Minerals, ResourceType.Food, ResourceType.Influence },
                        PicksCheapestAffordable: false,
                        InvestmentEagerness: 1.3f,
                        TargetGarrisonSize: 3,
                        PrefersStrongestUnit: false,
                        AggressionThreshold: null,
                        CommandModifier: 1.0f,
                        MinOpinionToAcceptPact: -10f,
                        ProactivePactOpinionThreshold: 20f,
                        PeacePowerRatioThreshold: 1.2f,
                        ResearchPriority: new[]
                        {
                            ResearchDomain.Economy, ResearchDomain.Diplomacy, ResearchDomain.Industry,
                            ResearchDomain.Energy, ResearchDomain.Logistics, ResearchDomain.Espionage, ResearchDomain.Weapons
                        },
                        EspionageThreshold: 1.3f,
                        PreferredEspionageMission: EspionageMissionType.StealTechnology,
                        DevelopsCapitalFirst: false,
                        ExpansionRange: 3);

                case EmpirePersonality.Militarist:
                    // Impots moyens-eleves, priorite au Minerai et a l'Energie (materiel des
                    // armees). Grande garnison de puissance maximale, attaque des qu'elle a
                    // ne serait-ce qu'un leger avantage (1.1x), bonus de commandement.
                    return new EmpirePersonalityProfileData(
                        PreferredTaxRate: 0.30f,
                        BuildPriority: new[] { ResourceType.Minerals, ResourceType.Energy, ResourceType.Credits, ResourceType.Food, ResourceType.Influence },
                        PicksCheapestAffordable: false,
                        InvestmentEagerness: 1.4f,
                        TargetGarrisonSize: 8,
                        PrefersStrongestUnit: true,
                        AggressionThreshold: 1.1f,
                        CommandModifier: 1.15f,
                        MinOpinionToAcceptPact: 40f,
                        ProactivePactOpinionThreshold: 60f,
                        PeacePowerRatioThreshold: 0.4f,
                        ResearchPriority: new[]
                        {
                            ResearchDomain.Weapons, ResearchDomain.Logistics, ResearchDomain.Industry,
                            ResearchDomain.Energy, ResearchDomain.Economy, ResearchDomain.Espionage, ResearchDomain.Diplomacy
                        },
                        EspionageThreshold: 1.2f,
                        PreferredEspionageMission: EspionageMissionType.DiscoverArmies,
                        DevelopsCapitalFirst: true,
                        ExpansionRange: 4);

                case EmpirePersonality.Opportunist:
                    // Impots moyens, aucun ordre fixe : saisit ce qui est finançable au
                    // moindre cout plutot que de suivre un plan. Garnison modeste, attaque
                    // seulement les cibles nettement plus faibles (1.6x) — opportuniste, pas
                    // temeraire.
                    return new EmpirePersonalityProfileData(
                        PreferredTaxRate: 0.25f,
                        BuildPriority: null,
                        PicksCheapestAffordable: true,
                        InvestmentEagerness: 1.2f,
                        TargetGarrisonSize: 5,
                        PrefersStrongestUnit: false,
                        AggressionThreshold: 1.6f,
                        CommandModifier: 1.0f,
                        MinOpinionToAcceptPact: 20f,
                        ProactivePactOpinionThreshold: 40f,
                        PeacePowerRatioThreshold: 0.9f,
                        ResearchPriority: new[]
                        {
                            ResearchDomain.Espionage, ResearchDomain.Diplomacy, ResearchDomain.Economy,
                            ResearchDomain.Weapons, ResearchDomain.Industry, ResearchDomain.Energy, ResearchDomain.Logistics
                        },
                        EspionageThreshold: 1.0f,
                        PreferredEspionageMission: EspionageMissionType.InfluenceGovernment,
                        DevelopsCapitalFirst: false,
                        ExpansionRange: 4);

                default:
                    throw new ArgumentOutOfRangeException(nameof(personality), personality, "Personnalite inconnue.");
            }
        }
    }
}
