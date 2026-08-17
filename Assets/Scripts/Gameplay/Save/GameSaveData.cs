using System;
using System.Collections.Generic;

namespace Espace.Gameplay.Save
{
    /// <summary>
    /// Arbre de donnees serialisable par <c>JsonUtility</c> representant une sauvegarde
    /// complete de partie.
    /// <para>
    /// <b>Classes a plat, pas les types du jeu directement :</b> <c>JsonUtility</c> ne
    /// serialise ni les dictionnaires, ni les <c>Nullable</c>, ni les references vers des
    /// <c>ScriptableObject</c> ; et les structs du jeu (<see cref="Espace.Data.ResourceBundle"/>,
    /// <see cref="Espace.Gameplay.Military.UnitBundle"/>...) ne portent pas l'attribut
    /// <c>[Serializable]</c>. Chaque enregistrement ci-dessous est donc une classe dediee,
    /// entierement composee de types primitifs et de <see cref="List{T}"/>, construite et lue
    /// par <c>SaveService</c> plutot que par les services eux-memes.
    /// </para>
    /// <para>
    /// <b>Ne sauvegarde que l'etat mutable, jamais le contenu regenerable :</b> la galaxie
    /// (positions, noms, gisements, routes) et le roster d'empires (identite, personnalite)
    /// sont entierement redetermines par <see cref="Espace.Gameplay.Galaxy.GalaxyConfig"/> et
    /// les assets <c>EmpireDefinition</c> a chaque lancement — desormais deterministes grace a
    /// la graine fixe (voir le commentaire de <c>GalaxyConfig</c>). Seul ce qui change au fil
    /// de la partie (proprietaire, tresor, garnisons, relations...) a besoin d'etre ecrit.
    /// </para>
    /// <para>
    /// <b>Limitations v1, documentees plutot que traitees comme des defauts :</b> les commandes
    /// de recrutement en cours et les propositions diplomatiques en attente ne sont pas
    /// sauvegardees. La fenetre de risque reste faible pour elles (sauvegarde automatique
    /// mensuelle, recrutements de quelques jours, propositions resolues quasi instantanement).
    /// </para>
    /// <para>
    /// <b>Les flottes en voyage, elles, sont sauvegardees depuis la Phase 17</b> (voir
    /// <see cref="FleetInTransitSaveData"/>). La justification d'origine — « les trajets ne
    /// durent que quelques jours » — est tombee avec le deplacement longue distance : une
    /// traversee dure desormais des semaines, et perdre une flotte partie depuis quinze jours
    /// parce qu'on a recharge n'aurait plus rien d'une limitation acceptable.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class GameSaveData
    {
        /// <summary>Incrementee a chaque changement de forme de ce fichier, pour detecter une sauvegarde d'une version incompatible du jeu.</summary>
        /// <remarks>
        /// 2 depuis la Phase 14 : <see cref="GarrisonSaveData"/> remplace le champ <c>SpaceFleet</c> par les quatre nouveaux types de vaisseaux et gagne <c>FleetName</c>.
        /// 3 depuis la Phase 15 : <see cref="GarrisonSaveData"/> gagne les quatre champs Amiral. Une sauvegarde d'une version anterieure ne les contient pas : <c>SaveService.Apply</c> ne doit alors surtout pas restaurer un Amiral « tout a zero » a partir des defauts <c>JsonUtility</c> — un nouvel Amiral est genere a la place, comme pour une toute nouvelle flotte.
        /// 4 depuis la Phase 17 : les flottes en voyage sont sauvegardees (<see cref="FleetsInTransit"/>). Une sauvegarde anterieure n'en contient aucune, ce qui est exactement le comportement d'avant : la liste reste simplement vide.
        /// 5 depuis la Phase 24 : les fragments du codex obtenus (<see cref="UnlockedFragments"/>). Une sauvegarde anterieure n'en contient aucun et se charge sans rien de special — <c>CodexService</c> relisant l'etat du monde chaque jour, elle retrouve des le lendemain tous les fragments que sa situation justifie. C'est la seule raison pour laquelle aucune migration n'est necessaire ici.
        /// 6 depuis la Phase 24, etape 5 : les decisions en attente (<see cref="Decisions"/>) et les ardoises pas encore echues (<see cref="Consequences"/>). Une sauvegarde anterieure n'en contient aucune, ce qui est exactement le comportement d'avant : les listes restent vides et le systeme repart d'une ardoise nette.
        /// </remarks>
        public int Version = 6;

        /// <summary>
        /// Vaut <c>(int)GameSpeed.Paused</c> si le temps etait en pause : <see cref="Espace.Core.IGameClock.IsPaused"/>
        /// n'est jamais sauvegarde separement, il se derive entierement de cette valeur (meme
        /// convention que <c>IGameClock</c> lui-meme).
        /// </summary>
        public GameDateData Date;
        public int Speed;

        public List<StarSystemSaveData> Systems = new List<StarSystemSaveData>();
        public List<BuildingSaveData> Buildings = new List<BuildingSaveData>();
        public List<EmpireEconomySaveData> Empires = new List<EmpireEconomySaveData>();
        public List<GarrisonSaveData> Garrisons = new List<GarrisonSaveData>();
        public List<FleetInTransitSaveData> FleetsInTransit = new List<FleetInTransitSaveData>();
        public DiplomacySaveData Diplomacy = new DiplomacySaveData();
        public List<ResearchProgressSaveData> Research = new List<ResearchProgressSaveData>();

        /// <summary>
        /// Numeros des fragments du codex obtenus (Phase 24, etape 3). Vide sur une sauvegarde
        /// anterieure a la version 5 — voir <see cref="Version"/> pour pourquoi cela suffit.
        /// </summary>
        public List<int> UnlockedFragments = new List<int>();

        /// <summary>Decisions posees et pas encore tranchees (Phase 24, etape 5).</summary>
        public List<PendingDecisionSaveData> Decisions = new List<PendingDecisionSaveData>();

        /// <summary>Ardoises contractees par une decision et pas encore echues.</summary>
        public List<ScheduledConsequenceSaveData> Consequences = new List<ScheduledConsequenceSaveData>();

        /// <summary>Prochain identifiant de decision, pour qu'un rechargement n'en reattribue pas un deja utilise.</summary>
        public int NextDecisionId = 1;
    }

    [Serializable]
    public sealed class GameDateData
    {
        public int Year;
        public int Month;
        public int Day;
    }

    /// <summary>Etat mutable d'un systeme, identifie par <c>StarSystemId.Value</c> (voir <see cref="GameSaveData"/>).</summary>
    [Serializable]
    public sealed class StarSystemSaveData
    {
        public int SystemId;
        public int OwnerId;
        public int Population;
        public int Wealth;
        public int DevelopmentLevel;
        public float Stability;
    }

    /// <summary>Un batiment acheve. Les constructions encore en cours ne sont pas sauvegardees (voir <see cref="GameSaveData"/>).</summary>
    [Serializable]
    public sealed class BuildingSaveData
    {
        public int SystemId;

        /// <summary>Cle de correspondance dans le catalogue au chargement : voir <see cref="Espace.Gameplay.Economy.BuildingType.DisplayName"/>.</summary>
        public string BuildingTypeDisplayName;
    }

    [Serializable]
    public sealed class EmpireEconomySaveData
    {
        public int EmpireId;
        public float Credits;
        public float Minerals;
        public float Energy;
        public float Food;
        public float Influence;
        public float TaxRate;
    }

    /// <summary>Garnison stationnee d'un empire sur un systeme (au plus une par paire, voir <c>MilitaryService</c>).</summary>
    [Serializable]
    public sealed class GarrisonSaveData
    {
        public int SystemId;
        public int OwnerId;
        public string FleetName;
        public int Infantry;
        public int Armored;
        public int SpecialForces;
        public int Fighter;
        public int Frigate;
        public int Cruiser;
        public int Battleship;

        /// <summary>Amiral de la flotte (Phase 15). Absent (defauts a zero) sur une sauvegarde anterieure a la version 3 — voir <see cref="GameSaveData.Version"/>.</summary>
        public string AdmiralName;
        public float AdmiralAttackBonus;
        public float AdmiralSpeedBonus;
        public float AdmiralDefenseBonus;
    }

    /// <summary>
    /// Flotte en cours de voyage (Phase 17) : itineraire, etape en cours et dates.
    /// <para>
    /// <b>Aucun identifiant de flotte n'est persiste</b> : rien nulle part n'y fait reference, et
    /// les identifiants sont de toute façon reattribues a chaque chargement. Ce sont le nom et
    /// l'Amiral qui doivent survivre — meme precedent que <see cref="GarrisonSaveData"/>.
    /// </para>
    /// <para>
    /// Une rencontre spatiale en attente n'est pas sauvegardee : au chargement les flottes
    /// reprennent leur etape sans en <i>entamer</i> une, donc aucun balayage ne se declenche et
    /// la rencontre est simplement oubliee. Le jeu etant deterministe avec un seul fichier de
    /// sauvegarde, « recharger pour retenter » n'existe pas ; la seule variable etait le choix du
    /// joueur, qu'il refera.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class FleetInTransitSaveData
    {
        public int OwnerId;
        public string FleetName;

        public int Infantry;
        public int Armored;
        public int SpecialForces;
        public int Fighter;
        public int Frigate;
        public int Cruiser;
        public int Battleship;

        public string AdmiralName;
        public float AdmiralAttackBonus;
        public float AdmiralSpeedBonus;
        public float AdmiralDefenseBonus;

        /// <summary>Itineraire complet, extremites incluses, en <c>StarSystemId.Value</c>.</summary>
        public List<int> Route = new List<int>();
        public int RouteIndex;
        public int OriginSystemId;

        public GameDateData JourneyStartDate;
        public GameDateData DepartureDate;
        public GameDateData LegArrivalDate;
        public bool IsRetreating;
    }

    [Serializable]
    public sealed class DiplomaticStatusSaveData
    {
        public int EmpireAId;
        public int EmpireBId;
        public int Status;
        public bool HasTradeTreaty;
    }

    [Serializable]
    public sealed class OpinionSaveData
    {
        public int ObserverId;
        public int TargetId;
        public float Value;
    }

    [Serializable]
    public sealed class EmbargoSaveData
    {
        public int FromEmpireId;
        public int ToEmpireId;
    }

    [Serializable]
    public sealed class DiplomacySaveData
    {
        public List<DiplomaticStatusSaveData> Statuses = new List<DiplomaticStatusSaveData>();
        public List<OpinionSaveData> Opinions = new List<OpinionSaveData>();
        public List<EmbargoSaveData> Embargoes = new List<EmbargoSaveData>();
    }

    [Serializable]
    public sealed class ResearchProgressSaveData
    {
        public int EmpireId;
        public int Domain;
        public int CompletedTiers;
        public float Progress;
        public bool IsActiveDomain;
    }

    /// <summary>
    /// Une decision en attente (Phase 24, etape 5).
    /// <para>
    /// <b>Seulement de quoi la reconstruire</b> — identifiant, genre, systeme, date. Les
    /// libelles et les couts viennent de <c>DecisionCatalogue</c>, qui est deterministe a partir
    /// de ces valeurs. Les ecrire ici aurait fige les textes d'une version dans les parties en
    /// cours.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class PendingDecisionSaveData
    {
        public int Id;
        public int Kind;
        public int SystemId;
        public GameDateData RaisedOn;
    }

    /// <summary>
    /// Une ardoise datee.
    /// <para>
    /// <b>Ses effets sont ecrits en toutes lettres</b>, contrairement a la decision qui les a
    /// produits : une ardoise est le resultat d'un choix deja fait, a des couts deja annonces au
    /// joueur. Les recalculer depuis le catalogue reviendrait a changer retroactivement le prix
    /// d'une decision prise sous d'autres conditions.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class ScheduledConsequenceSaveData
    {
        public int SystemId;
        public GameDateData DueOn;
        public float Credits;
        public float GarrisonFraction;
        public float Stability;
        public string Text;
    }
}
