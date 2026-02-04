using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Simple grabbable object setup for XR Interaction Toolkit.
/// Attach this to objects that need to be picked up and moved.
///
/// LEARNING OBJECTIVES:
/// - XR interaction basics
/// - Component dependencies
/// - Physics in VR
/// - Automatic component setup
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class XRGrabbable : MonoBehaviour
{
    [Header("Grab Settings")]
    [Tooltip("Can the object be grabbed with both hands?")]
    public bool allowTwoHandedGrab = false;

    [Tooltip("Smooth movement when releasing (recommended for VR)")]
    public bool smoothRelease = true;

    [Header("Physics Settings")]
    [Tooltip("Mass of the object (affects how it feels to hold)")]
    public float mass = 1f;

    [Tooltip("Air resistance (0 = no resistance, higher = more resistance)")]
    public float drag = 0.5f;

    [Tooltip("Rotational resistance")]
    public float angularDrag = 0.5f;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    void Awake()
    {
        SetupComponents();
    }

    /// <summary>
    /// Automatically configures required components
    /// </summary>
    void SetupComponents()
    {
        // Setup Rigidbody
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = drag;
        rb.angularDamping = angularDrag;
        rb.useGravity = true;
        rb.isKinematic = false;

        // Setup XR Grab Interactable
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        // Configure grab settings
        grabInteractable.movementType = smoothRelease ?
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking :
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;

        // Throw settings for realistic physics
        grabInteractable.throwOnDetach = true;
        grabInteractable.throwSmoothingDuration = 0.25f;
        grabInteractable.throwVelocityScale = 1.5f;

        // IMPORTANT: Make sure the object has a Collider!
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"{gameObject.name} needs a Collider to be grabbable! Adding BoxCollider...");
            gameObject.AddComponent<BoxCollider>();
        }
    }

    /// <summary>
    /// Optional: Visual feedback when object is grabbed
    /// Students can enhance this with haptics, sounds, or visual effects
    /// </summary>
    public void OnGrabbed()
    {
        Debug.Log($"{gameObject.name} was grabbed");

        // OPTIONAL ENHANCEMENTS:
        // - Trigger haptic feedback
        // - Play grab sound
        // - Change object color/material
        // - Show outline or highlight
    }

    /// <summary>
    /// Optional: Feedback when object is released
    /// </summary>
    public void OnReleased()
    {
        Debug.Log($"{gameObject.name} was released");

        // OPTIONAL ENHANCEMENTS:
        // - Play release sound
        // - Restore original color/material
        // - Remove outline
    }

    /// <summary>
    /// Editor helper - shows grab radius
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.1f);
    }

    // Wire up events (optional - students can use these for feedback)
    void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }
    }

    void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        OnGrabbed();
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        OnReleased();
    }
}
