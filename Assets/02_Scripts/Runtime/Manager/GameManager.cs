using R3;
using UnityEngine;

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
        PopupManager.Instance.ShowPopupAsync<PopupMVVM, Unit>(new object[] {viewModel_A });
    }
}
