using R3;
using UnityEngine;
using System;

public class ViewModelBase : IDisposable
{
    protected CompositeDisposable _cd = new();

    public void Dispose()
    {
        _cd.Dispose();
    }
}


public class ViewModel_A : ViewModelBase
{
    private readonly UserData _userModel;
    private readonly Subject<Unit> _moveEvent = new();
    public Observable<Unit> MoveEvent => _moveEvent;

    public ViewModel_A(UserData userModel)
    {
        _userModel = userModel;
        _moveEvent.AddTo(_cd);
    }
    public void AddFunc()
    {
        _userModel.AddGold(10);
        _moveEvent.OnNext(Unit.Default);
    }

}