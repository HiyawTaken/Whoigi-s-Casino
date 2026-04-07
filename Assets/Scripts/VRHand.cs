using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class VRHand : MonoBehaviour
{
    [Header("Hand Setup")]
    public XRNode handNode = XRNode.LeftHand;
    public Animator handAnimator;

    [Header("Haptic Feedback")]
    public float grabHapticAmplitude = 0.5f;
    public float grabHapticDuration = 0.1f;

    private InputDevice device;
    private NearFarInteractor nearFarInteractor;
    private XRDirectInteractor directInteractor;

    private static readonly int AnimGrip = Animator.StringToHash("Grip");
    private static readonly int AnimTrigger = Animator.StringToHash("Trigger");

    void Start()
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

        if (handAnimator == null || !device.isValid)
            return;

        device.TryGetFeatureValue(CommonUsages.grip, out float gripValue);
        device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue);

        handAnimator.SetFloat(AnimGrip, gripValue);
        handAnimator.SetFloat(AnimTrigger, triggerValue);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        SendHaptic(grabHapticAmplitude, grabHapticDuration);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        SendHaptic(grabHapticAmplitude * 0.5f, grabHapticDuration * 0.5f);
    }

    private void SendHaptic(float amplitude, float duration)
    {
        if (!device.isValid)
            device = InputDevices.GetDeviceAtXRNode(handNode);

        if (device.isValid)
            device.SendHapticImpulse(0, amplitude, duration);
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
