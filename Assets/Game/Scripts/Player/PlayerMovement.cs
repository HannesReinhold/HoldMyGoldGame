using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private InputSystem inputActions;

    private CharacterController controller;

    [Header("Gewicht")]
    public NetworkedPlayer networkedPlayer;
    [SerializeField] private int maxItemsForSlowdown = 10;   // ab hier ist die Kurve am Ende
    [SerializeField] private AnimationCurve slowdownCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [SerializeField] private Camera cam;
    [SerializeField] private float movementSpeed = 2.0f;
    [SerializeField] public float lookSensitivity = 1.0f;
    private Vector3 knockback;

    [Header("Knockback")]
    public float knockbackDecelGround = 20f;
    public float knockbackDecelAir = 4f;     // in der Luft weniger Reibung

    [SerializeField] private float jumpHeight = 1.5f;

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
        if (grounded && velocity.y < 0f)
            velocity.y = -2f;

        Vector2 input = GetPlayerMovement();
        float t = Mathf.Clamp01((float)networkedPlayer.GoldBarCount / maxItemsForSlowdown);

        // Springen (Taste gehalten = automatisch weiter springen)
        if (grounded && inputActions.FPSController.Jump.IsPressed())
        {
            float processedJumpHeight = slowdownCurve.Evaluate(t) * jumpHeight;
            velocity.y = Mathf.Sqrt(-2f * gravity * processedJumpHeight);
        }

        velocity.y += gravity * Time.deltaTime;


        float processedMovementSpeed = slowdownCurve.Evaluate(t)*movementSpeed;
        Vector3 move = (transform.right * input.x + transform.forward * input.y) * processedMovementSpeed;

        // Ein einziger Move: Laufen + Knockback + Vertikal
        Vector3 total = move + knockback + Vector3.up * velocity.y;
        controller.Move(total * Time.deltaTime);

        float decel = grounded ? knockbackDecelGround : knockbackDecelAir;
        knockback = Vector3.MoveTowards(knockback, Vector3.zero, decel * Time.deltaTime);
    }

    private void Jump()
    {
        if (grounded)
        {
            velocity.y = Mathf.Sqrt(-3f * gravity);
        }
    }

    public void AddKnockback(Vector3 impulse)
    {
        knockback = new Vector3(impulse.x, 0f, impulse.z);
        velocity.y = impulse.y;   // nach oben wegschleudern
    }

    public void ResetMotion()
    {
        knockback = Vector3.zero;
        velocity = Vector3.zero;
    }

    public void ResetLook()
    {
        xRotation = 0f;
        cam.transform.localRotation = Quaternion.identity;
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

    public void SetEnablePlayer(bool enabled, bool isOwner)
    {
        playerEnabled = enabled;


        if (!isOwner) return;
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