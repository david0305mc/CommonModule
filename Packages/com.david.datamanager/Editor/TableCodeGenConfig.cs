using UnityEngine;

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
}