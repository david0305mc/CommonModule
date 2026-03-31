using Cysharp.Threading.Tasks.Triggers;
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
        
        GameInputManager.Instance.JumpPressed += OnJump;
    }

    private void Update()
    {
        Vector2 input = Vector2.zero;

        if (GameInputManager.HasInstance)
        {
            input = GameInputManager.Instance.MoveValue;
        }

        Vector3 movement = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
        if (movement.sqrMagnitude > 0.001f)
        {
            navMeshAgent.Move(movement * moveSpeed * Time.deltaTime);
            var targetRotation = Quaternion.LookRotation(movement);
            transform.rotation = targetRotation;
        }
    }

    public void OnJump()
    {
        Debug.Log("Jump!");
    }

    void OnDestroy()
    {
        GameInputManager.Instance.JumpPressed -= OnJump;
    }
}
