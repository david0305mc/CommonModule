using System.Collections.Generic;

public class BattleData
{
    public HeroRuntimeData AllyUnit { get; private set; }
    private Dictionary<long, EnemyRuntimeData> enemies;

    public void Init()
    {
        AllyUnit = new HeroRuntimeData();
        enemies = new Dictionary<long, EnemyRuntimeData>();
    }
    

    public EnemyRuntimeData AddEnemy(int tid)
    {
        long uid = UserDataManager.Instance.User.GenerateUID();
        var enemy = EnemyRuntimeData.Create(uid, tid);
        enemies.Add(enemy.UID, enemy);
        return enemy;
    }
    
}

