
using System;
using PaladinTest;
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

public class UnitBaseData
{
    public ReactiveProperty<long> HP { get; } = new();
    public ReactiveProperty<long> MaxHP { get; } = new();
    public ReactiveProperty<long> AttackPower { get; } = new();

    public void TakeDamage(long damage)
    {
        HP.Value -= damage;
        if (HP.Value < 0)
            HP.Value = 0;
    }
    public void Heal(long amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        HP.Value = Math.Min(MaxHP.Value, HP.Value + amount);
    }
}


public class AllyUnitDataDto
{
    public long HP { get; set; }
    public long MaxHP { get; set; }
    public long AttackPower { get; set; }
    public long Exp { get; set; }
}

public class AllyUnitData : UnitBaseData, IDtoConvertible<AllyUnitDataDto>
{
    public ReactiveProperty<long> Exp { get; set; }

    public void ApplyDto(AllyUnitDataDto dto)
    {
        HP.Value = dto.HP;
        MaxHP.Value = dto.MaxHP;
        AttackPower.Value = dto.AttackPower;
        Exp.Value = dto.Exp;
    }

    public AllyUnitDataDto ToDto()
    {
        return new AllyUnitDataDto()
        {
            HP = HP.Value,
            MaxHP = MaxHP.Value,
            AttackPower = AttackPower.Value,
            Exp = Exp.Value
        };
    }
}

public class EnemyData : UnitBaseData
{
    public long RewardGold { get; set; }
}