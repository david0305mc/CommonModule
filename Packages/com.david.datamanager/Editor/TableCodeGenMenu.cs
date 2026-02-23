using UnityEditor;
using UnityEngine;

public static class TableCodeGenMenu
{

    [MenuItem("Tools/Data/Generate Tables")]
    public static void Generate()
    {
        // tableNames를 어디서 관리하느냐에 따라 바꾸면 됨
        var tableNames = new[] { "Coral" };
        // TableCodeGenMenu.Generate(tableNames);
        TableCodeGenerator.GenerateAll(tableNames);
        Debug.Log("테이블 코드 생성 완료");
    }

    [MenuItem("Tools/Data/Generate TableConfig")]
    public static void GenerateTableConfig()
    {
        Debug.Log("TableConfig 생성 실행");

        // 예시: ScriptableObject 생성
        var config = ScriptableObject.CreateInstance<TableCodeGenConfig>();

        string path = "Assets/TableCodeGenConfig.asset";
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Config 생성 완료: {path}");
    }
}
