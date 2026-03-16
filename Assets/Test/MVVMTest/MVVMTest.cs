using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;

#if MVVMTest

public class MVVMTest : Singleton<MVVMTest>
{


    // MVVM Test
    public async UniTask ShowMVVMPopup()
    {
        ViewModel_A vm = new ViewModel_A(UserDataManager.Instance.userData);
        var disposable = vm.MoveEvent.Subscribe(_ =>
        {
            Debug.Log("MoveEffect");
        });
        vm.AddTo(disposable);
        var popup = await PopupManager.Instance.ShowPopup<PopupMVVM, Unit>(new object[] { vm });
        Debug.Log("WaitForShowAsync");
        await popup.WaitForResultAsync();
        Debug.Log("WaitForResultAsync");
        vm.Dispose();
    }
}

#endif
