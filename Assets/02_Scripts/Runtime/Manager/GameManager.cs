using R3;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class GameManager : SingletonMono<GameManager>
{
    public async UniTask StartGame()
    {
        UserDataManager.Instance.Init();
        await SceneTransition.Instance.LoadSceneWithFadeAsync(GameDefine.MainSceneName);
    }
}
