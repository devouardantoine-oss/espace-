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
    /// <b>Limitations v1, documentees plutot que traitees comme des defauts :</b> les flottes
    /// en transit, les commandes de recrutement en cours et les propositions diplomatiques en
    /// attente ne sont pas sauvegardees — seules les garnisons deja stationnees le sont. La
    /// fenetre de risque reste faible (sauvegarde automatique mensuelle, trajets de quelques
    /// jours, propositions resolues quasi instantanement) ; a revisiter en Phase 12 si
    /// necessaire.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class GameSaveData
    {
        /// <summary>Incrementee a chaque changement de forme de ce fichier, pour detecter une sauvegarde d'une version incompatible du jeu.</summary>
        /// <remarks>
        /// 2 depuis la Phase 14 : <see cref="GarrisonSaveData"/> remplace le champ <c>SpaceFleet</c> par les quatre nouveaux types de vaisseaux et gagne <c>FleetName</c>.
        /// 3 depuis la Phase 15 : <see cref="GarrisonSaveData"/> gagne les quatre champs Amiral. Une sauvegarde d'une version anterieure ne les contient pas : <c>SaveService.Apply</c> ne doit alors surtout pas restaurer un Amiral « tout a zero » a partir des defauts <c>JsonUtility</c> — un nouvel Amiral est genere a la place, comme pour une toute nouvelle flotte.
        /// </remarks>
        public int Version = 3;

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
        public DiplomacySaveData Diplomacy = new DiplomacySaveData();
        public List<ResearchProgressSaveData> Research = new List<ResearchProgressSaveData>();
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
}
