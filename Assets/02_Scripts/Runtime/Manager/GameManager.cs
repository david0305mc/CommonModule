using R3;
using UnityEngine;
using System;
using Cysharp.Threading.Tasks;

public class GameManager : SingletonMono<GameManager>
{
    public UserData UserModel;

    protected override void Awake()
    {
        base.Awake();
        InitUserData();
    }

    private void InitUserData()
    {
        UserModel = new UserData();
        UserModel.InitData();
    }
    public void ShowTestA()
    {
        ViewModel_A viewModel_A = new ViewModel_A(UserModel);
        PopupManager.Instance.ShowPopupAsync<PopupMVVM, Unit>(new object[] { viewModel_A }).Forget();
    }

    public void ShowTestB()
    {
        PopupManager.Instance.ShowPopupAsync<PopupLevelUp, Unit>(new object[]
        {
            new System.Action(() =>
            {
                UserModel.AddGold(10);
            })
        }).Forget();
    }
}
