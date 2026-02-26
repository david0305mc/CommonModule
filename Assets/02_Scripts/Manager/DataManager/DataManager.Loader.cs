using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public partial class DataManager : Singleton<DataManager>
{
    private static TableCodeGenConfig _config;

    private static TableCodeGenConfig Config
    {
        get
        {
            if (_config != null)
                return _config;

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
    public async UniTask LoadDataAsync()
    {
        foreach (var tableName in Config.tableNames)
        {
            try
            {
                string data = await LoadTableDataAsync(tableName);

                if (string.IsNullOrEmpty(data))
                {
                    Debug.LogError($"데이터를 찾을 수 없습니다: {tableName}");
                    continue;
                }

                var method = GetType().GetMethod($"Bind{tableName}Data");
                if (method == null)
                {
                    Debug.LogError($"메서드를 찾을 수 없습니다: Bind{tableName}Data");
                    continue;
                }
                var tableType = Type.GetType($"DataManager+{tableName}");
                if (tableType == null)
                {
                    Debug.Log("tableType == null");
                }

                method.Invoke(this, new object[] { tableType, data });
            }
            catch (Exception e)
            {
                Debug.LogError($"테이블 로드 실패 {tableName}: {e.Message}");
            }
        }
    }
    public async UniTask<string> LoadTableDataAsync(string tableName)
    {
        return Resources.Load<TextAsset>(Path.Combine("Data", tableName))?.text;
    }
}
