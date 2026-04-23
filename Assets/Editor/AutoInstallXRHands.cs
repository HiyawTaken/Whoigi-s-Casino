using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs once per Unity session (triggered by [InitializeOnLoad] after compile).
/// Walks every enabled build scene, installs Left/Right Hand Tracking prefabs
/// under Camera Offset if not already present, then saves and re-opens the
/// originally active scene.
/// </summary>
[InitializeOnLoad]
public static class AutoInstallXRHands
{
    private const string SessionKey    = "XRHands_AutoInstalled_v3";
    private const string LeftHandGuid  = "b3ed8a0a703ebd34a9e44ed3d9f1fcf6";
    private const string RightHandGuid = "3f7511fbc40ae7a4b89c3298a3de199d";
    private const string ContainerName = "__XRHands";

    static AutoInstallXRHands()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += InstallAllScenes;
    }

    private static void InstallAllScenes()
    {
        var leftPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(
                              AssetDatabase.GUIDToAssetPath(LeftHandGuid));
        var rightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                              AssetDatabase.GUIDToAssetPath(RightHandGuid));

        if (leftPrefab == null || rightPrefab == null)
        {
            Debug.LogWarning("[AutoInstallXRHands] Hand prefabs not found — skipping.");
            return;
        }

        string originalScene = EditorSceneManager.GetActiveScene().path;

        // All enabled build scenes.
        var scenePaths = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToList();

        int installed = 0;
        foreach (var scenePath in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var cameraOffset = FindInScene("Camera Offset", scene);
            if (cameraOffset == null) continue;                 // scene has no XR rig
            if (cameraOffset.transform.Find(ContainerName) != null) continue; // already done

            // Build container
            var container = new GameObject(ContainerName);
            container.transform.SetParent(cameraOffset.transform, false);
            container.transform.localPosition = Vector3.zero;
            container.transform.localRotation = Quaternion.identity;
            container.transform.localScale    = Vector3.one;

            var left  = (GameObject)PrefabUtility.InstantiatePrefab(leftPrefab,  container.transform);
            var right = (GameObject)PrefabUtility.InstantiatePrefab(rightPrefab, container.transform);

            foreach (var go in new[] { left, right })
            {
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale    = Vector3.one;
                go.SetActive(true);
            }

            // Suppress stray controller arc LineRenderers.
            foreach (var side in new[] { "Left Controller", "Right Controller" })
            {
                var ctrl = FindInScene(side, scene);
                if (ctrl == null) continue;
                foreach (var lr in ctrl.GetComponentsInChildren<LineRenderer>(true))
                    lr.enabled = false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            installed++;
            Debug.Log($"[AutoInstallXRHands] Installed XR Hands in '{scene.name}'.");
        }

        // Re-open original scene.
        if (!string.IsNullOrEmpty(originalScene))
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

        Debug.Log($"[AutoInstallXRHands] Done. Installed in {installed} scene(s).");
    }

    private static GameObject FindInScene(string name, Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindRecursive(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform FindRecursive(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var f = FindRecursive(t.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
