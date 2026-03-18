using System.Collections.Generic;
using R3;



public class UserCurrencyData
{
    public ReactiveProperty<long> Gold = new ReactiveProperty<long>();
    public ReactiveProperty<long> Gem = new ReactiveProperty<long>();
    public ReactiveProperty<long> Heart = new ReactiveProperty<long>();

    public void ApplyDto(UserCurrencyDataDto dto)
    {
        Gold.Value = dto.Gold;
        Gem.Value = dto.Gem;
        Heart.Value = dto.Heart;
    }
}

public class UserCurrencyDataDto
{
    public long Gold;
    public long Gem;
    public long Heart;

    public static UserCurrencyDataDto ToDto(UserCurrencyData runtime)
    {
        return new UserCurrencyDataDto()
        {
            Gold = runtime.Gold.Value,
            Gem = runtime.Gem.Value,
            Heart = runtime.Heart.Value
        };
    }
}

public class SkillData
{
    public int SkillID;
    public ReactiveProperty<long> Level = new ReactiveProperty<long>();
}

public class SkillDataDto
{
    public int SkillID;
    public long Level;
}


public sealed class UserDataDto
{
    public UserCurrencyDataDto Currency;
    public Dictionary<int, SkillDataDto> SkillMap;    
}

public sealed class UserData
{
    public UserCurrencyData Currency;
    public Dictionary<int, SkillData> SkillMap;
    public void Init()
    {
        Currency = new UserCurrencyData();
        SkillMap = new Dictionary<int, SkillData>();
    }
}