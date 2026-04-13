using R3;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class GameManager : SingletonMono<GameManager>
{
    [SerializeField] private GameObject enemyobjPrefab;
    private Dictionary<int, EnemyObj> Enemis;

    public async UniTask StartGame()
    {
        UserDataManager.Instance.Init();
        await UserDataManager.Instance.LoadLocalDataAsync();
        await SceneTransition.Instance.LoadSceneWithFadeAsync(GameDefine.MainSceneName);
    }

    public void SpawnEnemy()
    {
        var enemyData = UserDataManager.Instance.Battle.AddEnemy(GameDefine.Enemy01);

    }
    public void SpawnEnemy(int unitId)
    {
        var enemyPrefab = ResourceManager.Instance.GetUnitPrefab(unitId);
        if (enemyPrefab == null)
        {
            Debug.LogError($"[MovementMainUI] Enemy prefab not found. UnitId: {unitId}");
            return;
        }

        if (WorldRoot.Instance == null || WorldRoot.Instance.SpawnPoint == null)
        {
            Debug.LogError("[MovementMainUI] SpawnPoint is null.");
            return;
        }

        var spawnedEnemy = Lean.Pool.LeanPool.Spawn(enemyPrefab, WorldRoot.Instance.SpawnPoint);
        spawnedEnemy.transform.localPosition = Vector3.zero;
        spawnedEnemy.transform.localRotation = Quaternion.identity;
    }
}
