using System.Collections.Generic;

public sealed class UserDataDto
{
    public long UIDSeed;
    public UserCurrencyDataDto CurrencyDto = new();
    public Dictionary<int, SkillDataDto> SkillDtos = new();
    public HeroDataDto HeroDto = new();
}

public sealed class UserData : IDtoConvertible<UserDataDto>
{
    private const long DefaultPersistentUid = 1000;

    public long NextPersistentUid { get; private set; } = DefaultPersistentUid;
    public UserCurrencyData Currency { get; private set; } = new();
    public Dictionary<int, SkillData> Skills { get; private set; } = new();
    public HeroData Hero { get; private set; } = new();

    public void ApplyDto(UserDataDto dto)
    {
        if (dto == null)
            return;

        if (dto.CurrencyDto != null)
            Currency.ApplyDto(dto.CurrencyDto);

        DataMapperUtil.ApplyDtoDictionary(Skills, dto.SkillDtos, dtoValue =>
        {
            var skillData = new SkillData(dtoValue.SkillID);
            skillData.ApplyDto(dtoValue);
            return skillData;
        });

        if (dto.HeroDto != null)
            Hero.ApplyDto(dto.HeroDto);

        NextPersistentUid = dto.UIDSeed > 0 ? dto.UIDSeed : DefaultPersistentUid;
    }

    public UserDataDto ToDto()
    {
        return new UserDataDto
        {
            UIDSeed = NextPersistentUid,
            CurrencyDto = Currency.ToDto(),
            SkillDtos = DataMapperUtil.ToDtoDictionary<int, SkillData, SkillDataDto>(Skills),
            HeroDto = Hero.ToDto()
        };
    }

    public long GeneratePersistentUid()
    {
        if (NextPersistentUid <= 0)
            NextPersistentUid = DefaultPersistentUid;

        return NextPersistentUid++;
    }
}
