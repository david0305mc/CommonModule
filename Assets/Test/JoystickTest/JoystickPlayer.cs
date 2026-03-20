
using UnityEngine;

public class JoystickPlayer : MonoBehaviour
{

    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        Vector2 move = GameInputManager.Instance.MoveValue;

        Vector3 dir = new Vector3(move.x, 0f, move.y);
        transform.position += dir * moveSpeed * Time.deltaTime;
    }
    void OnEnable()
    {
        GameInputManager.Instance.JumpPressed += OnJump;
    }

    void OnDisable()
    {
        GameInputManager.Instance.JumpPressed -= OnJump;
    }

    public void OnJump()
    {
        Debug.Log("Jump!");
    }
}
