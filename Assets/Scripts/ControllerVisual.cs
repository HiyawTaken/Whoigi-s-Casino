using System.Collections.Generic;
using UnityEngine;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRInputFeatureUsageBool = UnityEngine.XR.InputFeatureUsage<bool>;
using XRNode = UnityEngine.XR.XRNode;

/// <summary>
/// Drives a hand mesh from a tracked XR controller pose and controller inputs.
/// This is for controller-held Meta/Quest hands, not optical hand tracking.
/// </summary>
[DisallowMultipleComponent]
public class ControllerVisual : MonoBehaviour
{
    [Header("Controller")]
    [Tooltip("When enabled this visual reads the left controller. Otherwise it reads the right controller.")]
    public bool isLeftHand;

    [Tooltip("The XR node to read. This is synced from Is Left Hand by Reset/OnValidate and by the scene installer.")]
    public XRNode inputSource = XRNode.RightHand;

    [Tooltip("Set this to false when this object is already parented under a tracked controller object.")]
    public bool followControllerPose = true;

    [Tooltip("Local-space offset from the controller grip pose to the visible wrist/hand root.")]
    public Vector3 positionOffset = new Vector3(0f, -0.015f, 0.055f);

    [Tooltip("Local-space rotation offset from the controller grip pose to the visible wrist/hand root.")]
    public Vector3 rotationOffset = new Vector3(35f, 0f, 0f);

    [Tooltip("Hide the hand when the matching controller is not tracked.")]
    public bool hideWhenControllerNotTracked = true;

    [Header("Hand Model")]
    [Tooltip("Optional hand FBX/prefab. The installer assigns Assets/Models/LeftHand.fbx or RightHand.fbx.")]
    public GameObject handModelPrefab;

    [Tooltip("Existing child model root. If blank, Hand Model Prefab is instantiated at runtime.")]
    public Transform modelRoot;

    public Vector3 modelLocalPosition = Vector3.zero;
    public Vector3 modelLocalEulerAngles = Vector3.zero;
    public Vector3 modelLocalScale = Vector3.one;

    [Tooltip("Build a simple primitive hand if a skinned hand model cannot be found.")]
    public bool buildPrimitiveFallback;

    public Color skinColor = new Color(0.96f, 0.80f, 0.69f, 1f);

    [Header("Grabbing")]
    [Tooltip("Add a grip-based ControllerGrabber to this hand at runtime if one is missing.")]
    public bool addGrabber = true;

    [Tooltip("When enabled, only objects tagged with Grabbable can be picked up.")]
    public bool requireGrabbableTag = true;

    [Tooltip("Tag accepted when Require Grabbable Tag is enabled.")]
    public string grabbableTag = "Grabbable";

    [Header("Finger Curl")]
    [Range(0f, 1f)]
    public float openCurl = 0f;

    [Tooltip("Curl added to the index finger by the trigger.")]
    public float triggerCurlScale = 1f;

    [Tooltip("Curl added to middle, ring, and pinky by the grip.")]
    public float gripCurlScale = 1f;

    [Tooltip("Extra thumb curl when thumb controls are touched or pressed.")]
    [Range(0f, 1f)]
    public float thumbTouchCurl = 0.35f;

    public Vector3 proximalCurlEuler = new Vector3(-70f, 0f, 0f);
    public Vector3 intermediateCurlEuler = new Vector3(-80f, 0f, 0f);
    public Vector3 distalCurlEuler = new Vector3(-55f, 0f, 0f);
    public Vector3 thumbProximalCurlEuler = new Vector3(-35f, 25f, -20f);
    public Vector3 thumbIntermediateCurlEuler = new Vector3(-55f, 5f, 0f);
    public Vector3 thumbDistalCurlEuler = new Vector3(-45f, 0f, 0f);

    private readonly List<FingerRig> m_Fingers = new List<FingerRig>();
    private readonly List<Renderer> m_Renderers = new List<Renderer>();
    private XRInputDevice m_Device;
    private Material m_RuntimeMaterial;
    private bool m_ModelReady;

    private enum FingerKind
    {
        Thumb,
        Index,
        Middle,
        Ring,
        Pinky,
    }

    private sealed class FingerRig
    {
        public FingerKind kind;
        public Transform proximal;
        public Transform intermediate;
        public Transform distal;
        public Quaternion proximalRest;
        public Quaternion intermediateRest;
        public Quaternion distalRest;
    }

    private void Reset()
    {
        SyncNodeFromHandedness();
    }

    private void OnValidate()
    {
        SyncNodeFromHandedness();

        if (modelLocalScale == Vector3.zero)
            modelLocalScale = Vector3.one;
    }

    private void Awake()
    {
        SyncNodeFromHandedness();
        EnsureGrabber();
        EnsureModel();
        CacheHandRig();
        RefreshRenderers();
    }

    private void OnEnable()
    {
        SyncNodeFromHandedness();
        EnsureGrabber();
        EnsureModel();
        CacheHandRig();
        RefreshRenderers();
    }

    private void Update()
    {
        if (!m_ModelReady)
        {
            EnsureModel();
            CacheHandRig();
            RefreshRenderers();
        }

        if (!m_Device.isValid)
            m_Device = XRInputDevices.GetDeviceAtXRNode(inputSource);

        bool hasPose = TryApplyControllerPose();
        SetRenderersVisible(!hideWhenControllerNotTracked || hasPose);

        ReadCurl(out float trigger, out float grip, out bool thumbTouched);
        AnimateHand(trigger, grip, thumbTouched);
    }

    private void OnDestroy()
    {
        if (m_RuntimeMaterial != null)
            Destroy(m_RuntimeMaterial);
    }

    private void SyncNodeFromHandedness()
    {
        inputSource = isLeftHand ? XRNode.LeftHand : XRNode.RightHand;
    }

    private void EnsureGrabber()
    {
        if (!addGrabber)
            return;

        ControllerGrabber grabber = GetComponent<ControllerGrabber>();
        if (grabber == null)
        {
            grabber = gameObject.AddComponent<ControllerGrabber>();
            grabber.grabPointLocalOffset = new Vector3(0f, 0f, 0.055f);
            grabber.grabRadius = 0.75f;
            grabber.floorGrabRadius = 1.5f;
            grabber.floorGrabDownwardReach = 4f;
            grabber.grabThreshold = 0.55f;
            grabber.releaseThreshold = 0.35f;
            grabber.grabAnyRigidbody = true;
            grabber.requireGrabbableTag = true;
            grabber.grabbableTag = grabbableTag;
            grabber.maxGrabMass = 20f;
        }

        grabber.SetInputSource(inputSource);
        requireGrabbableTag = true;
        grabber.requireGrabbableTag = true;
        grabber.grabbableTag = grabbableTag;
        grabber.useFloorGrabAssist = true;
        grabber.showGrabPrompt = true;

        if (grabber.grabRadius < 0.75f)
            grabber.grabRadius = 0.75f;

        if (grabber.floorGrabRadius < 1.5f)
            grabber.floorGrabRadius = 1.5f;

        if (grabber.floorGrabDownwardReach < 4f)
            grabber.floorGrabDownwardReach = 4f;

        if (grabber.promptRadius < 2f)
            grabber.promptRadius = 2f;

        if (grabber.promptDownwardReach < 4f)
            grabber.promptDownwardReach = 4f;

        if (string.IsNullOrEmpty(grabber.promptText))
            grabber.promptText = "Press Grip to pick up";
    }

    private bool TryApplyControllerPose()
    {
        if (!m_Device.isValid)
            return false;

        bool tracked = true;
        if (m_Device.TryGetFeatureValue(XRCommonUsages.isTracked, out bool isTracked))
            tracked = isTracked;

        bool hasPosition = m_Device.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 position);
        bool hasRotation = m_Device.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion rotation);
        bool hasPose = tracked && hasPosition && hasRotation;

        if (!hasPose)
            return false;

        if (followControllerPose)
        {
            transform.localPosition = position + rotation * positionOffset;
            transform.localRotation = rotation * Quaternion.Euler(rotationOffset);
        }

        return true;
    }

    private void ReadCurl(out float trigger, out float grip, out bool thumbTouched)
    {
        trigger = 0f;
        grip = 0f;
        thumbTouched = false;

        if (!m_Device.isValid)
            return;

        if (!m_Device.TryGetFeatureValue(XRCommonUsages.trigger, out trigger) &&
            m_Device.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool triggerButton))
        {
            trigger = triggerButton ? 1f : 0f;
        }

        if (!m_Device.TryGetFeatureValue(XRCommonUsages.grip, out grip) &&
            m_Device.TryGetFeatureValue(XRCommonUsages.gripButton, out bool gripButton))
        {
            grip = gripButton ? 1f : 0f;
        }

        thumbTouched =
            TryReadBool(XRCommonUsages.primaryTouch) ||
            TryReadBool(XRCommonUsages.secondaryTouch) ||
            TryReadBool(XRCommonUsages.primary2DAxisTouch) ||
            TryReadBool(XRCommonUsages.primaryButton) ||
            TryReadBool(XRCommonUsages.secondaryButton) ||
            TryReadBool(XRCommonUsages.primary2DAxisClick);
    }

    private bool TryReadBool(XRInputFeatureUsageBool usage)
    {
        return m_Device.isValid &&
               m_Device.TryGetFeatureValue(usage, out bool value) &&
               value;
    }

    private void AnimateHand(float trigger, float grip, bool thumbTouched)
    {
        if (m_Fingers.Count == 0)
            return;

        float indexCurl = Mathf.Clamp01(openCurl + trigger * triggerCurlScale);
        float gripCurl = Mathf.Clamp01(openCurl + grip * gripCurlScale);
        float thumbCurl = Mathf.Clamp01(openCurl + Mathf.Max(grip * 0.45f, trigger * 0.2f, thumbTouched ? thumbTouchCurl : 0f));

        for (int i = 0; i < m_Fingers.Count; i++)
        {
            FingerRig finger = m_Fingers[i];
            float curl = finger.kind == FingerKind.Thumb ? thumbCurl :
                finger.kind == FingerKind.Index ? indexCurl : gripCurl;

            ApplyFingerCurl(finger, curl);
        }
    }

    private void ApplyFingerCurl(FingerRig finger, float curl)
    {
        bool isThumb = finger.kind == FingerKind.Thumb;

        Vector3 proximal = isThumb ? MirrorForLeftHand(thumbProximalCurlEuler) : proximalCurlEuler;
        Vector3 intermediate = isThumb ? MirrorForLeftHand(thumbIntermediateCurlEuler) : intermediateCurlEuler;
        Vector3 distal = isThumb ? MirrorForLeftHand(thumbDistalCurlEuler) : distalCurlEuler;

        if (finger.proximal != null)
            finger.proximal.localRotation = finger.proximalRest * Quaternion.Euler(proximal * curl);

        if (finger.intermediate != null)
            finger.intermediate.localRotation = finger.intermediateRest * Quaternion.Euler(intermediate * curl);

        if (finger.distal != null)
            finger.distal.localRotation = finger.distalRest * Quaternion.Euler(distal * curl);
    }

    private Vector3 MirrorForLeftHand(Vector3 euler)
    {
        if (!isLeftHand)
            return euler;

        return new Vector3(euler.x, -euler.y, -euler.z);
    }

    private void EnsureModel()
    {
        if (m_ModelReady)
            return;

        if (modelRoot == null)
            modelRoot = FindExistingModelRoot();

        if (modelRoot == null && handModelPrefab != null)
        {
            GameObject instance = Instantiate(handModelPrefab, transform);
            instance.name = isLeftHand ? "Left Hand Mesh" : "Right Hand Mesh";
            modelRoot = instance.transform;
        }

        if (modelRoot != null)
        {
            modelRoot.SetParent(transform, false);
            modelRoot.localPosition = modelLocalPosition;
            modelRoot.localRotation = Quaternion.Euler(modelLocalEulerAngles);
            modelRoot.localScale = modelLocalScale;
            m_ModelReady = true;
            return;
        }

        if (buildPrimitiveFallback)
        {
            modelRoot = BuildPrimitiveHand();
            m_ModelReady = modelRoot != null;
        }
    }

    private Transform FindExistingModelRoot()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Transform candidate = renderers[i].transform;
            while (candidate.parent != null && candidate.parent != transform)
                candidate = candidate.parent;

            if (candidate != transform)
                return candidate;
        }

        return null;
    }

    private void CacheHandRig()
    {
        m_Fingers.Clear();

        if (modelRoot == null)
            return;

        AddFinger(FingerKind.Thumb, "thumb", "Thumb", "thumb1", "thumb2", "thumb3", "ThumbMetacarpal", "ThumbProximal", "ThumbDistal");
        AddFinger(FingerKind.Index, "index", "Index", "index1", "index2", "index3", "IndexProximal", "IndexIntermediate", "IndexDistal");
        AddFinger(FingerKind.Middle, "middle", "Middle", "middle1", "middle2", "middle3", "MiddleProximal", "MiddleIntermediate", "MiddleDistal");
        AddFinger(FingerKind.Ring, "ring", "Ring", "ring1", "ring2", "ring3", "RingProximal", "RingIntermediate", "RingDistal");
        AddFinger(FingerKind.Pinky, "pinky", "Little", "pinky1", "pinky2", "pinky3", "LittleProximal", "LittleIntermediate", "LittleDistal");
    }

    private void AddFinger(
        FingerKind kind,
        string projectFingerName,
        string xrFingerName,
        string projectProximal,
        string projectIntermediate,
        string projectDistal,
        string xrProximal,
        string xrIntermediate,
        string xrDistal)
    {
        Transform proximal = FindBone(projectProximal, xrProximal, projectFingerName + "Proximal", xrFingerName + "Proximal");
        Transform intermediate = FindBone(projectIntermediate, xrIntermediate, projectFingerName + "Intermediate", xrFingerName + "Intermediate");
        Transform distal = FindBone(projectDistal, xrDistal, projectFingerName + "Distal", xrFingerName + "Distal");

        if (proximal == null && intermediate == null && distal == null)
            return;

        AddFingerRig(kind, proximal, intermediate, distal);
    }

    private void AddFingerRig(FingerKind kind, Transform proximal, Transform intermediate, Transform distal)
    {
        var rig = new FingerRig
        {
            kind = kind,
            proximal = proximal,
            intermediate = intermediate,
            distal = distal,
            proximalRest = proximal != null ? proximal.localRotation : Quaternion.identity,
            intermediateRest = intermediate != null ? intermediate.localRotation : Quaternion.identity,
            distalRest = distal != null ? distal.localRotation : Quaternion.identity,
        };

        m_Fingers.Add(rig);
    }

    private Transform FindBone(params string[] names)
    {
        if (modelRoot == null)
            return null;

        Transform[] transforms = modelRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < names.Length; i++)
        {
            string wanted = NormalizeName(names[i]);
            for (int j = 0; j < transforms.Length; j++)
            {
                string candidate = NormalizeName(transforms[j].name);
                if (candidate == wanted || candidate.EndsWith(wanted))
                    return transforms[j];
            }
        }

        for (int i = 0; i < names.Length; i++)
        {
            string wanted = NormalizeName(names[i]);
            for (int j = 0; j < transforms.Length; j++)
            {
                string candidate = NormalizeName(transforms[j].name);
                if (candidate.Contains(wanted))
                    return transforms[j];
            }
        }

        return null;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        int namespaceIndex = value.LastIndexOf(':');
        if (namespaceIndex >= 0 && namespaceIndex < value.Length - 1)
            value = value.Substring(namespaceIndex + 1);

        return value
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();
    }

    private void RefreshRenderers()
    {
        m_Renderers.Clear();
        GetComponentsInChildren(true, m_Renderers);
    }

    private void SetRenderersVisible(bool visible)
    {
        for (int i = 0; i < m_Renderers.Count; i++)
        {
            if (m_Renderers[i] != null)
                m_Renderers[i].enabled = visible;
        }
    }

    private Transform BuildPrimitiveHand()
    {
        EnsureRuntimeMaterial();

        var root = new GameObject(isLeftHand ? "Left Primitive Hand" : "Right Primitive Hand").transform;
        root.SetParent(transform, false);
        root.localPosition = modelLocalPosition;
        root.localRotation = Quaternion.Euler(modelLocalEulerAngles);
        root.localScale = modelLocalScale;

        float mirror = isLeftHand ? -1f : 1f;
        Transform palm = Box("Palm", root, Vector3.zero, new Vector3(0.08f, 0.025f, 0.1f));
        Box("Wrist", palm, new Vector3(0f, 0f, -0.06f), new Vector3(0.055f, 0.025f, 0.035f));

        CreatePrimitiveFinger(root, FingerKind.Index, "Index", new Vector3(-0.027f * mirror, 0.005f, 0.05f), 0.016f, 0.032f, 0.022f, 0.016f);
        CreatePrimitiveFinger(root, FingerKind.Middle, "Middle", new Vector3(-0.009f * mirror, 0.005f, 0.055f), 0.017f, 0.036f, 0.024f, 0.018f);
        CreatePrimitiveFinger(root, FingerKind.Ring, "Ring", new Vector3(0.011f * mirror, 0.005f, 0.052f), 0.016f, 0.032f, 0.022f, 0.016f);
        CreatePrimitiveFinger(root, FingerKind.Pinky, "Pinky", new Vector3(0.03f * mirror, 0.005f, 0.045f), 0.014f, 0.027f, 0.018f, 0.014f);

        Transform thumbBase = CreatePrimitiveFinger(root, FingerKind.Thumb, "Thumb", new Vector3(-0.044f * mirror, 0.001f, 0.003f), 0.018f, 0.026f, 0.02f, 0.015f);
        thumbBase.localRotation = Quaternion.Euler(-20f, 20f * mirror, 50f * mirror);

        return root;
    }

    private Transform CreatePrimitiveFinger(Transform parent, FingerKind kind, string prefix, Vector3 localPosition, float width, float proximalLength, float intermediateLength, float distalLength)
    {
        Transform proximal = FingerSegment(prefix + "_Proximal", parent, localPosition, width, proximalLength);
        Transform intermediate = FingerSegment(prefix + "_Intermediate", proximal, new Vector3(0f, 0f, proximalLength), width * 0.9f, intermediateLength);
        Transform distal = FingerSegment(prefix + "_Distal", intermediate, new Vector3(0f, 0f, intermediateLength), width * 0.75f, distalLength);

        AddFingerRig(kind, proximal, intermediate, distal);
        return proximal;
    }

    private Transform Box(string name, Transform parent, Vector3 localPosition, Vector3 localScale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = m_RuntimeMaterial;
        return go.transform;
    }

    private Transform FingerSegment(string name, Transform parent, Vector3 localPosition, float width, float length)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = localPosition;
        pivot.localRotation = Quaternion.identity;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = name + "_Visual";
        Destroy(visual.GetComponent<Collider>());
        visual.transform.SetParent(pivot, false);
        visual.transform.localPosition = new Vector3(0f, 0f, length * 0.5f);
        visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        visual.transform.localScale = new Vector3(width, length * 0.5f, width);
        visual.GetComponent<Renderer>().sharedMaterial = m_RuntimeMaterial;

        return pivot;
    }

    private void EnsureRuntimeMaterial()
    {
        if (m_RuntimeMaterial != null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null || shader.name == "Hidden/InternalErrorShader")
            shader = Shader.Find("Standard");

        m_RuntimeMaterial = new Material(shader);
        m_RuntimeMaterial.color = skinColor;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
