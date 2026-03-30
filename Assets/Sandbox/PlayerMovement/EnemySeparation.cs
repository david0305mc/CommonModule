using UnityEngine;
using UnityEngine.AI;

public class EnemySeparation : MonoBehaviour
{
    public float separationRadius = 1.2f;
    public float separationStrength = 2f;
    public LayerMask enemyLayer;

    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    void Update()
    {
        Vector3 separation = Vector3.zero;

        Collider[] hits = Physics.OverlapSphere(transform.position, separationRadius, enemyLayer);

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            Vector3 diff = transform.position - hit.transform.position;
            float dist = diff.magnitude;

            if (dist > 0.001f)
            {
                float weight = 1f - (dist / separationRadius);
                separation += diff.normalized * weight;
            }
        }

        separation.y = 0f;

        if (separation != Vector3.zero)
        {
            agent.Move(separation.normalized * separationStrength * Time.deltaTime);
        }
    }
}