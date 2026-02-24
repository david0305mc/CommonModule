
using UnityEngine;
using UnityEngine.UI;

public class UtilTest : MonoBehaviour
{
    [SerializeField] private Button button;

    void Awake()
    {
        button.onClick.AddListener(() =>
        {
            Debug.Log($"MathUtil.Clamp01(100f) {MathUtil.Clamp01(100f)}");
        });
    }
}
