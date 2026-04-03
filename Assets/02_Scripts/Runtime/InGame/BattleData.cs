using System.Collections.Generic;
using UnityEngine;

public class BattleData
{
    public HeroRuntimeData AllyUnit { get; private set; }
    private Dictionary<int, EnemyRuntimeData> enemies;

    public void Init()
    {
        AllyUnit = new HeroRuntimeData();
        enemies = new Dictionary<int, EnemyRuntimeData>();
    }

    public void AddEnemy()
    {
        
    }
    
}
