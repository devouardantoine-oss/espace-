using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Espace.Core
{
    /// <summary>
    /// Façade de journalisation du projet.
    /// <para>
    /// <b>Pourquoi ne pas appeler <c>Debug.Log</c> directement ?</b> Grace a
    /// <see cref="ConditionalAttribute"/>, le compilateur supprime <b>l'appel et ses
    /// arguments</b> quand le symbole n'est pas defini. Une ligne comme
    /// <c>GameLog.Info($"Credits: {amount}")</c> ne coute donc strictement rien en build
    /// release : l'interpolation de chaine n'est meme pas evaluee, ce qui elimine autant
    /// d'allocations et donc de pics de GC sur mobile.
    /// </para>
    /// <para>
    /// <see cref="Error"/> n'est volontairement pas conditionnel : on veut toujours voir
    /// les erreurs, y compris en production.
    /// </para>
    /// </summary>
    public static class GameLog
    {
        /// <summary>Symbole a ajouter dans Player Settings pour reactiver les logs dans un build release.</summary>
        private const string ForceLogsSymbol = "ESPACE_LOGS";

        /// <summary>Message d'information. Supprime des builds release.</summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [Conditional(ForceLogsSymbol)]
        public static void Info(string message)
        {
            Debug.Log(message);
        }

        /// <summary>Avertissement : situation anormale mais non bloquante. Supprime des builds release.</summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [Conditional(ForceLogsSymbol)]
        public static void Warning(string message)
        {
            Debug.LogWarning(message);
        }

        /// <summary>Erreur : toujours journalisee, y compris en release.</summary>
        public static void Error(string message)
        {
            Debug.LogError(message);
        }
    }
}
