using UnityEngine;
using UnityEngine.UI;

public class UIMain_Test: MonoBehaviour
{
    [SerializeField] Button testButton;
    void Awake()
    {
        testButton.onClick.AddListener(() =>
        {
            PopupManager.Instance.ShowPopupAsync<PopupTest, bool>();
        });
    }
}

