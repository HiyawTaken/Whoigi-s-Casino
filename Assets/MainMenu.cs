using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

public class MainMenu : MonoBehaviour
{
    [Header("Scene")]
    public string gameSceneName = "Game";

    [Header("Menu Placement")]
    public Canvas menuCanvas;
    public RectTransform menuRoot;
    public Vector2 canvasSize = new Vector2(1200f, 750f);
    public float menuDistance = 2.2f;
    public float menuScale = 0.004f;
    public bool followCamera = true;

    [Header("Controller Input")]
    public XRNode menuInputHand = XRNode.RightHand;
    [Range(0.1f, 1f)]
    public float menuNavigationDeadzone = 0.65f;
    public float menuNavigationRepeatDelay = 0.25f;
    public bool selectWithTrigger = true;
    public bool selectWithPrimaryButton = true;

    private readonly List<Button> menuButtons = new List<Button>();
    private UnityEngine.XR.InputDevice menuController;
    private int selectedButtonIndex = -1;
    private float nextMenuNavigationTime;
    private bool previousSelectButtonState;
    private bool previousTriggerButtonState;

    void Awake()
    {
        PauseMenu.ResetPauseState();
        ResolveReferences();
        ConfigureMenu();
        DisableControllerRayLines();
    }

    void Start()
    {
        AcquireMenuController();
        SelectDefaultButton();
    }

    void Update()
    {
        if (!menuController.isValid)
        {
            AcquireMenuController();
        }

        HandleMenuNavigation();
        HandleMenuSubmit();
    }

    void LateUpdate()
    {
        if (followCamera)
        {
            PositionMenuInFrontOfCamera();
        }
    }

    private void ResolveReferences()
    {
        if (menuRoot == null)
        {
            menuRoot = transform as RectTransform;
        }

        if (menuCanvas == null)
        {
            menuCanvas = GetComponentInParent<Canvas>(true);
        }
    }

    private void ConfigureMenu()
    {
        ConfigureCanvas();
        ConfigureBackground();
        ConfigureTitle();
        ConfigureButtons();
        PositionMenuInFrontOfCamera();
    }

    private void ConfigureCanvas()
    {
        if (menuCanvas == null)
        {
            return;
        }

        menuCanvas.renderMode = RenderMode.WorldSpace;
        menuCanvas.worldCamera = Camera.main;

        RectTransform canvasRect = menuCanvas.transform as RectTransform;
        if (canvasRect != null)
        {
            canvasRect.sizeDelta = canvasSize;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.localScale = Vector3.one * menuScale;
        }
    }

    private void ConfigureBackground()
    {
        Image panelImage = FindGraphicByName<Image>("Panel");
        if (panelImage == null)
        {
            return;
        }

        RectTransform panelRect = panelImage.transform as RectTransform;
        if (panelRect != null)
        {
            StretchToParent(panelRect);
        }

        panelImage.color = Color.white;
        panelImage.raycastTarget = false;
        panelImage.preserveAspect = false;
    }

    private void ConfigureTitle()
    {
        TextMeshProUGUI title = FindTitleText();
        if (title == null)
        {
            return;
        }

        title.text = "Whoigi's Casino";
        title.color = new Color(1f, 0.9f, 0.28f, 1f);
        title.fontSize = 72f;
        title.enableAutoSizing = true;
        title.fontSizeMin = 36f;
        title.fontSizeMax = 86f;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;

        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -55f);
        titleRect.sizeDelta = new Vector2(980f, 120f);
        titleRect.localScale = Vector3.one;
    }

    private void ConfigureButtons()
    {
        menuButtons.Clear();

        if (menuRoot == null)
        {
            return;
        }

        RectTransform rootRect = menuRoot;
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, -95f);
        rootRect.sizeDelta = new Vector2(720f, 320f);
        rootRect.localScale = Vector3.one;

        Button[] buttons = menuRoot.GetComponentsInChildren<Button>(true);
        Array.Sort(buttons, CompareButtonsTopToBottom);

        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            bool isQuit = button.name.IndexOf("quit", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isStart = button.name.IndexOf("start", StringComparison.OrdinalIgnoreCase) >= 0;

            ConfigureButtonVisual(button, isStart ? "START" : isQuit ? "QUIT" : button.name.ToUpperInvariant());

            if (isQuit && button.onClick.GetPersistentEventCount() == 0)
            {
                button.onClick.AddListener(QuitGame);
            }

            if (isStart && button.onClick.GetPersistentEventCount() == 0)
            {
                button.onClick.AddListener(PlayGame);
            }

            menuButtons.Add(button);
        }
    }

    private void ConfigureButtonVisual(Button button, string label)
    {
        RectTransform buttonRect = button.transform as RectTransform;
        if (buttonRect != null)
        {
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.localScale = Vector3.one;
            buttonRect.sizeDelta = new Vector2(420f, 86f);
            buttonRect.anchoredPosition = label == "START" ? new Vector2(0f, 55f) : new Vector2(0f, -60f);
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.02f, 0.02f, 0.025f, 0.88f);
            image.raycastTarget = true;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.02f, 0.02f, 0.025f, 0.88f);
        colors.highlightedColor = new Color(0.95f, 0.75f, 0.22f, 0.95f);
        colors.selectedColor = new Color(0.95f, 0.75f, 0.22f, 0.95f);
        colors.pressedColor = new Color(1f, 0.55f, 0.12f, 1f);
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI labelText = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (labelText == null)
        {
            return;
        }

        labelText.text = label;
        labelText.color = Color.white;
        labelText.fontSize = 44f;
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 24f;
        labelText.fontSizeMax = 54f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.raycastTarget = false;

        RectTransform textRect = labelText.rectTransform;
        StretchToParent(textRect);
        textRect.localScale = Vector3.one;
    }

    private void AcquireMenuController()
    {
        menuController = InputDevices.GetDeviceAtXRNode(menuInputHand);
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

    private void MoveSelection(int direction)
    {
        if (menuButtons.Count == 0)
        {
            ConfigureButtons();
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

    private void SelectDefaultButton()
    {
        if (menuButtons.Count == 0)
        {
            ConfigureButtons();
        }

        if (menuButtons.Count == 0)
        {
            return;
        }

        int startIndex = FindButtonIndex("start");
        SelectButton(startIndex >= 0 ? startIndex : 0);
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

    private void PositionMenuInFrontOfCamera()
    {
        if (menuCanvas == null || Camera.main == null)
        {
            return;
        }

        Transform cam = Camera.main.transform;
        Transform canvasTransform = menuCanvas.transform;
        canvasTransform.position = cam.position + cam.forward * menuDistance;
        canvasTransform.rotation = Quaternion.LookRotation(canvasTransform.position - cam.position, Vector3.up);
        canvasTransform.localScale = Vector3.one * menuScale;
    }

    private void DisableControllerRayLines()
    {
        MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
            {
                continue;
            }

            if (!behaviour.gameObject.scene.IsValid() || !behaviour.gameObject.scene.isLoaded)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            if (typeName.IndexOf("XRInteractorLineVisual", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                behaviour.enabled = false;
            }
        }
    }

    private TextMeshProUGUI FindTitleText()
    {
        if (menuCanvas == null)
        {
            return null;
        }

        TextMeshProUGUI[] texts = menuCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI text in texts)
        {
            if (text.text.IndexOf("Casino", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }
        }

        return null;
    }

    private T FindGraphicByName<T>(string objectName) where T : Component
    {
        if (menuCanvas == null)
        {
            return null;
        }

        T[] graphics = menuCanvas.GetComponentsInChildren<T>(true);
        foreach (T graphic in graphics)
        {
            if (graphic.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
            {
                return graphic;
            }
        }

        return null;
    }

    private void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    public void PlayGame()
    {
        PauseMenu.ResetPauseState();

        int gameSceneIndex = GetBuildIndexBySceneName(gameSceneName);
        if (gameSceneIndex >= 0)
        {
            SceneManager.LoadScene(gameSceneIndex);
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void QuitGame()
    {
        PauseMenu.ResetPauseState();
        Debug.Log("Quitting...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private int GetBuildIndexBySceneName(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string name = Path.GetFileNameWithoutExtension(scenePath);
            if (name.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
