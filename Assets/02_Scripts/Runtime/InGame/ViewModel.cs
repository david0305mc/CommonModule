using R3;
using UnityEngine;
using System;

public class ViewModelBase : IDisposable
{
    private CompositeDisposable _cd = new CompositeDisposable();

    public void Dispose()
    {
        _cd.Dispose();
    }
}


public class ViewModel_A : ViewModelBase
{
    UserData _userModel;
    public ViewModel_A(UserData userModel)
    {
        _userModel = userModel;
    }
    public void AddFunc()
    {
        _userModel.Gold.Value++;
    }
    
}