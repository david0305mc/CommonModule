using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInputManager : MonoBehaviour
{
    public static GameInputManager Instance { get; private set; }

    private GameInputActions inputActions;

    public Vector2 MoveValue { get; private set; }
    public Vector2 LookValue { get; private set; }

    public event Action JumpPressed;
    public event Action AttackPressed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        inputActions = new GameInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();

        inputActions.Player.Move.performed += OnMove;
        inputActions.Player.Move.canceled += OnMoveCanceled;

        inputActions.Player.Look.performed += OnLook;
        inputActions.Player.Look.canceled += OnLookCanceled;

        inputActions.Player.Jump.performed += OnJump;
        inputActions.Player.Attack.performed += OnAttack;
    }

    private void OnDisable()
    {
        inputActions.Player.Move.performed -= OnMove;
        inputActions.Player.Move.canceled -= OnMoveCanceled;

        inputActions.Player.Look.performed -= OnLook;
        inputActions.Player.Look.canceled -= OnLookCanceled;

        inputActions.Player.Jump.performed -= OnJump;
        inputActions.Player.Attack.performed -= OnAttack;

        inputActions.Player.Disable();
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        MoveValue = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        MoveValue = Vector2.zero;
    }

    private void OnLook(InputAction.CallbackContext ctx)
    {
        LookValue = ctx.ReadValue<Vector2>();
    }

    private void OnLookCanceled(InputAction.CallbackContext ctx)
    {
        LookValue = Vector2.zero;
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        JumpPressed?.Invoke();
    }

    private void OnAttack(InputAction.CallbackContext ctx)
    {
        AttackPressed?.Invoke();
    }

    public void SwitchToGameplay()
    {
        inputActions.UI.Disable();
        inputActions.Player.Enable();
    }

    public void SwitchToUI()
    {
        inputActions.Player.Disable();
        inputActions.UI.Enable();
    }
}