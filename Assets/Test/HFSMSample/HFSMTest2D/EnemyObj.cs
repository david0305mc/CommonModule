
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityHFSM;

namespace HFSMTest2D
{
    public class EnemyObj : MonoBehaviour
    {
        [SerializeField] private Text stateText;
        [SerializeField] private List<Transform> patrolPoints;
        private Transform _target;

        private StateMachine fsm;
        void Start()
        {
            _target = PlayerObj.Instance.transform;
            fsm = new StateMachine();
            fsm.AddState("Patrol", new UniTaskState(
                onEnterAsync:
                async ct =>
                {
                    while (!ct.IsCancellationRequested)
                    {
                        Patrol();
                        await UniTask.Yield(cancellationToken: ct);
                    }
                },
                externalCancellationToken: this.GetCancellationTokenOnDestroy()));
            fsm.AddState("Fight");
            fsm.AddState("Chase");
            fsm.AddState("Search");

            fsm.SetStartState("Patrol");
            fsm.Init();
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
            // var dist = _target.position - transform.position;
            // if (dist.magnitude > 1f)
            // {

            //     transform.position += dist * Time.deltaTime;      
            // }

        }

        void OnDestroy()
        {
            fsm?.OnExit();
            fsm = null;
        }

    }

}
