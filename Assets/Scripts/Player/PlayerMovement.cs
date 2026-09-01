using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles submarine body movement and rotation.
/// Fully compatible with the new Unity Input System package.
///
/// Control scheme (mobile & desktop):
///   CAMERA   - drag anywhere on the drag-zone to rotate the camera (yaw + pitch).
///              Camera rotation is handled by SubmarineCamera.cs.
///   JOYSTICK - Left/Right  : strafes the body along its own right vector (no camera turn).
///              Up/Down     : moves the body in the camera-forward direction, including pitch.
///   KEYBOARD - WASD / Arrow keys for movement, E for Interact, F for Scan.
///   BODY TURN - The body's yaw gradually slerps to match the camera's yaw.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Submarine Speed & Physics")]
    [SerializeField] private float moveSpeed      = 8f;
    [Tooltip("How quickly the submarine body rotates to match the camera yaw.")]
    [SerializeField] private float bodyTurnSpeed  = 3f;

    [Header("Input Asset Reference")]
    [SerializeField] private InputActionAsset inputActionsAsset;

    [Header("References")]
    [SerializeField] private TouchDragZone    dragZone;
    [SerializeField] private SubmarineCamera  submarineCamera;

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------

    private InputAction  moveAction;
    private InputAction  interactAction;
    private InputAction  scanAction;
    private Rigidbody    rb;
    private Vector2      moveInput;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (inputActionsAsset != null)
        {
            moveAction     = inputActionsAsset.FindAction("Move");
            interactAction = inputActionsAsset.FindAction("Interact");
            scanAction     = inputActionsAsset.FindAction("Scan");
        }
        else if (InputSystem.actions != null)
        {
            moveAction     = InputSystem.actions.FindAction("Move");
            interactAction = InputSystem.actions.FindAction("Interact");
            scanAction     = InputSystem.actions.FindAction("Scan");
        }
    }

    private void Start()
    {
        if (submarineCamera == null)
            submarineCamera = GetComponentInChildren<SubmarineCamera>();

        if (PlayerPrefs.GetInt("Save_HasPosition", 0) == 1)
        {
            float x = PlayerPrefs.GetFloat("Save_PosX", 0f);
            float y = PlayerPrefs.GetFloat("Save_PosY", 0f);
            float z = PlayerPrefs.GetFloat("Save_PosZ", 0f);
            float rotY = PlayerPrefs.GetFloat("Save_RotY", 0f);

            transform.position = new Vector3(x, y, z);
            transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            if (rb != null)
            {
                rb.position = new Vector3(x, y, z);
                rb.linearVelocity = Vector3.zero;
            }

            if (submarineCamera != null)
            {
                submarineCamera.SetYaw(rotY);
                submarineCamera.SnapYawToBody();
            }

            Debug.Log($"[PlayerMovement] Restored saved submarine position: ({x:F1}, {y:F1}, {z:F1}), rotY: {rotY:F1}");
        }
    }

    private void OnEnable()
    {
        if (inputActionsAsset != null)
        {
            var map = inputActionsAsset.FindActionMap("Player");
            if (map != null) map.Enable();
        }
        else if (InputSystem.actions != null)
        {
            var map = InputSystem.actions.FindActionMap("Player");
            if (map != null) map.Enable();
        }
    }

    private void OnDisable()
    {
        if (inputActionsAsset != null)
        {
            var map = inputActionsAsset.FindActionMap("Player");
            if (map != null) map.Disable();
        }
    }

    private void Update()
    {
        if (moveAction != null)
            moveInput = moveAction.ReadValue<Vector2>();

        // Keyboard fallback using the new Input System
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (moveInput == Vector2.zero)
            {
                float h = 0f;
                float v = 0f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;

                if (h != 0f || v != 0f)
                    moveInput = new Vector2(h, v).normalized;
            }

            if (kb.eKey.wasPressedThisFrame) Interact();
            if (kb.fKey.wasPressedThisFrame) Scan();
        }

        if (interactAction != null && interactAction.WasPressedThisFrame()) Interact();
        if (scanAction != null && scanAction.WasPressedThisFrame())         Scan();
    }

    private void FixedUpdate()
    {
        MoveSubmarine();
        TurnBodyToCamera();
    }

    // -----------------------------------------------------------------------
    // Movement
    // -----------------------------------------------------------------------

    public float GetEngineSpeedMultiplier()
    {
        if (GameManager.Instance == null) return 1f;
        return GameManager.Instance.EngineTier switch
        {
            1 => 1.00f,
            2 => 1.20f,
            3 => 1.40f,
            4 => 1.65f,
            5 => 2.00f,
            _ => 1.00f
        };
    }

    private void MoveSubmarine()
    {
        Vector3 camForward = submarineCamera != null
            ? submarineCamera.transform.forward
            : transform.forward;

        Vector3 bodyRight = transform.right;

        float effectiveSpeed = moveSpeed * GetEngineSpeedMultiplier();

        Vector3 targetVelocity =
            (camForward  * moveInput.y +
             bodyRight   * moveInput.x) * effectiveSpeed;

        rb.AddForce(targetVelocity - rb.linearVelocity, ForceMode.VelocityChange);
    }

    private void TurnBodyToCamera()
    {
        if (submarineCamera == null) return;

        Quaternion targetBodyRotation = Quaternion.Euler(0f, submarineCamera.CameraYaw, 0f);

        rb.MoveRotation(Quaternion.Slerp(
            rb.rotation,
            targetBodyRotation,
            bodyTurnSpeed * Time.fixedDeltaTime));
    }

    // -----------------------------------------------------------------------
    // Actions
    // -----------------------------------------------------------------------

    public void Interact()
    {
        var scanner = ScannerSystem.Instance;
        if (scanner != null)
        {
            scanner.TryInteract();
        }
        else
        {
            Debug.LogWarning("[PlayerMovement] ScannerSystem not found when pressing Interact.");
        }
    }

    public void Scan()
    {
        var scanner = ScannerSystem.Instance;
        if (scanner != null)
        {
            scanner.TryScan();
        }
        else
        {
            Debug.LogWarning("[PlayerMovement] ScannerSystem not found when pressing Scan.");
        }
    }
}