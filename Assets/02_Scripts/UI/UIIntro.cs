using UnityEngine;
using UnityEngine.UI;

public class UIIntro  : MonoBehaviour
{
    [SerializeField] private Button button;

    void Awake()
    {
        button.onClick.AddListener(() =>
        {
            // Debug.Log($"MathUtil.Clamp01(100f) {MathUtil.Clamp01(100f)}");
            DataManager.Instance.LoadDataAsync();
        });
    }
}
