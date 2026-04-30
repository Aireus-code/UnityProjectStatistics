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
}
