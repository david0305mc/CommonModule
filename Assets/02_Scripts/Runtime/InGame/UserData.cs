using System.Collections.Generic;
using R3;

public sealed class UserCurrencyData
{
    public ReactiveProperty<long> Gold { get; } = new(0);
    public ReactiveProperty<long> Gem { get; } = new(0);
    public ReactiveProperty<long> Heart { get; } = new(0);

    public void ApplyDto(UserCurrencyDataDto dto)
    {
        if (dto == null)
            return;

        Gold.Value = dto.Gold;
        Gem.Value = dto.Gem;
        Heart.Value = dto.Heart;
    }

    public UserCurrencyDataDto ToDto()
    {
        return new UserCurrencyDataDto
        {
            Gold = Gold.Value,
            Gem = Gem.Value,
            Heart = Heart.Value
        };
    }
}

public sealed class UserCurrencyDataDto
{
    public long Gold;
    public long Gem;
    public long Heart;

    public UserCurrencyData ToRuntimeData()
    {
        var data = new UserCurrencyData();
        data.ApplyDto(this);
        return data;
    }
}

public sealed class SkillData
{
    public int SkillID;
    public ReactiveProperty<long> Level { get; } = new(0);

    public void ApplyDto(SkillDataDto dto)
    {
        if (dto == null)
            return;

        SkillID = dto.SkillID;
        Level.Value = dto.Level;
    }

    public SkillDataDto ToDto()
    {
        return new SkillDataDto
        {
            SkillID = SkillID,
            Level = Level.Value
        };
    }
}

public sealed class SkillDataDto
{
    public int SkillID;
    public long Level;
}

public sealed class UserDataDto
{
    public UserCurrencyDataDto Currency;
    public Dictionary<int, SkillDataDto> SkillMap = new();
}

public sealed class UserData
{
    public UserCurrencyData Currency { get; private set; } = new();
    public Dictionary<int, SkillData> SkillMap { get; private set; } = new();

    public void ApplyDto(UserDataDto dto)
    {
        if (dto == null)
            return;

        if (dto.Currency != null)
            Currency.ApplyDto(dto.Currency);

        SkillMap.Clear();

        if (dto.SkillMap == null)
            return;

        foreach (var pair in dto.SkillMap)
        {
            if (pair.Value == null)
                continue;

            SkillMap[pair.Key].ApplyDto(pair.Value);
        }
    }

    public UserDataDto ToDto()
    {
        var dto = new UserDataDto
        {
            Currency = Currency.ToDto(),
            SkillMap = new Dictionary<int, SkillDataDto>()
        };

        foreach (var pair in SkillMap)
        {
            if (pair.Value == null)
                continue;

            dto.SkillMap[pair.Key] = pair.Value.ToDto();
        }

        return dto;
    }
}