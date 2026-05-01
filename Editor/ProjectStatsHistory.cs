using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class HistorySnapshot
{
    public string              date;
    public int                 total;
    public int                 totalLOC;
    public int                 scriptFileCount;
    public int                 commitCount;
    public string              lastCommitDate;
    public List<CategoryCount> categories = new List<CategoryCount>();
}

[Serializable]
public class CategoryCount
{
    public string name;
    public int    count;
}

[Serializable]
public class HistoryData
{
    public List<HistorySnapshot> snapshots = new List<HistorySnapshot>();
}

public static class ProjectStatsHistory
{
    private static readonly string FilePath = Path.GetFullPath(
        Path.Combine(Application.dataPath, "..", "ProjectSettings", "ProjectStatsHistory.json")
    );

    private static HistoryData data = null;

    public static void SaveSnapshot()
    {
        if (!ProjectStatsData.HasScanned || ProjectStatsData.TotalAssetCount == 0)
            return;

        Load();

        string today    = DateTime.Now.ToString("yyyy-MM-dd");
        var    existing = data.snapshots.FindIndex(s => s.date == today);

        var snapshot = new HistorySnapshot
        {
            date            = today,
            total           = ProjectStatsData.TotalAssetCount,
            totalLOC        = ProjectStatsData.TotalScriptLines,
            scriptFileCount = ProjectStatsData.Categories.Find(c => c.Filter == "t:MonoScript")?.Count ?? 0,
            commitCount     = ProjectStatsData.VcsCommitCount,
            lastCommitDate  = ProjectStatsData.VcsLastCommitTime
        };

        foreach (var cat in ProjectStatsData.Categories)
            snapshot.categories.Add(new CategoryCount { name = cat.Name, count = cat.Count });

        if (existing >= 0)
            data.snapshots[existing] = snapshot;
        else
            data.snapshots.Add(snapshot);

        data.snapshots.Sort((a, b) => string.Compare(a.date, b.date, StringComparison.Ordinal));
        File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
    }

    public static List<HistorySnapshot> GetSnapshots()
    {
        Load();
        return data.snapshots;
    }

    public static void ClearHistory()
    {
        data = new HistoryData();
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    private static void Load()
    {
        if (data != null) return;

        if (File.Exists(FilePath))
        {
            try
            {
                data = JsonUtility.FromJson<HistoryData>(File.ReadAllText(FilePath));
                if (data == null) data = new HistoryData();
            }
            catch
            {
                data = new HistoryData();
            }
        }
        else
        {
            data = new HistoryData();
        }
    }

    public static void Invalidate()
    {
        data = null;
    }
}
