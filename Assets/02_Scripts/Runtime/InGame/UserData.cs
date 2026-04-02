using System.Collections.Generic;

public sealed class UserDataDto
{
    public UserCurrencyDataDto Currency = new();
    public Dictionary<int, SkillDataDto> Skills = new();
    public AllyUnitDataDto AllyUnit = new();
}

public sealed class UserData : IDtoConvertible<UserDataDto>
{
    public UserCurrencyData Currency { get; private set; } = new();
    public Dictionary<int, SkillData> Skills { get; private set; } = new();
    public AllyUnitData AllyUnit { get; private set; } = new();

    public void ApplyDto(UserDataDto dto)
    {
        if (dto == null)
            return;

        if (dto.Currency != null)
            Currency.ApplyDto(dto.Currency);

        DataMapperUtil.ApplyDtoDictionary(Skills, dto.Skills, dtoValue =>
        {
            var skillData = new SkillData(dtoValue.SkillID);
            skillData.ApplyDto(dtoValue);
            return skillData;
        });
    }

    public UserDataDto ToDto()
    {
        return new UserDataDto
        {
            Currency = Currency.ToDto(),
            Skills = DataMapperUtil.ToDtoDictionary<int, SkillData, SkillDataDto>(Skills),
            AllyUnit = AllyUnit.ToDto()
        };
    }
}