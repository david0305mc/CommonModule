using System.Collections.Generic;

public static class UserDataMapper
{
    public static UserDataDto ToDto(this UserData runtime)
    {
        if (runtime == null)
            return null;

        var dto = new UserDataDto
        {
            Currency = new UserCurrencyDataDto
            {
                Gem = runtime.Currency.Gem.Value,
                Gold = runtime.Currency.Gold.Value,
                Heart = runtime.Currency.Heart.Value,
            },
            SkillMap = new Dictionary<int, SkillDataDto>()
        };

        if (runtime.SkillMap != null)
        {
            foreach (var pair in runtime.SkillMap)
            {
                dto.SkillMap[pair.Key] = pair.Value.ToDto();
            }
        }

        return dto;
    }

    public static void ApplyDto(this UserDataDto dto, UserData runtime)
    {
        if (dto == null || runtime == null)
            return;

        if (dto.Currency != null)
        {
            runtime.Currency.Gem.Value = dto.Currency.Gem;
            runtime.Currency.Gold.Value = dto.Currency.Gold;
            runtime.Currency.Heart.Value = dto.Currency.Heart;
        }

        runtime.SkillMap.Clear();

        if (dto.SkillMap != null)
        {
            foreach (var pair in dto.SkillMap)
            {
                runtime.SkillMap[pair.Key].ApplyDto(pair.Value);
            }
        }
    }
}