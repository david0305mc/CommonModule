using System.Collections.Generic;

public class BattleData
{
    public HeroRuntimeData AllyUnit { get; private set; }
    private Dictionary<int, EnemyRuntimeData> enemies;

    public void Init()
    {
        AllyUnit = new HeroRuntimeData();
        enemies = new Dictionary<int, EnemyRuntimeData>();
    }
    

    public EnemyRuntimeData AddEnemy(int tid)
    {
        var enemy = EnemyRuntimeData.Create(tid);
        enemies.Add(tid, enemy);
        return enemy;
    }
    
}
