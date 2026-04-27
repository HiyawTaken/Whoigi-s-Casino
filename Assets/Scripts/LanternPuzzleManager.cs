using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRNode = UnityEngine.XR.XRNode;

[DisallowMultipleComponent]
public sealed class LanternPuzzleManager : MonoBehaviour
{
    private const string LanternSceneName = "Lantern Game";

    [Header("Puzzle")]
    public int tokenReward = 10;
    public int generatedMoveCount = 8;
    public int puzzleSeed = 32;
    public bool includeDiagonalNeighbors;

    [Header("Candles")]
    public float candleScaleMultiplier = 0.8f;
    public float candleHeightMultiplier = 0.75f;
    public float candleSpacingMultiplier = 1.45f;
    public float candleLightRange = 1.6f;
    public float candleLightIntensity = 1.8f;

    [Header("Dot Interaction")]
    public float maxInteractDistance = 35f;
    public float viewportTargetRadius = 0.055f;
    public XRNode pointerInputSource = XRNode.RightHand;
    public bool allowEitherHandInteract = true;
    public float handPointerTargetRadius = 0.5f;
    public float handPointerVisualDistance = 10f;
    public Vector3 handPointerEulerOffset = new Vector3(35f, 0f, 0f);
    public bool disableControllerGrabbers = true;

    [Header("Player")]
    public Vector3 playerSpawnPosition = new Vector3(27f, 7f, 39f);
    public Vector3 playerCameraOffset = new Vector3(0f, 1.1176f, 0f);
    public Vector3 playerCameraLocalPosition = new Vector3(0f, -0.5f, 0f);
    public Vector3 cameraSelectionReferencePoint = new Vector3(27f, 7.6f, 43.5f);

    [Header("Return Door")]
    public string returnSceneName = "Game";
    public Vector3 returnDoorPosition = new Vector3(23f, 7f, 42f);
    public Vector3 returnDoorEulerAngles = new Vector3(0f, 35f, 0f);
    public bool createFallbackReturnDoor;

    [Header("HUD")]
    public float hudDistance = 2.4f;
    public float hudVerticalOffset = -0.55f;
    public float hudScale = 0.0032f;

    private readonly List<LanternPuzzleCandle> candles = new List<LanternPuzzleCandle>();
    private LanternPuzzleCandle[,] grid;
    private LanternDotInteractor dotInteractor;
    private Canvas hudCanvas;
    private Text hudText;
    private bool rewardGiven;

    public Vector3 PuzzleCenter
    {
        get
        {
            if (candles.Count == 0)
            {
                return transform.position;
            }

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < candles.Count; i++)
            {
                sum += candles[i] != null ? candles[i].AimPoint : Vector3.zero;
            }

            return sum / candles.Count;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapActiveScene()
    {
        TryCreateForScene(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateForScene(scene);
    }

    private static void TryCreateForScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != LanternSceneName)
        {
            return;
        }

        LanternPuzzleManager existing = FindFirstObjectByType<LanternPuzzleManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("Lantern Puzzle Manager");
        managerObject.AddComponent<LanternPuzzleManager>();
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != LanternSceneName)
        {
            enabled = false;
            return;
        }

        EnsurePlayerRig();

        if (disableControllerGrabbers)
        {
            DisableGrabbersForPuzzle();
        }

        BuildCandleGrid();
        GenerateSolvablePuzzle();
        EnsureDotInteractor();
        EnsureHud();
        EnsureReturnDoor();
        UpdateHud();
    }

    private void LateUpdate()
    {
        PositionHud();
    }

    private void EnsurePlayerRig()
    {
        VRController existingController = FindFirstObjectByType<VRController>(FindObjectsInactive.Include);
        if (existingController != null)
        {
            Camera existingCamera = FindCameraFor(existingController);
            if (existingCamera != null)
            {
                ConfigureCamera(existingCamera);
                PrepareSceneCameras(existingCamera);
            }

            return;
        }

        Camera playerCamera = SelectBestExistingCamera();
        GameObject playerRoot = new GameObject("XR Origin (VR)");
        TrySetTag(playerRoot, "Player");
        playerRoot.transform.position = playerSpawnPosition;
        playerRoot.transform.rotation = Quaternion.identity;

        CapsuleCollider capsule = playerRoot.AddComponent<CapsuleCollider>();
        capsule.radius = 0.5f;
        capsule.height = 1f;
        capsule.direction = 1;
        capsule.center = Vector3.zero;

        CharacterController characterController = playerRoot.AddComponent<CharacterController>();
        characterController.radius = 0.5f;
        characterController.height = 2f;
        characterController.slopeLimit = 45f;
        characterController.stepOffset = 0.3f;
        characterController.skinWidth = 0.08f;
        characterController.minMoveDistance = 0.001f;
        characterController.center = Vector3.zero;

        Rigidbody rigidbody = playerRoot.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;

        GameObject cameraOffset = new GameObject("Camera Offset");
        cameraOffset.transform.SetParent(playerRoot.transform, false);
        cameraOffset.transform.localPosition = playerCameraOffset;
        cameraOffset.transform.localRotation = Quaternion.identity;
        cameraOffset.transform.localScale = Vector3.one;

        if (playerCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            playerCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        playerCamera.transform.SetParent(cameraOffset.transform, false);
        playerCamera.transform.localPosition = playerCameraLocalPosition;
        playerCamera.transform.localRotation = Quaternion.identity;
        playerCamera.transform.localScale = Vector3.one;
        ConfigureCamera(playerCamera);

        VRController vrController = playerRoot.AddComponent<VRController>();
        vrController.inputSource = XRNode.LeftHand;
        vrController.sprintInputSource = XRNode.LeftHand;
        vrController.turnInputSource = XRNode.RightHand;
        vrController.headTransform = playerCamera.transform;

        EnsureControllerHandVisuals(cameraOffset.transform);
        PrepareSceneCameras(playerCamera);
    }

    private Camera FindCameraFor(VRController controller)
    {
        if (controller == null)
        {
            return null;
        }

        if (controller.headTransform != null)
        {
            Camera headCamera = controller.headTransform.GetComponent<Camera>();
            if (headCamera != null)
            {
                return headCamera;
            }
        }

        Camera childCamera = controller.GetComponentInChildren<Camera>(true);
        return childCamera != null ? childCamera : SelectBestExistingCamera();
    }

    private Camera SelectBestExistingCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera bestCamera = null;
        float bestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            float sqrDistance = (camera.transform.position - cameraSelectionReferencePoint).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestCamera = camera;
                bestSqrDistance = sqrDistance;
            }
        }

        return bestCamera;
    }

    private void ConfigureCamera(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        camera.gameObject.SetActive(true);
        camera.enabled = true;
        camera.nearClipPlane = 0.01f;
        TrySetTag(camera.gameObject, "MainCamera");

        if (camera.GetComponent<AudioListener>() == null)
        {
            camera.gameObject.AddComponent<AudioListener>();
        }
    }

    private void PrepareSceneCameras(Camera playerCamera)
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            bool isPlayerCamera = camera == playerCamera;
            camera.enabled = isPlayerCamera;
            TrySetTag(camera.gameObject, isPlayerCamera ? "MainCamera" : "Untagged");

            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = isPlayerCamera;
            }
        }
    }

    private void EnsureControllerHandVisuals(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        GameObject handsRoot = new GameObject("__ControllerHands");
        handsRoot.transform.SetParent(parent, false);
        handsRoot.transform.localPosition = Vector3.zero;
        handsRoot.transform.localRotation = Quaternion.identity;
        handsRoot.transform.localScale = Vector3.one;

        CreateControllerHandVisual(handsRoot.transform, true, "Left Controller Hand");
        CreateControllerHandVisual(handsRoot.transform, false, "Right Controller Hand");
    }

    private void CreateControllerHandVisual(Transform parent, bool isLeftHand, string name)
    {
        GameObject hand = new GameObject(name);
        hand.SetActive(false);
        hand.transform.SetParent(parent, false);
        hand.transform.localPosition = Vector3.zero;
        hand.transform.localRotation = Quaternion.identity;
        hand.transform.localScale = Vector3.one;

        ControllerVisual visual = hand.AddComponent<ControllerVisual>();
        visual.isLeftHand = isLeftHand;
        visual.inputSource = isLeftHand ? XRNode.LeftHand : XRNode.RightHand;
        visual.followControllerPose = true;
        visual.hideWhenControllerNotTracked = true;
        visual.handModelPrefab = null;
        visual.modelRoot = null;
        visual.modelLocalPosition = Vector3.zero;
        visual.modelLocalEulerAngles = Vector3.zero;
        visual.modelLocalScale = Vector3.one;
        visual.buildPrimitiveFallback = true;
        visual.addGrabber = false;

        hand.SetActive(true);
    }

    private void TrySetTag(GameObject target, string tagName)
    {
        if (target == null || string.IsNullOrEmpty(tagName))
        {
            return;
        }

        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
        }
    }

    public LanternPuzzleCandle FindTarget(Camera camera)
    {
        if (camera == null)
        {
            return null;
        }

        return FindTarget(new Ray(camera.transform.position, camera.transform.forward), out _);
    }

    public LanternPuzzleCandle FindTarget(Ray pointerRay, out Vector3 targetPoint)
    {
        targetPoint = pointerRay.origin + pointerRay.direction * Mathf.Min(maxInteractDistance, handPointerVisualDistance);
        LanternPuzzleCandle best = null;
        float bestScore = float.PositiveInfinity;
        Vector3 rayDirection = pointerRay.direction.normalized;

        for (int i = 0; i < candles.Count; i++)
        {
            LanternPuzzleCandle candle = candles[i];
            if (candle == null)
            {
                continue;
            }

            Vector3 worldPoint = candle.AimPoint;
            Vector3 toCandle = worldPoint - pointerRay.origin;
            float forwardDistance = Vector3.Dot(rayDirection, toCandle);
            if (forwardDistance <= 0f || forwardDistance > maxInteractDistance)
            {
                continue;
            }

            float perpendicularSqrDistance = Mathf.Max(0f, toCandle.sqrMagnitude - forwardDistance * forwardDistance);
            float allowedRadius = Mathf.Max(handPointerTargetRadius, candle.PointerRadius);
            if (perpendicularSqrDistance > allowedRadius * allowedRadius)
            {
                continue;
            }

            float score = perpendicularSqrDistance + forwardDistance * 0.0025f;
            if (score >= bestScore)
            {
                continue;
            }

            best = candle;
            bestScore = score;
            targetPoint = worldPoint;
        }

        return best;
    }

    public void InteractWith(LanternPuzzleCandle candle)
    {
        if (candle == null || rewardGiven)
        {
            return;
        }

        FlipMove(candle.Row, candle.Column);
        UpdateHud();

        if (AllCandlesLit())
        {
            WinPuzzle();
        }
    }

    private void BuildCandleGrid()
    {
        candles.Clear();

        LanternController[] controllers = FindObjectsByType<LanternController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            LanternController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            LanternPuzzleCandle candle = controller.GetComponent<LanternPuzzleCandle>();
            if (candle == null)
            {
                candle = controller.gameObject.AddComponent<LanternPuzzleCandle>();
            }

            candle.Configure(controller, this, candleScaleMultiplier, candleHeightMultiplier);
            EnsurePointLight(controller);
            candles.Add(candle);
        }

        int columns = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(candles.Count)));
        int rows = Mathf.CeilToInt(candles.Count / (float)columns);
        grid = new LanternPuzzleCandle[rows, columns];

        candles.Sort(CompareByZThenX);
        for (int row = 0; row < rows; row++)
        {
            int rowStart = row * columns;
            int rowCount = Mathf.Min(columns, candles.Count - rowStart);
            if (rowCount <= 0)
            {
                break;
            }

            candles.Sort(rowStart, rowCount, Comparer<LanternPuzzleCandle>.Create(CompareByX));

            for (int column = 0; column < rowCount; column++)
            {
                LanternPuzzleCandle candle = candles[rowStart + column];
                candle.SetGridPosition(row, column);
                grid[row, column] = candle;
            }
        }

        SpaceCandlesFromCenter();
    }

    private void SpaceCandlesFromCenter()
    {
        if (candles.Count == 0 || Mathf.Approximately(candleSpacingMultiplier, 1f))
        {
            return;
        }

        Vector3 center = Vector3.zero;
        int liveCount = 0;
        for (int i = 0; i < candles.Count; i++)
        {
            if (candles[i] == null)
            {
                continue;
            }

            center += candles[i].transform.position;
            liveCount++;
        }

        if (liveCount == 0)
        {
            return;
        }

        center /= liveCount;

        for (int i = 0; i < candles.Count; i++)
        {
            LanternPuzzleCandle candle = candles[i];
            if (candle == null)
            {
                continue;
            }

            Vector3 position = candle.transform.position;
            Vector3 offset = position - center;
            candle.transform.position = new Vector3(
                center.x + offset.x * candleSpacingMultiplier,
                position.y,
                center.z + offset.z * candleSpacingMultiplier);
        }
    }

    private int CompareByZThenX(LanternPuzzleCandle a, LanternPuzzleCandle b)
    {
        int zCompare = a.transform.position.z.CompareTo(b.transform.position.z);
        return zCompare != 0 ? zCompare : a.transform.position.x.CompareTo(b.transform.position.x);
    }

    private int CompareByX(LanternPuzzleCandle a, LanternPuzzleCandle b)
    {
        return a.transform.position.x.CompareTo(b.transform.position.x);
    }

    private void GenerateSolvablePuzzle()
    {
        rewardGiven = false;

        for (int i = 0; i < candles.Count; i++)
        {
            candles[i].SetLit(true);
        }

        if (grid == null || candles.Count == 0)
        {
            return;
        }

        System.Random random = new System.Random(puzzleSeed);
        int rows = grid.GetLength(0);
        int columns = grid.GetLength(1);

        for (int i = 0; i < generatedMoveCount; i++)
        {
            int row = random.Next(0, rows);
            int column = random.Next(0, columns);
            if (grid[row, column] != null)
            {
                FlipMove(row, column);
            }
        }

        if (AllCandlesLit() && grid[rows / 2, columns / 2] != null)
        {
            FlipMove(rows / 2, columns / 2);
        }
    }

    private void FlipMove(int row, int column)
    {
        ToggleGridCandle(row, column);
        ToggleGridCandle(row - 1, column);
        ToggleGridCandle(row + 1, column);
        ToggleGridCandle(row, column - 1);
        ToggleGridCandle(row, column + 1);

        if (!includeDiagonalNeighbors)
        {
            return;
        }

        ToggleGridCandle(row - 1, column - 1);
        ToggleGridCandle(row - 1, column + 1);
        ToggleGridCandle(row + 1, column - 1);
        ToggleGridCandle(row + 1, column + 1);
    }

    private void ToggleGridCandle(int row, int column)
    {
        if (grid == null ||
            row < 0 ||
            column < 0 ||
            row >= grid.GetLength(0) ||
            column >= grid.GetLength(1))
        {
            return;
        }

        LanternPuzzleCandle candle = grid[row, column];
        if (candle != null)
        {
            candle.ToggleLit();
        }
    }

    private bool AllCandlesLit()
    {
        if (candles.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < candles.Count; i++)
        {
            if (candles[i] != null && !candles[i].IsLit)
            {
                return false;
            }
        }

        return true;
    }

    private void WinPuzzle()
    {
        rewardGiven = true;
        PlayerData playerData = EnsurePlayerData();
        if (playerData != null)
        {
            playerData.AddTokens(tokenReward);
        }

        UpdateHud();
    }

    private PlayerData EnsurePlayerData()
    {
        return PlayerData.EnsureExists();
    }

    private void EnsurePointLight(LanternController controller)
    {
        if (controller.pointLight == null)
        {
            Light existingLight = controller.GetComponentInChildren<Light>(true);
            if (existingLight != null)
            {
                controller.pointLight = existingLight.gameObject;
            }
        }

        if (controller.pointLight == null)
        {
            GameObject lightObject = new GameObject("Puzzle Candle Light");
            lightObject.transform.SetParent(controller.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.64f, 0.24f, 1f);
            controller.pointLight = lightObject;
        }

        Light pointLight = controller.pointLight.GetComponent<Light>();
        if (pointLight != null)
        {
            pointLight.range = candleLightRange;
            pointLight.intensity = candleLightIntensity;
        }
    }

    private void DisableGrabbersForPuzzle()
    {
        ControllerGrabber[] grabbers = FindObjectsByType<ControllerGrabber>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < grabbers.Length; i++)
        {
            grabbers[i].enabled = false;
        }
    }

    private void EnsureDotInteractor()
    {
        dotInteractor = GetComponent<LanternDotInteractor>();
        if (dotInteractor == null)
        {
            dotInteractor = gameObject.AddComponent<LanternDotInteractor>();
        }

        dotInteractor.manager = this;
        dotInteractor.maxInteractDistance = maxInteractDistance;
        dotInteractor.pointerInputSource = pointerInputSource;
        dotInteractor.allowEitherHandInteract = allowEitherHandInteract;
        dotInteractor.pointerVisualDistance = handPointerVisualDistance;
        dotInteractor.pointerLocalEulerOffset = handPointerEulerOffset;
    }

    private void EnsureHud()
    {
        GameObject canvasObject = new GameObject("Lantern Puzzle HUD");
        hudCanvas = canvasObject.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.WorldSpace;
        hudCanvas.sortingOrder = 70;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        RectTransform canvasRect = canvasObject.transform as RectTransform;
        canvasRect.sizeDelta = new Vector2(720f, 120f);

        Image background = CreateImage("HUD Background", canvasRect, new Color(0f, 0f, 0f, 0.5f));
        StretchToParent(background.rectTransform);

        hudText = CreateText("Puzzle Status", canvasRect, 36, TextAnchor.MiddleCenter);
        hudText.color = new Color(1f, 0.88f, 0.35f, 1f);
        hudText.resizeTextForBestFit = true;
        hudText.resizeTextMinSize = 18;
        hudText.resizeTextMaxSize = 38;
        StretchToParent(hudText.rectTransform);
        hudText.rectTransform.offsetMin = new Vector2(24f, 10f);
        hudText.rectTransform.offsetMax = new Vector2(-24f, -10f);
    }

    private void EnsureReturnDoor()
    {
        LanternReturnDoor existingDoor = FindFirstObjectByType<LanternReturnDoor>(FindObjectsInactive.Include);
        if (existingDoor != null)
        {
            ConfigureLanternReturnDoor(existingDoor.gameObject);
            return;
        }

        GameObject placedDoor = FindPlacedReturnDoorCandidate();
        if (placedDoor != null)
        {
            ConfigureLanternReturnDoor(placedDoor);
            return;
        }

        if (!createFallbackReturnDoor)
        {
            Debug.LogWarning("No placed Lantern return door was found. Name or tag the door with Door, Exit, Return, Portal, Casino, Back, or leave a nearby primitive named Cube.");
            return;
        }

        GameObject doorRoot = new GameObject("Return To Casino Door");
        doorRoot.transform.position = returnDoorPosition;
        doorRoot.transform.rotation = Quaternion.Euler(returnDoorEulerAngles);
        doorRoot.transform.localScale = Vector3.one;

        ConfigureLanternReturnDoor(doorRoot);

        Material frameMaterial = CreateRuntimeMaterial(new Color(0.18f, 0.8f, 1f, 1f), true);
        Material portalMaterial = CreateRuntimeMaterial(new Color(0.08f, 0.26f, 0.5f, 0.82f), true);
        Material beaconMaterial = CreateRuntimeMaterial(new Color(1f, 0.9f, 0.18f, 1f), true);

        CreateDoorPart("Left Frame", doorRoot.transform, new Vector3(-2f, 1.55f, 0f), new Vector3(0.22f, 3.1f, 0.24f), frameMaterial);
        CreateDoorPart("Right Frame", doorRoot.transform, new Vector3(2f, 1.55f, 0f), new Vector3(0.22f, 3.1f, 0.24f), frameMaterial);
        CreateDoorPart("Top Frame", doorRoot.transform, new Vector3(0f, 3f, 0f), new Vector3(4.2f, 0.22f, 0.24f), frameMaterial);
        CreateDoorPart("Portal Surface", doorRoot.transform, new Vector3(0f, 1.55f, 0.05f), new Vector3(3.45f, 2.75f, 0.05f), portalMaterial);
        CreateDoorPart("Return Beacon", doorRoot.transform, new Vector3(0f, 4.8f, 0f), new Vector3(0.35f, 2.6f, 0.35f), beaconMaterial);

        Transform existingLabel = doorRoot.transform.Find("Return Door Label");
        GameObject labelObject = existingLabel != null ? existingLabel.gameObject : new GameObject("Return Door Label");
        labelObject.transform.SetParent(doorRoot.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 3.45f, -0.08f);
        labelObject.transform.localRotation = Quaternion.identity;
        labelObject.transform.localScale = Vector3.one;

        TextMesh label = labelObject.GetComponent<TextMesh>();
        if (label == null)
        {
            label = labelObject.AddComponent<TextMesh>();
        }

        label.text = "Back to Casino";
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 64;
        label.characterSize = 0.11f;
        label.color = new Color(1f, 0.92f, 0.42f, 1f);

        MeshRenderer labelRenderer = labelObject.GetComponent<MeshRenderer>();
        if (labelRenderer != null)
        {
            labelRenderer.sortingOrder = 50;
        }

        Light doorLight = doorRoot.AddComponent<Light>();
        doorLight.type = LightType.Point;
        doorLight.color = new Color(0.3f, 0.85f, 1f, 1f);
        doorLight.range = 7f;
        doorLight.intensity = 2.5f;
    }

    private LanternReturnDoor ConfigureLanternReturnDoor(GameObject doorObject)
    {
        if (doorObject == null)
        {
            return null;
        }

        LanternReturnDoor returnDoor = doorObject.GetComponent<LanternReturnDoor>();
        if (returnDoor == null)
        {
            returnDoor = doorObject.AddComponent<LanternReturnDoor>();
        }

        Vector3 triggerCenter = Vector3.up * 1.5f;
        Vector3 triggerSize = new Vector3(4.6f, 3.3f, 1.8f);
        if (TryGetRendererBounds(doorObject, out Bounds bounds))
        {
            triggerCenter = doorObject.transform.InverseTransformPoint(bounds.center);
            triggerSize = WorldSizeToLocal(doorObject.transform, new Vector3(
                Mathf.Max(4.6f, bounds.size.x + 1.4f),
                Mathf.Max(3.3f, bounds.size.y + 0.8f),
                Mathf.Max(1.8f, bounds.size.z + 1.2f)));
        }

        returnDoor.Configure(returnSceneName, triggerCenter, triggerSize);
        EnsureReturnDoorLabel(doorObject.transform);
        return returnDoor;
    }

    private GameObject FindPlacedReturnDoorCandidate()
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject bestCandidate = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidateTransform = transforms[i];
            if (candidateTransform == null)
            {
                continue;
            }

            GameObject candidate = candidateTransform.gameObject;
            if (!candidate.scene.IsValid() || candidate.scene.name != LanternSceneName)
            {
                continue;
            }

            if (!TryScorePlacedReturnDoorCandidate(candidate, out int score))
            {
                continue;
            }

            float distancePenalty = Vector3.Distance(candidateTransform.position, playerSpawnPosition) * 0.35f;
            score -= Mathf.RoundToInt(distancePenalty);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }

    private bool TryScorePlacedReturnDoorCandidate(GameObject candidate, out int score)
    {
        score = 0;
        if (candidate == null || candidate == gameObject)
        {
            return false;
        }

        string objectName = candidate.name != null ? candidate.name.ToLowerInvariant() : string.Empty;
        string tagName = candidate.tag != null ? candidate.tag.ToLowerInvariant() : string.Empty;

        if (objectName.Contains("return") || objectName.Contains("exit") || objectName.Contains("door") ||
            objectName.Contains("portal") || objectName.Contains("casino") || objectName.Contains("back"))
        {
            score += 120;
        }

        if (tagName.Contains("return") || tagName.Contains("exit") || tagName.Contains("door") ||
            tagName.Contains("portal") || tagName.Contains("casino"))
        {
            score += 100;
        }

        if (objectName == "cube" || objectName.StartsWith("cube "))
        {
            score += 70;
        }

        if (IsNearPlayerReturnSide(candidate))
        {
            score += 55;
        }

        if (score <= 0)
        {
            return false;
        }

        if (objectName.Contains("candle") || objectName.Contains("tree") || objectName.Contains("floor") ||
            objectName.Contains("table") || objectName.Contains("controller") || objectName.Contains("hand") ||
            objectName.Contains("camera") || objectName.Contains("light") || objectName.Contains("eventsystem") ||
            objectName.Contains("canvas") || objectName.Contains("music") || objectName.Contains("hud") ||
            objectName.Contains("puzzle"))
        {
            return false;
        }

        if (candidate.GetComponentInParent<LanternPuzzleCandle>() != null ||
            candidate.GetComponentInParent<VRController>() != null ||
            candidate.GetComponentInParent<ControllerVisual>() != null ||
            candidate.GetComponentInParent<ControllerGrabber>() != null ||
            candidate.GetComponentInParent<Canvas>() != null)
        {
            return false;
        }

        Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>(false);
        Collider[] colliders = candidate.GetComponentsInChildren<Collider>(false);
        if (renderers.Length == 0 && colliders.Length == 0)
        {
            return false;
        }

        score += Mathf.Min(25, renderers.Length * 5 + colliders.Length * 3);
        return true;
    }

    private bool IsNearPlayerReturnSide(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Vector3 offset = candidate.transform.position - playerSpawnPosition;
        return Mathf.Abs(offset.x) <= 7f &&
               Mathf.Abs(offset.y) <= 8f &&
               offset.z <= -0.35f &&
               offset.z >= -12f;
    }

    private void EnsureReturnDoorLabel(Transform doorTransform)
    {
        if (doorTransform == null || doorTransform.Find("Return Door Label") != null)
        {
            return;
        }

        GameObject labelObject = new GameObject("Return Door Label");
        labelObject.transform.SetParent(doorTransform, false);
        labelObject.transform.localPosition = Vector3.up * 2.4f;
        labelObject.transform.localRotation = Quaternion.identity;
        labelObject.transform.localScale = Vector3.one;

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = "Back to Casino";
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 64;
        label.characterSize = 0.1f;
        label.color = new Color(1f, 0.92f, 0.42f, 1f);
    }

    private bool TryGetRendererBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        if (target == null)
        {
            return false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private Vector3 WorldSizeToLocal(Transform target, Vector3 worldSize)
    {
        Vector3 scale = target != null ? target.lossyScale : Vector3.one;
        return new Vector3(
            worldSize.x / Mathf.Max(0.01f, Mathf.Abs(scale.x)),
            worldSize.y / Mathf.Max(0.01f, Mathf.Abs(scale.y)),
            worldSize.z / Mathf.Max(0.01f, Mathf.Abs(scale.z)));
    }

    private void CreateDoorPart(string objectName, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = objectName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private Material CreateRuntimeMaterial(Color color, bool emissive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null || shader.name == "Hidden/InternalErrorShader")
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;

        if (emissive && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.6f);
        }

        return material;
    }

    private void UpdateHud()
    {
        if (hudText == null)
        {
            return;
        }

        if (rewardGiven)
        {
            hudText.text = $"+{tokenReward} Tokens";
            return;
        }

        hudText.text = $"Lights On: {CountLitCandles()}/{candles.Count}";
    }

    private int CountLitCandles()
    {
        int litCount = 0;
        for (int i = 0; i < candles.Count; i++)
        {
            if (candles[i] != null && candles[i].IsLit)
            {
                litCount++;
            }
        }

        return litCount;
    }

    private void PositionHud()
    {
        Camera camera = GetBestCamera();
        if (hudCanvas == null || camera == null)
        {
            return;
        }

        Transform cameraTransform = camera.transform;
        Transform canvasTransform = hudCanvas.transform;
        canvasTransform.position = cameraTransform.position +
                                   cameraTransform.forward * hudDistance +
                                   Vector3.up * hudVerticalOffset;
        canvasTransform.rotation = Quaternion.LookRotation(canvasTransform.position - cameraTransform.position, Vector3.up);
        canvasTransform.localScale = Vector3.one * hudScale;
    }

    public Camera GetBestCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Camera bestCamera = null;
        float bestSqrDistance = float.PositiveInfinity;
        Vector3 center = PuzzleCenter;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null || !candidate.enabled)
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - center).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestCamera = candidate;
                bestSqrDistance = sqrDistance;
            }
        }

        return bestCamera != null ? bestCamera : Camera.main;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text CreateText(string objectName, Transform parent, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = GetBuiltinFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }
}

[DisallowMultipleComponent]
public sealed class LanternPuzzleCandle : MonoBehaviour
{
    private const string VisibleMarkerName = "Puzzle Candle Visible Marker";

    private static Material s_MarkerBodyMaterial;
    private static Material s_MarkerLitMaterial;
    private static Material s_MarkerUnlitMaterial;

    private LanternController controller;
    private Renderer[] renderers;
    private Renderer markerFlameRenderer;
    private Light markerLight;
    private Vector3 originalScale;

    public int Row { get; private set; }
    public int Column { get; private set; }
    public bool IsLit => controller != null && controller.isLit;
    public float PointerRadius
    {
        get
        {
            if (TryGetBounds(out Bounds bounds))
            {
                return Mathf.Clamp(bounds.extents.magnitude * 0.28f, 0.35f, 1.4f);
            }

            return 0.65f;
        }
    }

    public Vector3 AimPoint
    {
        get
        {
            if (TryGetBounds(out Bounds bounds))
            {
                return bounds.center;
            }

            return transform.position + Vector3.up * 0.25f;
        }
    }

    public void Configure(LanternController targetController, LanternPuzzleManager manager, float scaleMultiplier, float heightMultiplier)
    {
        controller = targetController;
        RefreshRenderers();

        if (originalScale == Vector3.zero)
        {
            originalScale = transform.localScale;
        }

        float safeScale = Mathf.Max(0.25f, scaleMultiplier);
        float safeHeight = Mathf.Max(0.25f, heightMultiplier);
        transform.localScale = new Vector3(
            originalScale.x * safeScale,
            originalScale.y * safeScale * safeHeight,
            originalScale.z * safeScale);
        EnsureVisibleMarker();
        EnableRenderers();
        controller.RefreshVisualState();
        ApplyPuzzleVisualState();
    }

    public void SetGridPosition(int row, int column)
    {
        Row = row;
        Column = column;
    }

    public void SetLit(bool lit)
    {
        controller?.SetLit(lit);
        ApplyPuzzleVisualState();
    }

    public void ToggleLit()
    {
        if (controller != null)
        {
            controller.ToggleLantern();
            ApplyPuzzleVisualState();
        }
    }

    private void RefreshRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void EnableRenderers()
    {
        RefreshRenderers();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
            }
        }
    }

    private void EnsureVisibleMarker()
    {
        if (markerFlameRenderer != null)
        {
            return;
        }

        Transform marker = transform.Find(VisibleMarkerName);
        if (marker == null)
        {
            marker = new GameObject(VisibleMarkerName).transform;
            marker.SetParent(transform, false);
            marker.localPosition = Vector3.zero;
            marker.localRotation = Quaternion.identity;
            marker.localScale = Vector3.one;

            Transform body = CreateMarkerPrimitive("Visible Candle Body", PrimitiveType.Cylinder, marker, new Vector3(0f, 0.24f, 0f), new Vector3(0.16f, 0.24f, 0.16f));
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.sharedMaterial = GetMarkerBodyMaterial();
            }

            Transform wick = CreateMarkerPrimitive("Visible Candle Wick", PrimitiveType.Cube, marker, new Vector3(0f, 0.52f, 0f), new Vector3(0.035f, 0.12f, 0.035f));
            Renderer wickRenderer = wick.GetComponent<Renderer>();
            if (wickRenderer != null)
            {
                wickRenderer.sharedMaterial = GetMarkerUnlitMaterial();
            }

            Transform flame = CreateMarkerPrimitive("Visible Candle Flame", PrimitiveType.Sphere, marker, new Vector3(0f, 0.68f, 0f), new Vector3(0.18f, 0.25f, 0.18f));
            markerFlameRenderer = flame.GetComponent<Renderer>();

            GameObject lightObject = new GameObject("Visible Candle Glow");
            lightObject.transform.SetParent(marker, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.68f, 0f);
            markerLight = lightObject.AddComponent<Light>();
            markerLight.type = LightType.Point;
            markerLight.color = new Color(1f, 0.65f, 0.18f, 1f);
            markerLight.range = 1.1f;
            markerLight.intensity = 0.75f;
        }

        if (markerFlameRenderer == null)
        {
            markerFlameRenderer = marker.GetComponentInChildren<Renderer>(true);
        }

        if (markerLight == null)
        {
            markerLight = marker.GetComponentInChildren<Light>(true);
        }

        RefreshRenderers();
    }

    private Transform CreateMarkerPrimitive(string objectName, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale)
    {
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = objectName;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localRotation = Quaternion.identity;
        primitive.transform.localScale = localScale;

        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        return primitive.transform;
    }

    private void ApplyPuzzleVisualState()
    {
        EnsureVisibleMarker();

        if (markerFlameRenderer != null)
        {
            markerFlameRenderer.sharedMaterial = IsLit ? GetMarkerLitMaterial() : GetMarkerUnlitMaterial();
            markerFlameRenderer.enabled = true;
        }

        if (markerLight != null)
        {
            markerLight.gameObject.SetActive(IsLit);
        }
    }

    private static Material GetMarkerBodyMaterial()
    {
        if (s_MarkerBodyMaterial == null)
        {
            s_MarkerBodyMaterial = CreateMarkerMaterial(new Color(1f, 0.92f, 0.76f, 1f), false);
        }

        return s_MarkerBodyMaterial;
    }

    private static Material GetMarkerLitMaterial()
    {
        if (s_MarkerLitMaterial == null)
        {
            s_MarkerLitMaterial = CreateMarkerMaterial(new Color(1f, 0.55f, 0.1f, 1f), true);
        }

        return s_MarkerLitMaterial;
    }

    private static Material GetMarkerUnlitMaterial()
    {
        if (s_MarkerUnlitMaterial == null)
        {
            s_MarkerUnlitMaterial = CreateMarkerMaterial(new Color(0.12f, 0.13f, 0.16f, 1f), false);
        }

        return s_MarkerUnlitMaterial;
    }

    private static Material CreateMarkerMaterial(Color color, bool emissive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null || shader.name == "Hidden/InternalErrorShader")
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;

        if (emissive && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.75f);
        }

        return material;
    }

    private bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }
}

[DisallowMultipleComponent]
public sealed class LanternDotInteractor : MonoBehaviour
{
    [NonSerialized] public LanternPuzzleManager manager;

    public float maxInteractDistance = 35f;
    public float dotScale = 0.0018f;
    public XRNode pointerInputSource = XRNode.RightHand;
    public bool allowEitherHandInteract = true;
    public Vector3 pointerLocalOffset = new Vector3(0f, 0f, 0.08f);
    public Vector3 pointerLocalEulerOffset = new Vector3(35f, 0f, 0f);
    public float pointerVisualDistance = 10f;
    public float pointerLineWidth = 0.025f;
    public Color normalColor = new Color(1f, 1f, 1f, 0.9f);
    public Color targetColor = new Color(1f, 0.78f, 0.2f, 1f);

    private Camera targetCamera;
    private Canvas dotCanvas;
    private Image dotImage;
    private LineRenderer pointerLine;
    private Material pointerMaterial;
    private Transform pointerSource;
    private ControllerVisual pointerVisual;
    private GameObject fallbackPointerSource;
    private XRInputDevice pointerDevice;
    private XRNode currentPointerNode;
    private bool previousInputPressed;

    private void Start()
    {
        EnsureDot();
    }

    private void OnDestroy()
    {
        if (pointerMaterial != null)
        {
            Destroy(pointerMaterial);
        }
    }

    private void Update()
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<LanternPuzzleManager>(FindObjectsInactive.Include);
        }

        targetCamera = manager != null ? manager.GetBestCamera() : Camera.main;

        EnsureDot();
        if (PauseMenu.GameIsPaused)
        {
            SetPointerVisible(false);
            previousInputPressed = ReadInteractPressed();
            return;
        }

        SetPointerVisible(true);
        Transform source = ResolvePointerSource();
        Ray pointerRay = BuildPointerRay(source);

        Vector3 pointerEnd = pointerRay.origin + pointerRay.direction * Mathf.Min(maxInteractDistance, pointerVisualDistance);
        LanternPuzzleCandle target = manager != null ? manager.FindTarget(pointerRay, out pointerEnd) : null;
        if (target == null)
        {
            pointerEnd = pointerRay.origin + pointerRay.direction * Mathf.Min(maxInteractDistance, pointerVisualDistance);
        }

        PositionPointer(pointerRay, pointerEnd, target != null);
        if (dotImage != null)
        {
            dotImage.color = target != null ? targetColor : normalColor;
        }

        bool inputPressed = ReadInteractPressed();
        if (target != null && inputPressed && !previousInputPressed)
        {
            manager.InteractWith(target);
        }

        previousInputPressed = inputPressed;
    }

    private void SetPointerVisible(bool visible)
    {
        if (dotCanvas != null && dotCanvas.gameObject.activeSelf != visible)
        {
            dotCanvas.gameObject.SetActive(visible);
        }

        if (pointerLine != null)
        {
            pointerLine.enabled = visible;
        }
    }

    private bool ReadInteractPressed()
    {
        return ReadDevicePressed(pointerInputSource) ||
               (allowEitherHandInteract && ReadDevicePressed(GetOtherHand(pointerInputSource))) ||
               (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.isPressed) ||
               (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed);
    }

    private XRNode GetOtherHand(XRNode node)
    {
        return node == XRNode.LeftHand ? XRNode.RightHand : XRNode.LeftHand;
    }

    private bool ReadDevicePressed(XRNode node)
    {
        XRInputDevice device = XRInputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
        {
            return false;
        }

        if (device.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool triggerButton) && triggerButton)
        {
            return true;
        }

        if (device.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool primaryButton) && primaryButton)
        {
            return true;
        }

        if (device.TryGetFeatureValue(XRCommonUsages.gripButton, out bool gripButton) && gripButton)
        {
            return true;
        }

        return device.TryGetFeatureValue(XRCommonUsages.grip, out float grip) && grip >= 0.55f;
    }

    private Transform ResolvePointerSource()
    {
        if (pointerSource != null && pointerSource.gameObject.activeInHierarchy)
        {
            return pointerSource;
        }

        pointerVisual = null;
        ControllerVisual[] visuals = FindObjectsByType<ControllerVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        ControllerVisual bestVisual = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < visuals.Length; i++)
        {
            ControllerVisual visual = visuals[i];
            if (visual == null)
            {
                continue;
            }

            int score = 0;
            if (visual.inputSource == pointerInputSource ||
                (pointerInputSource == XRNode.LeftHand && visual.isLeftHand) ||
                (pointerInputSource == XRNode.RightHand && !visual.isLeftHand))
            {
                score += 10;
            }

            if (visual.gameObject.activeInHierarchy)
            {
                score += 3;
            }

            if (visual.enabled)
            {
                score += 2;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestVisual = visual;
            }
        }

        if (bestVisual != null)
        {
            pointerVisual = bestVisual;
            pointerSource = bestVisual.transform;
            return pointerSource;
        }

        ControllerGrabber[] grabbers = FindObjectsByType<ControllerGrabber>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < grabbers.Length; i++)
        {
            if (grabbers[i] != null && grabbers[i].inputSource == pointerInputSource)
            {
                pointerSource = grabbers[i].transform;
                return pointerSource;
            }
        }

        pointerSource = UpdateFallbackPointerSource();
        return pointerSource;
    }

    private Transform UpdateFallbackPointerSource()
    {
        if (fallbackPointerSource == null)
        {
            fallbackPointerSource = new GameObject("Lantern Fallback Hand Pointer");
        }

        Transform parent = targetCamera != null && targetCamera.transform.parent != null ? targetCamera.transform.parent : null;
        if (fallbackPointerSource.transform.parent != parent)
        {
            fallbackPointerSource.transform.SetParent(parent, false);
        }

        RefreshPointerDevice();
        if (pointerDevice.isValid &&
            pointerDevice.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 position) &&
            pointerDevice.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion rotation))
        {
            fallbackPointerSource.transform.localPosition = position;
            fallbackPointerSource.transform.localRotation = rotation;
            return fallbackPointerSource.transform;
        }

        return targetCamera != null ? targetCamera.transform : transform;
    }

    private void RefreshPointerDevice()
    {
        if (pointerDevice.isValid && currentPointerNode == pointerInputSource)
        {
            return;
        }

        pointerDevice = XRInputDevices.GetDeviceAtXRNode(pointerInputSource);
        currentPointerNode = pointerInputSource;
    }

    private Ray BuildPointerRay(Transform source)
    {
        if (source == null)
        {
            Transform fallback = targetCamera != null ? targetCamera.transform : transform;
            return new Ray(fallback.position, fallback.forward);
        }

        Vector3 origin = source.TransformPoint(pointerLocalOffset);
        Quaternion rotation = source.rotation;

        if (pointerVisual != null && pointerVisual.transform == source)
        {
            rotation *= Quaternion.Inverse(Quaternion.Euler(pointerVisual.rotationOffset));
        }

        rotation *= Quaternion.Euler(pointerLocalEulerOffset);
        Vector3 direction = rotation * Vector3.forward;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            direction = targetCamera != null ? targetCamera.transform.forward : transform.forward;
        }

        return new Ray(origin, direction.normalized);
    }

    private void EnsureDot()
    {
        if (dotCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Lantern Dot Interactor");
        dotCanvas = canvasObject.AddComponent<Canvas>();
        dotCanvas.renderMode = RenderMode.WorldSpace;
        dotCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        RectTransform canvasRect = canvasObject.transform as RectTransform;
        canvasRect.sizeDelta = new Vector2(36f, 36f);

        GameObject dotObject = new GameObject("Dot");
        dotObject.transform.SetParent(canvasRect, false);
        dotImage = dotObject.AddComponent<Image>();
        dotImage.color = normalColor;
        dotImage.raycastTarget = false;

        RectTransform dotRect = dotObject.transform as RectTransform;
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = Vector2.zero;
        dotRect.sizeDelta = new Vector2(10f, 10f);

        GameObject lineObject = new GameObject("Lantern Hand Pointer Line");
        pointerLine = lineObject.AddComponent<LineRenderer>();
        pointerLine.positionCount = 2;
        pointerLine.useWorldSpace = true;
        pointerLine.numCapVertices = 4;
        pointerLine.numCornerVertices = 2;
        pointerLine.startWidth = pointerLineWidth;
        pointerLine.endWidth = pointerLineWidth * 0.55f;
        pointerLine.material = GetPointerMaterial();
    }

    private Material GetPointerMaterial()
    {
        if (pointerMaterial != null)
        {
            return pointerMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null || shader.name == "Hidden/InternalErrorShader")
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null || shader.name == "Hidden/InternalErrorShader")
        {
            shader = Shader.Find("Standard");
        }

        pointerMaterial = new Material(shader);
        return pointerMaterial;
    }

    private void PositionPointer(Ray pointerRay, Vector3 pointerEnd, bool hasTarget)
    {
        if (dotCanvas == null)
        {
            return;
        }

        Transform dotTransform = dotCanvas.transform;
        dotTransform.position = pointerEnd;

        if (targetCamera != null)
        {
            Vector3 lookDirection = dotTransform.position - targetCamera.transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                dotTransform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }
        }
        else
        {
            dotTransform.rotation = Quaternion.LookRotation(-pointerRay.direction, Vector3.up);
        }

        dotTransform.localScale = Vector3.one * dotScale;

        if (pointerLine == null)
        {
            return;
        }

        Color lineColor = hasTarget ? targetColor : normalColor;
        pointerLine.startWidth = pointerLineWidth;
        pointerLine.endWidth = pointerLineWidth * 0.55f;
        pointerLine.startColor = lineColor;
        pointerLine.endColor = new Color(lineColor.r, lineColor.g, lineColor.b, lineColor.a * 0.65f);
        pointerLine.SetPosition(0, pointerRay.origin);
        pointerLine.SetPosition(1, pointerEnd);
    }
}

public sealed class LanternReturnDoor : MonoBehaviour
{
    public string sceneToLoad = "Game";
    public float transitionDelay = 0.15f;
    public Vector3 triggerCenter = new Vector3(0f, 1.5f, 0f);
    public Vector3 triggerSize = new Vector3(4.6f, 3.3f, 1.8f);

    private bool isLoading;

    private void Awake()
    {
        EnsureTriggerSetup();
    }

    public void Configure(string targetSceneName, Vector3 center, Vector3 size)
    {
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            sceneToLoad = targetSceneName;
        }

        triggerCenter = center;
        triggerSize = new Vector3(
            Mathf.Max(0.75f, size.x),
            Mathf.Max(1f, size.y),
            Mathf.Max(0.75f, size.z));
        EnsureTriggerSetup();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isLoading || !IsPlayer(other))
        {
            return;
        }

        isLoading = true;
        Invoke(nameof(LoadTargetScene), Mathf.Max(0f, transitionDelay));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isLoading || collision == null)
        {
            return;
        }

        Collider other = collision.collider;
        if (other == null || !IsPlayer(other))
        {
            return;
        }

        isLoading = true;
        Invoke(nameof(LoadTargetScene), Mathf.Max(0f, transitionDelay));
    }

    private void EnsureTriggerSetup()
    {
        Rigidbody rigidbody = GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = gameObject.AddComponent<Rigidbody>();
        }

        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;

        BoxCollider trigger = null;
        BoxCollider[] boxColliders = GetComponents<BoxCollider>();
        for (int i = 0; i < boxColliders.Length; i++)
        {
            if (boxColliders[i] != null && boxColliders[i].isTrigger)
            {
                trigger = boxColliders[i];
                break;
            }
        }

        if (trigger == null)
        {
            trigger = gameObject.AddComponent<BoxCollider>();
        }

        trigger.isTrigger = true;
        trigger.center = triggerCenter;
        trigger.size = triggerSize;
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag("Player"))
        {
            return true;
        }

        Transform parent = other.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag("Player") || parent.GetComponent<VRController>() != null)
            {
                return true;
            }

            parent = parent.parent;
        }

        return other.GetComponentInParent<VRController>() != null;
    }

    private void LoadTargetScene()
    {
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}
