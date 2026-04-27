using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    private const string DefaultMainMenuSceneName = "MainMenu";
    private static int lastToggleFrame = -1;

    [Header("UI Reference")]
    public GameObject pauseMenuUI;

    [Header("Menu Placement")]
    public float menuDistance = 3.0f;
    public float menuScale = 0.004f;

    [Header("Scenes")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Pause Behavior")]
    public bool freezeGameWhenPaused = false;

    [Header("Input")]
    public XRNode pauseInputHand = XRNode.LeftHand;

    [Header("Menu Interaction")]
    public XRNode menuInputHand = XRNode.RightHand;
    public bool followCameraWhileOpen = true;
    [Range(0.1f, 1f)]
    public float menuNavigationDeadzone = 0.65f;
    public float menuNavigationRepeatDelay = 0.25f;
    public bool selectWithTrigger = true;
    public bool selectWithPrimaryButton = true;

    private UnityEngine.XR.InputDevice pauseController;
    private UnityEngine.XR.InputDevice menuController;
    private readonly List<Button> menuButtons = new List<Button>();
    private bool previousButtonState = false;
    private bool previousSelectButtonState = false;
    private bool previousTriggerButtonState = false;
    private float timeScaleBeforePause = 1f;
    private float nextMenuNavigationTime = 0f;
    private int selectedButtonIndex = -1;
    private bool menuReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnPlayStart()
    {
        ResetPauseState();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapPauseMenu()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsurePauseMenuForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetPauseState();
        EnsurePauseMenuForScene(scene);
    }

    private static void EnsurePauseMenuForScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name == DefaultMainMenuSceneName)
        {
            return;
        }

        PauseMenu[] pauseMenus = FindObjectsByType<PauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PauseMenu selectedMenu = null;
        for (int i = 0; i < pauseMenus.Length; i++)
        {
            PauseMenu candidate = pauseMenus[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            if (candidate.gameObject.scene == scene)
            {
                selectedMenu = candidate;
                break;
            }

            if (selectedMenu == null)
            {
                selectedMenu = candidate;
            }
        }

        if (selectedMenu == null)
        {
            GameObject pauseObject = new GameObject("Runtime Pause Menu");
            selectedMenu = pauseObject.AddComponent<PauseMenu>();
        }

        selectedMenu.gameObject.SetActive(true);
        selectedMenu.enabled = true;
        selectedMenu.EnsureMenuReady();
    }

    public static void ResetPauseState()
    {
        GameIsPaused = false;
        lastToggleFrame = -1;
        Time.timeScale = 1f;
    }

    void Awake()
    {
        ResetPauseState();
        ResetLocalInputState();
    }

    void Start()
    {
        EnsureMenuReady();
        AcquirePauseController();
        AcquireMenuController();
        SyncInputLatches();
        RefreshMenuButtons();

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(GameIsPaused);

            if (GameIsPaused)
            {
                PositionMenuInFront();
            }
        }

        if (!GameIsPaused)
        {
            Time.timeScale = 1f;
        }
    }

    void Update()
    {
        // Re-acquire device if lost
        if (!pauseController.isValid)
        {
            AcquirePauseController();
        }

        if (!menuController.isValid)
        {
            AcquireMenuController();
        }

        bool buttonPressed = ReadPausePressed();
        if (buttonPressed && !previousButtonState)
        {
            RequestTogglePause();
        }

        previousButtonState = buttonPressed;

        if (Keyboard.current != null &&
            (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame))
        {
            RequestTogglePause();
        }

        if (GameIsPaused && pauseMenuUI != null && pauseMenuUI.activeInHierarchy)
        {
            HandleMenuInteraction();
        }
    }

    void LateUpdate()
    {
        if (followCameraWhileOpen && GameIsPaused && pauseMenuUI != null && pauseMenuUI.activeInHierarchy)
        {
            PositionMenuInFront();
        }
    }

    private void AcquirePauseController()
    {
        pauseInputHand = XRNode.LeftHand;
        pauseController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
    }

    private void AcquireMenuController()
    {
        menuController = InputDevices.GetDeviceAtXRNode(menuInputHand);
    }

    private void ResetLocalInputState()
    {
        previousButtonState = false;
        previousSelectButtonState = false;
        previousTriggerButtonState = false;
        nextMenuNavigationTime = 0f;
        selectedButtonIndex = -1;
        timeScaleBeforePause = 1f;
    }

    private void SyncInputLatches()
    {
        previousButtonState = ReadPausePressed();
        previousSelectButtonState = menuController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool selectPressed) &&
                                    selectPressed;
        previousTriggerButtonState = menuController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerPressed) &&
                                     triggerPressed;
    }

    private bool ReadPausePressed()
    {
        if (!pauseController.isValid)
        {
            return false;
        }

        // Quest/Meta left controller Y maps to secondaryButton.
        if (pauseController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool secondaryButton) &&
            secondaryButton)
        {
            return true;
        }

        if (pauseController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool menuButton) &&
            menuButton)
        {
            return true;
        }

        return false;
    }

    private void RequestTogglePause()
    {
        if (lastToggleFrame == Time.frameCount)
        {
            return;
        }

        lastToggleFrame = Time.frameCount;
        TogglePause();
    }

    private void TogglePause()
    {
        if (GameIsPaused)
            Resume();
        else
            Pause();
    }

    public void Resume()
    {
        EnsureMenuReady();

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }

        Time.timeScale = freezeGameWhenPaused && timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
        GameIsPaused = false;
    }

    public void Pause()
    {
        EnsureMenuReady();

        if (pauseMenuUI == null)
        {
            Debug.LogWarning("PauseMenu has no pauseMenuUI assigned.");
            return;
        }

        timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        pauseMenuUI.SetActive(true);
        PositionMenuInFront();
        SelectDefaultButton();
        Time.timeScale = freezeGameWhenPaused ? 0f : 1f;
        GameIsPaused = true;
    }

    private void EnsureMenuReady()
    {
        if (menuReady && pauseMenuUI != null)
        {
            return;
        }

        if (pauseMenuUI == null || pauseMenuUI.GetComponentsInChildren<Button>(true).Length == 0)
        {
            BuildRuntimePauseMenu();
        }

        ConfigureCanvas();
        EnsureEventSystem();
        WireButtonActions();
        RefreshMenuButtons();

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(GameIsPaused);
        }

        menuReady = true;
    }

    private void BuildRuntimePauseMenu()
    {
        GameObject canvasObject = new GameObject(
            "Runtime Pause Menu Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        pauseMenuUI = canvasObject;

        RectTransform canvasRect = canvasObject.transform as RectTransform;
        canvasRect.sizeDelta = new Vector2(820f, 560f);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 900;
        canvas.overrideSorting = true;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 12f;

        Image background = CreateImage("Panel", canvasRect, new Color(0.02f, 0.02f, 0.025f, 0.92f));
        RectTransform panelRect = background.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(620f, 420f);

        Text title = CreateText("Title", panelRect, "PAUSED", 64, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);
        titleRect.sizeDelta = new Vector2(520f, 90f);

        CreateButton("ResumeButton", panelRect, "RESUME", new Vector2(0f, 80f), Resume);
        CreateButton("MainMenuButton", panelRect, "MAIN MENU", new Vector2(0f, -30f), LoadMenu);
        CreateButton("QuitButton", panelRect, "QUIT", new Vector2(0f, -140f), QuitGame);
    }

    private void ConfigureCanvas()
    {
        if (pauseMenuUI == null)
        {
            return;
        }

        Canvas canvas = pauseMenuUI.GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            canvas = pauseMenuUI.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 900);
        canvas.overrideSorting = true;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.dynamicPixelsPerUnit = Mathf.Max(10f, scaler.dynamicPixelsPerUnit);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("Runtime EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private void WireButtonActions()
    {
        if (pauseMenuUI == null)
        {
            return;
        }

        Button[] buttons = pauseMenuUI.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            string label = GetButtonSearchText(button);
            if (label.IndexOf("resume", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                button.onClick.RemoveListener(Resume);
                button.onClick.AddListener(Resume);
            }
            else if (label.IndexOf("main", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     label.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                button.onClick.RemoveListener(LoadMenu);
                button.onClick.AddListener(LoadMenu);
            }
            else if (label.IndexOf("quit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     label.IndexOf("exit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                button.onClick.RemoveListener(QuitGame);
                button.onClick.AddListener(QuitGame);
            }
        }
    }

    private string GetButtonSearchText(Button button)
    {
        string label = button.name;
        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            label += " " + text.text;
        }

        return label;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(string objectName, Transform parent, string textValue, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.text = textValue;
        text.font = GetBuiltinFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 20;
        text.resizeTextMaxSize = fontSize;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        Image buttonImage = CreateImage(objectName, parent, new Color(0.12f, 0.12f, 0.14f, 0.96f));
        RectTransform buttonRect = buttonImage.rectTransform;
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(420f, 82f);

        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.12f, 0.14f, 0.96f);
        colors.highlightedColor = new Color(0.95f, 0.72f, 0.18f, 1f);
        colors.selectedColor = new Color(0.95f, 0.72f, 0.18f, 1f);
        colors.pressedColor = new Color(1f, 0.86f, 0.34f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        Text buttonText = CreateText("Label", buttonRect, label, 38, TextAnchor.MiddleCenter);
        RectTransform textRect = buttonText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.pivot = new Vector2(0.5f, 0.5f);

        return button;
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

    void PositionMenuInFront()
    {
        if (pauseMenuUI == null || Camera.main == null)
        {
            return;
        }

        Transform cam = Camera.main.transform;
        pauseMenuUI.transform.position = cam.position + cam.forward * menuDistance;
        pauseMenuUI.transform.rotation = Quaternion.LookRotation(pauseMenuUI.transform.position - cam.position, Vector3.up);
        pauseMenuUI.transform.localScale = Vector3.one * menuScale;

        if (pauseMenuUI.transform is RectTransform rectTransform)
        {
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    private void HandleMenuInteraction()
    {
        HandleMenuNavigation();
        HandleMenuSubmit();
    }

    private void HandleMenuNavigation()
    {
        bool moved = false;

        if (menuController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis) &&
            Mathf.Abs(axis.y) >= menuNavigationDeadzone &&
            Time.unscaledTime >= nextMenuNavigationTime)
        {
            MoveSelection(axis.y > 0f ? -1 : 1);
            moved = true;
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
            {
                MoveSelection(-1);
                moved = true;
            }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
            {
                MoveSelection(1);
                moved = true;
            }
        }

        if (moved)
        {
            nextMenuNavigationTime = Time.unscaledTime + menuNavigationRepeatDelay;
        }
    }

    private void HandleMenuSubmit()
    {
        bool selectPressed = false;
        bool triggerPressed = false;

        if (selectWithPrimaryButton)
        {
            menuController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out selectPressed);
        }

        if (selectWithTrigger)
        {
            menuController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out triggerPressed);
        }

        bool keyboardPressed = Keyboard.current != null &&
                               (Keyboard.current.enterKey.wasPressedThisFrame ||
                                Keyboard.current.spaceKey.wasPressedThisFrame);

        if ((selectPressed && !previousSelectButtonState) ||
            (triggerPressed && !previousTriggerButtonState) ||
            keyboardPressed)
        {
            SubmitSelectedButton();
        }

        previousSelectButtonState = selectPressed;
        previousTriggerButtonState = triggerPressed;
    }

    private void RefreshMenuButtons()
    {
        menuButtons.Clear();

        if (pauseMenuUI == null)
        {
            return;
        }

        Button[] buttons = pauseMenuUI.GetComponentsInChildren<Button>(true);
        Array.Sort(buttons, CompareButtonsTopToBottom);

        foreach (Button button in buttons)
        {
            if (button != null && button.interactable)
            {
                menuButtons.Add(button);
            }
        }

        if (selectedButtonIndex >= menuButtons.Count)
        {
            selectedButtonIndex = -1;
        }
    }

    private int CompareButtonsTopToBottom(Button a, Button b)
    {
        RectTransform aRect = a.transform as RectTransform;
        RectTransform bRect = b.transform as RectTransform;

        if (aRect != null && bRect != null)
        {
            int yCompare = bRect.anchoredPosition.y.CompareTo(aRect.anchoredPosition.y);
            if (yCompare != 0)
            {
                return yCompare;
            }
        }

        return string.Compare(a.name, b.name, StringComparison.Ordinal);
    }

    private void SelectDefaultButton()
    {
        RefreshMenuButtons();

        if (menuButtons.Count == 0)
        {
            return;
        }

        int resumeIndex = FindButtonIndex("resume");
        SelectButton(resumeIndex >= 0 ? resumeIndex : 0);
    }

    private int FindButtonIndex(string namePart)
    {
        for (int i = 0; i < menuButtons.Count; i++)
        {
            if (menuButtons[i].name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return i;
            }
        }

        return -1;
    }

    private void MoveSelection(int direction)
    {
        if (menuButtons.Count == 0)
        {
            RefreshMenuButtons();
        }

        if (menuButtons.Count == 0)
        {
            return;
        }

        int nextIndex = selectedButtonIndex < 0 ? 0 : selectedButtonIndex + direction;
        if (nextIndex < 0)
        {
            nextIndex = menuButtons.Count - 1;
        }
        else if (nextIndex >= menuButtons.Count)
        {
            nextIndex = 0;
        }

        SelectButton(nextIndex);
    }

    private void SelectButton(int index)
    {
        if (index < 0 || index >= menuButtons.Count)
        {
            return;
        }

        selectedButtonIndex = index;
        Button selectedButton = menuButtons[selectedButtonIndex];
        selectedButton.Select();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
        }
    }

    private void SubmitSelectedButton()
    {
        if (selectedButtonIndex < 0 || selectedButtonIndex >= menuButtons.Count)
        {
            SelectDefaultButton();
        }

        if (selectedButtonIndex < 0 || selectedButtonIndex >= menuButtons.Count)
        {
            return;
        }

        menuButtons[selectedButtonIndex].onClick.Invoke();
    }

    public void LoadMenu()
    {
        ResetPauseState();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        ResetPauseState();
        Debug.Log("Quitting...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
