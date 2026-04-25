using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRNode = UnityEngine.XR.XRNode;

public static class ControllerHandsInstaller
{
    private const string LeftHandModelGuid = "c92727aee7290f7438ec55e146c85a9f";
    private const string RightHandModelGuid = "e806d360aaa271841aba8159c64f7ae2";
    private const string ContainerName = "__ControllerHands";

    private static readonly string[] LegacyContainers =
    {
        "__XRHands",
        "__MetaHands",
        "__HandVisual",
    };

    private static readonly string[] LegacyControllerVisuals =
    {
        "Left Controller Visual",
        "Right Controller Visual",
        "XR Controller Left",
        "XR Controller Right",
    };

    private static readonly string[] LegacySceneObjects =
    {
        "HandTracker",
        "HandTracker (1)",
    };

    [MenuItem("Tools/VR Hands/1 - Install Controller Hands (Meta Quest)")]
    public static void InstallActiveSceneFromMenu()
    {
        var scene = SceneManager.GetActiveScene();
        int installed = InstallInScene(scene, true);
        if (installed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[ControllerHandsInstaller] Installed Meta controller hands in active scene.");
        }
        else
        {
            Debug.LogWarning("[ControllerHandsInstaller] No Camera Offset found in active scene.");
        }
    }

    [MenuItem("Tools/VR Hands/2 - Install Controller Hands In Build Scenes")]
    public static void InstallBuildScenesFromMenu()
    {
        int installed = InstallAllEnabledBuildScenes();
        Debug.Log($"[ControllerHandsInstaller] Installed Meta controller hands in {installed} scene(s).");
    }

    [MenuItem("Tools/VR Hands/3 - Uninstall Controller Hands")]
    public static void UninstallActiveSceneFromMenu()
    {
        var scene = SceneManager.GetActiveScene();
        int removed = RemoveControllerHands(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[ControllerHandsInstaller] Removed {removed} controller hand container(s).");
    }

    public static int InstallAllEnabledBuildScenes()
    {
        string originalScenePath = SceneManager.GetActiveScene().path;

        var scenePaths = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .Where(path => !string.IsNullOrEmpty(path))
            .Distinct()
            .ToList();

        if (!string.IsNullOrEmpty(originalScenePath) && !scenePaths.Contains(originalScenePath))
            scenePaths.Add(originalScenePath);

        int installed = 0;
        foreach (string scenePath in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (InstallInScene(scene, true) == 0)
                continue;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            installed++;
        }

        if (!string.IsNullOrEmpty(originalScenePath))
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

        return installed;
    }

    public static int InstallInScene(Scene scene, bool removeLegacyHandTracking)
    {
        GameObject cameraOffset = FindInScene("Camera Offset", scene);
        if (cameraOffset == null)
            return 0;

        if (removeLegacyHandTracking)
        {
            RemoveLegacyHandTracking(cameraOffset.transform);
            RemoveLegacySceneObjects(scene);
        }

        RemoveControllerHands(scene);

        var leftModel = LoadModel(LeftHandModelGuid);
        var rightModel = LoadModel(RightHandModelGuid);

        var container = new GameObject(ContainerName);
        container.transform.SetParent(cameraOffset.transform, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;

        CreateHand(container.transform, true, XRNode.LeftHand, leftModel);
        CreateHand(container.transform, false, XRNode.RightHand, rightModel);

        return 1;
    }

    public static int RemoveControllerHands(Scene scene)
    {
        int removed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name != ContainerName)
                    continue;

                Object.DestroyImmediate(transform.gameObject);
                removed++;
                break;
            }
        }

        return removed;
    }

    private static void CreateHand(Transform parent, bool isLeftHand, XRNode node, GameObject model)
    {
        var root = new GameObject(isLeftHand ? "Left Controller Hand" : "Right Controller Hand");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var visual = root.AddComponent<ControllerVisual>();
        visual.isLeftHand = isLeftHand;
        visual.inputSource = node;
        visual.followControllerPose = true;
        visual.handModelPrefab = model;
        visual.positionOffset = new Vector3(0f, -0.015f, 0.055f);
        visual.rotationOffset = new Vector3(35f, 0f, 0f);
        visual.hideWhenControllerNotTracked = true;
        visual.buildPrimitiveFallback = model == null;

        var grabber = root.AddComponent<ControllerGrabber>();
        grabber.SetInputSource(node);
        grabber.grabPointLocalOffset = new Vector3(0f, 0f, 0.055f);
        grabber.grabRadius = 0.75f;
        grabber.useFloorGrabAssist = true;
        grabber.floorGrabRadius = 1.5f;
        grabber.floorGrabDownwardReach = 4f;
        grabber.grabThreshold = 0.55f;
        grabber.releaseThreshold = 0.35f;
        grabber.grabAnyRigidbody = true;
        grabber.requireGrabbableTag = true;
        grabber.grabbableTag = "Grabbable";
        grabber.showGrabPrompt = true;
        grabber.promptText = "Press Grip to pick up";
        grabber.promptRadius = 2f;
        grabber.promptDownwardReach = 4f;
        grabber.maxGrabMass = 20f;
        grabber.throwVelocityScale = 1f;
        grabber.throwAngularVelocityScale = 1f;

        if (model == null)
            return;

        GameObject instance = PrefabUtility.InstantiatePrefab(model, root.transform) as GameObject;
        if (instance == null)
            instance = Object.Instantiate(model, root.transform);

        instance.name = isLeftHand ? "Left Hand Mesh" : "Right Hand Mesh";
        instance.transform.localPosition = visual.modelLocalPosition;
        instance.transform.localRotation = Quaternion.Euler(visual.modelLocalEulerAngles);
        instance.transform.localScale = visual.modelLocalScale;
        visual.modelRoot = instance.transform;
    }

    private static GameObject LoadModel(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
            return null;

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void RemoveLegacyHandTracking(Transform cameraOffset)
    {
        foreach (string containerName in LegacyContainers)
            RemoveChildrenNamed(cameraOffset, containerName);

        foreach (string visualName in LegacyControllerVisuals)
            RemoveChildrenNamed(cameraOffset, visualName);
    }

    private static void RemoveLegacySceneObjects(Scene scene)
    {
        foreach (string objectName in LegacySceneObjects)
        {
            GameObject legacy = FindInScene(objectName, scene);
            while (legacy != null)
            {
                Object.DestroyImmediate(legacy);
                legacy = FindInScene(objectName, scene);
            }
        }
    }

    private static void RemoveChildrenNamed(Transform root, string childName)
    {
        while (true)
        {
            Transform child = FindRecursive(root, childName);
            if (child == null || child == root)
                return;

            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static GameObject FindInScene(string name, Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
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
}
