using System.Collections.Generic;
using Espace.Gameplay.Diplomacy;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;

namespace Espace.Gameplay.Empires
{
    /// <summary>Ce qu'un systeme reclame de son proprietaire.</summary>
    public enum SystemConcern
    {
        /// <summary>Stabilite sous le seuil critique : la revolte devient possible.</summary>
        Unstable = 0,

        /// <summary>Aucune garnison, et un voisin appartient a un empire en guerre contre nous.</summary>
        Exposed = 1
    }

    /// <summary>Un systeme qui demande quelque chose, et pourquoi.</summary>
    public readonly struct SystemAttention
    {
        public readonly StarSystemId SystemId;
        public readonly string Name;
        public readonly SystemConcern Concern;

        /// <summary>Stabilite du systeme. Sans objet hors de <see cref="SystemConcern.Unstable"/>.</summary>
        public readonly float Stability;

        public SystemAttention(StarSystemId systemId, string name, SystemConcern concern, float stability)
        {
            SystemId = systemId;
            Name = name;
            Concern = concern;
            Stability = stability;
        }

        /// <summary>Le motif en trois mots, tel que le panneau l'affiche a droite du nom.</summary>
        public string Describe()
        {
            switch (Concern)
            {
                case SystemConcern.Unstable:
                    return $"stabilite {Stability:P0}";

                case SystemConcern.Exposed:
                    return "sans garnison, ennemi mitoyen";

                default:
                    return string.Empty;
            }
        }
    }

    /// <summary>
    /// La liste « demandent quelque chose » du panneau Empire (Phase 24, etape 4).
    /// <para>
    /// <b>Le panneau Empire repond a une question — « est-ce que je vais bien ? »</b> Les quatre
    /// indicateurs y repondent globalement, et <see cref="EmpireAssessment.Explain"/> en une
    /// phrase. Il manquait la suite immediate : <i>et ou faut-il aller ?</i> Sans cette liste, un
    /// empire de quarante systemes oblige a ouvrir quarante fiches pour trouver les trois qui
    /// posent probleme.
    /// </para>
    /// <para>
    /// <b>Seuls des etats, jamais des evenements.</b> La maquette proposait aussi « chantier
    /// acheve ». C'est un fait ponctuel, pas une demande : il appartient au journal, qui le
    /// rapporte deja. Un systeme figure ici tant que la situation dure, et en disparait des
    /// qu'elle cesse — sans que personne ait a penser a l'en retirer.
    /// </para>
    /// <para>
    /// <b>Meme seuil que l'IA.</b> « Instable » veut dire sous
    /// <see cref="EmpireAssessment.CriticalStability"/>, la valeur exacte qui fait basculer un
    /// empire IA en consolidation et qui fait virer le halo d'un systeme a l'ambre sur la carte.
    /// Le joueur, la machine et la carte lisent la meme limite ; aucun nombre n'a ete invente
    /// pour ce panneau.
    /// </para>
    /// <para>
    /// <b>Ecrit dans une liste fournie par l'appelant.</b> <c>OnGUI</c> est appele plusieurs fois
    /// par frame : retourner une liste neuve produirait des ordures a chaque image. Le projet
    /// proscrit les allocations recurrentes, donc l'appelant garde sa liste et la fait remplir.
    /// </para>
    /// </summary>
    public static class SystemAttentionList
    {
        /// <summary>
        /// Nombre d'entrees affichables.
        /// <para>
        /// Le panneau fait 286 unites de haut au pire, en-tete et indicateurs compris. Au-dela de
        /// cinq lignes, la liste deborde ou force un defilement — et une liste d'urgences qu'il
        /// faut faire defiler a cesse d'etre une liste d'urgences.
        /// </para>
        /// </summary>
        public const int MaximumEntries = 5;

        /// <summary>
        /// Remplit <paramref name="into"/> des systemes de <paramref name="empireId"/> qui
        /// demandent quelque chose, du plus urgent au moins urgent.
        /// <para>
        /// L'ordre est : les instables d'abord, du plus bas au plus haut, puis les exposes. Une
        /// revolte se declenche toute seule ; une frontiere degarnie ne coute que si l'adversaire
        /// en profite.
        /// </para>
        /// </summary>
        public static void Fill(
            List<SystemAttention> into,
            int empireId,
            GalaxyMap map,
            IMilitaryService military,
            IDiplomacyService diplomacy)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();

            if (map == null)
            {
                return;
            }

            foreach (StarSystemState system in map.Systems)
            {
                if (system.OwnerId != empireId)
                {
                    continue;
                }

                if (system.Stability < EmpireAssessment.CriticalStability)
                {
                    into.Add(new SystemAttention(system.Id, system.Name, SystemConcern.Unstable, system.Stability));
                    continue;
                }

                // Un systeme instable est deja signale : inutile de lui reprocher aussi sa
                // garnison. Une ligne par systeme, sinon la liste se remplit de doublons.
                if (IsExposed(system, empireId, map, military, diplomacy))
                {
                    into.Add(new SystemAttention(system.Id, system.Name, SystemConcern.Exposed, system.Stability));
                }
            }

            Sort(into);

            if (into.Count > MaximumEntries)
            {
                into.RemoveRange(MaximumEntries, into.Count - MaximumEntries);
            }
        }

        /// <summary>
        /// Vrai si le systeme n'a aucune garnison et touche un monde tenu par un empire en guerre
        /// contre nous.
        /// <para>
        /// <b>La mitoyennete compte autant que la garnison vide.</b> En debut de partie la moitie
        /// des mondes sont degarnis sans que cela pose le moindre probleme ; ce qui rend un
        /// systeme expose, c'est qu'une flotte hostile puisse l'atteindre au prochain saut. Sans
        /// cette condition, la liste afficherait tout et n'apprendrait rien.
        /// </para>
        /// </summary>
        private static bool IsExposed(
            StarSystemState system,
            int empireId,
            GalaxyMap map,
            IMilitaryService military,
            IDiplomacyService diplomacy)
        {
            if (military == null || diplomacy == null)
            {
                return false;
            }

            if (military.GetGarrison(system.Id, empireId).TotalCount > 0)
            {
                return false;
            }

            foreach (StarSystemId neighborId in map.GetNeighbors(system.Id))
            {
                if (!map.TryGetSystem(neighborId, out StarSystemState neighbor))
                {
                    continue;
                }

                if (neighbor.OwnerId == StarSystemState.UnownedOwnerId || neighbor.OwnerId == empireId)
                {
                    continue;
                }

                if (diplomacy.GetStatus(empireId, neighbor.OwnerId) == DiplomaticStatus.War)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tri par insertion, en place.
        /// <para>
        /// <c>List.Sort</c> avec une lambda alloue un comparateur a chaque appel, et cette liste
        /// se recalcule regulierement. Sur cinq a quarante entrees deja presque triees,
        /// l'insertion est de toute facon plus rapide.
        /// </para>
        /// </summary>
        private static void Sort(List<SystemAttention> entries)
        {
            for (int i = 1; i < entries.Count; i++)
            {
                SystemAttention current = entries[i];
                int j = i - 1;

                while (j >= 0 && IsMoreUrgent(current, entries[j]))
                {
                    entries[j + 1] = entries[j];
                    j--;
                }

                entries[j + 1] = current;
            }
        }

        private static bool IsMoreUrgent(SystemAttention a, SystemAttention b)
        {
            if (a.Concern != b.Concern)
            {
                return a.Concern < b.Concern;
            }

            return a.Concern == SystemConcern.Unstable && a.Stability < b.Stability;
        }
    }
}
