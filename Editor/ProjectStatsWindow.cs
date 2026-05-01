using UnityEngine;
using UnityEditor;
using System;

public class ProjectStatsWindow : EditorWindow
{
    public static ProjectStatsWindow Instance;
    private Vector2 scrollPos;
    private int     selectedTab = 0;

    private static readonly string[] Tabs = { "Stats", "History" };

    public static void ShowWindow()
    {
        GetWindow<ProjectStatsWindow>("Project Stats");
    }

    private void OnEnable()
    {
        Instance = this;
        ProjectStatsScanner.ScanAssets();
    }

    private void OnDisable()
    {
        Instance = null;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        selectedTab = GUILayout.Toolbar(selectedTab, Tabs);
        EditorGUILayout.Space(4);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (selectedTab == 0)
        {
            DrawTimeSection();
            DrawSeparator();
            DrawAssetSection();
            DrawSeparator();
            DrawVCSSection();
        }
        else
        {
            DrawHistorySection();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTimeSection()
    {
        EditorGUILayout.Space(8);
        GUILayout.Label("TIME", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);
        GUILayout.Label("Created: " + ProjectStatsTime.GetProjectCreationDate(), EditorStyles.miniLabel);
        EditorGUILayout.Space(6);

        float snapEditor    = ProjectStatsData.EditorTotal;
        float snapPlay      = ProjectStatsData.PlayTotal;
        float snapUnfocused = ProjectStatsData.UnfocusedTotal;

        int editorSecs    = (int)snapEditor;
        int playSecs      = (int)snapPlay;
        int unfocusedSecs = (int)snapUnfocused;
        int totalSecs     = editorSecs + playSecs + unfocusedSecs;

        int sessionEditorSecs    = (int)(snapEditor    - ProjectStatsData.SessionStartEditor);
        int sessionPlaySecs      = (int)(snapPlay      - ProjectStatsData.SessionStartPlay);
        int sessionUnfocusedSecs = (int)(snapUnfocused - ProjectStatsData.SessionStartUnfocused);
        int sessionSecs          = sessionEditorSecs + sessionPlaySecs + sessionUnfocusedSecs;

        DrawTimeRow("Total time",        totalSecs,   true);
        DrawTimeRow("    In editor",     editorSecs);
        DrawTimeRow("    In play mode",  playSecs);
        DrawTimeRow("    Outside Unity", unfocusedSecs);
        EditorGUILayout.Space(8);
        DrawTimeRow("Session time",      sessionSecs, true);
        DrawTimeRow("    In editor",     sessionEditorSecs);
        DrawTimeRow("    In play mode",  sessionPlaySecs);
        DrawTimeRow("    Outside Unity", sessionUnfocusedSecs);
        EditorGUILayout.Space(4);
        DrawStatRow("Total sessions", ProjectStatsData.TotalSessions.ToString(), false);
        EditorGUILayout.Space(8);

        if (GUILayout.Button("Reset All Time"))
        {
            if (EditorUtility.DisplayDialog("Reset Time", "Are you sure?", "Reset", "Cancel"))
            {
                ProjectStatsData.EditorTotal           = 0f;
                ProjectStatsData.PlayTotal             = 0f;
                ProjectStatsData.UnfocusedTotal        = 0f;
                ProjectStatsData.TotalSessions         = 0;
                ProjectStatsData.SessionStartEditor    = 0f;
                ProjectStatsData.SessionStartPlay      = 0f;
                ProjectStatsData.SessionStartUnfocused = 0f;
                EditorPrefs.SetFloat(ProjectStatsData.KeySessionStartEditor,    0f);
                EditorPrefs.SetFloat(ProjectStatsData.KeySessionStartPlay,      0f);
                EditorPrefs.SetFloat(ProjectStatsData.KeySessionStartUnfocused, 0f);
                EditorPrefs.SetInt(ProjectStatsData.KeySessions, 0);
                ProjectStatsTime.Flush();
            }
        }

        EditorGUILayout.Space(8);
    }

    private void DrawAssetSection()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("ASSETS", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            ProjectStatsScanner.ScanAssets();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        if (!ProjectStatsData.HasScanned)
        {
            GUILayout.Label("Press Refresh to scan assets.", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);
            return;
        }

        GUILayout.Label("Last scanned:  " + ProjectStatsData.LastScanned, EditorStyles.miniLabel);
        EditorGUILayout.Space(8);

        GUILayout.Label("Count", EditorStyles.boldLabel);
        DrawStatRow("Total assets", ProjectStatsData.TotalAssetCount.ToString(), true);
        foreach (var cat in ProjectStatsData.Categories)
        {
            if (cat.Filter == "t:MonoScript")
                DrawStatRow("    " + cat.Name, cat.Count + " files  /  " + ProjectStatsData.TotalScriptLines.ToString("N0") + " lines", false);
            else
                DrawStatRow("    " + cat.Name, cat.Count.ToString(), false);
        }

        EditorGUILayout.Space(8);

        GUILayout.Label("Size", EditorStyles.boldLabel);
        DrawStatRow("Total size", FormatBytes(ProjectStatsData.TotalAssetBytes), true);
        foreach (var cat in ProjectStatsData.Categories)
        {
            if (ProjectStatsData.TotalAssetBytes == 0 || (double)cat.Bytes / ProjectStatsData.TotalAssetBytes > ProjectStatsData.OtherThreshold)
                DrawStatRow("    " + cat.Name, FormatBytes(cat.Bytes), false);
        }
        if (ProjectStatsData.OtherBytes > 0)
            DrawStatRow("    Other", FormatBytes(ProjectStatsData.OtherBytes), false);

        EditorGUILayout.Space(8);
    }

    private void DrawVCSSection()
    {
        EditorGUILayout.Space(8);
        GUILayout.Label("VERSION CONTROL", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        if (!ProjectStatsData.HasScanned)
        {
            GUILayout.Label("Press Refresh to scan.", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);
            return;
        }

        switch (ProjectStatsData.VcsType)
        {
            case "git":
                GUILayout.Label("Git — " + ProjectStatsData.VcsBranch, EditorStyles.miniLabel);
                EditorGUILayout.Space(6);
                DrawStatRow("First commit",  FormatCommitTime(ProjectStatsData.VcsFirstCommitTime), false);
                DrawStatRow("Last commit",   FormatCommitTime(ProjectStatsData.VcsLastCommitTime),  false);
                DrawStatRow("Total commits", ProjectStatsData.VcsCommitCount.ToString(),            false);
                DrawStatRow("Contributors",  ProjectStatsData.VcsContributors.ToString(),           false);
                break;
            case "plastic":
                GUILayout.Label("Unity Version Control — " + ProjectStatsData.VcsBranch, EditorStyles.miniLabel);
                EditorGUILayout.Space(6);
                DrawStatRow("Last commit",      FormatCommitTime(ProjectStatsData.VcsLastCommitTime), false);
                DrawStatRow("Total changesets", ProjectStatsData.VcsCommitCount.ToString(),           false);
                break;
            case "perforce":
                GUILayout.Label("Perforce detected", EditorStyles.miniLabel);
                GUILayout.Label("Stat tracking not supported for Perforce.", EditorStyles.miniLabel);
                break;
            case "none":
                GUILayout.Label("No version control detected.", EditorStyles.miniLabel);
                break;
        }

        EditorGUILayout.Space(8);
    }

    private void DrawHistorySection()
    {
        EditorGUILayout.Space(8);
        GUILayout.Label("ASSET HISTORY", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);
        ProjectStatsGraph.Draw(position.height);
        EditorGUILayout.Space(8);
    }

    private void DrawTimeRow(string label, int totalSeconds, bool bold = false)
    {
        int days    = totalSeconds / 86400;
        int hours   = totalSeconds % 86400 / 3600;
        int minutes = totalSeconds % 3600 / 60;
        int seconds = totalSeconds % 60;

        string formatted = days > 0
            ? string.Format("{0}d {1:D2}h {2:D2}m {3:D2}s", days, hours, minutes, seconds)
            : string.Format("{0:D2}h {1:D2}m {2:D2}s", hours, minutes, seconds);

        var style = bold
            ? EditorStyles.boldLabel
            : new GUIStyle(EditorStyles.boldLabel) { fontStyle = FontStyle.Normal };

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(160));
        GUILayout.Label(formatted, style);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawStatRow(string label, string value, bool bold)
    {
        var style = bold
            ? EditorStyles.boldLabel
            : new GUIStyle(EditorStyles.boldLabel) { fontStyle = FontStyle.Normal };

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(160));
        GUILayout.Label(value, style);
        EditorGUILayout.EndHorizontal();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1073741824L) return string.Format("{0:0.00} GB", bytes / 1073741824.0);
        if (bytes >= 1048576L)    return string.Format("{0:0.00} MB", bytes / 1048576.0);
        if (bytes >= 1024L)       return string.Format("{0:0.00} KB", bytes / 1024.0);
        return bytes + " B";
    }

    private static string FormatCommitTime(string unixTimestamp)
    {
        if (string.IsNullOrEmpty(unixTimestamp)) return "";
        if (!long.TryParse(unixTimestamp, out long unix)) return "";
        return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime.ToString("MMM dd, yyyy  HH:mm");
    }

    private void DrawSeparator()
    {
        EditorGUILayout.Space(4);
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        EditorGUILayout.Space(4);
    }
}
