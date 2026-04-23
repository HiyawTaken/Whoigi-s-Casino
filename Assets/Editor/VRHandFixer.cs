using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Installs XR Hands hand-tracking visuals (Left/Right Hand Tracking prefabs)
// under Camera Offset — the exact setup from "2023 Unity VR Basics – XR Hands".
//
// The XRHandSubsystem starts automatically through OpenXR; no extra manager
// component is required. Just drop the two prefabs under Camera Offset.
public static class VRHandFixer
{
    private const string LeftHandTrackingGuid  = "b3ed8a0a703ebd34a9e44ed3d9f1fcf6";
    private const string RightHandTrackingGuid = "3f7511fbc40ae7a4b89c3298a3de199d";
    private const string ContainerName         = "__XRHands";

    private static readonly string[] XROriginNames =
    {
        "XR Origin (XR Rig)",
        "XR Origin (VR)",
        "XR Origin",
        "XRRig",
        "XR Rig",
        "Complete XR Origin Set Up Variant",
    };

    // ────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/VR Hands/1 - Install Hand Visualizers (XR Hands)")]
    public static void Install()
    {
        // ── 1. Locate Camera Offset ──────────────────────────────────────
        var cameraOffset = FindInScene("Camera Offset");
        if (cameraOffset == null)
        {
            Debug.LogError("VRHandFixer: 'Camera Offset' not found in active scene.");
            return;
        }

        // ── 2. Remove stale pinch pointers / previous fixer attempts ─────
        int nuked = NukePinchAndLeftovers();

        // ── 3. Load hand prefabs ─────────────────────────────────────────
        var leftPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(
                              AssetDatabase.GUIDToAssetPath(LeftHandTrackingGuid));
        var rightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                              AssetDatabase.GUIDToAssetPath(RightHandTrackingGuid));

        if (leftPrefab == null || rightPrefab == null)
        {
            Debug.LogError(
                "VRHandFixer: Hand prefabs missing. " +
                $"Left={leftPrefab != null} Right={rightPrefab != null}. " +
                "Import the HandVisualizer sample via " +
                "Package Manager > XR Hands > Samples > Hand Visualizer.");
            return;
        }

        // ── 4. Build / refresh container under Camera Offset ─────────────
        var prev = cameraOffset.transform.Find(ContainerName);
        if (prev != null) Object.DestroyImmediate(prev.gameObject);

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

        MarkDirty();
        Debug.Log(
            $"VRHandFixer: Done. Nuked {nuked} stale object(s). " +
            "Left Hand Tracking + Right Hand Tracking installed under Camera Offset. " +
            "Press Play with headset connected (hand tracking enabled in OpenXR settings).");
    }

    // ────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/VR Hands/2 - Nuke Pinch Pointer + Leftovers Only")]
    public static void NukeOnly()
    {
        int n = NukePinchAndLeftovers();
        MarkDirty();
        Debug.Log($"VRHandFixer: Nuked {n} object(s).");
    }

    [MenuItem("Tools/VR Hands/3 - Uninstall Hand Visualizers")]
    public static void Uninstall()
    {
        var cameraOffset = FindInScene("Camera Offset");
        if (cameraOffset != null)
        {
            foreach (var name in new[] { ContainerName, "__MetaHands", "__HandVisual" })
            {
                var c = cameraOffset.transform.Find(name);
                if (c != null) Object.DestroyImmediate(c.gameObject);
            }
        }
        MarkDirty();
        Debug.Log("VRHandFixer: Uninstalled.");
    }

    [MenuItem("Tools/VR Hands/Diagnose")]
    public static void Diagnose()
    {
        // Camera Offset children
        var co = FindInScene("Camera Offset");
        if (co != null)
        {
            Debug.Log($"[Diag] Camera Offset children ({co.transform.childCount}):");
            foreach (Transform t in co.transform)
                Debug.Log($"[Diag]   '{t.name}'  active={t.gameObject.activeSelf}");
        }
        else
        {
            Debug.LogError("[Diag] 'Camera Offset' NOT found.");
        }

        // Hand Tracking prefab instances in scene
        var hands = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name.Contains("Hand Tracking"))
            .ToList();
        Debug.Log($"[Diag] Hand Tracking instances in scene: {hands.Count}");
        foreach (var h in hands)
            Debug.Log($"[Diag]   '{h.GetFullPath()}'  active={h.gameObject.activeInHierarchy}");

        // XR Origin
        foreach (var name in XROriginNames)
        {
            var go = FindInScene(name);
            if (go != null) Debug.Log($"[Diag] XR Origin found: '{go.name}'");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private static int NukePinchAndLeftovers()
    {
        int n = 0;
        var targets = new List<GameObject>();

        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                var tName = t.name;
                if (tName.Contains("Pinch_Pointer") ||
                    tName == "Pinch Pointer"         ||
                    tName == "__MetaHands"            ||
                    tName == "__HandVisual"           ||
                    tName == "OVRManager")
                {
                    targets.Add(t.gameObject);
                }
            }
        }

        foreach (var go in targets.Distinct())
        {
            if (go == null) continue;
            Debug.Log($"VRHandFixer: destroying '{go.name}' under " +
                      $"'{(go.transform.parent != null ? go.transform.parent.name : "<root>")}'");
            Object.DestroyImmediate(go);
            n++;
        }

        // Suppress stray controller arc LineRenderers.
        foreach (var side in new[] { "Left Controller", "Right Controller" })
        {
            var c = FindInScene(side);
            if (c == null) continue;
            foreach (var lr in c.GetComponentsInChildren<LineRenderer>(true))
                lr.enabled = false;
        }

        return n;
    }

    private static GameObject FindInScene(string name)
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
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

    private static void MarkDirty() =>
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
}

internal static class TransformPathExt
{
    public static string GetFullPath(this Transform t)
    {
        if (t.parent == null) return "/" + t.name;
        return t.parent.GetFullPath() + "/" + t.name;
    }
}
