using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UIIntro : MonoBehaviour
{
    [SerializeField] private Button button;

    void Awake()
    {
        button.onClick.AddListener(async () =>
        {
            // Debug.Log($"MathUtil.Clamp01(100f) {MathUtil.Clamp01(100f)}");
            await DataManager.Instance.LoadDataAsync();
            // foreach (var item in DataManager.Instance.CoralArray)
            // {
            //     Debug.Log($"item {item.id}");
            // }
            GameManager.Instance.StartGame().Forget();
            button.interactable = false;
        });
    }
}
