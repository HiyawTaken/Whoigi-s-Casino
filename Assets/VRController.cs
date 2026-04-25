using UnityEngine;
using UnityEngine.XR;

public class VRController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float boostMultiplier = 2f;

    [Header("Sprint")]
    public XRNode sprintInputSource = XRNode.LeftHand;
    public float sprintMultiplier = 1.75f;
    public bool sprintWithThumbstickClick = true;
    public bool sprintWithPrimaryButtonFallback = true;

    [Header("VR References")]
    public XRNode inputSource = XRNode.LeftHand;
    public XRNode turnInputSource = XRNode.RightHand;
    public Transform headTransform;

    [Header("Turning")]
    public bool enableControllerTurning = true;
    public bool useSmoothTurn;
    public float snapTurnAngle = 45f;
    public float smoothTurnSpeed = 90f;
    [Range(0.1f, 1f)]
    public float turnDeadzone = 0.65f;

    [Header("Gravity and Jumping")]
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;

    private float normalSpeed;
    private Vector3 velocity;
    private bool isGrounded;
    private bool canSnapTurn = true;

    // Score
    private int score = 0;

    private CharacterController controller;
    private InputDevice device;
    private InputDevice turnDevice;
    private InputDevice sprintDevice;

    void Awake()
    {
        ApplyRuntimeDefaults();
    }

    void OnValidate()
    {
        ApplyRuntimeDefaults();
    }

    void Start()
    {
        ApplyRuntimeDefaults();
        controller = GetComponent<CharacterController>();
        normalSpeed = moveSpeed;

        if (headTransform == null && Camera.main != null)
            headTransform = Camera.main.transform;

        device = InputDevices.GetDeviceAtXRNode(inputSource);
        turnDevice = InputDevices.GetDeviceAtXRNode(turnInputSource);
        sprintDevice = InputDevices.GetDeviceAtXRNode(sprintInputSource);
    }

    void Update()
    {
        if (!device.isValid)
            device = InputDevices.GetDeviceAtXRNode(inputSource);

        if (!turnDevice.isValid)
            turnDevice = InputDevices.GetDeviceAtXRNode(turnInputSource);

        if (!sprintDevice.isValid)
            sprintDevice = InputDevices.GetDeviceAtXRNode(sprintInputSource);

        if (PauseMenu.GameIsPaused)
        {
            canSnapTurn = true;
            return;
        }

        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        HandleMovement();
        HandleTurning();
        HandleJump();

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleMovement()
    {
        Vector2 inputAxis;
        if (!device.TryGetFeatureValue(CommonUsages.primary2DAxis, out inputAxis))
            return;

        Transform movementReference = headTransform != null ? headTransform : transform;
        Vector3 moveDirection = movementReference.forward * inputAxis.y +
                                movementReference.right * inputAxis.x;

        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        float currentSpeed = IsSprinting(inputAxis) ? moveSpeed * sprintMultiplier : moveSpeed;
        controller.Move(moveDirection * currentSpeed * Time.deltaTime);
    }

    private bool IsSprinting(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude < 0.01f || !sprintDevice.isValid)
            return false;

        if (sprintWithThumbstickClick &&
            sprintDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool stickClicked) &&
            stickClicked)
        {
            return true;
        }

        return sprintWithPrimaryButtonFallback &&
               sprintDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryButton) &&
               primaryButton;
    }

    private void ApplyRuntimeDefaults()
    {
        bool hadMissingTurnDefaults = snapTurnAngle <= 0f || smoothTurnSpeed <= 0f || turnDeadzone <= 0f;

        if (moveSpeed <= 0f)
            moveSpeed = 3f;

        if (boostMultiplier <= 0f)
            boostMultiplier = 2f;

        inputSource = XRNode.LeftHand;
        sprintInputSource = XRNode.LeftHand;
        turnInputSource = XRNode.RightHand;

        if (sprintMultiplier <= 1f)
            sprintMultiplier = 1.75f;

        if (!sprintWithThumbstickClick && !sprintWithPrimaryButtonFallback)
        {
            sprintWithThumbstickClick = true;
            sprintWithPrimaryButtonFallback = true;
        }

        if (snapTurnAngle <= 0f)
            snapTurnAngle = 45f;

        if (smoothTurnSpeed <= 0f)
            smoothTurnSpeed = 90f;

        if (turnDeadzone <= 0f || turnDeadzone > 1f)
            turnDeadzone = 0.65f;

        if (hadMissingTurnDefaults)
            enableControllerTurning = true;

        if (gravity == 0f)
            gravity = -9.81f;

        if (jumpHeight <= 0f)
            jumpHeight = 1.5f;
    }

    private bool IsControllerHand(XRNode node)
    {
        return node == XRNode.LeftHand || node == XRNode.RightHand;
    }

    private void HandleTurning()
    {
        if (!enableControllerTurning || !turnDevice.isValid)
            return;

        Vector2 turnAxis;
        if (!turnDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out turnAxis))
            return;

        float horizontal = turnAxis.x;
        if (Mathf.Abs(horizontal) < turnDeadzone)
        {
            canSnapTurn = true;
            return;
        }

        if (useSmoothTurn)
        {
            RotateRig(horizontal * smoothTurnSpeed * Time.deltaTime);
            return;
        }

        if (!canSnapTurn)
            return;

        RotateRig(Mathf.Sign(horizontal) * snapTurnAngle);
        canSnapTurn = false;
    }

    private void RotateRig(float yawDegrees)
    {
        if (Mathf.Approximately(yawDegrees, 0f))
            return;

        if (headTransform != null)
            transform.RotateAround(headTransform.position, Vector3.up, yawDegrees);
        else
            transform.Rotate(0f, yawDegrees, 0f);
    }

    private void HandleJump()
    {
        bool triggerPressed;
        if (turnDevice.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed))
        {
            if (triggerPressed && isGrounded)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Collectible"))
        {
            score++;
            Debug.Log("Collected! Score: " + score);
            Destroy(other.gameObject);

            if (score >= 10)
            {
                Debug.Log("YOU WIN!");
            }
        }

        if (other.gameObject.CompareTag("SpeedZone"))
        {
            moveSpeed = normalSpeed * boostMultiplier;
            Debug.Log("Speed boost!");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("SpeedZone"))
        {
            moveSpeed = normalSpeed;
        }
    }
}
