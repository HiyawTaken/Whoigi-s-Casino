using UnityEngine;

/// <summary>
/// Optional settings for objects picked up by ControllerGrabber.
/// Objects with a Rigidbody and Collider can be grabbed without this component.
/// </summary>
[DisallowMultipleComponent]
public class ControllerGrabbable : MonoBehaviour
{
    [Tooltip("Disable this to make this Rigidbody ignored by controller grabbing.")]
    public bool canBeGrabbed = true;

    [Tooltip("Allow grabbing even if this Rigidbody is currently kinematic.")]
    public bool allowKinematicGrab;

    [Tooltip("Ignore the grabber's mass limit for this object.")]
    public bool ignoreMassLimit;

    [Tooltip("Snap the object to the hand attach point when grabbed. Otherwise the object keeps its current offset.")]
    public bool snapToHand;

    [Tooltip("Leave this object in the scene hierarchy while grabbed. Useful for mounted controls like levers.")]
    public bool keepInPlaceWhenGrabbed;

    [Tooltip("Do not change Rigidbody gravity/kinematic settings while grabbed.")]
    public bool keepPhysicsWhileGrabbed;

    [Tooltip("Apply hand throw velocity when released.")]
    public bool applyThrowOnRelease = true;

    [Tooltip("Optional prompt text shown when a hand is close to this grabbable.")]
    public string promptOverride;

    [Tooltip("Optional per-object grab radius. Use this for mounted controls that should be reachable farther away.")]
    public float grabRadiusOverride;

    [Tooltip("Optional per-object prompt radius.")]
    public float promptRadiusOverride;

    [Tooltip("Optional per-object downward search distance.")]
    public float downwardReachOverride;

    [Tooltip("Velocity multiplier applied when the object is released.")]
    public float releaseVelocityScale = 1f;

    [Tooltip("Angular velocity multiplier applied when the object is released.")]
    public float releaseAngularVelocityScale = 1f;

    public virtual void OnGrabbed(ControllerGrabber grabber)
    {
    }

    public virtual void OnReleased(ControllerGrabber grabber)
    {
    }
}
