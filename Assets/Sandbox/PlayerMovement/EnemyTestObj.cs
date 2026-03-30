using UnityEngine;
using UnityEngine.AI;

public class EnemyTestObj : MonoBehaviour
{
    [SerializeField] private Transform target;
    NavMeshAgent enemyAgent;
    void Start()
    {
        enemyAgent = GetComponent<NavMeshAgent>();
        enemyAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    void Update()
    {
        enemyAgent.SetDestination(target.position);
    }



}
