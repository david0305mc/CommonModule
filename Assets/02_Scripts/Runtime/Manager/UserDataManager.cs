

public partial class UserDataManager : Singleton<UserDataManager>
{
    public UserData User { get; private set; }
    public BattleData Battle { get; private set; } 

    public void Init()
    {
        User = new UserData();
        Battle = new BattleData();

    }
}