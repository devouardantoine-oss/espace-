using System;
using System.Collections.Generic;
using Espace.Core;

namespace Espace.Gameplay.People
{
    /// <summary>
    /// Ce qu'un gouverneur retient de ce que son suzerain a fait (Phase 24, etape 6).
    /// <para>
    /// <b>Uniquement des faits dont il est temoin sur son propre monde.</b> Un gouverneur ne se
    /// formalise pas d'une bataille perdue a l'autre bout de la galaxie : il n'en sait rien.
    /// C'est la meme regle de confidentialite que la carte et le journal appliquent deja.
    /// </para>
    /// </summary>
    public enum GovernorFactKind
    {
        /// <summary>Le suzerain a envoye la troupe contre sa population.</summary>
        Repressed = 0,

        /// <summary>Le suzerain a paye pour apaiser.</summary>
        Conceded = 1,

        /// <summary>Le suzerain n'a rien fait.</summary>
        Ignored = 2,

        /// <summary>Une operation adverse a abouti sur son monde, sans qu'on l'en protege.</summary>
        Sabotaged = 3,

        /// <summary>Une bataille a ete gagnee ici : on est venu.</summary>
        Defended = 4,

        /// <summary>Une bataille a ete perdue ici.</summary>
        Abandoned = 5
    }

    /// <summary>Un fait, et le jour ou il s'est produit.</summary>
    public readonly struct GovernorFact
    {
        public readonly GovernorFactKind Kind;
        public readonly GameDate On;

        public GovernorFact(GovernorFactKind kind, GameDate on)
        {
            Kind = kind;
            On = on;
        }

        /// <summary>Le fait en une ligne, tel que la fiche du systeme l'affiche.</summary>
        public string Describe()
        {
            switch (Kind)
            {
                case GovernorFactKind.Repressed: return "la troupe est venue contre les siens";
                case GovernorFactKind.Conceded: return "on a paye pour apaiser";
                case GovernorFactKind.Ignored: return "personne n'a repondu";
                case GovernorFactKind.Sabotaged: return "une main etrangere a frappe ici";
                case GovernorFactKind.Defended: return "on est venu defendre";
                case GovernorFactKind.Abandoned: return "le monde est tombe une fois";
                default: return string.Empty;
            }
        }
    }

    /// <summary>
    /// La personne qui tient un monde pour le compte de son suzerain (Phase 24, etape 6).
    /// <para>
    /// <b>Meme technique de generation qu'<c>Admiral</c></b> (Phase 15) : un hachage
    /// deterministe de <c>(systemId, ownerId)</c>, jamais <c>System.Random</c> ni
    /// <c>UnityEngine.Random</c>. Deux parties de meme graine donnent les memes gouverneurs, et
    /// un gouverneur n'a pas besoin d'etre sauvegarde pour reapparaitre identique — seule sa
    /// memoire l'est.
    /// </para>
    /// <para>
    /// <b>Le temperament n'est pas une statistique de combat.</b> Un Amiral porte trois bonus qui
    /// modifient des batailles ; un gouverneur ne modifie rien du tout par lui-meme. Il
    /// <i>retient</i>, et ce qu'il retient finit par lui faire quitter son poste. C'est la seule
    /// chose qu'il fasse, et c'est ce qui transforme les chiffres des decisions en relation.
    /// </para>
    /// <para>
    /// <b>Il ne se souvient que de trois faits.</b> Ce n'est pas une economie de memoire, c'est
    /// une regle de jeu : une rancune finit par s'effacer, et un joueur qui a mal agi une fois
    /// doit pouvoir se racheter en trois bonnes decisions. Un registre infini rendrait toute
    /// erreur definitive.
    /// </para>
    /// </summary>
    public sealed class Governor
    {
        /// <summary>Nombre de faits retenus. Au-dela, le plus ancien s'efface.</summary>
        public const int MemorySize = 3;

        private static readonly string[] FirstNames =
        {
            "Oreth", "Salia", "Kervan", "Iona", "Tarsem", "Velya",
            "Dorn", "Ashaia", "Merik", "Sunel", "Yavor", "Nesha"
        };

        private static readonly string[] Surnames =
        {
            "Aldruin", "Bekhara", "Corvel", "Dashiv", "Emberly", "Farrow",
            "Ghiselle", "Harnok", "Iveline", "Joreth", "Kaltan", "Lomirr"
        };

        private readonly List<GovernorFact> _memory = new List<GovernorFact>(MemorySize);

        /// <summary>Nom complet, stable pour un couple (systeme, suzerain) donne.</summary>
        public string Name { get; }

        /// <summary>
        /// Loyaute de depart, avant tout fait retenu. Un gouverneur n'est ni un fanatique ni un
        /// traitre en puissance : la plage est etroite, et c'est ce que le joueur en fait qui
        /// compte.
        /// </summary>
        public float BaseLoyalty { get; }

        /// <summary>Les faits retenus, du plus ancien au plus recent.</summary>
        public IReadOnlyList<GovernorFact> Memory
        {
            get { return _memory; }
        }

        public Governor(string name, float baseLoyalty)
        {
            Name = name;
            BaseLoyalty = baseLoyalty;
        }

        /// <summary>
        /// Le gouverneur d'un systeme pour un suzerain donne.
        /// <para>
        /// <b>Le suzerain entre dans le hachage</b>, comme l'empire entre dans celui d'un Amiral :
        /// prendre un monde a l'adversaire y installe <i>quelqu'un d'autre</i>, avec sa propre
        /// memoire vierge. Le gouverneur precedent est parti avec l'ancien regime.
        /// </para>
        /// </summary>
        public static Governor Compute(int systemId, int ownerId)
        {
            string firstName = FirstNames[Mix(systemId, ownerId, 1) % (uint)FirstNames.Length];
            string surname = Surnames[Mix(systemId, ownerId, 2) % (uint)Surnames.Length];

            // 0,50 a 0,70 : etroit deliberement. Voir BaseLoyalty.
            float temperament = 0.50f + (Mix(systemId, ownerId, 3) % 200u) / 1000f;

            return new Governor($"{firstName} {surname}", temperament);
        }

        /// <summary>
        /// Ajoute un fait, en oubliant le plus ancien s'il faut faire de la place.
        /// </summary>
        public void Remember(GovernorFact fact)
        {
            _memory.Add(fact);

            if (_memory.Count > MemorySize)
            {
                _memory.RemoveAt(0);
            }
        }

        /// <summary>Restaure une memoire sauvegardee, en respectant le plafond.</summary>
        public void RestoreMemory(IEnumerable<GovernorFact> facts)
        {
            _memory.Clear();

            if (facts == null)
            {
                return;
            }

            foreach (GovernorFact fact in facts)
            {
                Remember(fact);
            }
        }

        /// <summary>Variante de la finalisation MurmurHash3 32 bits, identique a celle d'<c>Admiral</c>.</summary>
        private static uint Mix(int systemId, int ownerId, int salt)
        {
            unchecked
            {
                uint h = (uint)systemId;
                h = h * 0x9E3779B1u + (uint)ownerId;
                h ^= (uint)salt * 0x85EBCA77u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
