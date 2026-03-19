using UnityEngine;
using UnityEngine.InputSystem;

public class JoystickPlayer : MonoBehaviour
{
    private InputAction moveAction;

    void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        moveAction.performed += OnMove;
        moveAction.Enable();
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        Debug.Log(ctx.ReadValue<Vector2>());
    }
}
