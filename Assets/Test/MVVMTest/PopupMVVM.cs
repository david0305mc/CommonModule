using System;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupMVVM : PopupBase<Unit>
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button addButton;

    private ViewModel_A _vm;
    private CompositeDisposable _cd;

    public override void Awake()
    {
        base.Awake();
        _cd = new CompositeDisposable();

        addButton.onClick.AddListener(() =>
        {
            if (_vm != null)
                _vm.AddFunc();
        });
        GameManager.Instance.UserModel.Gold
    .Subscribe(text => titleText.SetText($"{text}"))
    .AddTo(_cd);
    }
    public override async UniTask Show()
    {
        await base.Show();

        _vm = (ViewModel_A)_args[0];
    }

    void OnDestroy()
    {
        _cd?.Dispose();
        _vm?.Dispose();
    }
}
