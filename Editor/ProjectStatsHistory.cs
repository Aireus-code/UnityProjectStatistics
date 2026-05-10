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
public class SessionEntry
{
    public long startTime;
    public int  durationSeconds;
}

[Serializable]
public class SessionDay
{
    public string             date;
    public int                totalTimeSeconds;
    public int                sessionCount;
    public List<SessionEntry> sessions = new List<SessionEntry>();
}

[Serializable]
public class HistoryData
{
    public List<HistorySnapshot> snapshots    = new List<HistorySnapshot>();
    public List<SessionDay>      sessionDays  = new List<SessionDay>();
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
        int    existing = data.snapshots.FindIndex(s => s.date == today);

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

    public static void Invalidate()
    {
        data = null;
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

    public static void AddOrUpdateSession(long sessionStartTime, int durationSeconds)
    {
        Load();

        string today   = DateTime.Now.ToString("yyyy-MM-dd");
        int    dayIdx  = data.sessionDays.FindIndex(d => d.date == today);

        if (dayIdx < 0)
        {
            data.sessionDays.Add(new SessionDay { date = today });
            dayIdx = data.sessionDays.Count - 1;
        }

        SessionDay day     = data.sessionDays[dayIdx];
        int        entryIdx = day.sessions.FindIndex(s => s.startTime == sessionStartTime);

        if (entryIdx < 0)
        {
            day.sessions.Add(new SessionEntry { startTime = sessionStartTime, durationSeconds = durationSeconds });
            day.sessionCount++;
        }
        else
        {
            day.sessions[entryIdx].durationSeconds = durationSeconds;
        }

        day.totalTimeSeconds = 0;
        foreach (var s in day.sessions)
            day.totalTimeSeconds += s.durationSeconds;

        File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
    }

    public static List<SessionDay> GetSessionDays()
    {
        Load();
        return data.sessionDays;
    }

    public static SessionDay GetSessionDay(string date)
    {
        Load();
        return data.sessionDays.Find(d => d.date == date);
    }
}

