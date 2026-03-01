using UnityEngine;
using R3;
using System;

public class UserDataManager : Singleton<UserDataManager>
{
    // Model은 데이터만 보관 (도메인/상태)
    public ReactiveProperty<int> TestValue { get; } = new ReactiveProperty<int>(0);
}


public sealed class PopupViewModel : IDisposable
{
    private readonly UserDataManager _model;
    private readonly CompositeDisposable _cd = new CompositeDisposable();

    // View가 바인딩할 출력(표시용)
    public ReadOnlyReactiveProperty<string> TitleText { get; }

    // View가 실행할 명령(버튼 클릭 등)
    public void Add()  => _model.TestValue.Value++;
    public void Minus() => _model.TestValue.Value--;

    public PopupViewModel(UserDataManager model)
    {
        _model = model;

        // int -> string 변환 같은 표시 로직은 ViewModel에서 처리
        TitleText = _model.TestValue
            .Select(v => v.ToString())
            .ToReadOnlyReactiveProperty()
            .AddTo(_cd);
    }

    public void Dispose()
    {
        _cd.Dispose();
    }
}