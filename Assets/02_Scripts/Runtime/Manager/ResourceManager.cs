using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ResourceManager : SingletonMono<ResourceManager>
{
    private readonly Dictionary<int, GameObject> unitPrefabs = new();

    public async UniTask PreLoading(CancellationToken cancellationToken = default)
    {
        unitPrefabs.Clear();

        var unitArray = DataManager.Instance?.UnitArray;
        if (unitArray == null)
        {
            Debug.LogError("[ResourceManager] UnitArray is null.");
            return;
        }

        foreach (var item in unitArray)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item == null)
                continue;

            if (string.IsNullOrWhiteSpace(item.prefabname))
            {
                Debug.LogWarning($"[ResourceManager] Invalid prefab path. UnitId: {item.id}");
                continue;
            }

            GameObject prefab = await LoadAsync<GameObject>(item.prefabname, cancellationToken);

            if (prefab == null)
            {
                Debug.LogWarning($"[ResourceManager] Preload failed. UnitId: {item.id}, Path: {item.prefabname}");
                continue;
            }

            unitPrefabs[item.id] = prefab;
        }
    }

    public async UniTask<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default)
        where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Debug.LogError("[ResourceManager] Path is null or empty.");
            return null;
        }

        ResourceRequest request = Resources.LoadAsync<T>(path);
        await request.ToUniTask(cancellationToken: cancellationToken);

        T asset = request.asset as T;
        if (asset == null)
        {
            Debug.LogError($"[ResourceManager] Failed to load asset. Type: {typeof(T).Name}, Path: {path}");
            return null;
        }

        return asset;
    }

    public GameObject GetUnitPrefab(int id)
    {
        if (unitPrefabs.TryGetValue(id, out var prefab))
            return prefab;

        Debug.LogWarning($"[ResourceManager] Unit prefab not found. Id: {id}");
        return null;
    }

    public bool TryGetUnitPrefab(int id, out GameObject prefab)
    {
        return unitPrefabs.TryGetValue(id, out prefab);
    }
}