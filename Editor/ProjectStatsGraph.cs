using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProjectStatsGraph
{
    private static Vector2 commitListScrollPos;

    private static Vector2 sessionListScrollPos;
    private static string  selectedSessionDate = "";

    public static int ViewMode    = 0;
    public static int TimeRange   = 0;
    public static int Aggregation = 0;

    private static Vector2 toggleScrollPos;
    private static int     hoveredIndex = -1;

    private static readonly string[] TimeRangeLabels   = { "30 Days", "90 Days", "Lifetime" };
    private static readonly string[] AggregationLabels = { "None", "Weekly", "Monthly" };


    private static readonly (string label, string tooltip)[] ViewButtons =
    {
        ("Total Assets",        "Total asset count over time"),
        ("Assets By Category",   "Individual asset category counts over time"),
        ("Total Commits",       "Total commit count over time"),
        ("Total Lines of Code", "Total lines of code over time"),
        ("Session Time",  "Time spent with this project open per day"),
    };


    private static readonly Color[] CategoryColors =
    {
        new Color(0.95f, 0.40f, 0.40f),
        new Color(0.40f, 0.85f, 0.40f),
        new Color(0.40f, 0.60f, 0.95f),
        new Color(0.95f, 0.80f, 0.30f),
        new Color(0.80f, 0.45f, 0.95f),
        new Color(0.40f, 0.90f, 0.90f),
        new Color(0.95f, 0.55f, 0.20f),
        new Color(0.60f, 0.95f, 0.50f),
        new Color(0.95f, 0.40f, 0.75f),
        new Color(0.50f, 0.70f, 0.95f),
        new Color(0.95f, 0.70f, 0.50f),
        new Color(0.70f, 0.95f, 0.70f),
        new Color(0.70f, 0.50f, 0.95f),
        new Color(0.95f, 0.90f, 0.40f),
        new Color(0.40f, 0.95f, 0.80f),
        new Color(0.95f, 0.60f, 0.60f),
    };

    private static readonly Color BarColor      = new Color(0.30f, 0.60f, 0.90f, 0.85f);
    private static readonly Color BarHoverColor = new Color(0.50f, 0.75f, 1.00f, 1.00f);
    private static readonly Color GridColor     = new Color(1f, 1f, 1f, 0.05f);
    private static readonly Color AxisColor     = new Color(1f, 1f, 1f, 0.20f);
    private static readonly Color TooltipBg     = new Color(0.15f, 0.15f, 0.15f, 0.95f);
    private static readonly Color DotOutline    = new Color(0.1f, 0.1f, 0.1f, 0.8f);


    public static void Draw(float windowHeight)
    {
        LoadPrefs();
        DrawControls();
        EditorGUILayout.Space(4);

        var snapshots = GetFilteredSnapshots();

        if (snapshots.Count == 0)
        {
            EditorGUILayout.Space(20);
            GUILayout.Label("No history data yet. Hit Refresh to start tracking.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        if      (ViewMode == 0) DrawBarGraph(snapshots, windowHeight);
        else if (ViewMode == 1) DrawLineGraph(snapshots, windowHeight);
        else if (ViewMode == 2) DrawCommitsGraph(snapshots, windowHeight);
        else if (ViewMode == 3) DrawCodeGraph(snapshots, windowHeight);
        else if (ViewMode == 4) DrawTimeGraph(snapshots, windowHeight);
    }

    private static void DrawControls()
    {
        DrawViewButtons();
        EditorGUILayout.Space(2);
        DrawControlRow("Range", TimeRangeLabels,   TimeRange,   v => { TimeRange   = v; SavePrefs(); }, 200);
        EditorGUILayout.Space(2);
        DrawControlRow("Group", AggregationLabels, Aggregation, v => { Aggregation = v; SavePrefs(); }, 200);
    }

    private static void DrawViewButtons()
    {
        float buttonWidth = 0;
        foreach (var (label, _) in ViewButtons)
        {
            float w = EditorStyles.toolbarButton.CalcSize(new GUIContent(label)).x + 8;
            if (w > buttonWidth) buttonWidth = w;
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("View", GUILayout.Width(40));

        for (int i = 0; i < ViewButtons.Length; i++)
        {
            var (label, tooltip) = ViewButtons[i];
            DrawViewButton(i, label, tooltip, buttonWidth);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawViewButton(int index, string label, string tooltip, float width)
    {
        bool active = ViewMode == index;

        GUIStyle style = GUI.skin.FindStyle("toolbarbutton") ?? EditorStyles.toolbarButton;
        
        bool clicked = GUI.Toggle(
            GUILayoutUtility.GetRect(new GUIContent(label), style, GUILayout.Width(width)),
            active,
            new GUIContent(label, tooltip),
            style
        );
        
        if (clicked != active && clicked) { ViewMode = index; SavePrefs(); }
    }

    private static void DrawControlRow(string label, string[] options, int current, Action<int> onChange, float width)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(40));
        int next = GUILayout.Toolbar(current, options, GUILayout.Width(width));
        if (next != current) onChange(next);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawBarGraph(List<HistorySnapshot> snapshots, float windowHeight)
    {
        float graphHeight = Mathf.Max(150, windowHeight - 200);
        Rect  graphRect   = GUILayoutUtility.GetRect(0, graphHeight, GUILayout.ExpandWidth(true));
        graphRect = Deflate(graphRect, 40, 10, 20, 10);

        if (Event.current.type != EventType.Repaint &&
            Event.current.type != EventType.MouseMove &&
            Event.current.type != EventType.Layout)
            return;

        int     count    = snapshots.Count;
        float   yMax     = NiceMax(snapshots.Max(s => s.total));
        float   barWidth = graphRect.width / count;
        Vector2 mouse    = Event.current.mousePosition;

        DrawGrid(graphRect, yMax);
        hoveredIndex = -1;

        for (int i = 0; i < count; i++)
        {
            float x       = graphRect.x + i * barWidth;
            float height  = snapshots[i].total / yMax * graphRect.height;
            float y       = graphRect.yMax - height;
            var   barRect = new Rect(x + 1, y, barWidth - 2, height);

            bool hovered = mouse.x >= x && mouse.x < x + barWidth &&
                           mouse.y >= graphRect.y && mouse.y <= graphRect.yMax;
            if (hovered) hoveredIndex = i;

            EditorGUI.DrawRect(barRect, hovered ? BarHoverColor : BarColor);
        }

        DrawAxes(graphRect, snapshots.Select(s => s.date).ToList(), barWidth);

        if (hoveredIndex >= 0)
            DrawBarTooltip(mouse, snapshots, hoveredIndex);
    }

    private static void DrawLineGraph(List<HistorySnapshot> snapshots, float windowHeight)
    {
        var categories = ProjectStatsData.Categories;

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical();
        float graphHeight = Mathf.Max(150, windowHeight - 200);
        Rect  graphRect   = GUILayoutUtility.GetRect(0, graphHeight, GUILayout.ExpandWidth(true));
        graphRect = Deflate(graphRect, 40, 10, 20, 10);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.Width(140));
        EditorGUILayout.Space(4);
        GUILayout.Label("Categories", EditorStyles.boldLabel);
        toggleScrollPos = EditorGUILayout.BeginScrollView(toggleScrollPos, GUILayout.Height(graphHeight));
        for (int i = 0; i < categories.Count; i++)
        {
            bool current = GetCategoryToggle(categories[i].Name);
            bool next    = EditorGUILayout.ToggleLeft(categories[i].Name, current, ColoredLabel(GetCategoryColor(i)));
            if (next != current) SetCategoryToggle(categories[i].Name, next);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        if (Event.current.type != EventType.Repaint &&
            Event.current.type != EventType.MouseMove &&
            Event.current.type != EventType.Layout)
            return;

        int maxVal = 0;
        foreach (var snap in snapshots)
            foreach (var cat in snap.categories)
                if (GetCategoryToggle(cat.name) && cat.count > maxVal)
                    maxVal = cat.count;

        if (maxVal == 0) return;

        float   yMax         = NiceMax(maxVal);
        int     count        = snapshots.Count;
        Vector2 mouse        = Event.current.mousePosition;
        int     closestIndex = -1;
        float   closestDist  = float.MaxValue;

        DrawGrid(graphRect, yMax);

        for (int i = 0; i < count; i++)
        {
            float x    = GetX(graphRect, i, count);
            float dist = Mathf.Abs(mouse.x - x);
            if (dist < closestDist && graphRect.Contains(mouse))
            {
                closestDist  = dist;
                closestIndex = i;
            }
        }

        var dotPositions = new List<(float x, float y, string name, int count)>();

        Handles.BeginGUI();

        for (int ci = 0; ci < categories.Count; ci++)
        {
            if (!GetCategoryToggle(categories[ci].Name)) continue;

            Handles.color = GetCategoryColor(ci);

            for (int i = 0; i < count; i++)
            {
                int   currCount = GetCategoryCount(snapshots[i], categories[ci].Name);
                float x         = GetX(graphRect, i, count);
                float y         = GetY(graphRect, currCount, yMax);

                if (i > 0)
                {
                    int   prevCount = GetCategoryCount(snapshots[i - 1], categories[ci].Name);
                    float x0        = GetX(graphRect, i - 1, count);
                    float y0        = GetY(graphRect, prevCount, yMax);

                    if (prevCount > 0 || currCount > 0)
                        Handles.DrawLine(new Vector3(x0, y0), new Vector3(x, y));
                }

                if (currCount > 0)
                {
                    Handles.color = DotOutline;
                    Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, 4f);
                    Handles.color = GetCategoryColor(ci);
                    Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, 3f);
                    dotPositions.Add((x, y, categories[ci].Name, currCount));
                }
            }
        }

        if (closestIndex >= 0)
        {
            float hx = GetX(graphRect, closestIndex, count);
            Handles.color = AxisColor;
            Handles.DrawLine(new Vector3(hx, graphRect.y), new Vector3(hx, graphRect.yMax));
        }

        Handles.EndGUI();

        DrawAxes(graphRect, snapshots.Select(s => s.date).ToList());

        if (graphRect.Contains(mouse))
        {
            bool overDot = false;
            foreach (var dot in dotPositions)
            {
                if (Vector2.Distance(mouse, new Vector2(dot.x, dot.y)) <= 8f)
                {
                    DrawTooltipBox(mouse, new List<string> { dot.name, dot.count.ToString() });
                    overDot = true;
                    break;
                }
            }

            if (!overDot && closestIndex >= 0)
                DrawLineTooltip(mouse, snapshots[closestIndex]);
        }
    }

    private static void DrawCommitsGraph(List<HistorySnapshot> snapshots, float windowHeight)
    {
        if (ProjectStatsData.VcsType != "git" && ProjectStatsData.VcsType != "plastic")
        {
            EditorGUILayout.Space(20);
            GUILayout.Label("No version control detected.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        float commitListHeight = 160f;
        float graphHeight      = Mathf.Max(150, windowHeight - 200 - commitListHeight);
        Rect  graphRect        = GUILayoutUtility.GetRect(0, graphHeight, GUILayout.ExpandWidth(true));
        graphRect = Deflate(graphRect, 40, 10, 20, 10);

        if (Event.current.type != EventType.Repaint &&
            Event.current.type != EventType.MouseMove &&
            Event.current.type != EventType.Layout)
            goto DrawList;

        int     count    = snapshots.Count;
        float   yMax     = NiceMax(snapshots.Max(s => s.commitCount));
        float   barWidth = graphRect.width / count;
        Vector2 mouse    = Event.current.mousePosition;

        DrawGrid(graphRect, yMax);
        hoveredIndex = -1;

        for (int i = 0; i < count; i++)
        {
            float x       = graphRect.x + i * barWidth;
            float height  = snapshots[i].commitCount / yMax * graphRect.height;
            float y       = graphRect.yMax - height;
            var   barRect = new Rect(x + 1, y, barWidth - 2, height);

            bool hovered = mouse.x >= x && mouse.x < x + barWidth &&
                        mouse.y >= graphRect.y && mouse.y <= graphRect.yMax;
            if (hovered) hoveredIndex = i;

            EditorGUI.DrawRect(barRect, hovered ? BarHoverColor : BarColor);
        }

        DrawAxes(graphRect, snapshots.Select(s => s.date).ToList(), barWidth);

        if (hoveredIndex >= 0)
            DrawCommitsTooltip(mouse, snapshots, hoveredIndex);

        DrawList:
        DrawCommitList(commitListHeight);
    }

    private static void DrawCommitList(float height)
    {
        if (ProjectStatsData.CommitLog.Count == 0) return;

        EditorGUILayout.Space(4);
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)), new Color(1f, 1f, 1f, 0.1f));
        EditorGUILayout.Space(4);

        GUILayout.Label("Commit History", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        commitListScrollPos = EditorGUILayout.BeginScrollView(commitListScrollPos, GUILayout.Height(height));

        var dateStyle = new GUIStyle(EditorStyles.miniLabel);
        dateStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

        foreach (var (timestamp, message) in ProjectStatsData.CommitLog)
        {
            string date = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime.ToString("MMM dd, yyyy  HH:mm");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(date,    dateStyle,              GUILayout.Width(160));
            GUILayout.Label(message, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawCodeGraph(List<HistorySnapshot> snapshots, float windowHeight)
    {
        float graphHeight = Mathf.Max(150, windowHeight - 200);
        Rect  graphRect   = GUILayoutUtility.GetRect(0, graphHeight, GUILayout.ExpandWidth(true));
        graphRect = Deflate(graphRect, 40, 10, 20, 10);

        if (Event.current.type != EventType.Repaint &&
            Event.current.type != EventType.MouseMove &&
            Event.current.type != EventType.Layout)
            return;

        int     count    = snapshots.Count;
        float   yMax     = NiceMax(snapshots.Max(s => s.totalLOC));
        float   barWidth = graphRect.width / count;
        Vector2 mouse    = Event.current.mousePosition;

        DrawGrid(graphRect, yMax);
        hoveredIndex = -1;

        for (int i = 0; i < count; i++)
        {
            float x       = graphRect.x + i * barWidth;
            float height  = snapshots[i].totalLOC / yMax * graphRect.height;
            float y       = graphRect.yMax - height;
            var   barRect = new Rect(x + 1, y, barWidth - 2, height);

            bool hovered = mouse.x >= x && mouse.x < x + barWidth &&
                        mouse.y >= graphRect.y && mouse.y <= graphRect.yMax;
            if (hovered) hoveredIndex = i;

            EditorGUI.DrawRect(barRect, hovered ? BarHoverColor : BarColor);
        }

        DrawAxes(graphRect, snapshots.Select(s => s.date).ToList(), barWidth);

        if (hoveredIndex >= 0)
            DrawCodeTooltip(mouse, snapshots, hoveredIndex);
    }

    private static void DrawTimeGraph(List<HistorySnapshot> snapshots, float windowHeight)
    {
        var sessionDays = ProjectStatsHistory.GetSessionDays();
        if (sessionDays == null || sessionDays.Count == 0)
        {
            EditorGUILayout.Space(20);
            GUILayout.Label("No session data yet.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        var filtered = GetFilteredSessionDays(sessionDays);
        if (filtered.Count == 0)
        {
            EditorGUILayout.Space(20);
            GUILayout.Label("No session data in selected range.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        float sessionListHeight = string.IsNullOrEmpty(selectedSessionDate) ? 0 : 160f;
        float graphHeight       = Mathf.Max(150, windowHeight - 200 - sessionListHeight - (string.IsNullOrEmpty(selectedSessionDate) ? 0 : 30));
        Rect  graphRect         = GUILayoutUtility.GetRect(0, graphHeight, GUILayout.ExpandWidth(true));
        graphRect = Deflate(graphRect, 40, 10, 20, 10);

        if (Event.current.type != EventType.Repaint &&
            Event.current.type != EventType.MouseMove &&
            Event.current.type != EventType.MouseDown &&
            Event.current.type != EventType.Layout)
            goto DrawSessionList;

        int     count    = filtered.Count;
        float   yMax     = NiceMax(filtered.Max(d => d.totalTimeSeconds));
        float   barWidth = graphRect.width / count;
        Vector2 mouse    = Event.current.mousePosition;

        DrawGrid(graphRect, yMax);
        hoveredIndex = -1;

        for (int i = 0; i < count; i++)
        {
            float x       = graphRect.x + i * barWidth;
            float height  = filtered[i].totalTimeSeconds / yMax * graphRect.height;
            float y       = graphRect.yMax - height;
            var   barRect = new Rect(x + 1, y, barWidth - 2, height);

            bool hovered = mouse.x >= x && mouse.x < x + barWidth &&
                        mouse.y >= graphRect.y && mouse.y <= graphRect.yMax;
            if (hovered) hoveredIndex = i;

            bool selected = filtered[i].date == selectedSessionDate;
            Color barColor = selected ? new Color(0.90f, 0.70f, 0.20f, 0.85f) : hovered ? BarHoverColor : BarColor;
            EditorGUI.DrawRect(barRect, barColor);

            if (hovered && Event.current.type == EventType.MouseDown)
            {
                selectedSessionDate = filtered[i].date == selectedSessionDate ? "" : filtered[i].date;
                Event.current.Use();
            }
        }

        DrawTimeAxes(graphRect, filtered);

        if (hoveredIndex >= 0)
            DrawTimeTooltip(mouse, filtered, hoveredIndex);

        DrawSessionList:
        if (!string.IsNullOrEmpty(selectedSessionDate))
            DrawSessionList(selectedSessionDate, sessionListHeight);
    }

    private static void DrawTimeAxes(Rect rect, List<SessionDay> days)
    {
        int count     = days.Count;
        int maxLabels = Mathf.Max(1, (int)(rect.width / 60));
        int step      = Mathf.Max(1, count / maxLabels);
        float barWidth = rect.width / count;

        for (int i = 0; i < count; i += step)
            GUI.Label(
                new Rect(rect.x + i * barWidth + barWidth / 2 - 30, rect.yMax + 2, 60, 16),
                FormatDateShort(days[i].date),
                CenteredMiniLabel()
            );
    }

    private static void DrawTimeTooltip(Vector2 mouse, List<SessionDay> days, int index)
    {
        var day = days[index];
        var lines = new List<string>();
        lines.Add(FormatDate(day.date));
        lines.Add("Time:      " + FormatSeconds(day.totalTimeSeconds));
        lines.Add("Sessions:  " + day.sessionCount);
        DrawTooltipBox(mouse, lines);
    }

    private static void DrawSessionList(string date, float height)
    {
        var day = ProjectStatsHistory.GetSessionDay(date);
        if (day == null) return;

        EditorGUILayout.Space(4);
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)), new Color(1f, 1f, 1f, 0.1f));
        EditorGUILayout.Space(4);

        GUILayout.Label("Sessions — " + FormatDate(date), EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        sessionListScrollPos = EditorGUILayout.BeginScrollView(sessionListScrollPos, GUILayout.Height(height));

        var dateStyle = new GUIStyle(EditorStyles.miniLabel);
        dateStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

        foreach (var session in day.sessions)
        {
            string start    = DateTimeOffset.FromUnixTimeSeconds(session.startTime).LocalDateTime.ToString("hh:mm tt");
            string duration = FormatSeconds(session.durationSeconds);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(start,    dateStyle,              GUILayout.Width(80));
            GUILayout.Label(duration, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private static string FormatSeconds(int totalSeconds)
    {
        int days    = totalSeconds / 86400;
        int hours   = totalSeconds % 86400 / 3600;
        int minutes = totalSeconds % 3600 / 60;
        int seconds = totalSeconds % 60;

        return days > 0
            ? string.Format("{0}d {1:D2}h {2:D2}m {3:D2}s", days, hours, minutes, seconds)
            : string.Format("{0:D2}h {1:D2}m {2:D2}s", hours, minutes, seconds);
    }

    private static List<SessionDay> GetFilteredSessionDays(List<SessionDay> days)
    {
        DateTime cutoff = TimeRange == 0 ? DateTime.Now.AddDays(-30)
                        : TimeRange == 1 ? DateTime.Now.AddDays(-90)
                        : DateTime.MinValue;

        var filtered = days
            .Where(d => DateTime.TryParse(d.date, out DateTime dt) && dt >= cutoff)
            .ToList();

        if (Aggregation == 1) return AggregateSessionDaysByWeek(filtered);
        if (Aggregation == 2) return AggregateSessionDaysByMonth(filtered);
        return filtered;
    }

    private static List<SessionDay> AggregateSessionDaysByWeek(List<SessionDay> days)
    {
        return days
            .GroupBy(d =>
            {
                DateTime.TryParse(d.date, out DateTime dt);
                int diff = (int)dt.DayOfWeek - (int)DayOfWeek.Monday;
                if (diff < 0) diff += 7;
                return dt.AddDays(-diff).ToString("yyyy-MM-dd");
            })
            .Select(g =>
            {
                var merged = new SessionDay { date = g.Key };
                foreach (var d in g)
                {
                    merged.sessions.AddRange(d.sessions);
                    merged.sessionCount    += d.sessionCount;
                    merged.totalTimeSeconds += d.totalTimeSeconds;
                }
                return merged;
            })
            .OrderBy(d => d.date)
            .ToList();
    }

    private static List<SessionDay> AggregateSessionDaysByMonth(List<SessionDay> days)
    {
        return days
            .GroupBy(d =>
            {
                DateTime.TryParse(d.date, out DateTime dt);
                return dt.ToString("yyyy-MM");
            })
            .Select(g =>
            {
                var merged = new SessionDay { date = g.Key };
                foreach (var d in g)
                {
                    merged.sessions.AddRange(d.sessions);
                    merged.sessionCount    += d.sessionCount;
                    merged.totalTimeSeconds += d.totalTimeSeconds;
                }
                return merged;
            })
            .OrderBy(d => d.date)
            .ToList();
    }

    private static void DrawBarTooltip(Vector2 mouse, List<HistorySnapshot> snapshots, int index)
    {
        var snap  = snapshots[index];
        int prev  = index > 0 ? snapshots[index - 1].total : snap.total;
        int delta = snap.total - prev;

        var lines = new List<string>();
        lines.Add(FormatDate(snap.date));
        lines.Add("Total: " + snap.total + (index > 0 && delta != 0 ? "  (" + (delta >= 0 ? "+" : "") + delta + ")" : ""));
        lines.Add("─────────────────");

        if (index > 0)
        {
            foreach (var cat in snap.categories)
            {
                int diff = cat.count - GetCategoryCount(snapshots[index - 1], cat.name);
                if (diff != 0)
                    lines.Add(cat.name.PadRight(20) + (diff >= 0 ? "+" : "") + diff);
            }
        }

        DrawTooltipBox(mouse, lines);
    }

    private static void DrawLineTooltip(Vector2 mouse, HistorySnapshot snap)
    {
        var lines = new List<string>();
        lines.Add(FormatDate(snap.date));
        lines.Add("─────────────────");

        foreach (var cat in snap.categories)
        {
            if (!GetCategoryToggle(cat.name) || cat.count == 0) continue;
            lines.Add(cat.name.PadRight(20) + cat.count);
        }

        DrawTooltipBox(mouse, lines);
    }

    private static void DrawCommitsTooltip(Vector2 mouse, List<HistorySnapshot> snapshots, int index)
    {
        var snap  = snapshots[index];
        int prev  = index > 0 ? snapshots[index - 1].commitCount : snap.commitCount;
        int delta = snap.commitCount - prev;

        var lines = new List<string>();
        lines.Add(FormatDate(snap.date));
        lines.Add("Total commits: " + snap.commitCount + (index > 0 && delta != 0 ? "  (" + (delta >= 0 ? "+" : "") + delta + ")" : ""));

        if (!string.IsNullOrEmpty(snap.lastCommitDate))
            lines.Add("Latest commit: " + FormatCommitTime(snap.lastCommitDate));

        DrawTooltipBox(mouse, lines);
    }

    private static void DrawCodeTooltip(Vector2 mouse, List<HistorySnapshot> snapshots, int index)
    {
        var snap     = snapshots[index];
        int prevLOC  = index > 0 ? snapshots[index - 1].totalLOC        : snap.totalLOC;
        int prevFiles = index > 0 ? snapshots[index - 1].scriptFileCount : snap.scriptFileCount;
        int locDelta  = snap.totalLOC        - prevLOC;
        int fileDelta = snap.scriptFileCount - prevFiles;

        var lines = new List<string>();
        lines.Add(FormatDate(snap.date));
        lines.Add("LOC: " + snap.totalLOC.ToString("N0") + (index > 0 && locDelta != 0 ? "  (" + (locDelta >= 0 ? "+" : "") + locDelta + ")" : ""));
        lines.Add("Files: " + snap.scriptFileCount + (index > 0 && fileDelta != 0 ? "  (" + (fileDelta >= 0 ? "+" : "") + fileDelta + ")" : ""));

        DrawTooltipBox(mouse, lines);
    }

    private static void DrawTooltipBox(Vector2 mouse, List<string> lines)
    {
        if (lines.Count == 0) return;

        var   style   = EditorStyles.miniLabel;
        int   padding = 6;
        float lineH   = style.lineHeight + 2;
        float width   = lines.Max(l => style.CalcSize(new GUIContent(l)).x) + padding * 2;
        float height  = lines.Count * lineH + padding * 2;

        float x = mouse.x + 12;
        float y = mouse.y - height / 2;

        if (x + width > Screen.width)  x = mouse.x - width - 4;
        if (y < 0)                      y = 0;
        if (y + height > Screen.height) y = Screen.height - height;

        var r = new Rect(x, y, width, height);
        EditorGUI.DrawRect(r, TooltipBg);
        EditorGUI.DrawRect(new Rect(r.x,          r.y,          r.width, 1),        AxisColor);
        EditorGUI.DrawRect(new Rect(r.x,          r.yMax - 1,   r.width, 1),        AxisColor);
        EditorGUI.DrawRect(new Rect(r.x,          r.y,          1,       r.height), AxisColor);
        EditorGUI.DrawRect(new Rect(r.xMax - 1,   r.y,          1,       r.height), AxisColor);

        for (int i = 0; i < lines.Count; i++)
            GUI.Label(new Rect(x + padding, y + padding + i * lineH, width, lineH), lines[i], style);
    }

    private static void DrawGrid(Rect rect, float yMax)
    {
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 0.4f));

        int interval  = GetInterval(yMax);
        int lineCount = (int)(yMax / interval);

        for (int i = 0; i <= lineCount; i++)
        {
            float val = i * interval;
            float y   = rect.yMax - val / yMax * rect.height;
            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, 1), i == 0 ? AxisColor : GridColor);
            GUI.Label(new Rect(rect.x - 38, y - 8, 36, 16), val.ToString(), RightMiniLabel());
        }

        EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y, 1, rect.height), AxisColor);
    }

    private static void DrawAxes(Rect rect, List<string> dates, float barWidth = 0)
    {
        int count     = dates.Count;
        int maxLabels = Mathf.Max(1, (int)(rect.width / 60));
        int step      = Mathf.Max(1, count / maxLabels);

        for (int i = 0; i < count; i += step)
        {
            float x = barWidth > 0
                ? rect.x + i * barWidth + barWidth / 2
                : GetX(rect, i, count);

            GUI.Label(
                new Rect(x - 30, rect.yMax + 2, 60, 16),
                FormatDateShort(dates[i]),
                CenteredMiniLabel()
            );
        }
    }

    private static List<HistorySnapshot> GetFilteredSnapshots()
    {
        var all = ProjectStatsHistory.GetSnapshots();
        if (all == null || all.Count == 0) return new List<HistorySnapshot>();

        DateTime cutoff = TimeRange == 0 ? DateTime.Now.AddDays(-30)
                        : TimeRange == 1 ? DateTime.Now.AddDays(-90)
                        : DateTime.MinValue;

        var filtered = all
            .Where(s => DateTime.TryParse(s.date, out DateTime d) && d >= cutoff)
            .ToList();

        if (Aggregation == 1) return AggregateByWeek(filtered);
        if (Aggregation == 2) return AggregateByMonth(filtered);
        return filtered;
    }

    private static List<HistorySnapshot> AggregateByWeek(List<HistorySnapshot> snapshots)
    {
        return snapshots
            .GroupBy(s =>
            {
                DateTime.TryParse(s.date, out DateTime d);
                int diff = (int)d.DayOfWeek - (int)DayOfWeek.Monday;
                if (diff < 0) diff += 7;
                return d.AddDays(-diff).ToString("yyyy-MM-dd");
            })
            .Select(g => g.Last())
            .OrderBy(s => s.date)
            .ToList();
    }

    private static List<HistorySnapshot> AggregateByMonth(List<HistorySnapshot> snapshots)
    {
        return snapshots
            .GroupBy(s =>
            {
                DateTime.TryParse(s.date, out DateTime d);
                return d.ToString("yyyy-MM");
            })
            .Select(g => g.Last())
            .OrderBy(s => s.date)
            .ToList();
    }

    private static int GetCategoryCount(HistorySnapshot snap, string name)
    {
        foreach (var cat in snap.categories)
            if (cat.name == name) return cat.count;
        return 0;
    }

    private static float NiceMax(int value)
    {
        if (value <= 0) return 50;
        int interval = GetInterval(value);
        return ((value / interval) + 2) * interval;
    }

    private static int GetInterval(float value)
    {
        if      (value <= 200)  return 25;
        else if (value <= 500)  return 50;
        else if (value <= 1000) return 100;
        else if (value <= 2000) return 200;
        else if (value <= 5000) return 500;
        else                    return 1000;
    }

    private static float GetX(Rect rect, int index, int count)
    {
        return rect.x + (count == 1 ? rect.width / 2 : index * rect.width / (count - 1));
    }

    private static float GetY(Rect rect, int value, float yMax)
    {
        return rect.yMax - value / yMax * rect.height;
    }

    private static Color GetCategoryColor(int index)
    {
        return CategoryColors[index % CategoryColors.Length];
    }

    private static Rect Deflate(Rect r, float left, float right, float bottom, float top)
    {
        return new Rect(r.x + left, r.y + top, r.width - left - right, r.height - top - bottom);
    }

    private static string FormatDate(string iso)
    {
        return DateTime.TryParse(iso, out DateTime d) ? d.ToString("MMM dd, yyyy") : iso;
    }

    private static string FormatDateShort(string iso)
    {
        return DateTime.TryParse(iso, out DateTime d) ? d.ToString("MM/dd") : iso;
    }

    private static string FormatCommitTime(string unixTimestamp)
    {
        if (string.IsNullOrEmpty(unixTimestamp)) return "";
        if (!long.TryParse(unixTimestamp, out long unix)) return "";
        return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime.ToString("MMM dd, yyyy  HH:mm");
    }

    private static bool GetCategoryToggle(string name)
    {
        return EditorPrefs.GetBool("ProjectStats_Toggle_" + name, true);
    }

    private static void SetCategoryToggle(string name, bool value)
    {
        EditorPrefs.SetBool("ProjectStats_Toggle_" + name, value);
    }

    private static void LoadPrefs()
    {
        ViewMode    = EditorPrefs.GetInt("ProjectStats_GraphView",   0);
        TimeRange   = EditorPrefs.GetInt("ProjectStats_TimeRange",   0);
        Aggregation = EditorPrefs.GetInt("ProjectStats_Aggregation", 0);
    }

    private static void SavePrefs()
    {
        EditorPrefs.SetInt("ProjectStats_GraphView",   ViewMode);
        EditorPrefs.SetInt("ProjectStats_TimeRange",   TimeRange);
        EditorPrefs.SetInt("ProjectStats_Aggregation", Aggregation);
    }

    private static GUIStyle RightMiniLabel()
    {
        var s = new GUIStyle(EditorStyles.miniLabel);
        s.alignment = TextAnchor.MiddleRight;
        return s;
    }

    private static GUIStyle CenteredMiniLabel()
    {
        var s = new GUIStyle(EditorStyles.miniLabel);
        s.alignment = TextAnchor.MiddleCenter;
        return s;
    }

    private static GUIStyle ColoredLabel(Color color)
    {
        var s = new GUIStyle(EditorStyles.miniLabel);
        s.normal.textColor = color;
        return s;
    }
}
