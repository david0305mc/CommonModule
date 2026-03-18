using UnityEngine;

public static class UserDataMapper
{
    public static UserDataDto ToDto(this UserData runtime)
    {
        var dto = new UserDataDto
        {
            Currency = new UserCurrencyDataDto()
            {
                Gem = runtime.Currency.Gem.Value,
                Gold = runtime.Currency.Gold.Value,
                Heart = runtime.Currency.Heart.Value,
            },
            SkillMap = new System.Collections.Generic.Dictionary<int, SkillDataDto>()
            {
                
            }
        };

        return dto;
    }
    public static void ApplyDto(this UserDataDto dto, UserData runtime)
    {
        runtime.Currency.Gem.Value = dto.Currency.Gem;
        runtime.Currency.Gold.Value = dto.Currency.Gold;
        runtime.Currency.Heart.Value = dto.Currency.Heart;
        runtime.SkillMap.Clear();
        foreach (var item in runtime.SkillMap)
        {
            runtime.SkillMap.Add(item.Key, item.Value);
        }


    }
}
