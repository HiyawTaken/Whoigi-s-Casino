using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the VR hand setup aligned with this project: Quest/Meta controllers
/// drive visible hand meshes. The older XR Hands tracking sample prefabs are
/// intentionally removed by ControllerHandsInstaller because they require the
/// OpenXR hand-tracking feature instead of controller input.
/// </summary>
[InitializeOnLoad]
public static class AutoInstallXRHands
{
    private const string SessionKey = "ControllerHands_AutoInstalled_v5";

    static AutoInstallXRHands()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += InstallControllerHands;
    }

    private static void InstallControllerHands()
    {
        int installed = ControllerHandsInstaller.InstallAllEnabledBuildScenes();
        Debug.Log($"[AutoInstallXRHands] Installed controller-driven Meta hands in {installed} scene(s).");
    }
}
