// Assets/Editor/DefineSymbolSwitcher.cs
#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;

public static class DefineSymbolSwitcher
{
    // 요청하신 심볼 명칭 그대로 사용
    private const string DEV = "DEV";
    private const string QA = "QA";
    private const string RELEASE = "RELEASE";

    private const string MenuRoot = "Tools/Build Defines/";

    [MenuItem(MenuRoot + "Set DEV", false, 10)]
    public static void SetDev() => SetOnly(DEV);

    [MenuItem(MenuRoot + "Set QA", false, 11)]
    public static void SetQa() => SetOnly(QA);

    [MenuItem(MenuRoot + "Set RELEASE", false, 12)]
    public static void SetRelease() => SetOnly(RELEASE);

    [MenuItem(MenuRoot + "Clear All", false, 30)]
    public static void ClearAll() => SetOnly(null);

    [MenuItem(MenuRoot + "Toggle/DEV", false, 50)]
    public static void ToggleDev() => Toggle(DEV);

    [MenuItem(MenuRoot + "Toggle/QA", false, 51)]
    public static void ToggleQa() => Toggle(QA);

    [MenuItem(MenuRoot + "Toggle/RELEASE", false, 52)]
    public static void ToggleRelease() => Toggle(RELEASE);

    // 체크 표시(현재 포함 여부)
    [MenuItem(MenuRoot + "Set DEV", true)]
    private static bool ValidateSetDev() { SetCheck("Set DEV", DEV); return true; }

    [MenuItem(MenuRoot + "Set QA", true)]
    private static bool ValidateSetQa() { SetCheck("Set QA", QA); return true; }

    [MenuItem(MenuRoot + "Set RELEASE", true)]
    private static bool ValidateSetRelease() { SetCheck("Set RELEASE", RELEASE); return true; }

    private static void SetCheck(string itemName, string symbol)
    {
        var group = EditorUserBuildSettings.selectedBuildTargetGroup;
        var defines = GetDefines(group);
        Menu.SetChecked(MenuRoot + itemName, defines.Contains(symbol));
    }

    private static void SetOnly(string symbolOrNull)
    {
        var group = EditorUserBuildSettings.selectedBuildTargetGroup;

        var next = new[] { DEV, QA, RELEASE }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Except(symbolOrNull == null ? Array.Empty<string>() : new[] { symbolOrNull })
            .ToHashSet();

        // 현재 define에서 DEV/QA/RELEASE만 제거
        var cur = GetDefines(group).ToList();
        cur.RemoveAll(d => d == DEV || d == QA || d == RELEASE);

        // 선택한 것만 추가
        if (!string.IsNullOrWhiteSpace(symbolOrNull))
            cur.Add(symbolOrNull);

        SetDefines(group, cur);
        Log(group);
    }

    private static void Toggle(string symbol)
    {
        var group = EditorUserBuildSettings.selectedBuildTargetGroup;

        var cur = GetDefines(group).ToList();
        bool has = cur.Contains(symbol);

        if (has)
        {
            cur.RemoveAll(d => d == symbol);
        }
        else
        {
            // 서로 배타로 쓰고 싶으면(DEV/QA/RELEASE 중 하나만) 아래 3줄 유지
            cur.RemoveAll(d => d == DEV || d == QA || d == RELEASE);
            cur.Add(symbol);
        }

        SetDefines(group, cur);
        Log(group);
    }

    private static string[] GetDefines(BuildTargetGroup group)
    {
        var raw = PlayerSettings.GetScriptingDefineSymbolsForGroup(group) ?? "";
        return raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(s => s.Trim())
                  .Where(s => !string.IsNullOrEmpty(s))
                  .Distinct()
                  .ToArray();
    }

    private static void SetDefines(BuildTargetGroup group, System.Collections.Generic.IEnumerable<string> symbols)
    {
        var joined = string.Join(";", symbols.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, joined);
        AssetDatabase.SaveAssets();
    }

    private static void Log(BuildTargetGroup group)
    {
        var raw = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        UnityEngine.Debug.Log($"[Defines] ({group}) => {raw}");
    }
}
#endif
