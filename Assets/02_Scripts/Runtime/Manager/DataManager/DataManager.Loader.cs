#pragma warning disable 114
using System;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class DataManager : Singleton<DataManager>
{
    private static TableCodeGenConfig _config;

    private static TableCodeGenConfig Config
    {
        get
        {
            if (_config != null) return _config;

#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets("t:TableCodeGenConfig");
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _config = AssetDatabase.LoadAssetAtPath<TableCodeGenConfig>(path);
                if (_config != null) return _config;
            }
#endif

            // 런타임에서는 Resources에서 로드(필요 시 경로 맞춰서 변경)
            _config = Resources.Load<TableCodeGenConfig>("TableCodeGenConfig");
            return _config;
        }
    }

    [Preserve]
    public async UniTask LoadDataAsync()
    {
        if (Config == null)
        {
            Debug.LogError("TableCodeGenConfig를 찾을 수 없습니다. (Resources/TableCodeGenConfig.asset 확인)");
            return;
        }

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

                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var method = GetType().GetMethod($"Bind{tableName}Data", flags);
                if (method == null)
                {
                    Debug.LogError($"메서드를 찾을 수 없습니다: Bind{tableName}Data");
                    continue;
                }

                // ✅ 중첩 타입( DataManager+{tableName} )은 GetNestedType이 가장 안전
                var tableType = GetType().GetNestedType(tableName, BindingFlags.Public | BindingFlags.NonPublic);
                if (tableType == null)
                {
                    Debug.LogError($"tableType을 찾을 수 없습니다: DataManager+{tableName}");
                    continue;
                }

                method.Invoke(this, new object[] { tableType, data });
            }
            catch (Exception e)
            {
                Debug.LogError($"테이블 로드 실패 {tableName}: {e}");
            }
        }
    }

    [Preserve]
    public UniTask<string> LoadTableDataAsync(string tableName)
    {
        // Resources/Data/{tableName}.txt(or .bytes/.csv 등 TextAsset) 형태 가정
        var ta = Resources.Load<TextAsset>(Path.Combine("Data", tableName));
        return UniTask.FromResult(ta != null ? ta.text : null);
    }
}
