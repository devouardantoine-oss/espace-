namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Personnalité d'un empire, qui pilote les décisions autonomes de l'IA
    /// (<see cref="AIDecisionMaker"/>) via <see cref="EmpirePersonalityProfile"/>.
    /// <para>
    /// Non utilisée pour l'empire du joueur (les décisions sont humaines), mais chaque
    /// <see cref="EmpireDefinition"/> en porte tout de même une par simplicité de modèle de
    /// données — un enum ne peut pas être nul.
    /// </para>
    /// </summary>
    public enum EmpirePersonality
    {
        Pacifist,
        Expansionist,
        Mercantile,
        Militarist,
        Opportunist
    }
}
