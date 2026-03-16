using System;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if MVVMTest

public class PopupMVVM : PopupBase<Unit>
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button addButton;
    [SerializeField] private Button backToIntroButton;

    private ViewModel_A _vm;
    private CompositeDisposable _cd;

    protected override void Awake()
    {
        base.Awake();

        addButton.onClick.AddListener(() =>
        {
            _vm?.AddFunc();
        });
        backToIntroButton.onClick.AddListener(() =>
        {

        });
    }
    public override async UniTask Show()
    {
        base.Show();
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

#endif