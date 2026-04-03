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
}
