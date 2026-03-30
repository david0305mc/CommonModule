using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInputManager : MonoBehaviour
{
    public static GameInputManager Instance { get; private set; }
    public static bool HasInstance => Instance != null;

    private GameInputActions inputActions;
    private Vector2 hardwareMoveValue;
    private Vector2 virtualMoveValue;
    private FloatingJoystick registeredMoveJoystick;

    public Vector2 MoveValue => CanReadPlayerInput ? ResolveMoveValue() : Vector2.zero;
    public Vector2 HardwareMoveValue => CanReadPlayerInput ? hardwareMoveValue : Vector2.zero;
    public Vector2 VirtualMoveValue => CanReadPlayerInput ? virtualMoveValue : Vector2.zero;
    public Vector2 LookValue { get; private set; }

    public event Action JumpPressed;
    public event Action AttackPressed;

    private bool CanReadPlayerInput => inputActions != null && inputActions.Player.enabled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (inputActions == null)
        {
            inputActions = new GameInputActions();
        }
    }

    private void OnEnable()
    {
        if (inputActions == null)
        {
            inputActions = new GameInputActions();
        }

        inputActions.Player.Enable();
        SyncVirtualMoveInput();

        inputActions.Player.Move.performed += OnMove;
        inputActions.Player.Move.canceled += OnMoveCanceled;

        inputActions.Player.Look.performed += OnLook;
        inputActions.Player.Look.canceled += OnLookCanceled;

        inputActions.Player.Jump.performed += OnJump;
        inputActions.Player.Attack.performed += OnAttack;
    }

    private void OnDisable()
    {
        if (inputActions == null)
        {
            return;
        }

        inputActions.Player.Move.performed -= OnMove;
        inputActions.Player.Move.canceled -= OnMoveCanceled;

        inputActions.Player.Look.performed -= OnLook;
        inputActions.Player.Look.canceled -= OnLookCanceled;

        inputActions.Player.Jump.performed -= OnJump;
        inputActions.Player.Attack.performed -= OnAttack;

        inputActions.Player.Disable();
        inputActions.UI.Disable();

        ResetCachedInput();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        inputActions?.Dispose();
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        hardwareMoveValue = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        hardwareMoveValue = Vector2.zero;
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

        SyncVirtualMoveInput();
        LookValue = Vector2.zero;
    }

    public void SwitchToUI()
    {
        inputActions.Player.Disable();
        inputActions.UI.Enable();

        hardwareMoveValue = Vector2.zero;
        LookValue = Vector2.zero;
    }

    public void RegisterVirtualJoystick(FloatingJoystick joystick)
    {
        if (joystick == null)
        {
            return;
        }

        if (registeredMoveJoystick != null && registeredMoveJoystick != joystick)
        {
            return;
        }

        registeredMoveJoystick = joystick;
        virtualMoveValue = Vector2.ClampMagnitude(joystick.InputVector, 1f);
    }

    public void UnregisterVirtualJoystick(FloatingJoystick joystick)
    {
        if (registeredMoveJoystick != joystick)
        {
            return;
        }

        registeredMoveJoystick = null;
        virtualMoveValue = Vector2.zero;
    }

    public void SetVirtualMoveInput(FloatingJoystick joystick, Vector2 input)
    {
        if (joystick == null)
        {
            return;
        }

        if (registeredMoveJoystick != null && registeredMoveJoystick != joystick)
        {
            return;
        }

        registeredMoveJoystick = joystick;
        virtualMoveValue = Vector2.ClampMagnitude(input, 1f);
    }

    private Vector2 ResolveMoveValue()
    {
        if (virtualMoveValue.sqrMagnitude > 0f)
        {
            return virtualMoveValue;
        }

        return hardwareMoveValue;
    }

    private void SyncVirtualMoveInput()
    {
        if (registeredMoveJoystick == null)
        {
            virtualMoveValue = Vector2.zero;
            return;
        }

        virtualMoveValue = Vector2.ClampMagnitude(registeredMoveJoystick.InputVector, 1f);
    }

    private void ResetCachedInput()
    {
        hardwareMoveValue = Vector2.zero;
        virtualMoveValue = Vector2.zero;
        LookValue = Vector2.zero;
    }
}
