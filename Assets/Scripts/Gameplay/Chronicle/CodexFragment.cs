using System.Collections.Generic;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Research;

namespace Espace.Gameplay.Chronicle
{
    /// <summary>Ce qui fait apparaitre un fragment.</summary>
    public enum FragmentTriggerKind
    {
        /// <summary>La pression administrative du joueur franchit un seuil.</summary>
        AdministrativePressure = 0,

        /// <summary>Une faction perd son dernier systeme, et ses archives deviennent lisibles.</summary>
        FactionAnnihilated = 1,

        /// <summary>Le joueur acheve le dernier palier d'un domaine de recherche.</summary>
        ResearchMastered = 2
    }

    /// <summary>
    /// Etat du monde tel que le codex a besoin de le lire (Phase 24, etape 3).
    /// <para>
    /// Rassembler les trois seules donnees qui declenchent un fragment evite a
    /// <see cref="CodexLibrary"/> de connaitre <c>GalaxyMap</c>, <c>IEconomyService</c> ou
    /// <c>IResearchService</c> — donc de rendre les regles verifiables sans scene ni service,
    /// comme <see cref="SystemGlyph"/> ou <c>EmpireStateBand</c> avant elles.
    /// </para>
    /// </summary>
    public readonly struct CodexWorldState
    {
        /// <summary>Pression administrative du joueur, de 0 a <c>AdministrationModel.MaximumPressure</c>.</summary>
        public readonly float AdministrativePressure;

        private readonly IReadOnlyCollection<FactionLineage> _annihilated;
        private readonly IReadOnlyCollection<ResearchDomain> _mastered;

        public CodexWorldState(
            float administrativePressure,
            IReadOnlyCollection<FactionLineage> annihilatedLineages,
            IReadOnlyCollection<ResearchDomain> masteredDomains)
        {
            AdministrativePressure = administrativePressure;
            _annihilated = annihilatedLineages;
            _mastered = masteredDomains;
        }

        /// <summary>Vrai si cette faction a perdu son dernier systeme.</summary>
        public bool IsAnnihilated(FactionLineage lineage)
        {
            return _annihilated != null && Contains(_annihilated, lineage);
        }

        /// <summary>Vrai si le joueur a acheve le dernier palier de ce domaine.</summary>
        public bool IsMastered(ResearchDomain domain)
        {
            return _mastered != null && Contains(_mastered, domain);
        }

        // Boucle explicite plutot que Linq.Contains : ce test tourne pour douze fragments a
        // chaque jour de jeu, et le projet proscrit les allocations par frame.
        private static bool Contains<T>(IReadOnlyCollection<T> values, T sought)
        {
            foreach (T value in values)
            {
                if (EqualityComparer<T>.Default.Equals(value, sought))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Un fragment du journal de l'Empire disparu (Phase 24, etape 3).
    /// <para>
    /// <b>Ce n'est pas un journal intime, c'est une lettre.</b> Son auteur occupait la place du
    /// joueur, savait qu'il y aurait un successeur, et lui ecrit directement. La ressemblance
    /// devient totale non par un tour de magie, mais parce que <b>la position fabrique la meme
    /// personne</b> : n'importe qui atteignant cette taille ecrit la meme chose et se trompe
    /// pareil.
    /// </para>
    /// <para>
    /// <b>La progression du ton porte tout le recit</b>, et elle est dans les textes eux-memes :
    /// le fragment I n'a ni signature ni « je » et passe pour un debris administratif ; le II
    /// dit « je » ; le III tutoie le lecteur. Le joueur comprend qu'on s'adresse a lui avant
    /// qu'on le lui dise.
    /// </para>
    /// <para>
    /// <b>Structure pure.</b> Aucun <c>MonoBehaviour</c>, aucun service : un fragment sait dire
    /// si l'etat du monde le declenche, et rien d'autre.
    /// </para>
    /// </summary>
    public readonly struct CodexFragment
    {
        /// <summary>
        /// Rang du fragment, de 1 a <see cref="CodexLibrary.Count"/>. C'est aussi la <b>cle de
        /// sauvegarde</b> : un numero deja publie ne se reattribue jamais a un autre texte.
        /// </summary>
        public readonly int Number;

        /// <summary>Chiffre romain affiche, « I » a « XII ».</summary>
        public readonly string Numeral;

        /// <summary>Titre court.</summary>
        public readonly string Title;

        /// <summary>Le texte de la lettre. Trois lignes au plus : voir la contrainte d'ecran.</summary>
        public readonly string Text;

        /// <summary>Ce que le fragment etablit, en une ligne, sous le texte.</summary>
        public readonly string Reveal;

        public readonly FragmentTriggerKind TriggerKind;

        /// <summary>Seuil de pression. Sans objet hors de <see cref="FragmentTriggerKind.AdministrativePressure"/>.</summary>
        public readonly float PressureThreshold;

        /// <summary>Faction a aneantir. Sans objet hors de <see cref="FragmentTriggerKind.FactionAnnihilated"/>.</summary>
        public readonly FactionLineage Lineage;

        /// <summary>Domaine a maitriser. Sans objet hors de <see cref="FragmentTriggerKind.ResearchMastered"/>.</summary>
        public readonly ResearchDomain Domain;

        /// <summary>
        /// Vrai pour le seul fragment III, sous lequel l'interface affiche les chiffres reels de
        /// l'empire du joueur. C'est le moment ou la lettre cesse de parler d'un autre.
        /// </summary>
        public readonly bool ShowsPlayerLedger;

        private CodexFragment(
            int number, string numeral, string title, string text, string reveal,
            FragmentTriggerKind triggerKind, float pressureThreshold,
            FactionLineage lineage, ResearchDomain domain, bool showsPlayerLedger)
        {
            Number = number;
            Numeral = numeral;
            Title = title;
            Text = text;
            Reveal = reveal;
            TriggerKind = triggerKind;
            PressureThreshold = pressureThreshold;
            Lineage = lineage;
            Domain = domain;
            ShowsPlayerLedger = showsPlayerLedger;
        }

        /// <summary>Fragment delivre par la pression administrative.</summary>
        public static CodexFragment FromPressure(
            int number, string numeral, string title, string text, string reveal,
            float threshold, bool showsPlayerLedger = false)
        {
            return new CodexFragment(
                number, numeral, title, text, reveal,
                FragmentTriggerKind.AdministrativePressure, threshold,
                FactionLineage.Unknown, default(ResearchDomain), showsPlayerLedger);
        }

        /// <summary>Fragment delivre par les archives d'une faction aneantie.</summary>
        public static CodexFragment FromArchives(
            int number, string numeral, string title, string text, string reveal,
            FactionLineage lineage)
        {
            return new CodexFragment(
                number, numeral, title, text, reveal,
                FragmentTriggerKind.FactionAnnihilated, 0f,
                lineage, default(ResearchDomain), false);
        }

        /// <summary>Fragment delivre par la maitrise d'un domaine de recherche.</summary>
        public static CodexFragment FromResearch(
            int number, string numeral, string title, string text, string reveal,
            ResearchDomain domain)
        {
            return new CodexFragment(
                number, numeral, title, text, reveal,
                FragmentTriggerKind.ResearchMastered, 0f,
                FactionLineage.Unknown, domain, false);
        }

        /// <summary>
        /// Vrai si l'etat du monde delivre ce fragment.
        /// <para>
        /// <b>La condition n'est jamais « depuis quand ».</b> Un fragment deja obtenu le reste
        /// meme si la pression retombe — mais cette memoire appartient a
        /// <see cref="CodexService"/>, pas ici : une fonction pure ne se souvient de rien.
        /// </para>
        /// </summary>
        /// <summary>
        /// Vrai si ce fragment est <b>hors d'atteinte par nature</b> pour un joueur de cette
        /// filiation.
        /// <para>
        /// Il n'existe qu'un cas : les archives de sa propre faction. On ne s'aneantit pas
        /// soi-meme, donc le seul document qu'un joueur ne recuperera jamais est celui de son
        /// propre peuple — parce qu'il <i>est</i> ce peuple. Chaque faction en ayant desormais
        /// un, la partie est symetrique : d'ou qu'on parte, on termine a onze fragments sur
        /// douze, et jamais les onze memes.
        /// </para>
        /// <para>
        /// L'interface s'en sert pour distinguer « pas encore trouve » de « ne sera jamais
        /// trouve ». Un joueur qui reste bloque a 11/12 sans explication croit avoir manque
        /// quelque chose ; c'est le contraire qu'on veut lui dire.
        /// </para>
        /// </summary>
        public bool IsBeyondReachFor(FactionLineage playerLineage)
        {
            return TriggerKind == FragmentTriggerKind.FactionAnnihilated
                   && playerLineage != FactionLineage.Unknown
                   && Lineage == playerLineage;
        }

        public bool IsDeliveredBy(CodexWorldState state)
        {
            switch (TriggerKind)
            {
                case FragmentTriggerKind.AdministrativePressure:
                    return state.AdministrativePressure > PressureThreshold;

                case FragmentTriggerKind.FactionAnnihilated:
                    return state.IsAnnihilated(Lineage);

                case FragmentTriggerKind.ResearchMastered:
                    return state.IsMastered(Domain);

                default:
                    return false;
            }
        }
    }
}
