using UnityEngine;
using UnityEngine.UI;

public class UIMain : MonoBehaviour
{
    [SerializeField] Button testButton;
    void Awake()
    {
        testButton.onClick.AddListener(() =>
        {
            PopupManager.Instance.ShowPopupAsync<PopupOK, bool>();
        });
    }
}
