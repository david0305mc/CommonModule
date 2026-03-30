using UnityEngine;

public class PlayerMovementTest : MonoBehaviour
{
    [SerializeField] private FloatingJoystick joystick;
    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        Vector2 input = Vector2.zero;

        if (GameInputManager.HasInstance)
        {
            input = GameInputManager.Instance.MoveValue;
        }
        else if (joystick != null)
        {
            // Keep the sandbox scene usable even if the input manager is not present.
            input = joystick.InputVector;
        }

        Vector3 movement = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
        transform.Translate(movement * moveSpeed * Time.deltaTime);
    }

    public void OnJump()
    {
        Debug.Log("Jump!");
    }
}
