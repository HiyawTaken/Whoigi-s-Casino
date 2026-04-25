using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Backward-compatible menu wrapper for the project hand setup.
/// The active implementation is controller-driven Meta/Quest hands.
/// </summary>
public static class VRHandFixer
{
    [MenuItem("Tools/VR Hands/Install Controller Hands")]
    public static void Install()
    {
        ControllerHandsInstaller.InstallActiveSceneFromMenu();
    }

    [MenuItem("Tools/VR Hands/Install Controller Hands In Build Scenes")]
    public static void InstallAllBuildScenes()
    {
        ControllerHandsInstaller.InstallBuildScenesFromMenu();
    }

    [MenuItem("Tools/VR Hands/Uninstall Controller Hands")]
    public static void Uninstall()
    {
        ControllerHandsInstaller.UninstallActiveSceneFromMenu();
    }

    [MenuItem("Tools/VR Hands/Diagnose")]
    public static void Diagnose()
    {
        var scene = SceneManager.GetActiveScene();
        var cameraOffset = FindInScene("Camera Offset");

        if (cameraOffset == null)
        {
            Debug.LogError("[VRHandFixer] Camera Offset not found in active scene.");
            return;
        }

        Debug.Log($"[VRHandFixer] Camera Offset children ({cameraOffset.transform.childCount}):");
        foreach (Transform child in cameraOffset.transform)
            Debug.Log($"[VRHandFixer]   {child.name} active={child.gameObject.activeSelf}");

        var controllerHands = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ControllerVisual>(true))
            .ToList();

        Debug.Log($"[VRHandFixer] ControllerVisual instances: {controllerHands.Count}");
        foreach (ControllerVisual hand in controllerHands)
        {
            string side = hand.isLeftHand ? "Left" : "Right";
            Debug.Log($"[VRHandFixer]   {side}: {GetFullPath(hand.transform)} model={(hand.modelRoot != null ? hand.modelRoot.name : "<runtime>")}");
        }

        var grabbers = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ControllerGrabber>(true))
            .ToList();

        Debug.Log($"[VRHandFixer] ControllerGrabber instances: {grabbers.Count}");
        foreach (ControllerGrabber grabber in grabbers)
            Debug.Log($"[VRHandFixer]   {grabber.inputSource}: {GetFullPath(grabber.transform)} radius={grabber.grabRadius}");
    }

    private static GameObject FindInScene(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform found = FindRecursive(root.transform, name);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindRecursive(Transform transform, string name)
    {
        if (transform.name == name)
            return transform;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform found = FindRecursive(transform.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static string GetFullPath(Transform transform)
    {
        if (transform.parent == null)
            return "/" + transform.name;

        return GetFullPath(transform.parent) + "/" + transform.name;
    }
}
