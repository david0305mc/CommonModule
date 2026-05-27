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
    [SerializeField] private Animator animator;
    private const float _attackRange = 1.5f;
    private const float _attackExitRange = 2.4f;
    private const float _fightKeepDistance = 1f;
    private const float _searchRange = 3f;
    private const string _zombieIdleAnimation = "ZombieIdle";
    private const string _zombieAttackAnimation = "ZombieAttack";
    private float _minDistance = 0.5f;
    [SerializeField, Min(0f)] private float _moveSpeed = 2f;

    private Transform[] patrolPoints;
    private Transform target;
    private NavMeshAgent enemyAgent;

    private StateMachine _fsm;
    private float DistanceToTarget => target == null ? 0 : Vector3.Distance(transform.position, target.position);

    private void Start()
    {
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
        }, beforeOnEnter: s =>
        {
            enemyAgent.ResetPath();
            enemyAgent.velocity = Vector3.zero;
            enemyAgent.isStopped = true;
        }, afterOnExit: s =>
        {
            if(enemyAgent.isStopped)
            {
                enemyAgent.isStopped = false;
            }
        });
        fightFsm.AddState(nameof(FightState.Wait), new UniTaskState(async ct =>
        {
            animator.Play(_zombieIdleAnimation);
            while(!ct.IsCancellationRequested)
            {
                FacePositionImmediately(target.position);
                await UniTask.Yield(cancellationToken:ct);
            }
        }));
        fightFsm.AddState(nameof(FightState.Telegraph), new UniTaskState(async ct =>
        {
            while(!ct.IsCancellationRequested)
            {
                FacePositionImmediately(target.position);
                await UniTask.Yield(cancellationToken:ct);
            }
        }));
        fightFsm.AddState(nameof(FightState.Attack), new UniTaskState(async ct =>
        {
            animator.Play(_zombieAttackAnimation, 0, 0f);
            await WaitForAnimationEndAsync(_zombieAttackAnimation, ct);
            fightFsm.RequestStateChange(nameof(FightState.Wait));
        }));
        fightFsm.AddExitTransition(nameof(FightState.Wait));
        fightFsm.AddTransition(new TransitionAfter(nameof(FightState.Wait), nameof(FightState.Telegraph), 0.1f));
        fightFsm.AddTransition(new TransitionAfter(nameof(FightState.Telegraph), nameof(FightState.Attack), 0.2f));

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
            animator.Play("ZombieWalk");
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

    private async UniTask WaitForAnimationEndAsync(string stateName, CancellationToken ct)
    {
        int stateHash = Animator.StringToHash(stateName);
        await UniTask.Yield(cancellationToken: ct);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool isTargetState = stateInfo.shortNameHash == stateHash || stateInfo.IsName(stateName);
            if (isTargetState && stateInfo.normalizedTime >= 1f && !animator.IsInTransition(0))
            {
                break;
            }

            await UniTask.Yield(cancellationToken: ct);
        }
    }

    private async UniTask PatrolState(CancellationToken ct)
    {
        animator.Play("ZombieWalk");
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
        animator.Play("ZombieRun");
        while (!ct.IsCancellationRequested)
        {
            MoveToward(target.position, 0f);
            await UniTask.Yield(ct);
        }
    }
    private void MoveToward(Vector3 targetPos, float minDist)
    {
        FacePositionImmediately(targetPos);
        enemyAgent.stoppingDistance = minDist;
        if (Vector3.Distance(transform.position, targetPos) <= minDist)
        {
            enemyAgent.ResetPath();
            return;
        }

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
}
