using System;
using R3;

public class UnitBaseData
{
    public ReactiveProperty<long> HP { get; } = new();
    public ReactiveProperty<long> MaxHP { get; } = new();
    public ReactiveProperty<long> AttackPower { get; } = new();
    public DataManager.Unit UnitTable { get; protected set; }

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

    public static EnemyRuntimeData Create(int tid)
    {
        var enemy = new EnemyRuntimeData()
        {
            UnitTable = DataManager.Instance.GetUnitData(tid)
        };
        enemy.AttackPower.Value = enemy.UnitTable.damage;
        enemy.MaxHP.Value = enemy.UnitTable.hp;
        enemy.HP.Value = enemy.UnitTable.hp;

        return enemy;
    }
}