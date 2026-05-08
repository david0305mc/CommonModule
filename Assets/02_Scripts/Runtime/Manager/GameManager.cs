using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : SingletonMono<GameManager>
{
    private readonly Dictionary<long, EnemyObj> enemies = new();
    private bool isStartingGame;

    public IReadOnlyDictionary<long, EnemyObj> Enemies => enemies;

    public async UniTask StartGame()
    {
        if (isStartingGame)
        {
            Debug.LogWarning("[GameManager] StartGame is already running.");
            return;
        }

        var sceneTransition = SceneTransition.Instance;
        if (sceneTransition == null)
        {
            Debug.LogError("[GameManager] SceneTransition is null.");
            return;
        }

        isStartingGame = true;

        try
        {
            UserDataManager.Instance.Init();
            enemies.Clear();

            await UserDataManager.Instance.LoadLocalDataAsync();
            await sceneTransition.LoadSceneWithFadeAsync(GameDefine.MainSceneName);
        }
        finally
        {
            isStartingGame = false;
        }
    }

    public void SpawnEnemy(int unitTid)
    {
        TrySpawnEnemy(unitTid, out _);
    }

    public bool TrySpawnEnemy(int unitTid, out EnemyObj spawnedEnemy)
    {
        spawnedEnemy = null;

        var battleData = UserDataManager.Instance.Battle;
        if (battleData == null)
        {
            Debug.LogError("[GameManager] BattleData is not initialized.");
            return false;
        }

        if (!ResourceManager.HasInstance)
        {
            Debug.LogError("[GameManager] ResourceManager is null.");
            return false;
        }

        if (!ResourceManager.Instance.TryGetUnitPrefab(unitTid, out var enemyPrefab) || enemyPrefab == null)
        {
            Debug.LogError($"[GameManager] Enemy prefab not found. UnitId: {unitTid}");
            return false;
        }

        if (!WorldRoot.HasInstance)
        {
            Debug.LogError("[GameManager] WorldRoot is null.");
            return false;
        }

        var worldRoot = WorldRoot.Instance;
        if (worldRoot.SpawnPoint == null)
        {
            Debug.LogError("[GameManager] SpawnPoint is null.");
            return false;
        }

        var spawnedObject = Lean.Pool.LeanPool.Spawn(enemyPrefab, worldRoot.SpawnPoint);
        if (spawnedObject == null)
        {
            Debug.LogError($"[GameManager] Enemy spawn failed. UnitId: {unitTid}");
            return false;
        }

        if (!spawnedObject.TryGetComponent(out spawnedEnemy))
        {
            Debug.LogError($"[GameManager] Spawned prefab has no EnemyObj component. UnitId: {unitTid}");
            Lean.Pool.LeanPool.Despawn(spawnedObject);
            spawnedEnemy = null;
            return false;
        }

        spawnedEnemy.transform.localPosition = Vector3.zero;
        spawnedEnemy.transform.localRotation = Quaternion.identity;

        var enemyData = battleData.AddEnemy(unitTid);
        enemies.Add(enemyData.UID, spawnedEnemy);

        return true;
    }

    public bool TryGetEnemy(long uid, out EnemyObj enemy)
    {
        return enemies.TryGetValue(uid, out enemy);
    }

    public bool DespawnEnemy(long uid)
    {
        if (!enemies.TryGetValue(uid, out var enemy) || enemy == null)
            return false;

        Lean.Pool.LeanPool.Despawn(enemy);
        enemies.Remove(uid);

        return true;
    }
}
