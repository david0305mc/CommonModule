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

        addButton.onClick.AddListener(() =>
        {
            _vm?.AddFunc();
        });
    }
    public override async UniTask Show()
    {
        await base.Show();
        _cd?.Dispose();
        _cd = new CompositeDisposable();

        _vm = (ViewModel_A)_args[0];
        _vm.TitleText.Subscribe(goldText =>
        {
            titleText.SetText(goldText);
        }).AddTo(_cd);
    }

    public override async UniTask CloseAsync()
    {
        _cd?.Dispose();
        base.CloseAsync();
    }
}
