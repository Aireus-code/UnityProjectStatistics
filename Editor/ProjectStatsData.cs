using System.Collections.Generic;
using UnityEditor;

public static class ProjectStatsData
{
    public class AssetCategory
    {
        public string Name;
        public string Filter;
        public int    Count;
        public long   Bytes;
    }

    public static readonly List<AssetCategory> Categories = new List<AssetCategory>
    {
        new AssetCategory { Name = "Textures",            Filter = "t:Texture2D" },
        new AssetCategory { Name = "Meshes",              Filter = "t:Mesh" },
        new AssetCategory { Name = "Materials",           Filter = "t:Material" },
        new AssetCategory { Name = "Prefabs",             Filter = "t:Prefab" },
        new AssetCategory { Name = "Scripts",             Filter = "t:MonoScript" },
        new AssetCategory { Name = "Scriptable Objects",  Filter = "t:ScriptableObject" },
        new AssetCategory { Name = "Animations",          Filter = "t:AnimationClip" },
        new AssetCategory { Name = "Animator Controllers",Filter = "t:AnimatorController" },
        new AssetCategory { Name = "Scenes",              Filter = "t:Scene" },
        new AssetCategory { Name = "Audio",               Filter = "t:AudioClip" },
        new AssetCategory { Name = "Sprite Atlases",      Filter = "t:SpriteAtlas" },
        new AssetCategory { Name = "Video Clips",         Filter = "t:VideoClip" },
        new AssetCategory { Name = "Shaders",             Filter = "t:Shader" },
        new AssetCategory { Name = "Timeline Assets",     Filter = "t:TimelineAsset" },
        new AssetCategory { Name = "Render Textures",     Filter = "t:RenderTexture" },
        new AssetCategory { Name = "Terrain",             Filter = "t:TerrainData" },
    };

    public static readonly string KeyEditor               = UnityEngine.Application.dataPath + "_editorTime";
    public static readonly string KeyPlay                 = UnityEngine.Application.dataPath + "_playTime";
    public static readonly string KeyUnfocused            = UnityEngine.Application.dataPath + "_unfocusedTime";
    public static readonly string KeySessions             = UnityEngine.Application.dataPath + "_totalSessions";
    public static readonly string KeySessionID            = UnityEngine.Application.dataPath + "_sessionID";
    public static readonly string KeySessionStartEditor   = UnityEngine.Application.dataPath + "_sessionStartEditor";
    public static readonly string KeySessionStartPlay     = UnityEngine.Application.dataPath + "_sessionStartPlay";
    public static readonly string KeySessionStartUnfocused= UnityEngine.Application.dataPath + "_sessionStartUnfocused";
    public static readonly string KeyCreationDate         = UnityEngine.Application.dataPath + "_creationDate";

    public static float  EditorTotal      = 0f;
    public static float  PlayTotal        = 0f;
    public static float  UnfocusedTotal   = 0f;
    public static int    TotalSessions    = 0;
    public static float  SessionStartEditor    = 0f;
    public static float  SessionStartPlay      = 0f;
    public static float  SessionStartUnfocused = 0f;
    public static double SessionStart     = 0;
    public static bool   Initialized      = false;
    public static bool   InPlayMode       = false;
    public static bool   IsFocused        = true;
    public static float  TimeSinceLastSave = 0f;
    public static float  SaveInterval     = 5f;
    public static string CachedCreationDate = "";

    public static int    TotalAssetCount  = 0;
    public static long   TotalAssetBytes  = 0;
    public static long   OtherBytes       = 0;
    public static bool   HasScanned       = false;
    public static string LastScanned      = "";
    public static float  OtherThreshold   = 0.01f;
    public static int    TotalScriptLines = 0;

    public static string VcsType             = "";
    public static string VcsBranch           = "";
    public static string VcsFirstCommitTime  = "";
    public static string VcsLastCommitTime   = "";
    public static int    VcsCommitCount      = 0;
    public static int    VcsContributors     = 0;
}
