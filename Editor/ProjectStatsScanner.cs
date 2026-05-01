using UnityEditor;
using System;
using System.IO;
using System.Linq;

public static class ProjectStatsScanner
{
    public static void ScanAssets()
    {
        ProjectStatsData.TotalAssetCount  = 0;
        ProjectStatsData.TotalAssetBytes  = 0;
        ProjectStatsData.OtherBytes       = 0;
        ProjectStatsData.TotalScriptLines = 0;

        foreach (var cat in ProjectStatsData.Categories)
        {
            cat.Count = 0;
            cat.Bytes = 0;

            string[] guids = AssetDatabase.FindAssets(cat.Filter, new[] { "Assets" });
            cat.Count = guids.Length;

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string fullPath  = Path.GetFullPath(
                    Path.Combine(UnityEngine.Application.dataPath, "..", assetPath)
                );

                try
                {
                    if (File.Exists(fullPath))
                    {
                        cat.Bytes += new FileInfo(fullPath).Length;
                        if (cat.Filter == "t:MonoScript")
                            ProjectStatsData.TotalScriptLines += CountCodeLines(fullPath);
                    }
                }
                catch { }
            }

            ProjectStatsData.TotalAssetCount += cat.Count;
            ProjectStatsData.TotalAssetBytes += cat.Bytes;
        }

        if (ProjectStatsData.TotalAssetBytes > 0)
        {
            foreach (var cat in ProjectStatsData.Categories)
            {
                if ((double)cat.Bytes / ProjectStatsData.TotalAssetBytes <= ProjectStatsData.OtherThreshold)
                    ProjectStatsData.OtherBytes += cat.Bytes;
            }
        }

        ProjectStatsData.HasScanned  = true;
        ProjectStatsData.LastScanned = DateTime.Now.ToString("MMM dd, yyyy  HH:mm");

        ScanVCS();
        ProjectStatsHistory.SaveSnapshot();
    }

    private static int CountCodeLines(string fullPath)
    {
        int  count          = 0;
        bool inBlockComment = false;

        foreach (string line in File.ReadAllLines(fullPath))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (inBlockComment)
            {
                if (trimmed.Contains("*/")) inBlockComment = false;
                continue;
            }
            if (trimmed.StartsWith("//")) continue;
            if (trimmed.Contains("/*") && !trimmed.Contains("*/"))
            {
                inBlockComment = true;
                continue;
            }
            count++;
        }
        return count;
    }

    private static void ScanVCS()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));

        if (Directory.Exists(Path.Combine(projectRoot, ".git")))
        {
            ProjectStatsData.VcsType = "git";
            ScanGit();
        }
        else if (Directory.Exists(Path.Combine(projectRoot, ".plastic")))
        {
            ProjectStatsData.VcsType = "plastic";
            ScanPlastic();
        }
        else if (File.Exists(Path.Combine(projectRoot, ".p4config")))
        {
            ProjectStatsData.VcsType = "perforce";
        }
        else
        {
            ProjectStatsData.VcsType = "none";
        }
    }

    private static void ScanGit()
    {
        ProjectStatsData.VcsBranch = RunCommand("git", "rev-parse --abbrev-ref HEAD");

        string countOutput = RunCommand("git", "rev-list --count HEAD");
        int.TryParse(countOutput, out ProjectStatsData.VcsCommitCount);

        ProjectStatsData.VcsLastCommitTime  = RunCommand("git", "log -1 --format=%at");
        ProjectStatsData.VcsFirstCommitTime = RunCommand("git", "log --reverse --format=%at");
        if (ProjectStatsData.VcsFirstCommitTime.Contains('\n'))
            ProjectStatsData.VcsFirstCommitTime = ProjectStatsData.VcsFirstCommitTime.Split('\n')[0];

        string emails = RunCommand("git", "log --format=%ae");
        ProjectStatsData.VcsContributors = emails
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .Count();
    }

    private static void ScanPlastic()
    {
        string output = RunCommand("cm", "log --format={changesetid} --limit=1");
        if (int.TryParse(output, out int latest))
            ProjectStatsData.VcsCommitCount = latest;

        ProjectStatsData.VcsBranch         = RunCommand("cm", "status --head --format={branch}");
        ProjectStatsData.VcsFirstCommitTime = "";
        ProjectStatsData.VcsLastCommitTime  = RunCommand("cm", "log --limit=1 --format={date}");
    }

    private static string RunCommand(string executable, string args)
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName               = executable;
            proc.StartInfo.Arguments              = args;
            proc.StartInfo.WorkingDirectory       = projectRoot;
            proc.StartInfo.UseShellExecute        = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError  = true;
            proc.StartInfo.CreateNoWindow         = true;
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return output;
        }
        catch { return ""; }
    }
}
