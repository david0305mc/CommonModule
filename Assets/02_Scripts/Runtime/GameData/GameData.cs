using System;
using R3;

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


public class HeroRuntimeData : UnitBaseData
{
    public ReactiveProperty<long> Exp { get; set; }
}

public class EnemyRuntimeData : UnitBaseData
{
    public long RewardGold { get; set; }
}