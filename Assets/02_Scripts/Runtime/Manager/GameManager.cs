using R3;
using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

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
    public async UniTask ShowTestA()
    {
        ViewModel_A vm = new ViewModel_A(UserModel);
        var disposable = vm.MoveEvent.Subscribe(_ =>
        {
            MoveEffect();
        });
        vm.AddTo(disposable);
        var popup = await PopupManager.Instance.ShowPopup<PopupMVVM, Unit>(new object[] { vm });
        Debug.Log("WaitForShowAsync");
        await popup.WaitForResultAsync();
        Debug.Log("WaitForResultAsync");
        vm.Dispose();
    }

    public void MoveEffect()
    {
        // some Event
        Debug.Log("MoveEffect");
    }
}
