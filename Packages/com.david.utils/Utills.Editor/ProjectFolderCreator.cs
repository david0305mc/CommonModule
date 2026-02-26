#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

public static class ProjectFolderCreator
{
    // 루트 폴더(서브폴더 없는 것들)
    private static readonly string[] RootFolders =
    {
        "01_Scenes",
        "03_Prefabs",
        "04_Models",
        "06_Animations",
        "07_Materials",
        "09_Shaders",
        "10_Fonts",
        "11_Plugins",
        "12_Addressables",
        "Resources",
        "21_Lighting",
        "97_ETC",
    };

    // 서브폴더가 필요한 것들만 따로
    private static readonly Dictionary<string, string[]> SubFolders = new()
    {
        { "02_Scripts", new[] { "Runtime", "Editor" } },
        { "05_Art",     new[] { "Textures", "Sprites", "UI" } },
        { "08_Audio",   new[] { "BGM", "SFX" } },
        { "97_ETC",     new[] { "Docs", "Temp", "Reference" } },
    };

    [MenuItem("Tools/Project Setup/Create Default Folders", priority = 0)]
    public static void CreateDefaultFolders()
    {
        const string assetsRoot = "Assets";

        // 1) 루트 폴더 생성
        foreach (var folder in RootFolders)
            EnsureFolder(assetsRoot, folder);

        // 2) 서브 폴더 포함된 루트 생성 + 서브 생성
        foreach (var kv in SubFolders)
        {
            var root = kv.Key;
            var subs = kv.Value;

            EnsureFolder(assetsRoot, root);

            var parent = $"{assetsRoot}/{root}";
            foreach (var sub in subs)
                EnsureFolder(parent, sub);
        }

        AssetDatabase.Refresh();
    }

    private static void EnsureFolder(string parentPath, string folderName)
    {
        var fullPath = $"{parentPath}/{folderName}";
        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parentPath, folderName);
    }
}
#endif