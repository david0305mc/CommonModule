using System.Collections.Generic;
using UnityEngine;

public partial class UserDataManager : Singleton<UserDataManager>
{
    public UserData UserData;

    public void Init()
    {
        UserData = new UserData();
        UserData.Init();
    }
}
