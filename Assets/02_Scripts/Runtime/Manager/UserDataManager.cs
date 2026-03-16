using System.Collections.Generic;
using UnityEngine;

public class UserDataManager : Singleton<UserDataManager>
{
    public UserCurrencyData Currency;
    public Dictionary<int, SkillData> SkillMap;

    public void Init()
    {
        Currency = new UserCurrencyData();
        SkillMap = new Dictionary<int, SkillData>();
    }

    public void LoadUserData()
    {
        
    }

    public void SaveUserData()
    {

    }

    public void AddGem(long add)
    {
        Currency.Gem.Value += add;
    }
}
