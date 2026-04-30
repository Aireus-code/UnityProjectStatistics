using UnityEditor;
using System;
using System.IO;

[InitializeOnLoad]
public static class ProjectStatsTime
{
    static ProjectStatsTime()
    {
        EditorApplication.update               += OnUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.focusChanged         += OnFocusChanged;
        EditorApplication.quitting             += OnEditorQuit;
    }

    private static void OnUpdate()
    {
        if (!ProjectStatsData.Initialized)
        {
            ProjectStatsData.EditorTotal    = EditorPrefs.GetFloat(ProjectStatsData.KeyEditor,    0f);
            ProjectStatsData.PlayTotal      = EditorPrefs.GetFloat(ProjectStatsData.KeyPlay,      0f);
            ProjectStatsData.UnfocusedTotal = EditorPrefs.GetFloat(ProjectStatsData.KeyUnfocused, 0f);
            ProjectStatsData.TotalSessions  = EditorPrefs.GetInt(ProjectStatsData.KeySessions,    0);

            string savedID = EditorPrefs.GetString(ProjectStatsData.KeySessionID, "");

            if (string.IsNullOrEmpty(savedID))
            {
                ProjectStatsData.SessionStartEditor    = ProjectStatsData.EditorTotal;
                ProjectStatsData.SessionStartPlay      = ProjectStatsData.PlayTotal;
                ProjectStatsData.SessionStartUnfocused = ProjectStatsData.UnfocusedTotal;
                EditorPrefs.SetFloat(ProjectStatsData.KeySessionStartEditor,    ProjectStatsData.SessionStartEditor);
                EditorPrefs.SetFloat(ProjectStatsData.KeySessionStartPlay,      ProjectStatsData.SessionStartPlay);
                EditorPrefs.SetFloat(ProjectStatsData.KeySessionStartUnfocused, ProjectStatsData.SessionStartUnfocused);
                ProjectStatsData.TotalSessions++;
                EditorPrefs.SetInt(ProjectStatsData.KeySessions,      ProjectStatsData.TotalSessions);
                EditorPrefs.SetString(ProjectStatsData.KeySessionID,  Guid.NewGuid().ToString());
            }
            else
            {
                ProjectStatsData.SessionStartEditor    = EditorPrefs.GetFloat(ProjectStatsData.KeySessionStartEditor,    0f);
                ProjectStatsData.SessionStartPlay      = EditorPrefs.GetFloat(ProjectStatsData.KeySessionStartPlay,      0f);
                ProjectStatsData.SessionStartUnfocused = EditorPrefs.GetFloat(ProjectStatsData.KeySessionStartUnfocused, 0f);
            }

            ProjectStatsData.SessionStart = EditorApplication.timeSinceStartup;
            ProjectStatsData.Initialized  = true;
            return;
        }

        double now   = EditorApplication.timeSinceStartup;
        float  delta = (float)(now - ProjectStatsData.SessionStart);
        ProjectStatsData.SessionStart = now;

        if (ProjectStatsData.InPlayMode)
            ProjectStatsData.PlayTotal += delta;
        else if (ProjectStatsData.IsFocused)
            ProjectStatsData.EditorTotal += delta;
        else
            ProjectStatsData.UnfocusedTotal += delta;

        ProjectStatsData.TimeSinceLastSave += delta;
        if (ProjectStatsData.TimeSinceLastSave >= ProjectStatsData.SaveInterval)
            Flush();

        if (ProjectStatsWindow.Instance != null)
            ProjectStatsWindow.Instance.Repaint();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.ExitingPlayMode)
            Flush();

        if      (state == PlayModeStateChange.EnteredPlayMode) ProjectStatsData.InPlayMode = true;
        else if (state == PlayModeStateChange.EnteredEditMode) ProjectStatsData.InPlayMode = false;
    }

    private static void OnFocusChanged(bool focused)
    {
        ProjectStatsData.IsFocused = focused;
    }

    public static void Flush()
    {
        EditorPrefs.SetFloat(ProjectStatsData.KeyEditor,    ProjectStatsData.EditorTotal);
        EditorPrefs.SetFloat(ProjectStatsData.KeyPlay,      ProjectStatsData.PlayTotal);
        EditorPrefs.SetFloat(ProjectStatsData.KeyUnfocused, ProjectStatsData.UnfocusedTotal);
        ProjectStatsData.TimeSinceLastSave = 0f;
    }

    private static void OnEditorQuit()
    {
        EditorPrefs.DeleteKey(ProjectStatsData.KeySessionID);
        ProjectStatsScanner.ScanAssets();
        Flush();
    }

    public static string GetProjectCreationDate()
    {
        if (!string.IsNullOrEmpty(ProjectStatsData.CachedCreationDate))
            return ProjectStatsData.CachedCreationDate;

        string saved = EditorPrefs.GetString(ProjectStatsData.KeyCreationDate, "");
        if (!string.IsNullOrEmpty(saved))
        {
            ProjectStatsData.CachedCreationDate = saved;
            return ProjectStatsData.CachedCreationDate;
        }

        string settingsPath = Path.GetFullPath(
            Path.Combine(UnityEngine.Application.dataPath, "..", "ProjectSettings", "ProjectSettings.asset")
        );

        DateTime date = DetectCreationDate(settingsPath);
        ProjectStatsData.CachedCreationDate = date.ToString("MMM dd, yyyy");
        EditorPrefs.SetString(ProjectStatsData.KeyCreationDate, ProjectStatsData.CachedCreationDate);
        return ProjectStatsData.CachedCreationDate;
    }

    private static DateTime DetectCreationDate(string path)
    {
        DateTime tier1 = File.GetCreationTime(path);
        if (tier1.Year > 1970) return tier1;

        if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.LinuxEditor)
        {
            DateTime tier2 = TryStatCommand(path);
            if (tier2.Year > 1970) return tier2;
        }

        return File.GetLastWriteTime(path);
    }

    private static DateTime TryStatCommand(string path)
    {
        try
        {
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName               = "stat";
            proc.StartInfo.Arguments              = $"--printf=%W \"{path}\"";
            proc.StartInfo.UseShellExecute        = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.CreateNoWindow         = true;
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();

            if (long.TryParse(output, out long unixTime) && unixTime > 0)
                return DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;
        }
        catch { }

        return DateTime.MinValue;
    }
}
