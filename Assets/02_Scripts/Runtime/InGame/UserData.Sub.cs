
using R3;

public sealed class UserCurrencyData : IDtoConvertible<UserCurrencyDataDto>
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
}

public sealed class SkillData : IDtoConvertible<SkillDataDto>
{
    public int SkillID { get; private set; }
    public ReactiveProperty<long> Level { get; } = new(0);
    public SkillData(int skillId)
    {
        SkillID = skillId;
    }

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


public sealed class HeroData : IDtoConvertible<HeroDataDto>
{
    public long UID { get; private set; }
    public int TableID { get; private set; }
    public ReactiveProperty<long> Level { get; private set; } = new();

    public void ApplyDto(HeroDataDto dto)
    {
        UID = dto.UID;
        TableID = dto.TableID;
        Level.Value = dto.Level;
    }

    public HeroDataDto ToDto()
    {
        return new HeroDataDto()
        {
            UID = UID,
            TableID = TableID,
            Level = Level.Value
        };
    }
}
public sealed class HeroDataDto
{
    public long UID;
    public int TableID;
    public long Level;
}
