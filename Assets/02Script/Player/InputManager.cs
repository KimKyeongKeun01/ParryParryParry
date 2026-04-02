using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [SerializeField] private InputAction inputAction;
    private static InputManager instance;
    public static InputManager Instance
    {
        get { return instance; }
        private set { instance = value; }
    }
    Player player;

    public Action<float> moveAction;
    public Action<float> verticalMoveAction;
    public Action<bool> jumpAction;
    public Action dashAction;
    public Action<bool> throwAction;
    public Action<bool> guardAction;
    public Action<bool> slamAction;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        player = Player.Instance;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 dir = context.ReadValue<Vector2>();
        moveAction?.Invoke(dir.x);
        verticalMoveAction?.Invoke(dir.y);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            jumpAction?.Invoke(true);
        }

        if (context.canceled)
        {
            jumpAction?.Invoke(false);
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            dashAction?.Invoke();
        }
    }
    
    public void OnThrow(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            throwAction?.Invoke(true);
        }
        if (context.canceled)
        {
            throwAction?.Invoke(false);
        }
    }

    public void OnGuard(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            guardAction?.Invoke(true);
        }
        if (context.canceled)
        {
            guardAction?.Invoke(false);
        }
    }

    public void OnSlam(InputAction.CallbackContext context)
    {
        if (context.started) slamAction?.Invoke(true);
        if (context.canceled) slamAction?.Invoke(false);
    }

    public void OnRestart(InputAction.CallbackContext context)
    {
        if (context.started)
            GameManager.Instance.OnGameOver();
    }
}
