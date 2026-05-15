
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using UnityHFSM;

namespace HFSMTest2D
{
    public class EnemyObj : MonoBehaviour
    {
        [SerializeField] private Text stateText;
        [SerializeField] private List<Transform> patrolPoints;

        private float _moveSpeed = 3f;
        private float _minDistance = 1f;
        private Transform _target;

        private StateMachine fsm;
        void Start()
        {
            _target = PlayerObj.Instance.transform;
            fsm = new StateMachine();
            fsm.AddState("Patrol", new UniTaskState(
                onEnterAsync: PatrolState,
                externalCancellationToken: this.GetCancellationTokenOnDestroy()));
            fsm.AddState("Fight", new UniTaskState(
                onEnterAsync: async ct =>
                {
                    while (!ct.IsCancellationRequested)
                    {
                        await UniTask.Yield(cancellationToken: ct);
                    }

                }, externalCancellationToken: this.GetCancellationTokenOnDestroy()));
            fsm.AddState("Chase");
            fsm.AddState("Search");

            fsm.SetStartState("Patrol");
            fsm.Init();
        }

        private void MoveToward(Vector3 targetPos, float minDist)
        {
            var dist = Vector3.Distance(transform.position, targetPos);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, Math.Max(0, Math.Min(_moveSpeed * Time.deltaTime, dist - minDist)));
        }

        private async UniTask PatrolState(CancellationToken ct)
        {
            int currPatrolIndex = 0;
            var targetPatrolPoint = patrolPoints[currPatrolIndex];
            while (!ct.IsCancellationRequested)
            {
                MoveToward(targetPatrolPoint.position, _minDistance);


                await UniTask.Yield(cancellationToken: ct);
            }
        }

        private void Patrol()
        {
            if (_target == null)
            {
                return;
            }

            var dist = _target.position - transform.position;
            if (dist.magnitude > 1f)
            {
                transform.position += dist * Time.deltaTime;
            }
        }

        private void MoveToward(Vector3 targetPos)
        {
            var dist = targetPos - transform.position;
            if (dist.magnitude > 1f)
            {
                transform.position += dist * Time.deltaTime;
            }
        }

        void Update()
        {
            if (fsm == null)
            {
                return;
            }

            fsm.OnLogic();
            if (stateText != null)
            {
                stateText.text = fsm.GetActiveHierarchyPath();
            }
        }

        void OnDestroy()
        {
            fsm?.OnExit();
            fsm = null;
        }


        private int FindClosestPatrolPoint()
        {
            float minDistance = Vector2.Distance(transform.position, patrolPoints[0].position);
            int minIndex = 0;

            for (int i = 1; i < patrolPoints.Count; i++)
            {
                float distance = Vector2.Distance(transform.position, patrolPoints[i].position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    minIndex = i;
                }
            }

            return minIndex;
        }

    }

}
