using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Optional runtime companion. Attach to any persistent GameObject.
/// Verifies the XRHandSubsystem is running and auto-spawns hand prefabs
/// under Camera Offset if they are missing.
///
/// Preferred setup path: Editor menu  Tools > VR Hands > 1 - Install Hand Visualizers
/// This script is a runtime safety net only.
/// </summary>
public class XRHandsAutoSetup : MonoBehaviour
{
    [Header("Hand Tracking Prefabs")]
    [Tooltip("Assets/Samples/XR Hands/1.4.0/HandVisualizer/Prefabs/Left Hand Tracking.prefab")]
    public GameObject leftHandPrefab;

    [Tooltip("Assets/Samples/XR Hands/1.4.0/HandVisualizer/Prefabs/Right Hand Tracking.prefab")]
    public GameObject rightHandPrefab;

    [Header("Auto-Spawn")]
    [Tooltip("Spawn the prefabs at runtime if not already present under Camera Offset.")]
    public bool autoSpawnIfMissing = true;

    private static readonly List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();

    private void Start()
    {
        Invoke(nameof(VerifySubsystem), 0.5f);
    }

    private void VerifySubsystem()
    {
        SubsystemManager.GetSubsystems(s_Subsystems);
        bool running = false;
        foreach (var sub in s_Subsystems)
        {
            if (sub.running) { running = true; break; }
        }

        if (!running)
        {
            Debug.LogWarning(
                "[XRHandsAutoSetup] XRHandSubsystem is NOT running. " +
                "Check: OpenXR Hand Tracking feature enabled in " +
                "Project Settings > XR Plug-in Management > OpenXR Features, " +
                "and that you are on a device / simulator that supports hand tracking.");
        }
        else
        {
            Debug.Log("[XRHandsAutoSetup] XRHandSubsystem is running.");
        }

        if (!autoSpawnIfMissing) return;

        var cameraOffset = FindCameraOffset();
        if (cameraOffset == null)
        {
            Debug.LogWarning("[XRHandsAutoSetup] 'Camera Offset' not found — auto-spawn skipped.");
            return;
        }

        if (leftHandPrefab != null && cameraOffset.Find("Left Hand Tracking") == null)
        {
            var go = Instantiate(leftHandPrefab, cameraOffset);
            go.name = "Left Hand Tracking";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            Debug.Log("[XRHandsAutoSetup] Spawned Left Hand Tracking.");
        }

        if (rightHandPrefab != null && cameraOffset.Find("Right Hand Tracking") == null)
        {
            var go = Instantiate(rightHandPrefab, cameraOffset);
            go.name = "Right Hand Tracking";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            Debug.Log("[XRHandsAutoSetup] Spawned Right Hand Tracking.");
        }
    }

    private static Transform FindCameraOffset()
    {
        var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in all)
        {
            if (t.name == "Camera Offset") return t;
        }
        return null;
    }
}
