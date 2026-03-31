using UnityEngine;
using UnityEngine.AI;

public class EnemyTestObj : MonoBehaviour
{
    private Transform target;
    NavMeshAgent enemyAgent;
    void Start()
    {
        enemyAgent = GetComponent<NavMeshAgent>();
        enemyAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        target = WorldObj.Instance.PlayerObj.transform;
    }

    void Update()
    {
        enemyAgent.SetDestination(target.position);
    }



}
