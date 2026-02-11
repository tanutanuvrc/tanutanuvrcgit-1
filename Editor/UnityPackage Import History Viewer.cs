#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// インポートされたUnityPackageの履歴を表示するエディタウィンドウ
/// </summary>
public class ImportHistoryWindow : EditorWindow
{
    private Vector2 scrollPos;
    private GUIStyle recordBoxStyle;
    private Texture folderIcon;

    [MenuItem("Tools/UnityPackageインポート履歴")]
    public static void ShowWindow()
    {
        var w = GetWindow<ImportHistoryWindow>(false, "Package履歴", true);
        w.minSize = new Vector2(350, 200);
        w.Show();
    }

    private void OnEnable()
    {
        folderIcon = EditorGUIUtility.IconContent("Folder Icon").image;
        RefreshAndCleanup();
    }

    private void OnFocus()
    {
        RefreshAndCleanup();
    }

    private void RefreshAndCleanup()
    {
        PackageImportLogger.LoadHistory();
        CleanupInvalidRecords();
        Repaint();
    }

    private void OnGUI()
    {
        if (recordBoxStyle == null)
        {
            recordBoxStyle = new GUIStyle(EditorStyles.helpBox);
            recordBoxStyle.margin = new RectOffset(6, 6, 6, 6);
            recordBoxStyle.padding = new RectOffset(10, 10, 10, 10);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("UnityPackage インポート履歴", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        if (PackageImportLogger.history.Count == 0)
        {
            EditorGUILayout.HelpBox("インポート履歴がありません。(´・∀・｀)ﾍｯ", MessageType.Info);
        }
        else
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos))
            {
                scrollPos = scroll.scrollPosition;
                for (int i = 0; i < PackageImportLogger.history.Count; i++)
                {
                    DrawRecord(PackageImportLogger.history[i], i);
                }
            }
        }

        DrawFooter();
    }

    private void DrawRecord(PackageImportRecord record, int index)
    {
        using (new EditorGUILayout.VerticalScope(recordBoxStyle))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(record.packageName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("削除", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    if (EditorUtility.DisplayDialog("履歴の削除", $"「{record.packageName}」の履歴を削除しますか？", "削除", "キャンセル"))
                    {
                        PackageImportLogger.history.RemoveAt(index);
                        PackageImportLogger.SaveHistory();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            EditorGUILayout.Space(2);

            var paths = record.rootFolderPaths;
            for (int j = 0; j < paths.Count; j++)
            {
                string root = paths[j];
                using (new EditorGUILayout.HorizontalScope(EditorStyles.textField))
                {
                    var content = new GUIContent($" {root}", folderIcon);
                    EditorGUILayout.LabelField(content, EditorStyles.miniLabel, GUILayout.Height(25));
                    
                    GUILayout.FlexibleSpace();

                    if (!string.IsNullOrEmpty(root) && GUILayout.Button("開く", GUILayout.Width(100), GUILayout.Height(25)))
                    {
                        var target = GetTargetPath(root);
                        if (target == null)
                        {
                            if (EditorUtility.DisplayDialog(
                                "参照先が見つかりません",
                                $"対象のフォルダやアセットが見つかりませんでした。\n{root}\nを履歴から削除しますか？",
                                "はい", "いいえ"))
                            {
                                record.rootFolderPaths.RemoveAt(j);
                                if (record.rootFolderPaths.Count == 0)
                                {
                                    PackageImportLogger.history.RemoveAt(index);
                                }
                                PackageImportLogger.SaveHistory();
                                Repaint();
                                GUIUtility.ExitGUI();
                            }
                        }
                        else
                        {
                            PingFirstChild(target);
                        }
                    }
                }
                EditorGUILayout.Space(1);
            }
        }
    }

    private void DrawFooter()
    {
        EditorGUILayout.Space(5);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Height(30)))
            {
                RefreshAndCleanup();
            }
            if (GUILayout.Button("全消去", GUILayout.Width(80), GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("全消去", "すべての履歴を削除しますか？", "すべて消す", "やめる"))
                {
                    PackageImportLogger.history.Clear();
                    PackageImportLogger.SaveHistory();
                }
            }
        }
        EditorGUILayout.Space(8);
    }

    private string GetTargetPath(string path)
    {
        if (AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            return path;
        }
        return null;
    }

    private void PingFirstChild(string folderPath)
    {
        string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });

        if (guids != null && guids.Length > 0)
        {
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path != folderPath)
                {
                    var childObj = AssetDatabase.LoadMainAssetAtPath(path);
                    if (childObj != null)
                    {
                        EditorGUIUtility.PingObject(childObj);
                        Selection.activeObject = childObj;
                        return;
                    }
                }
            }
        }

        var folderObj = AssetDatabase.LoadMainAssetAtPath(folderPath);
        if (folderObj != null)
        {
            EditorGUIUtility.PingObject(folderObj);
            Selection.activeObject = folderObj;
        }
    }

    private void CleanupInvalidRecords()
    {
        bool changed = false;
        for (int i = PackageImportLogger.history.Count - 1; i >= 0; i--)
        {
            var record = PackageImportLogger.history[i];
            int countBefore = record.rootFolderPaths.Count;
            record.rootFolderPaths.RemoveAll(r => GetTargetPath(r) == null);
            
            if (record.rootFolderPaths.Count == 0)
            {
                PackageImportLogger.history.RemoveAt(i);
                changed = true;
            }
            else if (countBefore != record.rootFolderPaths.Count)
            {
                changed = true;
            }
        }
        if (changed) PackageImportLogger.SaveHistory();
    }
}

// --- 補助データクラス ---

[Serializable]
public class PackageImportRecord
{
    public string packageName;
    public List<string> rootFolderPaths = new List<string>();
}

[Serializable]
public class HistoryContainer
{
    public List<PackageImportRecord> records;
}

[InitializeOnLoad]
public static class PackageImportLogger
{
    private const string HistoryJsonFileName = "PackageImportHistory.json";
    private const string RootFolderName = "Assets/たぬたぬ"; // 除外用ルート名
    private const string ForceFolder = "Assets/たぬたぬ/インポート履歴";
    private static string currentPackageName;
    private static HashSet<string> assetPathSet = new HashSet<string>();
    private const int MaxHistoryCount = 200;

    public static List<PackageImportRecord> history = new List<PackageImportRecord>();

    static PackageImportLogger()
    {
        LoadHistory();
        AssetDatabase.importPackageStarted += OnImportPackageStarted;
        AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
    }

    private static void OnImportPackageStarted(string packageName)
    {
        currentPackageName = packageName;
        assetPathSet.Clear();
    }

    private static void OnImportPackageCompleted(string packageName)
    {
        if (!string.IsNullOrEmpty(currentPackageName) && currentPackageName == packageName && assetPathSet.Count > 0)
        {
            var rootFolders = assetPathSet
                .Select(path => GetTopLevelFolder(path))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();

            if (rootFolders.Count > 0)
            {
                var record = new PackageImportRecord { packageName = packageName, rootFolderPaths = rootFolders };
                history.RemoveAll(r => r.packageName == packageName && r.rootFolderPaths.SequenceEqual(rootFolders));
                history.Insert(0, record);
                if (history.Count > MaxHistoryCount) history.RemoveRange(MaxHistoryCount, history.Count - MaxHistoryCount);
                SaveHistory();
            }
        }
        currentPackageName = null;
        assetPathSet.Clear();
    }

    private static string GetTopLevelFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        assetPath = assetPath.Replace('\\', '/');

        // --- 追加：自身の管理用フォルダ「Assets/たぬたぬ」以下のパスは無視する ---
        if (assetPath.Contains(RootFolderName)) return null;

        if (!assetPath.StartsWith("Assets/")) return null;
        var parts = assetPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"Assets/{parts[1]}" : null;
    }

    class ImportPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!string.IsNullOrEmpty(currentPackageName) && importedAssets != null)
            {
                foreach (var path in importedAssets) assetPathSet.Add(path);
            }
        }
    }

    public static void SaveHistory()
    {
        try
        {
            if (!Directory.Exists(ForceFolder)) Directory.CreateDirectory(ForceFolder);
            var json = JsonUtility.ToJson(new HistoryContainer { records = history }, true);
            File.WriteAllText($"{ForceFolder}/{HistoryJsonFileName}", json);
        }
        catch (Exception e) { Debug.LogError($"Save Error: {e}"); }
    }

    public static void LoadHistory()
    {
        try
        {
            var path = $"{ForceFolder}/{HistoryJsonFileName}";
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var container = JsonUtility.FromJson<HistoryContainer>(json);
                if (container != null) { history = container.records ?? new List<PackageImportRecord>(); return; }
            }
            history = new List<PackageImportRecord>();
        }
        catch { history = new List<PackageImportRecord>(); }
    }
}
#endif