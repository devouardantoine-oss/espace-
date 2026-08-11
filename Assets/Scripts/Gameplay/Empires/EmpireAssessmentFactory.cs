using System.Collections.Generic;
using Espace.Gameplay.Economy;
using Espace.Gameplay.Galaxy;
using Espace.Gameplay.Military;
using UnityEngine;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Construit l'<see cref="EmpireAssessment"/> d'un empire a partir de l'etat du jeu
    /// (Phase 22, P7).
    /// <para>
    /// <b>Pourquoi une classe a part.</b> Le calcul vivait au fond d'<see cref="AIDecisionMaker"/>,
    /// donc seul le module economique en beneficiait : recherche, espionnage, diplomatie et
    /// armee continuaient a decider chacun dans leur coin, sans savoir si l'empire etait au bord
    /// de la faillite ou en position de force. La documentation d'<see cref="EmpireAssessment"/>
    /// annoncait pourtant une photographie « calculee une fois par mois et partagee par tous ses
    /// preneurs de decision » — c'est ce fichier qui rend la phrase vraie.
    /// </para>
    /// <para>
    /// <b>Un seul calcul par empire et par mois.</b> <c>AIController</c> appelle
    /// <see cref="Assess"/> une fois, puis passe la structure aux cinq modules. Ce n'est pas
    /// qu'une economie de CPU : si chaque module recalculait de son cote, deux d'entre eux
    /// pourraient lire des valeurs differentes le meme mois — l'economie relachant les impots
    /// pendant que l'armee se prepare a la guerre.
    /// </para>
    /// <para>
    /// <b>Fonction pure vis-a-vis de Unity</b> (aucun <c>MonoBehaviour</c>, aucun
    /// <c>ServiceLocator</c>) : elle ne prend que les services dont elle a besoin en parametre,
    /// donc elle se verifie sans scene, comme les <c>DecisionMaker</c> qu'elle alimente.
    /// </para>
    /// </summary>
    public static class EmpireAssessmentFactory
    {
        /// <summary>
        /// Plafond du rapport de forces. Au-dela, « je domine tres largement » et « j'ecrase »
        /// appellent la meme decision : inutile de laisser une division par une puissance quasi
        /// nulle produire des nombres astronomiques.
        /// </summary>
        public const float MaximumMilitaryRatio = 5f;

        /// <summary>
        /// Photographie de la situation de l'empire, a partir de ce que le joueur voit lui-meme.
        /// <para>
        /// <b>Aucun bonus cache.</b> Tresorerie, stabilite moyenne, place restante, garnisons
        /// limitrophes : tout est lisible sur l'interface. Une IA mieux informee que le joueur
        /// serait une triche, et une triche n'apprend rien au joueur sur le jeu.
        /// </para>
        /// </summary>
        public static EmpireAssessment Assess(Empire empire, GalaxyMap map, IEconomyService economy, IMilitaryService military)
        {
            List<StarSystemState> owned = EmpireHoldings.OwnedSystems(empire.Id, map);
            if (owned.Count == 0)
            {
                return new EmpireAssessment(0f, 0f, 1f, 0f, 0);
            }

            float stabilitySum = 0f;
            int population = 0;
            int capacity = 0;

            foreach (StarSystemState system in owned)
            {
                stabilitySum += system.Stability;
                population += system.Population;
                capacity += PopulationModel.CapacityFor(system.DevelopmentLevel);
            }

            // Depenses de reference : le cout d'administration, seule charge recurrente que ce
            // module connaisse. L'entretien de flotte lui echappe, ce qui rend l'estimation
            // optimiste — donc prudente dans le bon sens : l'IA se croira un peu plus riche
            // qu'elle ne l'est, jamais l'inverse.
            float monthlyOutgoings = Mathf.Max(1f, AdministrationModel.InfluenceUpkeep(owned.Count));
            float runway = economy.GetTreasury(empire.Id).Credits / monthlyOutgoings;

            float headroom = capacity > 0 ? 1f - population / (float)capacity : 0f;

            return new EmpireAssessment(
                runway,
                stabilitySum / owned.Count,
                MilitaryRatioAgainstNeighbours(empire.Id, map, military),
                headroom,
                owned.Count);
        }

        /// <summary>
        /// Puissance militaire de l'empire rapportee a celle de son voisin le plus fort.
        /// <para>
        /// <b>Le rapport de forces se lit sur les seuls voisins immediats</b>, pas sur la
        /// galaxie entiere : un empire lointain trois fois plus puissant ne menace personne tant
        /// qu'aucune frontiere ne le separe de vous. C'est aussi ce que le joueur percoit en
        /// regardant sa carte, donc l'IA ne sait rien de plus que lui.
        /// </para>
        /// <para>
        /// <b>Sans voisin, le rapport vaut 1</b> — ni menace, ni proie. Un empire isole se juge
        /// donc sur sa seule situation interieure, ce qui est exactement ce qu'il devrait faire.
        /// A l'inverse, un empire arme face a des voisins desarmes obtient un rapport plafonne
        /// (voir <see cref="MaximumMilitaryRatio"/>).
        /// </para>
        /// </summary>
        public static float MilitaryRatioAgainstNeighbours(int empireId, GalaxyMap map, IMilitaryService military)
        {
            if (military == null)
            {
                return 1f;
            }

            List<int> neighbours = EmpireHoldings.NeighboringEmpires(empireId, map);
            if (neighbours.Count == 0)
            {
                // Ni menace ni proie : l'empire se juge sur sa seule situation interieure.
                return 1f;
            }

            float strongestNeighbour = 0f;
            foreach (int neighbourId in neighbours)
            {
                strongestNeighbour = Mathf.Max(strongestNeighbour, EmpireHoldings.TotalPower(neighbourId, map, military));
            }

            float own = EmpireHoldings.TotalPower(empireId, map, military);

            if (strongestNeighbour <= 0f)
            {
                return own > 0f ? MaximumMilitaryRatio : 1f;
            }

            return Mathf.Min(MaximumMilitaryRatio, own / strongestNeighbour);
        }
    }
}
