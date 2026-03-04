using R3;
using UnityEngine;
using UnityEngine.UI;

public class PopupLevelUp : PopupBase<Unit>
{
    [SerializeField] private Button addButton;

    public override void Awake()
    {
        base.Awake();
    }
    public void Bind(System.Action callback)
    {
        addButton.onClick.RemoveAllListeners();
        addButton.onClick.AddListener(() =>
        {
            callback?.Invoke();
            callback = null;
        });
    }

}
