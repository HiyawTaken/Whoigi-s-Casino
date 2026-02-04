using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class VRController : MonoBehaviour
{
    // Movement
    public float moveSpeed = 3f;
    private float normalSpeed;
    public float boostMultiplier = 2f;

    // VR References
    public XRNode inputSource = XRNode.LeftHand;
    public Transform headTransform;

    // Gravity and jumping
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;
    private Vector3 velocity;
    private bool isGrounded;

    // Score
    private int score = 0;

    private CharacterController controller;
    private InputDevice device;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        normalSpeed = moveSpeed;

        // Find the device (left controller)
        device = InputDevices.GetDeviceAtXRNode(inputSource);
    }

    void Update()
    {
        // Make sure we have the device
        if (!device.isValid)
        {
            device = InputDevices.GetDeviceAtXRNode(inputSource);
        }

        // Ground check
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Get joystick input from VR controller
        Vector2 inputAxis;
        if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out inputAxis))
        {
            // Calculate movement direction based on head direction
            Vector3 moveDirection = headTransform.forward * inputAxis.y +
                                    headTransform.right * inputAxis.x;

            // Keep movement horizontal (don't move up/down with head tilt)
            moveDirection.y = 0;

            // Move the player
            controller.Move(moveDirection * moveSpeed * Time.deltaTime);
        }

        // Jump with right trigger
        bool triggerPressed;
        InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightDevice.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed))
        {
            if (triggerPressed && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
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
