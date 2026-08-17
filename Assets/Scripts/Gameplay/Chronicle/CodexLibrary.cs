using System.Collections.Generic;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Empires;
using Espace.Gameplay.Research;

namespace Espace.Gameplay.Chronicle
{
    /// <summary>
    /// Les douze fragments du journal de l'Empire disparu (Phase 24, etape 3).
    /// <para>
    /// <b>Contenu, pas comportement</b> — meme separation que <c>EmpireDefinition</c> face a
    /// <c>EmpirePersonalityProfile</c>. Les textes vivent ici, la mecanique dans
    /// <see cref="CodexFragment"/>, la memoire dans <see cref="CodexService"/>.
    /// </para>
    /// <para>
    /// <b>Pourquoi une table en dur plutot que des ScriptableObjects.</b> Ces douze textes sont
    /// une trame, pas des reglages : leur ordre, leurs seuils et leur progression de ton sont
    /// solidaires, et un fragment modifie isolement dans l'inspecteur casserait le recit sans
    /// que rien ne le signale. Une table compilee les rend verifiables par des tests — c'est
    /// d'ailleurs un test, et non une relecture, qui garantit que les quatre seuils de pression
    /// sont strictement croissants et atteignables.
    /// </para>
    /// <para>
    /// <b>La contrainte d'ecran gouverne l'ecriture.</b> L'interface tient dans 700 x 286 unites
    /// logiques : un texte qui demande de lire plus de quarante mots d'un coup ne sera pas lu.
    /// Aucun fragment ne depasse trois lignes.
    /// </para>
    /// </summary>
    public static class CodexLibrary
    {
        /// <summary>
        /// Seuil du fragment IV.
        /// <para>
        /// <b>Il ne peut pas valoir exactement le plafond.</b>
        /// <c>AdministrationModel.Pressure</c> sature a
        /// <see cref="AdministrationModel.MaximumPressure"/> : une comparaison stricte a cette
        /// valeur ne serait jamais vraie, et le fragment le plus important du jeu ne serait
        /// jamais delivre. D'ou la marge — et le test qui verifie que chaque seuil reste
        /// atteignable.
        /// </para>
        /// </summary>
        public const float CeilingThreshold = AdministrationModel.MaximumPressure - 0.01f;

        /// <summary>
        /// Nombre de fragments delivrables en cours de partie. Le treizieme est vierge et arrive
        /// en fin de partie : c'est le joueur qui l'ecrit.
        /// </summary>
        public const int Count = 12;

        private static readonly CodexFragment[] Fragments =
        {
            // --- Par la pression : le recit suit ce que le joueur ressent, pas son score ------

            CodexFragment.FromPressure(
                1, "I", "Premiere gene",
                "« Note de service. La ligne d'entretien du secteur depasse la prevision de onze "
                + "pour cent. Sans objet : la prevision sera corrigee au prochain exercice. »",
                "Ni contexte, ni signature, ni « je ». Un debris administratif sans interet.",
                threshold: 0f),

            CodexFragment.FromPressure(
                2, "II", "La lenteur",
                "« Je n'arrive plus a savoir ce qui se passe dans mes propres provinces. Ce n'est "
                + "pas qu'on me cache des choses. C'est que plus personne n'a les moyens de me "
                + "les dire. »",
                "Premier « je ». Il decrit le symptome que vous venez de rencontrer.",
                threshold: 0.12f),

            CodexFragment.FromPressure(
                3, "III", "Les chiffres",
                "« Voici ce que ca coute de tenir ce que tu tiens. Compare avec tes propres "
                + "colonnes. Je te laisse trouver l'annee ou ca bascule. »",
                "Il passe au tutoiement. Il s'adresse a quelqu'un, et ce quelqu'un, c'est vous.",
                threshold: 0.25f,
                showsPlayerLedger: true),

            CodexFragment.FromPressure(
                4, "IV", "L'aveu",
                "« J'ai essaye les quatre. Je te les donne dans l'ordre ou je les ai tentees, pas "
                + "dans l'ordre ou je les recommande — je n'en recommande aucune. »",
                "Vous jouez une partie deja jouee.",
                threshold: CeilingThreshold),

            // --- Par les archives : chaque faction detenait un morceau, et le rend en mourant -

            CodexFragment.FromArchives(
                5, "V", "Archives du Cartel",
                "« Ordre d'effacement. Motif porte au registre : cout de conservation. Signe. »",
                "L'Empire a detruit sa propre memoire par economie, pas par honte.",
                FactionLineage.Confins),

            CodexFragment.FromArchives(
                6, "VI", "Livres de la Ligue",
                "« Exercice −612. Ecart : negatif. Exercice −611. Ecart : negatif. »",
                "Deux siecles de colonnes negatives. Tout le monde savait. Personne n'a rien dit.",
                FactionLineage.Oskar),

            CodexFragment.FromArchives(
                7, "VII", "Temoignage du Bastion",
                "« Nous avons tenu les secteurs pendant huit cents ans apres que les paiements "
                + "ont cesse. Personne n'est venu nous dire d'arreter. Personne n'est venu du "
                + "tout. »",
                "Les faits bruts, enfin lisibles — et vous ne les obtenez qu'en detruisant celui qui les portait.",
                FactionLineage.Drathmoor),

            CodexFragment.FromArchives(
                8, "VIII", "Memoire de Vharin",
                "« Vous demandez quand cela a commence. Cela n'a pas commence. Cela a toujours "
                + "ete en train de finir. »",
                "La seule chronologie continue de l'Unification a aujourd'hui.",
                FactionLineage.Vharin),

            CodexFragment.FromArchives(
                9, "IX", "Ce que l'Essaim est",
                "« Il y a eu une voix. Une seule fois. Elle a attendu quatre-vingt-onze jours. »",
                "L'Empire s'est ruine a encercler une chose qui avait tente de lui parler. Vous venez de l'aneantir aussi.",
                FactionLineage.Kethra),

            // --- Par la recherche : comprendre le monde, c'est comprendre qui l'a fait -------

            CodexFragment.FromResearch(
                10, "X", "Les routes",
                "« Le reseau n'est pas une decouverte. C'est une decision. Regarde ou il ne va pas. »",
                "Les hyperroutes sont l'oeuvre de la Dispersion. Les culs-de-sac sont des degats, pas des accidents.",
                ResearchDomain.Logistics),

            CodexFragment.FromResearch(
                11, "XI", "Le declassement",
                "« Certains mondes ont ete debranches pour que d'autres tiennent. Tu as sans "
                + "doute pris l'un des deux pour une chance. »",
                "L'inegalite des six mondes d'origine n'en est pas une : elle a ete decidee.",
                ResearchDomain.Economy),

            // Place en dernier, et non parmi les archives V a IX, pour deux raisons qui vont
            // dans le meme sens. La premiere est technique : les numeros sont la cle de
            // sauvegarde, et inserer ce fragment au milieu aurait decale les suivants — un
            // joueur ayant obtenu « Les routes » se serait retrouve avec les archives de l'Aube
            // a la place. La seconde est narrative, et c'est la vraie : le registre de l'Aube a
            // ete detruit. Ce n'est pas une archive, c'est une absence — et une page presque
            // effacee est exactement ce qui doit preceder la page entierement vierge.
            CodexFragment.FromArchives(
                12, "XII", "Le registre arrache",
                "« Registre de bord. Origine : efface. Cap : efface. Motif du depart : efface. "
                + "Les pages n'ont pas ete perdues — elles ont ete arrachees, dans l'ordre, par "
                + "quelqu'un qui prenait son temps. »",
                "L'Aube n'a pas egare sa carte. Son commandant l'a detruite pour que ses descendants ne cherchent jamais a rentrer.",
                FactionLineage.Aube)
        };

        /// <summary>Les douze fragments, dans l'ordre de leur numero.</summary>
        public static IReadOnlyList<CodexFragment> All
        {
            get { return Fragments; }
        }

        /// <summary>
        /// Le fragment de ce numero. Leve si le numero est hors de 1..<see cref="Count"/> :
        /// un numero inconnu vient forcement d'un defaut de code, pas d'une donnee de partie.
        /// </summary>
        public static CodexFragment ByNumber(int number)
        {
            return Fragments[number - 1];
        }

        /// <summary>Vrai si ce numero designe un fragment existant.</summary>
        public static bool IsKnownNumber(int number)
        {
            return number >= 1 && number <= Count;
        }
    }
}
