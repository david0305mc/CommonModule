using UnityEngine;

public class WorldRoot : SingletonMono<WorldRoot>
{
    [SerializeField] private EnemyPatrolPoints _enemyPatrolPoints;
    public EnemyPatrolPoints EnemyPatrolPoints => _enemyPatrolPoints;
    public GameObject PlayerObj;
    public Transform SpawnPoint;


    protected override void Awake()
    {
        base.Awake();
    }
}
