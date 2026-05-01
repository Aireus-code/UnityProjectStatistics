using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProjectStatsGraph
{
    public static int ViewMode   = 0; // 0 = Total, 1 = Per Category
    public static int TimeRange  = 0; // 0 = 30 Days, 1 = 90 Days, 2 = Lifetime
    public static int Aggregation = 0; // 0 = None, 1 = Weekly, 2 = Monthly

    private static Vector2 toggleScrollPos;
    private static int     hoveredIndex = -1;

    private static readonly string[] ViewLabels        = { "Total", "Per Category" };
    private static readonly string[] TimeRangeLabels   = { "30 Days", "90 Days", "Lifetime" };
    private static readonly string[] AggregationLabels = { "None", "Weekly", "Monthly" };

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

    private static readonly Color BarColor         = new Color(0.30f, 0.60f, 0.90f, 0.85f);
    private static readonly Color BarHoverColor    = new Color(0.50f, 0.75f, 1.00f, 1.00f);
    private static readonly Color GridColor        = new Color(1f, 1f, 1f, 0.05f);
    private static readonly Color AxisColor        = new Color(1f, 1f, 1f, 0.20f);
    private static readonly Color TooltipBg        = new Color(0.15f, 0.15f, 0.15f, 0.95f);


    public static void Draw()
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

        if (ViewMode == 0)
            DrawBarGraph(snapshots);
        else
            DrawLineGraph(snapshots);
    }


    private static void DrawControls()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("View", GUILayout.Width(40));
        int newView = GUILayout.Toolbar(ViewMode, ViewLabels, GUILayout.Width(180));
        if (newView != ViewMode) { ViewMode = newView; SavePrefs(); }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Range", GUILayout.Width(40));
        int newRange = GUILayout.Toolbar(TimeRange, TimeRangeLabels, GUILayout.Width(200));
        if (newRange != TimeRange) { TimeRange = newRange; SavePrefs(); }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Group", GUILayout.Width(40));
        int newAgg = GUILayout.Toolbar(Aggregation, AggregationLabels, GUILayout.Width(200));
        if (newAgg != Aggregation) { Aggregation = newAgg; SavePrefs(); }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }


    private static void DrawBarGraph(List<HistorySnapshot> snapshots)
    {
        float graphHeight = Mathf.Max(200, GUILayoutUtility.GetLastRect().height);
        Rect  graphRect   = GUILayoutUtility.GetRect(0, 220, GUILayout.ExpandWidth(true));
        graphRect = Deflate(graphRect, 40, 10, 20, 10);

        if (Event.current.type != EventType.Repaint &&
            Event.current.type != EventType.MouseMove &&
            Event.current.type != EventType.Layout)
            return;

        int   count    = snapshots.Count;
        int   maxVal   = snapshots.Max(s => s.total);
        float yMax     = NiceMax(maxVal);
        float barWidth = graphRect.width / count;

        DrawGrid(graphRect, yMax);

        Vector2 mouse = Event.current.mousePosition;
        hoveredIndex  = -1;

        for (int i = 0; i < count; i++)
        {
            float x      = graphRect.x + i * barWidth;
            float height = (snapshots[i].total / yMax) * graphRect.height;
            float y      = graphRect.yMax - height;
            var   barRect = new Rect(x + 1, y, barWidth - 2, height);

            bool hovered = barRect.Contains(mouse) || 
                           (mouse.x >= x && mouse.x < x + barWidth && mouse.y >= graphRect.y && mouse.y <= graphRect.yMax);

            if (hovered) hoveredIndex = i;

            EditorGUI.DrawRect(barRect, hovered ? BarHoverColor : BarColor);
        }

        DrawAxes(graphRect, snapshots.Select(s => s.date).ToList(), yMax);

        if (hoveredIndex >= 0)
            DrawBarTooltip(mouse, snapshots, hoveredIndex);
    }


    private static void DrawLineGraph(List<HistorySnapshot> snapshots)
    {
        var categories = ProjectStatsData.Categories;

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical();
        Rect graphRect = GUILayoutUtility.GetRect(0, 220, GUILayout.ExpandWidth(true));
        graphRect = Deflate(graphRect, 40, 10, 20, 10);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.Width(140));
        EditorGUILayout.Space(4);
        GUILayout.Label("Categories", EditorStyles.boldLabel);
        toggleScrollPos = EditorGUILayout.BeginScrollView(toggleScrollPos, GUILayout.Height(210));
        for (int i = 0; i < categories.Count; i++)
        {
            bool current = GetCategoryToggle(categories[i].Name);
            bool next    = EditorGUILayout.ToggleLeft(
                categories[i].Name,
                current,
                ColoredLabel(CategoryColors[i % CategoryColors.Length])
            );
            if (next != current) SetCategoryToggle(categories[i].Name, next);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        if (Event.current.type != EventType.Repaint &&
            Event.current.type != EventType.MouseMove &&
            Event.current.type != EventType.Layout)
            return;

        int   maxVal = 0;
        foreach (var snap in snapshots)
            foreach (var cat in snap.categories)
                if (GetCategoryToggle(cat.name) && cat.count > maxVal)
                    maxVal = cat.count;

        if (maxVal == 0) return;

        float yMax = NiceMax(maxVal);
        DrawGrid(graphRect, yMax);

        Vector2 mouse        = Event.current.mousePosition;
        int     closestIndex = -1;
        float   closestDist  = float.MaxValue;

        int count = snapshots.Count;
        for (int i = 0; i < count; i++)
        {
            float x    = graphRect.x + (count == 1 ? graphRect.width / 2 : i * graphRect.width / (count - 1));
            float dist = Mathf.Abs(mouse.x - x);
            if (dist < closestDist && graphRect.Contains(mouse))
            {
                closestDist  = dist;
                closestIndex = i;
            }
        }

        Handles.BeginGUI();

        for (int ci = 0; ci < categories.Count; ci++)
        {
            if (!GetCategoryToggle(categories[ci].Name)) continue;
            Handles.color = CategoryColors[ci % CategoryColors.Length];

            for (int i = 1; i < count; i++)
            {
                int   prevCount = GetCategoryCount(snapshots[i - 1], categories[ci].Name);
                int   currCount = GetCategoryCount(snapshots[i],     categories[ci].Name);

                if (prevCount == 0 && currCount == 0) continue;

                float x0 = graphRect.x + (count == 1 ? graphRect.width / 2 : (i - 1) * graphRect.width / (count - 1));
                float x1 = graphRect.x + (count == 1 ? graphRect.width / 2 : i       * graphRect.width / (count - 1));
                float y0 = graphRect.yMax - (prevCount / yMax) * graphRect.height;
                float y1 = graphRect.yMax - (currCount / yMax) * graphRect.height;
                Handles.DrawLine(new Vector3(x0, y0), new Vector3(x1, y1));
            }
        }

        if (closestIndex >= 0)
        {
            float hx = graphRect.x + (count == 1 ? graphRect.width / 2 : closestIndex * graphRect.width / (count - 1));
            Handles.color = AxisColor;
            Handles.DrawLine(new Vector3(hx, graphRect.y), new Vector3(hx, graphRect.yMax));
        }

        var dotPositions = new List<(float x, float y, string name, int count)>();

        for (int ci = 0; ci < categories.Count; ci++)
        {
            if (!GetCategoryToggle(categories[ci].Name)) continue;

            for (int i = 0; i < count; i++)
            {
                int catCount = GetCategoryCount(snapshots[i], categories[ci].Name);
                if (catCount == 0) continue;

                float x = graphRect.x + (count == 1 ? graphRect.width / 2 : i * graphRect.width / (count - 1));
                float y = graphRect.yMax - (catCount / yMax) * graphRect.height;

                Handles.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, 4f);

                Handles.color = CategoryColors[ci % CategoryColors.Length];
                Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, 3f);

                dotPositions.Add((x, y, categories[ci].Name, catCount));
            }
        }

        Handles.EndGUI();

        DrawAxes(graphRect, snapshots.Select(s => s.date).ToList(), yMax);

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
                DrawLineTooltip(mouse, snapshots[closestIndex], graphRect);
        }
    }


    private static void DrawBarTooltip(Vector2 mouse, List<HistorySnapshot> snapshots, int index)
    {
        var   snap  = snapshots[index];
        var   lines = new List<string>();
        int   prev  = index > 0 ? snapshots[index - 1].total : snap.total;
        int   delta = snap.total - prev;

        lines.Add(FormatDate(snap.date));
        lines.Add("Total: " + snap.total + (index > 0 ? "  (" + (delta >= 0 ? "+" : "") + delta + ")" : ""));
        lines.Add("─────────────────");

        if (index > 0)
        {
            foreach (var cat in snap.categories)
            {
                int prevCount = GetCategoryCount(snapshots[index - 1], cat.name);
                int diff      = cat.count - prevCount;
                if (diff != 0)
                    lines.Add(cat.name.PadRight(20) + (diff >= 0 ? "+" : "") + diff);
            }
        }

        DrawTooltipBox(mouse, lines);
    }

    private static void DrawLineTooltip(Vector2 mouse, HistorySnapshot snap, Rect graphRect)
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

    private static void DrawTooltipBox(Vector2 mouse, List<string> lines)
    {
        if (lines.Count == 0) return;

        var  style   = EditorStyles.miniLabel;
        int  padding = 6;
        float lineH  = style.lineHeight + 2;
        float width  = lines.Max(l => style.CalcSize(new GUIContent(l)).x) + padding * 2;
        float height = lines.Count * lineH + padding * 2;

        float x = mouse.x + 12;
        float y = mouse.y - height / 2;

        if (x + width > Screen.width)  x = mouse.x - width - 4;
        if (y < 0)                      y = 0;
        if (y + height > Screen.height) y = Screen.height - height;

        var tooltipRect = new Rect(x, y, width, height);
        EditorGUI.DrawRect(tooltipRect, TooltipBg);
        EditorGUI.DrawRect(new Rect(tooltipRect.x, tooltipRect.y, tooltipRect.width, 1), AxisColor);
        EditorGUI.DrawRect(new Rect(tooltipRect.x, tooltipRect.yMax - 1, tooltipRect.width, 1), AxisColor);
        EditorGUI.DrawRect(new Rect(tooltipRect.x, tooltipRect.y, 1, tooltipRect.height), AxisColor);
        EditorGUI.DrawRect(new Rect(tooltipRect.xMax - 1, tooltipRect.y, 1, tooltipRect.height), AxisColor);

        for (int i = 0; i < lines.Count; i++)
            GUI.Label(new Rect(x + padding, y + padding + i * lineH, width, lineH), lines[i], style);
    }


    private static void DrawGrid(Rect rect, float yMax)
    {
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 0.4f));

        int interval = yMax <= 200 ? 25 : 50;
        int lineCount = (int)(yMax / interval);

        for (int i = 0; i <= lineCount; i++)
        {
            float val = i * interval;
            float y   = rect.yMax - (val / yMax) * rect.height;

            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, 1), i == 0 ? AxisColor : GridColor);
            GUI.Label(
                new Rect(rect.x - 38, y - 8, 36, 16),
                val.ToString(),
                RightMiniLabel()
            );
        }

        EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y, 1, rect.height), AxisColor);
    }

    private static void DrawAxes(Rect rect, List<string> dates, float yMax)
    {
        int count     = dates.Count;
        int maxLabels = Mathf.Max(1, (int)(rect.width / 60));
        int step      = Mathf.Max(1, count / maxLabels);

        for (int i = 0; i < count; i += step)
        {
            float x = rect.x + (count == 1 ? rect.width / 2 : i * rect.width / (count - 1));
            GUI.Label(new Rect(x - 30, rect.yMax + 2, 60, 16), FormatDateShort(dates[i]), CenteredMiniLabel());
        }
    }


    private static List<HistorySnapshot> GetFilteredSnapshots()
    {
        var all = ProjectStatsHistory.GetSnapshots();
        if (all == null || all.Count == 0) return new List<HistorySnapshot>();

        DateTime cutoff = DateTime.MinValue;
        if (TimeRange == 0)      cutoff = DateTime.Now.AddDays(-30);
        else if (TimeRange == 1) cutoff = DateTime.Now.AddDays(-90);

        var filtered = all.Where(s =>
        {
            if (!DateTime.TryParse(s.date, out DateTime d)) return false;
            return d >= cutoff;
        }).ToList();

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
        int interval = value <= 200 ? 25 : 50;
        int rounded  = ((value / interval) + 2) * interval; // round up then add one extra interval
        return rounded;
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
        ViewMode    = EditorPrefs.GetInt("ProjectStats_GraphView",  0);
        TimeRange   = EditorPrefs.GetInt("ProjectStats_TimeRange",  0);
        Aggregation = EditorPrefs.GetInt("ProjectStats_Aggregation",0);
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
