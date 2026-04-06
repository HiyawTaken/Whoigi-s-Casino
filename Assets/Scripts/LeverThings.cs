using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class SlotMachineLever : MonoBehaviour
{
    [Header("Reel References")]
    public ReelSpinner[] reels; // Drag your 3 reel objects here in Inspector

    [Header("Lever Movement")]
    public float pullAngle = 45f;
    private Quaternion uprightRotation;

    // VR Controller detection
    private InputDevice rightController;
    private bool wasPressed = false;

    void Start()
    {
        uprightRotation = transform.localRotation;
    }

    void Update()
    {
        // 1. Check for VR Input (B Button)
        if (!rightController.isValid)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);
            if (devices.Count > 0) rightController = devices[0];
        }

        rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bButton);

        // 2. Combine Inputs (VR B-Button OR Space Key for testing)
        bool pressed = bButton || Input.GetKeyDown(KeyCode.Space);

        if (pressed && !wasPressed)
        {
            TriggerMachine();
        }

        wasPressed = pressed;
    }

    void TriggerMachine()
    {
        Debug.Log("Lever Actuated!");

        // Visually tilt the lever down slightly for feedback
        transform.localRotation = Quaternion.Euler(pullAngle, 0, 0);

        // Tell every reel in the array to toggle
        foreach (ReelSpinner reel in reels)
        {
            reel.ToggleSpin();
        }

        // Return the lever to upright after a short delay
        Invoke("ResetLever", 0.2f);
    }

    void ResetLever()
    {
        transform.localRotation = uprightRotation;
    }
}