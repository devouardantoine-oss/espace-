using Espace.Gameplay.Empires;

namespace Espace.Tests.EditMode
{
    /// <summary>
    /// Situations de reference partagees par les tests des cinq preneurs de decision
    /// (Phase 22, P7).
    /// <para>
    /// Depuis que <see cref="EmpireAssessment"/> est passe aux cinq modules, chaque test doit
    /// declarer <b>dans quelle situation</b> se trouve l'empire qu'il observe. Nommer les quatre
    /// situations ici plutot que de recopier cinq nombres a chaque appel evite deux ennuis :
    /// un test qui verifierait la mauvaise posture sans que personne ne s'en apercoive, et une
    /// modification des seuils de <c>DerivePosture</c> qui obligerait a retoucher quatre-vingts
    /// appels.
    /// </para>
    /// <para>
    /// <b>Chaque fabrique verifie la posture qu'elle pretend produire</b> (voir
    /// <c>StrategicModelTests.Fixtures_ProduceThePostureTheyClaim</c>) : sans cela, un
    /// deplacement de seuil transformerait silencieusement tous les tests « empire sain » en
    /// tests « empire menace », et ils continueraient a passer en verifiant autre chose.
    /// </para>
    /// </summary>
    public static class AssessmentFixtures
    {
        /// <summary>
        /// Empire solvable, stable, a peine plus fort que son voisin, avec de la place chez lui :
        /// posture <see cref="StrategicPosture.Expanding"/>.
        /// <para>
        /// C'est la situation de reference de tous les tests anterieurs a la Phase 22 : sous
        /// cette posture, aucun module ne se comporte differemment d'avant.
        /// </para>
        /// </summary>
        public static EmpireAssessment Healthy()
        {
            return new EmpireAssessment(6f, 0.85f, 1.1f, 0.6f, 6);
        }

        /// <summary>Tresorerie exsangue : posture <see cref="StrategicPosture.Consolidating"/>.</summary>
        public static EmpireAssessment Broke()
        {
            return new EmpireAssessment(0.5f, 0.9f, 1.1f, 0.6f, 6);
        }

        /// <summary>Voisin nettement plus fort : posture <see cref="StrategicPosture.Defending"/>.</summary>
        public static EmpireAssessment Threatened()
        {
            return new EmpireAssessment(6f, 0.85f, 0.4f, 0.6f, 6);
        }

        /// <summary>Avantage militaire net et plus de place chez soi : posture <see cref="StrategicPosture.Aggressive"/>.</summary>
        public static EmpireAssessment Dominant()
        {
            return new EmpireAssessment(6f, 0.85f, 2.5f, 0.05f, 6);
        }
    }
}
