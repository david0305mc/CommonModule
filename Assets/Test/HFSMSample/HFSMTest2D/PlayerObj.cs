using UnityEngine;

namespace HFSMTest2D
{
    public class PlayerObj : MonoBehaviour
    {
        public static PlayerObj Instance { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1);
            input *= 5;

            transform.position += (Vector3)(input * Time.deltaTime);
        }
    }

}
