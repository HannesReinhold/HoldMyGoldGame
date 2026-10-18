using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private InputSystem inputActions;

    private CharacterController controller;

    [Header("Gewicht")]
    public NetworkedPlayer networkedPlayer;
    public PlayerUpgradeStats playerUpgradeStats;
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
    [SerializeField] private BodyWallBlocker wallBlocker;

    private float xRotation = 0f;

    // Movement Vars
    public Vector3 velocity;
    public float gravity = -9.81f;
    public bool grounded;

    public bool movementEnabled;
    public bool playerEnabled;

    private bool inputBlocked;

    public float Pitch => xRotation;

    public float slowdown = 1;

    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
    }

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
        if (!playerEnabled || inputBlocked) return;

        Vector2 looking = GetPlayerLook();
        float lookX = looking.x * lookSensitivity * Time.deltaTime;
        float lookY = looking.y * lookSensitivity * Time.deltaTime;

        float before = xRotation;
        xRotation -= lookY;

        float maxDown = GetMaxLookDown();
        if (xRotation > maxDown)
        {
            // Limit ist enger geworden (mehr Gold): Blick sanft hochdrücken statt ruckartig springen
            // Spieler schiebt aktiv in das Limit: einfach am Limit stoppen
            float limited = before > maxDown
                ? Mathf.MoveTowards(before, maxDown, limitPullSpeed * Time.deltaTime)
                : maxDown;

            xRotation = Mathf.Min(xRotation, limited);
        }

        xRotation = Mathf.Max(xRotation, -maxLookUp);

        cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * lookX);
    }

    private void DoMovement()
    {
        if (!movementEnabled) return;

        grounded = controller.isGrounded;
        if (grounded && velocity.y < 0f)
            velocity.y = -2f;

        Vector2 input = inputBlocked ? Vector2.zero : GetPlayerMovement();
        float goldRatio = (float)networkedPlayer.GoldBarCount
                  / maxItemsForSlowdown;
        float t = Mathf.Clamp01(
            goldRatio / playerUpgradeStats.WidthMultiplier
        );

        // Springen (Taste gehalten = automatisch weiter springen)
        bool jumpHeld = !inputBlocked && inputActions.FPSController.Jump.IsPressed();
        if (grounded && jumpHeld)
        {
            float processedJumpHeight = slowdownCurve.Evaluate(t) * jumpHeight * playerUpgradeStats.JumpingHeightMultiplier;
            velocity.y = Mathf.Sqrt(-2f * gravity * processedJumpHeight);
            networkedPlayer.PlayJumpSound();
        }

        velocity.y += gravity * Time.deltaTime;


        float processedMovementSpeed = slowdownCurve.Evaluate(t)*movementSpeed * playerUpgradeStats.RunningSpeedMultiplier;
        slowdown = slowdownCurve.Evaluate(t);
        Vector3 move = (transform.right * input.x + transform.forward * input.y) * processedMovementSpeed;

        // Ein einziger Move: Laufen + Knockback + Vertikal
        Vector3 total = move + knockback + Vector3.up * velocity.y;
        controller.Move(total * Time.deltaTime);
        wallBlocker.Resolve();

        float decel = grounded ? knockbackDecelGround : knockbackDecelAir;
        knockback = Vector3.MoveTowards(knockback, Vector3.zero, decel * Time.deltaTime);
    }

    public void AddKnockback(Vector3 impulse)
    {
        float t = Mathf.Clamp01((float)(networkedPlayer.GoldBarCount / maxItemsForSlowdown)/ playerUpgradeStats.WidthMultiplier);
        float factor = slowdownCurve.Evaluate(t);
        impulse *= factor;
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

    [Header("Blickwinkel (Gold im Weg)")]
    [SerializeField] private float maxLookUp = 90f;
    [SerializeField] private float maxLookDownFree = 90f;      // wenig Gold: voll nach unten
    [SerializeField] private float maxLookDownBlocked = 30f;   // viel Gold: nur noch so weit
    [SerializeField] private int goldFreeUntil = 10;           // bis hierhin keine Einschränkung
    [SerializeField] private int goldBlockedFrom = 30;         // ab hier gilt der Mindestwinkel
    [SerializeField] private float limitPullSpeed = 120f;      // Grad/s, mit der der Blick hochgedrückt wird

    private float GetMaxLookDown()
    {
        if (networkedPlayer == null) return maxLookDownFree;

        // 0 bis goldFreeUntil -> 0, goldBlockedFrom und mehr -> 1, dazwischen linear
        float t = Mathf.InverseLerp(goldFreeUntil, goldBlockedFrom, networkedPlayer.GoldBarCount);
        return Mathf.Lerp(maxLookDownFree, maxLookDownBlocked, t);
    }
}