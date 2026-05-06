using System.Collections.Generic;

public class BattleData
{
    private const long FirstRuntimeUid = 1;

    public HeroRuntimeData AllyUnit { get; private set; }
    private Dictionary<long, EnemyRuntimeData> enemies;
    private long nextRuntimeUid;

    public void Init()
    {
        AllyUnit = new HeroRuntimeData();
        enemies = new Dictionary<long, EnemyRuntimeData>();
        nextRuntimeUid = FirstRuntimeUid;
    }
    

    public EnemyRuntimeData AddEnemy(int tid)
    {
        long uid = GenerateRuntimeUid();
        var enemy = EnemyRuntimeData.Create(uid, tid);
        enemies.Add(enemy.UID, enemy);
        return enemy;
    }

    private long GenerateRuntimeUid()
    {
        if (nextRuntimeUid <= 0)
            nextRuntimeUid = FirstRuntimeUid;

        return nextRuntimeUid++;
    }
}


