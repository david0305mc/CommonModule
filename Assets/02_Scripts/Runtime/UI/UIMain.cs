using R3;
using UnityEngine;
using UnityEngine.UI;

public class UIMain : MonoBehaviour
{
    [SerializeField] Button testButton;
    void Awake()
    {
        testButton.onClick.AddListener(() =>
        {
            GameManager.Instance.ShowTestB();
        });
    }
}
