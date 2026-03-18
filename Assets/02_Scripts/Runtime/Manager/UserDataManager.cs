
public partial class UserDataManager : Singleton<UserDataManager>
{
    public UserData UserData { get; private set; }

    public void Init()
    {
        UserData = new UserData();
    }
}