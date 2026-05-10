using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ProjectStatsExporter
{
    public static void ExportCSV(string path)
    {
        var snapshots = ProjectStatsHistory.GetSnapshots();
        if (snapshots == null || snapshots.Count == 0)
        {
            EditorUtility.DisplayDialog("Export Failed", "No history data to export. Hit Refresh first.", "OK");
            return;
        }

        var sb = new StringBuilder();

        // Header row
        var headers = new List<string> { "Date", "Total Assets" };
        foreach (var cat in ProjectStatsData.Categories)
            headers.Add(cat.Name);
        headers.Add("LOC");
        headers.Add("Script Files");
        headers.Add("Commits");
        sb.AppendLine(string.Join(",", headers));

        // Data rows
        foreach (var snap in snapshots)
        {
            var values = new List<string> { snap.date, snap.total.ToString() };
            foreach (var cat in ProjectStatsData.Categories)
            {
                int count = 0;
                foreach (var c in snap.categories)
                    if (c.name == cat.Name) { count = c.count; break; }
                values.Add(count.ToString());
            }
            values.Add(snap.totalLOC.ToString());
            values.Add(snap.scriptFileCount.ToString());
            values.Add(snap.commitCount.ToString());
            sb.AppendLine(string.Join(",", values));
        }

        File.WriteAllText(path, sb.ToString());
        EditorUtility.DisplayDialog("Export Complete", "History exported to:\n" + path, "OK");
    }

    public static void ExportReport(string path)
    {
        if (!ProjectStatsData.HasScanned)
        {
            EditorUtility.DisplayDialog("Export Failed", "No stats data available. Hit Refresh first.", "OK");
            return;
        }

        var sb = new StringBuilder();
        string line = new string('═', 40);

        sb.AppendLine("Project Stats Report — " + DateTime.Now.ToString("MMM dd, yyyy"));
        sb.AppendLine(line);
        sb.AppendLine();

        // Time
        sb.AppendLine("TIME");
        sb.AppendLine("Created:          " + ProjectStatsTime.GetProjectCreationDate());

        float snapEditor    = ProjectStatsData.EditorTotal;
        float snapPlay      = ProjectStatsData.PlayTotal;
        float snapUnfocused = ProjectStatsData.UnfocusedTotal;
        int   totalSecs     = (int)(snapEditor + snapPlay + snapUnfocused);

        sb.AppendLine("Total time:       " + FormatTime(totalSecs));
        sb.AppendLine("  In editor:      " + FormatTime((int)snapEditor));
        sb.AppendLine("  In play mode:   " + FormatTime((int)snapPlay));
        sb.AppendLine("  Outside Unity:  " + FormatTime((int)snapUnfocused));
        sb.AppendLine("Total sessions:   " + ProjectStatsData.TotalSessions);
        sb.AppendLine();

        // Assets
        sb.AppendLine("ASSETS");
        sb.AppendLine("Total assets:     " + ProjectStatsData.TotalAssetCount);
        foreach (var cat in ProjectStatsData.Categories)
        {
            if (cat.Filter == "t:MonoScript")
                sb.AppendLine("  " + cat.Name.PadRight(22) + cat.Count + " files  /  " + ProjectStatsData.TotalScriptLines.ToString("N0") + " lines");
            else
                sb.AppendLine("  " + cat.Name.PadRight(22) + cat.Count);
        }
        sb.AppendLine("Total size:       " + FormatBytes(ProjectStatsData.TotalAssetBytes));
        sb.AppendLine();

        // VCS
        sb.AppendLine("VERSION CONTROL");
        switch (ProjectStatsData.VcsType)
        {
            case "git":
                sb.AppendLine("Git — " + ProjectStatsData.VcsBranch);
                sb.AppendLine("First commit:     " + FormatCommitTime(ProjectStatsData.VcsFirstCommitTime));
                sb.AppendLine("Last commit:      " + FormatCommitTime(ProjectStatsData.VcsLastCommitTime));
                sb.AppendLine("Total commits:    " + ProjectStatsData.VcsCommitCount);
                sb.AppendLine("Contributors:     " + ProjectStatsData.VcsContributors);
                break;
            case "plastic":
                sb.AppendLine("Unity Version Control — " + ProjectStatsData.VcsBranch);
                sb.AppendLine("Last commit:      " + FormatCommitTime(ProjectStatsData.VcsLastCommitTime));
                sb.AppendLine("Total changesets: " + ProjectStatsData.VcsCommitCount);
                break;
            case "perforce":
                sb.AppendLine("Perforce detected — stat tracking not supported");
                break;
            case "none":
                sb.AppendLine("No version control detected");
                break;
        }

        string reportText = sb.ToString();
        File.WriteAllText(path, reportText);
        GUIUtility.systemCopyBuffer = reportText;
        EditorUtility.DisplayDialog("Export Complete", "Report exported and copied to clipboard:\n" + path, "OK");
    }

    private static string FormatTime(int totalSeconds)
    {
        int days    = totalSeconds / 86400;
        int hours   = totalSeconds % 86400 / 3600;
        int minutes = totalSeconds % 3600 / 60;
        int seconds = totalSeconds % 60;

        return days > 0
            ? string.Format("{0}d {1:D2}h {2:D2}m {3:D2}s", days, hours, minutes, seconds)
            : string.Format("{0:D2}h {1:D2}m {2:D2}s", hours, minutes, seconds);
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
}
