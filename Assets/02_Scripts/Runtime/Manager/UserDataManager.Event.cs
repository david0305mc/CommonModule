using Cysharp.Threading.Tasks;
using UnityEngine;

public partial class UserDataManager : Singleton<UserDataManager>
{

    public void AddGem()
    {
        UserData.Currency.Gem.Value += 1;
        SaveLocalDataAsync().Forget();
    }
}
