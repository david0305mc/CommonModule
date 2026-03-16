using System.Collections.Generic;
using UnityEngine;


public sealed class UserDataDto
{
    public UserCurrencyDataDTO Currency;
    public Dictionary<int, SkillDataDTO> SkillMap;
}

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
