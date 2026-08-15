using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles submarine body movement and rotation.
///
/// Control scheme (mobile):
///   CAMERA   — drag anywhere on the drag-zone to rotate the camera (yaw + pitch).
///              Camera rotation is handled by SubmarineCamera.cs.
///   JOYSTICK — Left/Right  : strafes the body along its own right vector (no camera turn).
///              Up/Down     : moves the body in the camera-forward direction, including pitch
///                            (so if the camera looks down, the sub dives forward-and-down).
///   BODY TURN — The body's yaw gradually slerps to match the camera's yaw so the submarine
///               always ends up facing where the player is looking.
///               Body pitch stays at 0 (the camera provides the visual pitch illusion).
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

        // Lock rotation so physics doesn't fight our manual rotation
        rb.freezeRotation = true;

        moveAction     = InputSystem.actions.FindAction("Move");
        interactAction = InputSystem.actions.FindAction("Interact");
        scanAction     = InputSystem.actions.FindAction("Scan");
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
        moveInput = moveAction.ReadValue<Vector2>();

        if (interactAction.WasPressedThisFrame()) Interact();
        if (scanAction.WasPressedThisFrame())     Scan();
    }

    private void FixedUpdate()
    {
        MoveSubmarine();
        TurnBodyToCamera();
    }

    // -----------------------------------------------------------------------
    // Movement
    // -----------------------------------------------------------------------

    /// <summary>
    /// Moves the submarine camera-relatively:
    ///   - Forward/Backward: along the camera's full forward vector (includes pitch).
    ///     This lets the sub naturally dive or ascend when the camera looks up/down.
    ///   - Left/Right: strafe along the body's own right axis (no camera rotation needed).
    /// </summary>
    private void MoveSubmarine()
    {
        // Camera's full forward direction (includes pitch tilt)
        Vector3 camForward = submarineCamera != null
            ? submarineCamera.transform.forward
            : transform.forward;

        // Strafe uses the body's right so the sub doesn't roll when strafing sideways
        Vector3 bodyRight = transform.right;

        Vector3 targetVelocity =
            (camForward  * moveInput.y +
             bodyRight   * moveInput.x) * moveSpeed;

        // VelocityChange gives instant, physics-friendly velocity matching
        rb.AddForce(targetVelocity - rb.linearVelocity, ForceMode.VelocityChange);
    }

    // -----------------------------------------------------------------------
    // Body rotation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gradually rotates the submarine body's yaw to match the camera's yaw.
    /// Body pitch is kept at 0 — the camera provides the visual pitch impression.
    /// </summary>
    private void TurnBodyToCamera()
    {
        if (submarineCamera == null) return;

        // Build a flat (no pitch) target quaternion from the camera's world yaw
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
        Debug.Log("Interacted!");
    }

    public void Scan()
    {
        Debug.Log("Scanned!");
    }
}