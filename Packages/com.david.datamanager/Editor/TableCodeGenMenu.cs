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

        string resourcesFolderPath = "Assets/Resources";
        string assetPath = resourcesFolderPath + "/TableCodeGenConfig.asset";

        // Resources 폴더 없으면 생성
        if (!AssetDatabase.IsValidFolder(resourcesFolderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
            Debug.Log("Resources 폴더 생성 완료");
        }

        var existing = AssetDatabase.LoadAssetAtPath<TableCodeGenConfig>(assetPath);

        // 🔴 이미 존재할 경우 경고 팝업
        if (existing != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "TableConfig 이미 존재",
                "기존 TableCodeGenConfig.asset 파일이 존재합니다.\n\n덮어쓰시겠습니까?",
                "덮어쓰기",
                "취소"
            );

            if (!overwrite)
            {
                Debug.Log("생성 취소됨");
                Selection.activeObject = existing;
                return;
            }

            // 기존 파일 삭제
            AssetDatabase.DeleteAsset(assetPath);
        }

        var config = ScriptableObject.CreateInstance<TableCodeGenConfig>();

        AssetDatabase.CreateAsset(config, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = config;

        Debug.Log($"Config 생성 완료: {assetPath}");
    }
}
