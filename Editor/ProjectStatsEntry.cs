using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

public class ProjectStatsToolbarButton
{
    const string k_ElementName = "Project Stats/Open";

    [MainToolbarElement(k_ElementName, defaultDockPosition = MainToolbarDockPosition.Right)]
    static IEnumerable<MainToolbarElement> CreateButton()
    {
        var icon = EditorGUIUtility.IconContent("d_AnalyticsTracker Icon");
        yield return new MainToolbarButton(
            new MainToolbarContent("", icon.image as Texture2D, "Open Project Stats"),
            () => ProjectStatsWindow.ShowWindow()
        );
    }
}

public class ProjectStatsMenu
{
    [MenuItem("Project Stats/Open")]
    public static void Open()
    {
        ProjectStatsWindow.ShowWindow();
    }

    [MenuItem("Project Stats/Clear All Data")]
    public static void ClearAllData()
    {
        if (EditorUtility.DisplayDialog(
            "Clear All Data",
            "This will delete all saved stats including time, asset history, and version control data. Are you sure?",
            "Clear",
            "Cancel"))
        {
            EditorPrefs.DeleteKey(ProjectStatsData.KeyEditor);
            EditorPrefs.DeleteKey(ProjectStatsData.KeyPlay);
            EditorPrefs.DeleteKey(ProjectStatsData.KeyUnfocused);
            EditorPrefs.DeleteKey(ProjectStatsData.KeySessions);
            EditorPrefs.DeleteKey(ProjectStatsData.KeySessionID);
            EditorPrefs.DeleteKey(ProjectStatsData.KeySessionStartEditor);
            EditorPrefs.DeleteKey(ProjectStatsData.KeySessionStartPlay);
            EditorPrefs.DeleteKey(ProjectStatsData.KeySessionStartUnfocused);
            EditorPrefs.DeleteKey(ProjectStatsData.KeyCreationDate);

            ProjectStatsData.EditorTotal           = 0f;
            ProjectStatsData.PlayTotal             = 0f;
            ProjectStatsData.UnfocusedTotal        = 0f;
            ProjectStatsData.TotalSessions         = 0;
            ProjectStatsData.SessionStartEditor    = 0f;
            ProjectStatsData.SessionStartPlay      = 0f;
            ProjectStatsData.SessionStartUnfocused = 0f;
            ProjectStatsData.CachedCreationDate    = "";

            ProjectStatsHistory.ClearHistory();
            ProjectStatsData.Initialized = false;
        }
    }

    [MenuItem("Project Stats/Reinitialize")]
    public static void Reinitialize()
    {
        ProjectStatsData.Initialized = false;
    }

    [MenuItem("Project Stats/Export History as CSV")]
    public static void ExportCSV()
    {
        string path = EditorUtility.SaveFilePanel("Export History as CSV", "", "ProjectStatsHistory", "csv");
        if (string.IsNullOrEmpty(path)) return;
        ProjectStatsExporter.ExportCSV(path);
    }

    [MenuItem("Project Stats/Export Stats Report")]
    public static void ExportReport()
    {
        string path = EditorUtility.SaveFilePanel("Export Stats Report", "", "ProjectStatsReport", "txt");
        if (string.IsNullOrEmpty(path)) return;
        ProjectStatsExporter.ExportReport(path);
}
}
