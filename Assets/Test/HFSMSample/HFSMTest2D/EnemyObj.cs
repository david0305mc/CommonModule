using UnityEngine;

namespace HFSMTest2D
{
    public class EnemyObj : MonoBehaviour
    {
        private Transform _target;

        void Start()
        {
            _target = PlayerObj.Instance.transform;
        }

        void Update()
        {

            var dist = _target.position - transform.position;
            if (dist.magnitude > 1f)
            {

                transform.position += dist * Time.deltaTime;      
            }
            
        }

    }

}
