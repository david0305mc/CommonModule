
using UnityEngine;

public class JoystickPlayer : MonoBehaviour
{


    [SerializeField] private FloatingJoystick joystick;
    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        Vector2 input = joystick.InputVector;
        transform.Translate(new Vector3(input.x, input.y, 0f) * moveSpeed * Time.deltaTime);
    }

    public void OnJump()
    {
        Debug.Log("Jump!");
    }
}
