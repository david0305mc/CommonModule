using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework.Constraints;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityHFSM;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyObj : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Fight,
        Search,
    }

    public enum FightState
    {
        Wait,
        Telegraph,
        Attack,
    }

    [SerializeField] private ColliderDetection _detectionRange;
    [SerializeField] TextMeshProUGUI _stateText;

    private const float _attackRange = 2f;
    private const float _attackExitRange = 2.4f;
    private const float _fightKeepDistance = 1f;
    private const float _searchRange = 3f;
    private float _minDistance = 0.5f;
    [SerializeField, Min(0f)] private float _moveSpeed = 2f;

    private Transform[] patrolPoints;
    private Transform target;
    private NavMeshAgent enemyAgent;
    private Animator animator;
    private StateMachine _fsm;
    private float DistanceToTarget => target == null ? 0 : Vector3.Distance(transform.position, target.position);

    private void Start()
    {
        animator = GetComponent<Animator>();
        enemyAgent = GetComponent<NavMeshAgent>();
        ApplyAgentSettings();
        target = WorldRoot.Instance.PlayerObj.transform;
        patrolPoints = WorldRoot.Instance.EnemyPatrolPoints.Points;
        InitFsm();
        InitDetection();
    }

    private void OnValidate()
    {
        if (TryGetComponent(out NavMeshAgent agent))
        {
            agent.speed = _moveSpeed;
        }
    }

    private void ApplyAgentSettings()
    {
        enemyAgent.speed = _moveSpeed;
        enemyAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    private void InitDetection()
    {
        _detectionRange.SetOnTriggerAction(other =>
        {
            if (other.CompareTag("Player"))
            {
                _fsm.Trigger("PlayerSpotted");
            }
        });
    }

    private void InitFsm()
    {
        HybridStateMachine fightFsm = new HybridStateMachine(needsExitTime: true, beforeOnLogic: s =>
        {
            MoveToward(target.position);
        });
        fightFsm.AddState(nameof(FightState.Wait), new UniTaskState(async ct =>
        {
            // animator.Play("GuardIdle");
        }));
        fightFsm.AddState(nameof(FightState.Telegraph), new UniTaskState(async ct =>
        {
            // animator.Play("GuardTelegraph");
        }));
        fightFsm.AddState(nameof(FightState.Attack), new UniTaskState(async ct =>
        {
            // animator.Play("GuardHit");
            await UniTask.WaitForSeconds(0.5f, cancellationToken: ct);
            fightFsm.RequestStateChange(nameof(FightState.Wait));
        }));
        fightFsm.AddExitTransition(nameof(FightState.Wait));
        fightFsm.AddTransition(new TransitionAfter(nameof(FightState.Wait), nameof(FightState.Telegraph), 0.5f));
        fightFsm.AddTransition(new TransitionAfter(nameof(FightState.Telegraph), nameof(FightState.Attack), 0.42f));

        _fsm = new StateMachine();
        _fsm.AddState(nameof(EnemyState.Fight), fightFsm);
        _fsm.AddState(nameof(EnemyState.Patrol), new UniTaskState(
            onEnterAsync: PatrolState,
            externalCancellationToken: this.GetCancellationTokenOnDestroy()));
        _fsm.AddState(nameof(EnemyState.Chase), new UniTaskState(
            onEnterAsync: ChaseState,
            externalCancellationToken: this.GetCancellationTokenOnDestroy()));

        _fsm.AddState(nameof(EnemyState.Search), new UniTaskState(onEnterAsync: async ct =>
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Yield(cancellationToken: ct);
            }
        }, externalCancellationToken: this.GetCancellationTokenOnDestroy()));

        _fsm.AddTriggerTransition("PlayerSpotted", nameof(EnemyState.Patrol), nameof(EnemyState.Chase),
            onTransition: transition => LogTransition(transition));

        _fsm.AddTransition(nameof(EnemyState.Chase), nameof(EnemyState.Fight),
            s => { return DistanceToTarget <= _attackRange; },
            onTransition: transition => LogTransition(transition));
        _fsm.AddTransition(nameof(EnemyState.Fight), nameof(EnemyState.Chase),
            s => { return DistanceToTarget > _attackExitRange; },
            onTransition: transition => LogTransition(transition));
        _fsm.AddTransition(nameof(EnemyState.Chase), nameof(EnemyState.Search),
            s => { return DistanceToTarget > _searchRange; },
            onTransition: transition => LogTransition(transition));
        _fsm.AddTransition(nameof(EnemyState.Search), nameof(EnemyState.Chase),
            s => { return DistanceToTarget <= _searchRange; },
            onTransition: transition => LogTransition(transition));
        _fsm.AddTransition(new TransitionAfter(nameof(EnemyState.Search), nameof(EnemyState.Patrol), 2f,
            onTransition: transition => LogTransition(transition)));
        _fsm.SetStartState(nameof(EnemyState.Patrol));
        _fsm.Init();
    }

    private async UniTask PatrolState(CancellationToken ct)
    {
        int patrolIndex = 0;
        while (!ct.IsCancellationRequested)
        {
            var targetPos = patrolPoints[patrolIndex].position;
            await MoveToAsync(targetPos, ct);
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
    }

    private async UniTask MoveToAsync(Vector3 targetPos, CancellationToken ct)
    {
        enemyAgent.SetDestination(targetPos);
        while (!ct.IsCancellationRequested)
        {
            if (!enemyAgent.pathPending && enemyAgent.remainingDistance <= enemyAgent.stoppingDistance + 0.1f)
            {
                break;
            }
            await UniTask.Yield(cancellationToken: ct);
        }
    }

    private async UniTask ChaseState(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            MoveToward(target.position);
            await UniTask.Yield(ct);
        }
    }
    private void MoveToward(Vector3 targetPos)
    {
        FacePositionImmediately(targetPos);
        enemyAgent.SetDestination(targetPos);
    }

    private void FacePositionImmediately(Vector3 targetPos)
    {
        Vector3 direction = targetPos - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void LogTransition(TransitionBase<string> transition)
    {
        Debug.Log($"[EnemyObj] State Transition: {transition.from} -> {transition.to}");
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("플레이어 발견!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("플레이어 놓침!");
        }
    }

    void Update()
    {
        if (_fsm == null)
        {
            return;
        }

        _fsm.OnLogic();
        if (_stateText != null)
        {
            _stateText.text = _fsm.GetActiveHierarchyPath();
        }
    }

    void OnDestroy()
    {
        _fsm?.OnExit();
        _fsm = null;
    }


    // private void Update()
    // {
    //     if (target == null)
    //         return;
    //     var dir = target.position - transform.position;
    //     dir.y = 0;
    //     transform.rotation = Quaternion.LookRotation(dir);

    //     float distanceToTarget = Vector3.Distance(transform.position, target.position);

    //     if (distanceToTarget <= stopDistance)
    //     {
    //         enemyAgent.ResetPath();
    //         return;
    //     }

    //     destinationUpdateTimer -= Time.deltaTime;
    //     if (destinationUpdateTimer <= 0f)
    //     {
    //         destinationUpdateTimer = destinationUpdateInterval;
    //         SetDestivation();
    //     }
    // }

    // private void SetDestivation()
    // {
    //     Vector3 separationOffset = CalculateSeparationOffset();
    //     Vector3 desiredDestination = target.position + separationOffset;

    //     if (NavMesh.SamplePosition(desiredDestination, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
    //     {
    //         enemyAgent.SetDestination(hit.position);
    //     }
    //     else
    //     {
    //         enemyAgent.SetDestination(target.position);
    //     }
    // }

    // private Vector3 CalculateSeparationOffset()
    // {
    //     Vector3 separation = Vector3.zero;

    //     Collider[] hits = Physics.OverlapSphere(transform.position, separationRadius, enemyLayer);

    //     foreach (Collider hit in hits)
    //     {
    //         if (hit.gameObject == gameObject) continue;

    //         Vector3 diff = transform.position - hit.transform.position;
    //         diff.y = 0f;

    //         float dist = diff.magnitude;
    //         if (dist < 0.001f) continue;

    //         float weight = 1f - (dist / separationRadius);
    //         separation += diff.normalized * weight;
    //     }

    //     separation.y = 0f;

    //     if (separation.sqrMagnitude > 0.001f)
    //     {
    //         separation = separation.normalized * separationStrength;
    //     }

    //     return separation;
    // }
}
