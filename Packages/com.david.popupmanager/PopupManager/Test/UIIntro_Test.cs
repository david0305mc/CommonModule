using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UIIntro_Test: MonoBehaviour
{
    [SerializeField] private Button button;

    void Awake()
    {
        button.onClick.AddListener(async () =>
        {
            StartGame().Forget();
        });
    }

    private async UniTask StartGame()
    {
        await SceneTransition.Instance.LoadSceneWithFadeAsync("02_Main_Test");
    }
}
