using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;

public class ProjectStatsToolbarButton
{
    const string k_ElementName = "Project Stats/Open";

    [MainToolbarElement(k_ElementName, defaultDockPosition = MainToolbarDockPosition.Right)]
    static IEnumerable<MainToolbarElement> CreateButton()
    {
        yield return new MainToolbarButton(
            new MainToolbarContent("Project Stats", "Open Project Stats"),
            () => ProjectStatsWindow.ShowWindow()
        );
    }
}

public class ProjectStatsMenu
{
    [MenuItem("Project Stats/Open")]
    public static void Open() { ProjectStatsWindow.ShowWindow(); }

    [MenuItem("Project Stats/Clear All Data")]
    public static void ClearAllData()
    {
        if (EditorUtility.DisplayDialog("Clear All Data", "This will delete all saved stats. Are you sure?", "Clear", "Cancel"))
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
        }
    }

    [MenuItem("Project Stats/Reinitialize")]
    public static void Reinitialize()
    {
        ProjectStatsData.Initialized = false;
    }
}
