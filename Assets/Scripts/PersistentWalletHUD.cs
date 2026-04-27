using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRNode = UnityEngine.XR.XRNode;

[DisallowMultipleComponent]
public class PersistentWalletHUD : MonoBehaviour
{
    public static PersistentWalletHUD Instance { get; private set; }

    [Header("Placement")]
    public Vector3 cameraLocalPosition = new Vector3(-0.58f, 0.22f, 1.25f);
    public Vector3 cameraLocalEulerAngles = new Vector3(0f, 8f, 0f);
    public float hudScale = 0.0024f;

    [Header("Hand Placement")]
    public bool attachToHand = true;
    public XRNode handInputSource = XRNode.LeftHand;
    public Vector3 handLocalPosition = new Vector3(0.13f, 0.08f, 0.04f);
    public Vector3 handLocalEulerAngles = new Vector3(62f, 0f, 0f);
    public float handHudScale = 0.00105f;
    public bool showOnlyWhileGripHeld = true;
    [Range(0f, 1f)]
    public float gripShowThreshold = 0.35f;

    [Header("Style")]
    public Vector2 panelSize = new Vector2(260f, 118f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.78f);
    public Color textColor = new Color(1f, 0.88f, 0.35f, 1f);

    private Canvas walletCanvas;
    private Text walletText;
    private Camera targetCamera;
    private Transform targetHand;
    private XRInputDevice handDevice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapHud()
    {
        EnsureExists();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
        if (Instance != null)
        {
            Instance.targetCamera = null;
            Instance.targetHand = null;
            Instance.RefreshText();
        }
    }

    public static PersistentWalletHUD EnsureExists()
    {
        if (Instance != null)
        {
            return Instance;
        }

        PersistentWalletHUD existing = FindFirstObjectByType<PersistentWalletHUD>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.BecomeInstance();
            return existing;
        }

        GameObject hudObject = new GameObject("Persistent Wallet HUD");
        return hudObject.AddComponent<PersistentWalletHUD>();
    }

    private void Awake()
    {
        if (Instance == this)
        {
            return;
        }

        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        BecomeInstance();
    }

    private void OnEnable()
    {
        PlayerData.EnsureExists();
        PlayerData.OnMoneyChanged += HandleMoneyChanged;
        PlayerData.OnTokensChanged += HandleTokensChanged;
        RefreshText();
    }

    private void OnDisable()
    {
        PlayerData.OnMoneyChanged -= HandleMoneyChanged;
        PlayerData.OnTokensChanged -= HandleTokensChanged;
    }

    private void LateUpdate()
    {
        EnsureHud();
        bool shouldShow = ShouldShowHud();
        SetHudVisible(shouldShow);
        if (shouldShow)
        {
            PositionHud();
        }
    }

    private void BecomeInstance()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureHud();
        RefreshText();
    }

    private void HandleMoneyChanged(int value)
    {
        RefreshText();
    }

    private void HandleTokensChanged(int value)
    {
        RefreshText();
    }

    private void EnsureHud()
    {
        if (walletCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Wallet HUD Canvas");
        canvasObject.transform.SetParent(transform, false);

        walletCanvas = canvasObject.AddComponent<Canvas>();
        walletCanvas.renderMode = RenderMode.WorldSpace;
        walletCanvas.sortingOrder = 500;
        walletCanvas.overrideSorting = true;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 12f;

        RectTransform canvasRect = canvasObject.transform as RectTransform;
        canvasRect.sizeDelta = panelSize;

        Image background = CreateImage("Wallet HUD Background", canvasRect, backgroundColor);
        StretchToParent(background.rectTransform);

        walletText = CreateText("Wallet HUD Text", canvasRect);
        StretchToParent(walletText.rectTransform);
        walletText.rectTransform.offsetMin = new Vector2(18f, 10f);
        walletText.rectTransform.offsetMax = new Vector2(-18f, -10f);
    }

    private void PositionHud()
    {
        if (walletCanvas == null)
        {
            return;
        }

        Transform canvasTransform = walletCanvas.transform;
        if (attachToHand)
        {
            targetHand = ResolveHand();
            if (targetHand != null)
            {
                if (canvasTransform.parent != targetHand)
                {
                    canvasTransform.SetParent(targetHand, false);
                }

                canvasTransform.localPosition = handLocalPosition;
                canvasTransform.localRotation = Quaternion.Euler(handLocalEulerAngles);
                canvasTransform.localScale = Vector3.one * Mathf.Max(0.0005f, handHudScale);
                return;
            }
        }

        targetCamera = ResolveCamera();
        if (targetCamera == null)
        {
            return;
        }

        Transform cameraTransform = targetCamera.transform;
        if (canvasTransform.parent != cameraTransform)
        {
            canvasTransform.SetParent(cameraTransform, false);
        }

        canvasTransform.localPosition = cameraLocalPosition;
        canvasTransform.localRotation = Quaternion.Euler(cameraLocalEulerAngles);
        canvasTransform.localScale = Vector3.one * Mathf.Max(0.0005f, hudScale);
    }

    private Transform ResolveHand()
    {
        if (targetHand != null && targetHand.gameObject.activeInHierarchy)
        {
            return targetHand;
        }

        ControllerVisual[] visuals = FindObjectsByType<ControllerVisual>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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
            if (visual.inputSource == handInputSource ||
                (handInputSource == XRNode.LeftHand && visual.isLeftHand) ||
                (handInputSource == XRNode.RightHand && !visual.isLeftHand))
            {
                score += 20;
            }

            if (visual.gameObject.activeInHierarchy)
            {
                score += 5;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestVisual = visual;
            }
        }

        if (bestVisual != null)
        {
            return bestVisual.transform;
        }

        ControllerGrabber[] grabbers = FindObjectsByType<ControllerGrabber>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < grabbers.Length; i++)
        {
            if (grabbers[i] != null && grabbers[i].inputSource == handInputSource)
            {
                return grabbers[i].transform;
            }
        }

        return null;
    }

    private bool ShouldShowHud()
    {
        if (!showOnlyWhileGripHeld)
        {
            return true;
        }

        if (!handDevice.isValid)
        {
            handDevice = XRInputDevices.GetDeviceAtXRNode(handInputSource);
        }

        if (!handDevice.isValid)
        {
            return false;
        }

        if (handDevice.TryGetFeatureValue(XRCommonUsages.gripButton, out bool gripButton) && gripButton)
        {
            return true;
        }

        return handDevice.TryGetFeatureValue(XRCommonUsages.grip, out float grip) &&
               grip >= gripShowThreshold;
    }

    private void SetHudVisible(bool visible)
    {
        if (walletCanvas != null && walletCanvas.gameObject.activeSelf != visible)
        {
            walletCanvas.gameObject.SetActive(visible);
        }
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
        {
            return targetCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
            {
                return cameras[i];
            }
        }

        return null;
    }

    private void RefreshText()
    {
        EnsureHud();
        if (walletText == null)
        {
            return;
        }

        walletText.text = $"TOKENS {PlayerData.tokens}\nMONEY ${PlayerData.money}";
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

    private Text CreateText(string objectName, Transform parent)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = GetBuiltinFont();
        text.fontSize = 34;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = textColor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 22;
        text.resizeTextMaxSize = 36;
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
