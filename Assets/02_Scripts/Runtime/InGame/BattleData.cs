using System.Collections.Generic;
using UnityEngine;

public class BattleData
{
    public AllyUnitData AllyUnit { get; private set; }
    private Dictionary<int, EnemyData> enemies;

    public void Init()
    {
        AllyUnit = new AllyUnitData();
        enemies = new Dictionary<int, EnemyData>();
    }

    public void AddEnemy()
    {
        
    }
    
}
