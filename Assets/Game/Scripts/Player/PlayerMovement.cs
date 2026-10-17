using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private InputSystem inputActions;

    private CharacterController controller;

    [SerializeField] private Camera cam;
    [SerializeField] private float movementSpeed = 2.0f;
    [SerializeField] public float lookSensitivity = 1.0f;

    private float xRotation = 0f;

    // Movement Vars
    private Vector3 velocity;
    public float gravity = -9.81f;
    private bool grounded;

    public bool movementEnabled;
    public bool playerEnabled;

    private void Awake()
    {
        inputActions = new InputSystem();
        inputActions.FPSController.PauseMenu.performed += ctx => PauseGame();
        inputActions.FPSController.Jump.performed += ctx => Jump();
    }
    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void Update()
    {
        
        DoMovement();
        DoLooking();
    }

    private void DoLooking()
    {
        if (!playerEnabled) return;

        Vector2 looking = GetPlayerLook();
        float lookX = looking.x * lookSensitivity * Time.deltaTime;
        float lookY = looking.y * lookSensitivity * Time.deltaTime;

        xRotation -= lookY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * lookX);
    }

    private void DoMovement()
    {
        if (!movementEnabled) return;

        grounded = controller.isGrounded;
        if (grounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        Vector2 movement = GetPlayerMovement();
        Vector3 move = transform.right * movement.x + transform.forward * movement.y;
        controller.Move(move * movementSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void Jump()
    {
        if (grounded)
        {
            velocity.y = Mathf.Sqrt(-2f * gravity);
        }
    }

    private void PauseGame()
    {
        //GameManager.Instance.PauseGame();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    public Vector2 GetPlayerMovement()
    {
        return inputActions.FPSController.Move.ReadValue<Vector2>();
    }

    public Vector2 GetPlayerLook()
    {
        return inputActions.FPSController.Look.ReadValue<Vector2>();
    }

    public void SetEnableMovement(bool enabled)
    {
        movementEnabled = enabled;
    }

    public void SetEnablePlayer(bool enabled)
    {
        playerEnabled = enabled;

        if(enabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}