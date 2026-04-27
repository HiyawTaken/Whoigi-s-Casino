using UnityEngine;

[DisallowMultipleComponent]
public class SlotMachineLeverGrab : ControllerGrabbable
{
    public Transform leverPivot;
    public SlotMachineController slotMachine;
    public float pulledAngle = 50f;
    public float returnSpeed = 180f;
    public bool triggerSpinOnGrab = true;

    private ControllerGrabber m_ActiveGrabber;
    private Quaternion m_RestRotation;
    private bool m_HasRestRotation;
    private const string DefaultLeverPrompt = "Press Grip to pull lever";

    private void Reset()
    {
        ApplyLeverDefaults();
    }

    private void Awake()
    {
        ApplyLeverDefaults();
        CacheRestRotation();
    }

    private void OnValidate()
    {
        ApplyLeverDefaults();
    }

    private void Update()
    {
        RefreshPromptText();

        if (leverPivot == null)
            return;

        if (m_ActiveGrabber != null)
        {
            leverPivot.localRotation = m_RestRotation * Quaternion.Euler(0f, 0f, pulledAngle);
            return;
        }

        if (m_HasRestRotation)
        {
            leverPivot.localRotation = Quaternion.RotateTowards(
                leverPivot.localRotation,
                m_RestRotation,
                returnSpeed * Time.deltaTime);
        }
    }

    public override void OnGrabbed(ControllerGrabber grabber)
    {
        ApplyLeverDefaults();
        CacheRestRotation();

        m_ActiveGrabber = grabber;
        leverPivot.localRotation = m_RestRotation * Quaternion.Euler(0f, 0f, pulledAngle);
        RefreshPromptText();

        if (triggerSpinOnGrab && slotMachine != null)
            slotMachine.PullLever();
    }

    public override void OnReleased(ControllerGrabber grabber)
    {
        if (m_ActiveGrabber == grabber)
            m_ActiveGrabber = null;
    }

    private void ApplyLeverDefaults()
    {
        canBeGrabbed = true;
        allowKinematicGrab = true;
        ignoreMassLimit = true;
        snapToHand = false;
        keepInPlaceWhenGrabbed = true;
        keepPhysicsWhileGrabbed = true;
        applyThrowOnRelease = false;
        releaseVelocityScale = 0f;
        releaseAngularVelocityScale = 0f;

        RefreshPromptText();

        if (grabRadiusOverride <= 0f)
            grabRadiusOverride = 4f;

        if (promptRadiusOverride <= 0f)
            promptRadiusOverride = 4.5f;

        if (downwardReachOverride <= 0f)
            downwardReachOverride = 6f;

        if (leverPivot == null && transform.parent != null)
            leverPivot = transform.parent;

        if (slotMachine == null)
            slotMachine = GetComponentInParent<SlotMachineController>();

        if (pulledAngle == 0f)
            pulledAngle = 50f;

        if (returnSpeed <= 0f)
            returnSpeed = 180f;
    }

    private void RefreshPromptText()
    {
        if (slotMachine == null)
            slotMachine = GetComponentInParent<SlotMachineController>();

        if (Application.isPlaying && slotMachine != null)
        {
            promptOverride = slotMachine.GetLeverPrompt();
            return;
        }

        if (string.IsNullOrEmpty(promptOverride) || promptOverride == DefaultLeverPrompt)
            promptOverride = "Press Grip to spin";
    }

    private void CacheRestRotation()
    {
        if (m_HasRestRotation || leverPivot == null)
            return;

        m_RestRotation = leverPivot.localRotation;
        m_HasRestRotation = true;
    }
}
