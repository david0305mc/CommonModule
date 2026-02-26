using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(
    fileName = "TableCodeGenConfig",
    menuName = "Tools/Table CodeGen Config"
)]
public sealed class TableCodeGenConfig : ScriptableObject
{
    [Header("Output Paths")]
    public string dataTableDefPath = "Assets/02_Scripts/Manager/DataManager/DataManager.Data.cs";
    public string configTableDefPath = "Assets/02_Scripts/Manager/DataManager/ConfigTable.cs";
    public string tableEnumDefPath = "Assets/02_Scripts/Manager/DataManager/EnumTable.cs";

    [Header("CSV Folder (Resources/Data 기준이면 그대로 둬도 됨)")]
    public string csvFolderPath = "Assets/Resources/Data";

    [Header("Special Table Names")]
    public string configTableName = "ConfigTable.csv";
    public string enumTableName = "EnumTable.csv";

    public string[] tableNames;

#if UNITY_EDITOR
    [ContextMenu("Refresh Table Names From CSV")]
    public void RefreshTableNames()
    {
        if (!Directory.Exists(csvFolderPath))
        {
            Debug.LogWarning($"CSV 폴더가 존재하지 않습니다: {csvFolderPath}");
            tableNames = new string[0];
            return;
        }

        var csvFiles = Directory.GetFiles(csvFolderPath, "*.csv", SearchOption.TopDirectoryOnly);

        tableNames = csvFiles
            .Select(Path.GetFileName)
            .Where(name =>
                !string.Equals(name, configTableName) &&
                !string.Equals(name, enumTableName))
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name)
            .ToArray();

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        Debug.Log($"TableNames 갱신 완료. 총 {tableNames.Length}개");
    }
#endif
}