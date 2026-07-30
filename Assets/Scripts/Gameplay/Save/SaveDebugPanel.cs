using Espace.Core;
using UnityEngine;

namespace Espace.Gameplay.Save
{
    /// <summary>
    /// Panneau de diagnostic temporaire pour la sauvegarde : etat du fichier, boutons
    /// Sauvegarder maintenant / Recharger, dernier resultat.
    /// <para>
    /// Meme statut IMGUI temporaire que les autres panneaux depuis la Phase 2 ; seul emplacement
    /// encore libre, en bas au centre.
    /// </para>
    /// </summary>
    public sealed class SaveDebugPanel : MonoBehaviour
    {
        private const int PanelWidth = 260;
        private const int PanelHeight = 90;
        private const int Gap = 10;

        private ISaveService _save;
        private string _lastResult = "";

        private void OnGUI()
        {
            if (_save == null && !ServiceLocator.TryGet(out _save))
            {
                return;
            }

            var rect = new Rect((Screen.width - PanelWidth) / 2f, Screen.height - PanelHeight - Gap, PanelWidth, PanelHeight);
            GUI.Box(rect, string.Empty);

            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, PanelWidth - 16, PanelHeight - 12));

            GUILayout.Label(_save.SaveFileExists ? "Sauvegarde : presente" : "Sauvegarde : aucune");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sauvegarder maintenant"))
            {
                _save.SaveNow();
                _lastResult = "Sauvegarde ecrite.";
            }

            if (GUILayout.Button("Recharger"))
            {
                _lastResult = _save.TryLoadAndApply(out string error) ? "Sauvegarde rechargee." : $"Echec : {error}";
            }

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_lastResult))
            {
                GUILayout.Label(_lastResult);
            }

            GUILayout.EndArea();
        }
    }
}
