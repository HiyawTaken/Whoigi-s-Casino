using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor utility that replaces XRI's pinch-pointer "spoon" visuals with
// the Unity XR Hands HandVisualizer sample hands, which read OpenXR hand
// tracking data directly. Works on Meta Quest via OpenXR (same data source
// that is already driving the pinch pointer — confirmed visible in Play).
public static class VRHandFixer
{
    private const string LeftHandTrackingGuid  = "b3ed8a0a703ebd34a9e44ed3d9f1fcf6";
    private const string RightHandTrackingGuid = "3f7511fbc40ae7a4b89c3298a3de199d";
    private const string ContainerName         = "__XRHands";

    [MenuItem("Tools/VR Hands/1 - Install Hand Visualizers (XR Hands)")]
    public static void Install()
    {
        var cameraOffset = FindInScene("Camera Offset");
        if (cameraOffset == null) { Debug.LogError("VRHandFixer: 'Camera Offset' not found"); return; }

        int nuked = NukePinchAndLeftovers();

        var leftPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(LeftHandTrackingGuid));
        var rightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(RightHandTrackingGuid));
        if (leftPrefab == null || rightPrefab == null)
        {
            Debug.LogError($"VRHandFixer: hand prefabs missing. Left={leftPrefab} Right={rightPrefab}. Ensure XR Hands HandVisualizer sample exists under Assets/Samples/XR Hands/1.4.0/HandVisualizer/.");
            return;
        }

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
        Debug.Log($"VRHandFixer: installed XR Hands visualizers. Nuked {nuked} spoon/leftover object(s). Press Play, headset on.");
    }

    [MenuItem("Tools/VR Hands/2 - Nuke Pinch Pointer + Leftovers Only")]
    public static void NukeOnly()
    {
        int n = NukePinchAndLeftovers();
        MarkDirty();
        Debug.Log($"VRHandFixer: nuked {n} object(s).");
    }

    [MenuItem("Tools/VR Hands/3 - Uninstall Hand Visualizers")]
    public static void Uninstall()
    {
        var cameraOffset = FindInScene("Camera Offset");
        if (cameraOffset != null)
        {
            var c = cameraOffset.transform.Find(ContainerName);
            if (c != null) Object.DestroyImmediate(c.gameObject);
            var old = cameraOffset.transform.Find("__MetaHands");
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        MarkDirty();
    }

    [MenuItem("Tools/VR Hands/Diagnose")]
    public static void Diagnose()
    {
        foreach (var side in new[] { "Left Controller", "Right Controller" })
        {
            var c = FindInScene(side);
            if (c == null) { Debug.LogError($"[Diag] {side}: NOT FOUND"); continue; }
            foreach (Transform t in c.transform)
                Debug.Log($"[Diag] {side} / {t.name}  active={t.gameObject.activeSelf} localPos={t.localPosition}");
            foreach (var r in c.GetComponentsInChildren<Renderer>(true))
                Debug.Log($"[Diag]   renderer '{r.gameObject.name}' enabled={r.enabled} GO-active={r.gameObject.activeInHierarchy}");
        }
        var co = FindInScene("Camera Offset");
        if (co != null)
            foreach (Transform t in co.transform)
                Debug.Log($"[Diag] Camera Offset / {t.name}  active={t.gameObject.activeSelf}");

        var hands = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name.Contains("Hand Tracking"))
            .ToList();
        Debug.Log($"[Diag] XR Hands prefabs in scene: {hands.Count}");
        foreach (var h in hands) Debug.Log($"[Diag]   {h.GetFullPath()}  active={h.gameObject.activeInHierarchy}");
    }

    // ---------- Helpers ----------

    // Destroy pinch-pointer and teleport line visuals, plus previous fixer attempts.
    // Returns count of objects destroyed.
    private static int NukePinchAndLeftovers()
    {
        int n = 0;
        var targets = new List<GameObject>();

        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                var name = t.name;
                if (name.Contains("Pinch_Pointer") || name == "Pinch Pointer" ||
                    name == "__MetaHands" || name == "__HandVisual" ||
                    name == "OVRManager" ||
                    name == "XR Controller Right" && t.parent != null && t.parent.name == "Right Controller")
                {
                    targets.Add(t.gameObject);
                }
            }
        }

        // Remove duplicates and nested targets.
        foreach (var go in targets.Distinct())
        {
            if (go == null) continue;
            Debug.Log($"VRHandFixer: destroying '{go.name}' under '{(go.transform.parent != null ? go.transform.parent.name : "<root>")}'");
            Object.DestroyImmediate(go);
            n++;
        }

        // Also disable teleport-interactor LineRenderers so they don't render blue arcs.
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
        for (int i = 0; i < t.childCount; i++) { var f = FindRecursive(t.GetChild(i), name); if (f != null) return f; }
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
