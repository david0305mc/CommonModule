using UnityEngine;
using Cysharp.Threading.Tasks;
using System.IO;
using UnityEditor;

public static class TableCodeLoader
{
    private static TableCodeGenConfig _config;

    private static TableCodeGenConfig Config
    {
        get
        {
            if (_config != null) 
                return _config;

            // 1) 프로젝트 내 TableCodeGenConfig 검색
            var guids = AssetDatabase.FindAssets("t:TableCodeGenConfig");
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _config = AssetDatabase.LoadAssetAtPath<TableCodeGenConfig>(path);
                if (_config != null) return _config;
            }
            return _config;
        }
    }
    public static async UniTask LoadAllDataAsync()
    {
        foreach (var tableName in _config.tableNames)
        {
            var data = LoadTableDataAsync(tableName);
        }
        

    }

    public static async UniTask<string> LoadTableDataAsync(string tableName)
    {
        return Resources.Load<TextAsset>(Path.Combine("Data", tableName))?.text;
    }

}
