using System.Collections.Generic;
using UnityEngine;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRNode = UnityEngine.XR.XRNode;

/// <summary>
/// Grabs nearby Rigidbody objects using Quest/Meta controller grip input.
/// </summary>
[DisallowMultipleComponent]
public class ControllerGrabber : MonoBehaviour
{
    private const int MaxCandidates = 64;
    private static readonly Collider[] s_CandidateColliders = new Collider[MaxCandidates];
    private static readonly Dictionary<Rigidbody, ControllerGrabber> s_HeldBodies = new Dictionary<Rigidbody, ControllerGrabber>();

    [Header("Controller")]
    public XRNode inputSource = XRNode.RightHand;

    [Tooltip("Grip value required to start grabbing.")]
    [Range(0f, 1f)]
    public float grabThreshold = 0.55f;

    [Tooltip("Grip value below this releases the held object.")]
    [Range(0f, 1f)]
    public float releaseThreshold = 0.35f;

    [Tooltip("Use the trigger as a fallback grab input if the grip value is unavailable.")]
    public bool useTriggerAsFallback = true;

    [Header("Reach")]
    [Tooltip("Local-space offset from this hand root to the palm/attach point.")]
    public Vector3 grabPointLocalOffset = new Vector3(0f, 0f, 0.055f);

    [Tooltip("How far from the palm an object can be picked up.")]
    public float grabRadius = 0.75f;

    [Tooltip("Allow grip to pick up floor objects below the hand without changing player height.")]
    public bool useFloorGrabAssist = true;

    [Tooltip("Horizontal reach used by Floor Grab Assist.")]
    public float floorGrabRadius = 1.5f;

    [Tooltip("How far downward from the hand Floor Grab Assist can search.")]
    public float floorGrabDownwardReach = 4f;

    [Tooltip("Broad search radius used only for grabbables with per-object reach overrides.")]
    public float extendedGrabbableSearchRadius = 5f;

    [Tooltip("Broad downward search used only for grabbables with per-object reach overrides.")]
    public float extendedGrabbableDownwardReach = 6f;

    [Tooltip("Layers that can be grabbed.")]
    public LayerMask grabbableLayers = ~0;

    [Tooltip("If true, any Rigidbody in reach can be grabbed. If false, only objects with ControllerGrabbable can be grabbed.")]
    public bool grabAnyRigidbody = true;

    [Tooltip("If enabled, the object, hit collider, or ControllerGrabbable object must have Grabbable Tag.")]
    public bool requireGrabbableTag = true;

    [Tooltip("Tag accepted when Require Grabbable Tag is enabled.")]
    public string grabbableTag = "Grabbable";

    [Tooltip("Rigidbody mass limit for unmarked objects. Set to 0 or less for no limit.")]
    public float maxGrabMass = 20f;

    [Header("Prompt")]
    [Tooltip("Show a floating instruction when this hand is close enough to grab something.")]
    public bool showGrabPrompt = true;

    [Tooltip("Text shown above nearby grabbable objects.")]
    public string promptText = "Press Grip to pick up";

    [Tooltip("Horizontal distance used for showing the prompt.")]
    public float promptRadius = 2f;

    [Tooltip("How far downward from the hand the prompt can search for floor objects.")]
    public float promptDownwardReach = 4f;

    [Tooltip("How far above the object's bounds the prompt appears.")]
    public float promptHeight = 0.12f;

    public Color promptColor = Color.white;
    public Color promptShadowColor = new Color(0f, 0f, 0f, 0.8f);
    public float promptCharacterSize = 0.035f;

    [Header("Throw")]
    [Tooltip("Multiplier for hand velocity applied to released objects.")]
    public float throwVelocityScale = 1f;

    [Tooltip("Multiplier for hand angular velocity applied to released objects.")]
    public float throwAngularVelocityScale = 1f;

    public Rigidbody HeldRigidbody => m_HeldRigidbody;
    public bool IsHolding => m_HeldRigidbody != null;

    private XRInputDevice m_Device;
    private Rigidbody m_HeldRigidbody;
    private ControllerGrabbable m_HeldSettings;
    private Transform m_PreviousParent;
    private XRNode m_CurrentDeviceNode;
    private bool m_PreviousUseGravity;
    private bool m_PreviousIsKinematic;
    private RigidbodyInterpolation m_PreviousInterpolation;
    private CollisionDetectionMode m_PreviousCollisionDetectionMode;
    private bool m_ModifiedHeldPhysics;
    private bool m_ReparentedHeldBody;
    private bool m_WasGripPressed;
    private Transform m_PromptRoot;
    private TextMesh m_PromptText;
    private TextMesh m_PromptShadow;
    private Vector3 m_LastGrabPointPosition;
    private Quaternion m_LastGrabPointRotation;
    private Vector3 m_GrabPointVelocity;
    private Vector3 m_GrabPointAngularVelocity;

    private void Awake()
    {
        ApplyRuntimeDefaults();
    }

    private void OnValidate()
    {
        ApplyRuntimeDefaults();
    }

    private void OnEnable()
    {
        ApplyRuntimeDefaults();
        RefreshDevice();
        m_LastGrabPointPosition = GrabPointPosition;
        m_LastGrabPointRotation = GrabPointRotation;
    }

    private void OnDisable()
    {
        ReleaseHeldObject();
        SetPromptVisible(false);
    }

    private void OnDestroy()
    {
        if (m_PromptRoot != null)
            Destroy(m_PromptRoot.gameObject);
    }

    private void Update()
    {
        if (!m_Device.isValid || m_CurrentDeviceNode != inputSource)
            RefreshDevice();

        UpdateGrabPointVelocity();

        float grip = ReadGripValue();
        bool gripPressed = grip >= grabThreshold || (m_WasGripPressed && grip > releaseThreshold);

        if (!m_WasGripPressed && gripPressed)
            TryGrabNearestObject();
        else if (m_WasGripPressed && !gripPressed)
            ReleaseHeldObject();

        UpdateGrabPrompt();
        m_WasGripPressed = gripPressed;
    }

    private void RefreshDevice()
    {
        m_Device = XRInputDevices.GetDeviceAtXRNode(inputSource);
        m_CurrentDeviceNode = inputSource;
    }

    public void SetInputSource(XRNode node)
    {
        if (inputSource == node && m_CurrentDeviceNode == node && m_Device.isValid)
            return;

        inputSource = node;
        RefreshDevice();
    }

    private void ApplyRuntimeDefaults()
    {
        if (grabThreshold <= 0f)
            grabThreshold = 0.55f;

        if (releaseThreshold <= 0f || releaseThreshold >= grabThreshold)
            releaseThreshold = 0.35f;

        if (grabRadius < 0.75f)
            grabRadius = 0.75f;

        if (floorGrabRadius < 1.5f)
            floorGrabRadius = 1.5f;

        if (floorGrabDownwardReach < 4f)
            floorGrabDownwardReach = 4f;

        if (extendedGrabbableSearchRadius < floorGrabRadius)
            extendedGrabbableSearchRadius = 5f;

        if (extendedGrabbableDownwardReach < floorGrabDownwardReach)
            extendedGrabbableDownwardReach = 6f;

        useFloorGrabAssist = true;
        showGrabPrompt = true;
        requireGrabbableTag = true;

        if (promptRadius < 2f)
            promptRadius = 2f;

        if (promptDownwardReach < 4f)
            promptDownwardReach = 4f;

        if (promptCharacterSize <= 0f)
            promptCharacterSize = 0.035f;

        if (string.IsNullOrEmpty(promptText))
            promptText = "Press Grip to pick up";

        if (string.IsNullOrEmpty(grabbableTag))
            grabbableTag = "Grabbable";

        if (maxGrabMass < 0f)
            maxGrabMass = 0f;
    }

    private float ReadGripValue()
    {
        if (!m_Device.isValid)
            return 0f;

        if (m_Device.TryGetFeatureValue(XRCommonUsages.grip, out float grip))
            return grip;

        if (m_Device.TryGetFeatureValue(XRCommonUsages.gripButton, out bool gripButton))
            return gripButton ? 1f : 0f;

        if (!useTriggerAsFallback)
            return 0f;

        if (m_Device.TryGetFeatureValue(XRCommonUsages.trigger, out float trigger))
            return trigger;

        if (m_Device.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool triggerButton))
            return triggerButton ? 1f : 0f;

        return 0f;
    }

    private void TryGrabNearestObject()
    {
        if (m_HeldRigidbody != null)
            return;

        Rigidbody nearest = FindNearestCandidate(grabRadius, 0f, out ControllerGrabbable settings);
        if (nearest == null && useFloorGrabAssist)
            nearest = FindNearestCandidate(floorGrabRadius, floorGrabDownwardReach, out settings);
        if (nearest == null)
            nearest = FindNearestCandidate(extendedGrabbableSearchRadius, extendedGrabbableDownwardReach, out settings, true, false);

        if (nearest == null)
            return;

        GrabObject(nearest, settings);
    }

    private Rigidbody FindNearestCandidate(float radius, float downwardReach, out ControllerGrabbable settings, bool requireReachOverride = false, bool promptSearch = false)
    {
        settings = null;

        Vector3 center = GrabPointPosition;
        Vector3 searchEnd = center + Vector3.down * Mathf.Max(0f, downwardReach);
        int count = downwardReach > 0f
            ? Physics.OverlapCapsuleNonAlloc(center, searchEnd, radius, s_CandidateColliders, grabbableLayers, QueryTriggerInteraction.Ignore)
            : Physics.OverlapSphereNonAlloc(center, radius, s_CandidateColliders, grabbableLayers, QueryTriggerInteraction.Ignore);
        Rigidbody nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider candidateCollider = s_CandidateColliders[i];
            s_CandidateColliders[i] = null;

            if (candidateCollider == null)
                continue;

            Rigidbody body = candidateCollider.attachedRigidbody;
            if (body == null || body == m_HeldRigidbody)
                continue;

            if (transform.IsChildOf(body.transform) || body.transform.IsChildOf(transform))
                continue;

            if (s_HeldBodies.TryGetValue(body, out ControllerGrabber holder) && holder != this)
                continue;

            ControllerGrabbable candidateSettings = body.GetComponent<ControllerGrabbable>();
            if (candidateSettings == null)
                candidateSettings = body.GetComponentInParent<ControllerGrabbable>();

            if (requireReachOverride && !HasReachOverride(candidateSettings, promptSearch))
                continue;

            if (!IsCandidateGrabbable(body, candidateCollider, candidateSettings))
                continue;

            float candidateRadius = GetCandidateRadius(radius, candidateSettings, promptSearch);
            float candidateDownwardReach = GetCandidateDownwardReach(downwardReach, candidateSettings);
            Vector3 candidateSearchEnd = center + Vector3.down * Mathf.Max(0f, candidateDownwardReach);
            Vector3 referencePoint = candidateDownwardReach > 0f
                ? ClosestPointOnSegment(center, candidateSearchEnd, candidateCollider.bounds.center)
                : center;
            Vector3 closestPoint = candidateCollider.ClosestPoint(referencePoint);
            float sqrDistance = (closestPoint - referencePoint).sqrMagnitude;
            if (sqrDistance > candidateRadius * candidateRadius)
                continue;

            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearest = body;
            settings = candidateSettings;
            nearestSqrDistance = sqrDistance;
        }

        return nearest;
    }

    private bool HasReachOverride(ControllerGrabbable settings, bool promptSearch)
    {
        if (settings == null)
            return false;

        float radiusOverride = promptSearch ? settings.promptRadiusOverride : settings.grabRadiusOverride;
        return radiusOverride > 0f || settings.downwardReachOverride > 0f;
    }

    private float GetCandidateRadius(float fallbackRadius, ControllerGrabbable settings, bool promptSearch)
    {
        if (settings == null)
            return fallbackRadius;

        float overrideRadius = promptSearch ? settings.promptRadiusOverride : settings.grabRadiusOverride;
        return overrideRadius > 0f ? overrideRadius : fallbackRadius;
    }

    private float GetCandidateDownwardReach(float fallbackReach, ControllerGrabbable settings)
    {
        return settings != null && settings.downwardReachOverride > 0f
            ? settings.downwardReachOverride
            : fallbackReach;
    }

    private Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 segment = end - start;
        float lengthSqr = segment.sqrMagnitude;
        if (lengthSqr <= 0.0001f)
            return start;

        float t = Vector3.Dot(point - start, segment) / lengthSqr;
        return start + segment * Mathf.Clamp01(t);
    }

    private bool IsCandidateGrabbable(Rigidbody body, Collider candidateCollider, ControllerGrabbable settings)
    {
        if (settings != null && !settings.canBeGrabbed)
            return false;

        if (!grabAnyRigidbody && settings == null)
            return false;

        if (requireGrabbableTag && !HasRequiredTag(body, candidateCollider, settings))
            return false;

        if (body.isKinematic && (settings == null || !settings.allowKinematicGrab))
            return false;

        if (maxGrabMass > 0f && body.mass > maxGrabMass && (settings == null || !settings.ignoreMassLimit))
            return false;

        return true;
    }

    private void UpdateGrabPrompt()
    {
        if (!showGrabPrompt || m_HeldRigidbody != null)
        {
            SetPromptVisible(false);
            return;
        }

        Rigidbody nearest = FindNearestCandidate(Mathf.Max(promptRadius, grabRadius), promptDownwardReach, out ControllerGrabbable settings, false, true);
        if (nearest == null)
            nearest = FindNearestCandidate(extendedGrabbableSearchRadius, extendedGrabbableDownwardReach, out settings, true, true);

        if (nearest == null)
        {
            SetPromptVisible(false);
            return;
        }

        string prompt = settings != null && !string.IsNullOrEmpty(settings.promptOverride)
            ? settings.promptOverride
            : promptText;

        EnsurePrompt();
        m_PromptRoot.position = GetPromptPosition(nearest);
        FacePromptTowardCamera();
        m_PromptText.text = prompt;
        m_PromptShadow.text = prompt;
        m_PromptText.color = promptColor;
        m_PromptShadow.color = promptShadowColor;
        m_PromptText.characterSize = promptCharacterSize;
        m_PromptShadow.characterSize = promptCharacterSize;
        SetPromptVisible(true);
    }

    private void EnsurePrompt()
    {
        if (m_PromptRoot != null)
            return;

        GameObject root = new GameObject("Grab Prompt");
        root.hideFlags = HideFlags.DontSave;
        m_PromptRoot = root.transform;

        m_PromptText = CreatePromptText(root.transform, "Prompt Text", Vector3.zero, promptColor);
        m_PromptShadow = CreatePromptText(root.transform, "Prompt Shadow", new Vector3(0.006f, -0.006f, 0.002f), promptShadowColor);
    }

    private TextMesh CreatePromptText(Transform parent, string objectName, Vector3 localOffset, Color color)
    {
        var textObject = new GameObject(objectName);
        textObject.hideFlags = HideFlags.DontSave;
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localOffset;
        textObject.transform.localRotation = Quaternion.identity;
        textObject.transform.localScale = Vector3.one;

        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.text = promptText;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = promptCharacterSize;
        textMesh.fontSize = 64;
        textMesh.color = color;

        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sortingOrder = 100;

        return textMesh;
    }

    private void SetPromptVisible(bool visible)
    {
        if (m_PromptRoot != null && m_PromptRoot.gameObject.activeSelf != visible)
            m_PromptRoot.gameObject.SetActive(visible);
    }

    private Vector3 GetPromptPosition(Rigidbody body)
    {
        Bounds bounds;
        if (TryGetObjectBounds(body, out bounds))
            return bounds.center + Vector3.up * (bounds.extents.y + promptHeight);

        return body.position + Vector3.up * promptHeight;
    }

    private bool TryGetObjectBounds(Rigidbody body, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = default;

        Collider[] colliders = body.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].isTrigger)
                continue;

            if (!hasBounds)
                bounds = colliders[i].bounds;
            else
                bounds.Encapsulate(colliders[i].bounds);

            hasBounds = true;
        }

        if (hasBounds)
            return true;

        Renderer[] renderers = body.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!hasBounds)
                bounds = renderers[i].bounds;
            else
                bounds.Encapsulate(renderers[i].bounds);

            hasBounds = true;
        }

        return hasBounds;
    }

    private void FacePromptTowardCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 toCamera = m_PromptRoot.position - camera.transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
            m_PromptRoot.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }

    private bool HasRequiredTag(Rigidbody body, Collider candidateCollider, ControllerGrabbable settings)
    {
        if (string.IsNullOrEmpty(grabbableTag))
            return true;

        return body.CompareTag(grabbableTag) ||
               candidateCollider.CompareTag(grabbableTag) ||
               (settings != null && settings.CompareTag(grabbableTag));
    }

    private void GrabObject(Rigidbody body, ControllerGrabbable settings)
    {
        m_HeldRigidbody = body;
        m_HeldSettings = settings;
        m_PreviousParent = body.transform.parent;
        m_PreviousUseGravity = body.useGravity;
        m_PreviousIsKinematic = body.isKinematic;
        m_PreviousInterpolation = body.interpolation;
        m_PreviousCollisionDetectionMode = body.collisionDetectionMode;
        m_ModifiedHeldPhysics = settings == null || !settings.keepPhysicsWhileGrabbed;
        m_ReparentedHeldBody = settings == null || !settings.keepInPlaceWhenGrabbed;

        s_HeldBodies[body] = this;

        if (m_ModifiedHeldPhysics)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        if (m_ReparentedHeldBody)
        {
            Transform targetParent = transform;
            if (settings != null && settings.snapToHand)
            {
                body.transform.SetParent(targetParent, false);
                body.transform.localPosition = grabPointLocalOffset;
                body.transform.localRotation = Quaternion.identity;
            }
            else
            {
                body.transform.SetParent(targetParent, true);
            }
        }

        settings?.OnGrabbed(this);
    }

    private void ReleaseHeldObject()
    {
        if (m_HeldRigidbody == null)
            return;

        Rigidbody released = m_HeldRigidbody;
        ControllerGrabbable settings = m_HeldSettings;

        if (m_ReparentedHeldBody)
            released.transform.SetParent(m_PreviousParent, true);

        if (m_ModifiedHeldPhysics)
        {
            released.useGravity = m_PreviousUseGravity;
            released.isKinematic = m_PreviousIsKinematic;
            released.interpolation = m_PreviousInterpolation;
            released.collisionDetectionMode = m_PreviousCollisionDetectionMode;
        }

        float velocityScale = throwVelocityScale * (settings != null ? settings.releaseVelocityScale : 1f);
        float angularVelocityScale = throwAngularVelocityScale * (settings != null ? settings.releaseAngularVelocityScale : 1f);
        bool applyThrow = settings == null || settings.applyThrowOnRelease;

        if (applyThrow && !released.isKinematic)
        {
            released.linearVelocity = m_GrabPointVelocity * velocityScale;
            released.angularVelocity = m_GrabPointAngularVelocity * angularVelocityScale;
        }

        s_HeldBodies.Remove(released);
        settings?.OnReleased(this);

        m_HeldRigidbody = null;
        m_HeldSettings = null;
        m_PreviousParent = null;
        m_ModifiedHeldPhysics = false;
        m_ReparentedHeldBody = false;
    }

    private void UpdateGrabPointVelocity()
    {
        Vector3 currentPosition = GrabPointPosition;
        Quaternion currentRotation = GrabPointRotation;
        float deltaTime = Time.deltaTime;

        if (deltaTime > 0f)
        {
            m_GrabPointVelocity = (currentPosition - m_LastGrabPointPosition) / deltaTime;
            m_GrabPointAngularVelocity = CalculateAngularVelocity(m_LastGrabPointRotation, currentRotation, deltaTime);
        }

        m_LastGrabPointPosition = currentPosition;
        m_LastGrabPointRotation = currentRotation;
    }

    private Vector3 CalculateAngularVelocity(Quaternion from, Quaternion to, float deltaTime)
    {
        Quaternion delta = to * Quaternion.Inverse(from);
        delta.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f)
            angle -= 360f;

        if (axis == Vector3.zero || float.IsNaN(axis.x))
            return Vector3.zero;

        return axis.normalized * (angle * Mathf.Deg2Rad / deltaTime);
    }

    private Vector3 GrabPointPosition => transform.TransformPoint(grabPointLocalOffset);
    private Quaternion GrabPointRotation => transform.rotation;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = m_HeldRigidbody != null ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(GrabPointPosition, grabRadius);

        if (showGrabPrompt && promptDownwardReach > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(GrabPointPosition, GrabPointPosition + Vector3.down * promptDownwardReach);
        }
    }
}
