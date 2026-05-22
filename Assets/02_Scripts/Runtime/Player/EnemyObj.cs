using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityHFSM;

[RequireComponent(typeof(CharacterController))]
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

    private const float _attackRange = 2f;
    private const float _attackExitRange = 2.4f;
    private const float _fightKeepDistance = 1f;
    private const float _searchRange = 7f;
    private float _minDistance = 0.5f;
    private float _moveSpeed = 3f;


    private Transform target;
    private NavMeshAgent enemyAgent;
    private Animator animator;
    private StateMachine _fsm;
    private float DistanceToTarget => target == null ? 0 : Vector3.Distance(transform.position, target.position);

    private void Start()
    {
        animator = GetComponent<Animator>();
        enemyAgent = GetComponent<NavMeshAgent>();
        enemyAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        target = WorldRoot.Instance.PlayerObj.transform;
        InitFsm();
    }

    private void InitFsm()
    {
        HybridStateMachine fightFsm = new HybridStateMachine(needsExitTime: true, beforeOnLogic: s =>
        {
            MoveToward(target.position);
        });
        fightFsm.AddState(nameof(FightState.Wait), new UniTaskState(async ct =>
        {
            animator.Play("GuardIdle");
        }));
        fightFsm.AddState(nameof(FightState.Telegraph), new UniTaskState(async ct =>
        {
            animator.Play("GuardTelegraph");
        }));
        fightFsm.AddState(nameof(FightState.Attack), new UniTaskState(async ct =>
        {
            animator.Play("GuardHit");
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
        }, onLogic: state =>
        {
            state.fsm.StateCanExit();
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
        while (!ct.IsCancellationRequested)
        {
            MoveToward(target.position);
            await UniTask.Yield(ct);
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

    private void MoveToward(Vector3 _targetPos)
    {
        enemyAgent.SetDestination(_targetPos);
    }

    private void LogTransition(TransitionBase<string> transition)
    {
        Debug.Log($"[EnemyObj] State Transition: {transition.from} -> {transition.to}");
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
