using Cysharp.Threading.Tasks;

public partial class UserDataManager : Singleton<UserDataManager>
{
    public void AddGem(long amount = 1)
    {
        if (amount <= 0)
            return;

        UserData.Currency.Gem.Value += amount;
        SaveLocalDataAsync().Forget();
    }
}