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
    public long UID { get; private set; }
    public long RewardGold { get; set; }

    public static EnemyRuntimeData Create(long uid, int tid)
    {
        if (uid <= 0)
            throw new ArgumentOutOfRangeException(nameof(uid));

        var unitTable = DataManager.Instance.GetUnitData(tid);
        if (unitTable == null)
            throw new ArgumentException($"Unit data not found. tid: {tid}", nameof(tid));

        var enemy = new EnemyRuntimeData()
        {
            UID = uid,
            UnitTable = unitTable
        };
        enemy.AttackPower.Value = unitTable.damage;
        enemy.MaxHP.Value = unitTable.hp;
        enemy.HP.Value = unitTable.hp;

        return enemy;
    }
}
