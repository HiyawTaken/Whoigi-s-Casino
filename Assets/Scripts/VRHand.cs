using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class VRHand : MonoBehaviour
{
    [Header("Hand Setup")]
    public XRNode handNode = XRNode.RightHand;

    [Header("Hand Appearance")]
    public Color skinColor = new Color(1f, 0.82f, 0.70f);

    [Header("Haptic Feedback")]
    public float grabHapticAmplitude = 0.5f;
    public float grabHapticDuration = 0.1f;

    private InputDevice device;
    private NearFarInteractor nearFarInteractor;
    private XRDirectInteractor directInteractor;

    // Hand parts
    private Transform palm;
    private Transform[] fingers = new Transform[5];
    private Transform[] fingertips = new Transform[5];

    // Finger curl state
    private float gripValue;
    private float triggerValue;

    void Start()
    {
        device = InputDevices.GetDeviceAtXRNode(handNode);
        BuildHand();
        HookInteractors();
    }

    void BuildHand()
    {
        // Palm
        palm = CreatePart("Palm", transform, new Vector3(0f, 0f, 0.02f), new Vector3(0.075f, 0.02f, 0.09f));

        // Finger base positions (spread across palm width)
        Vector3[] fingerOffsets = new Vector3[]
        {
            new Vector3(-0.033f, 0f, 0.065f), // index
            new Vector3(-0.011f, 0f, 0.072f), // middle
            new Vector3( 0.011f, 0f, 0.068f), // ring
            new Vector3( 0.033f, 0f, 0.058f), // pinky
            new Vector3(-0.048f, 0f, 0.010f), // thumb
        };

        for (int i = 0; i < 5; i++)
        {
            fingers[i] = CreatePart("Finger" + i, palm, fingerOffsets[i], new Vector3(0.015f, 0.015f, 0.035f));
            fingertips[i] = CreatePart("Tip" + i, fingers[i], new Vector3(0f, 0f, 0.03f), new Vector3(0.013f, 0.013f, 0.025f));
        }
    }

    Transform CreatePart(string partName, Transform parent, Vector3 localPos, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = partName;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = scale;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader.name == "Hidden/InternalErrorShader")
            mat = new Material(Shader.Find("Standard"));
        mat.color = skinColor;
        go.GetComponent<Renderer>().material = mat;

        return go.transform;
    }

    void HookInteractors()
    {
        nearFarInteractor = GetComponentInChildren<NearFarInteractor>();
        directInteractor = GetComponentInChildren<XRDirectInteractor>();

        if (nearFarInteractor != null)
        {
            nearFarInteractor.selectEntered.AddListener(OnGrab);
            nearFarInteractor.selectExited.AddListener(OnRelease);
        }
        if (directInteractor != null)
        {
            directInteractor.selectEntered.AddListener(OnGrab);
            directInteractor.selectExited.AddListener(OnRelease);
        }
    }

    void Update()
    {
        if (!device.isValid)
            device = InputDevices.GetDeviceAtXRNode(handNode);

        if (!device.isValid) return;

        device.TryGetFeatureValue(CommonUsages.grip, out gripValue);
        device.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);

        CurlFingers();
    }

    void CurlFingers()
    {
        // Grip curls all fingers, trigger adds extra curl to index
        float[] curls = new float[]
        {
            Mathf.Max(gripValue, triggerValue), // index curls on trigger too
            gripValue,
            gripValue,
            gripValue,
            gripValue * 0.7f, // thumb curls less
        };

        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Lerp(0f, 70f, curls[i]);
            fingers[i].localRotation = Quaternion.Euler(90f - angle, 0f, 0f);
            fingertips[i].localRotation = Quaternion.Euler(90f - angle * 0.6f, 0f, 0f);
        }
    }

    private void OnGrab(SelectEnterEventArgs args) => SendHaptic(grabHapticAmplitude, grabHapticDuration);
    private void OnRelease(SelectExitEventArgs args) => SendHaptic(grabHapticAmplitude * 0.5f, grabHapticDuration * 0.5f);

    private void SendHaptic(float amplitude, float duration)
    {
        if (!device.isValid) device = InputDevices.GetDeviceAtXRNode(handNode);
        if (device.isValid) device.SendHapticImpulse(0, amplitude, duration);
    }

    void OnDestroy()
    {
        if (nearFarInteractor != null)
        {
            nearFarInteractor.selectEntered.RemoveListener(OnGrab);
            nearFarInteractor.selectExited.RemoveListener(OnRelease);
        }
        if (directInteractor != null)
        {
            directInteractor.selectEntered.RemoveListener(OnGrab);
            directInteractor.selectExited.RemoveListener(OnRelease);
        }
    }
}
