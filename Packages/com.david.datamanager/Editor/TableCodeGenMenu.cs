using UnityEditor;
using UnityEngine;

public static class TableCodeGenMenu
{

    [MenuItem("Tools/Data/Generate Tables")]
    public static void Generate()
    {
        TableCodeGenerator.GenerateAll();
        Debug.Log("테이블 코드 생성 완료");
    }

    [MenuItem("Tools/Data/Generate TableConfig")]
    public static void GenerateTableConfig()
    {
        Debug.Log("TableConfig 생성 실행");

        // 1️⃣ Resources 폴더 경로
        string resourcesFolderPath = "Assets/Resources";
        string assetPath = resourcesFolderPath + "/TableCodeGenConfig.asset";

        // 2️⃣ Resources 폴더 없으면 생성
        if (!AssetDatabase.IsValidFolder(resourcesFolderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
            Debug.Log("Resources 폴더 생성 완료");
        }

        // 3️⃣ 이미 존재하는지 체크
        var existing = AssetDatabase.LoadAssetAtPath<TableCodeGenConfig>(assetPath);
        if (existing != null)
        {
            Debug.LogWarning("이미 TableCodeGenConfig가 존재합니다.");
            Selection.activeObject = existing;
            return;
        }

        // 4️⃣ ScriptableObject 생성
        var config = ScriptableObject.CreateInstance<TableCodeGenConfig>();

        AssetDatabase.CreateAsset(config, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = config;

        Debug.Log($"Config 생성 완료: {assetPath}");
    }
}
