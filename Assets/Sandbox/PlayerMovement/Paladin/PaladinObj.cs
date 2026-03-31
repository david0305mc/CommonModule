using UnityEngine;
using UnityEngine.AI;

public class PaladinObj : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    private NavMeshAgent navMeshAgent;

    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    private void Update()
    {
        Vector2 input = Vector2.zero;

        if (GameInputManager.HasInstance)
        {
            input = GameInputManager.Instance.MoveValue;
        }

        Vector3 movement = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
        navMeshAgent.Move(movement * moveSpeed * Time.deltaTime);
    }

    public void OnJump()
    {
        Debug.Log("Jump!");
    }

    void OnEnable()
    {

    }
}
