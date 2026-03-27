using UnityEngine;

public class PlayerMovementTest : MonoBehaviour
{
    

    [SerializeField] private FloatingJoystick joystick;
    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        Vector2 input = joystick.InputVector;
        transform.Translate(new Vector3(input.x, 0f, input.y) * moveSpeed * Time.deltaTime);
    }

    public void OnJump()
    {
        Debug.Log("Jump!");
    }
}
