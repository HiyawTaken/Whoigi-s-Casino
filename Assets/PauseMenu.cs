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
    private static int lastToggleFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnPlayStart()
    {
        ResetPauseState();
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

        // Y on the left controller, or B if this is changed back to the right controller.
        bool buttonPressed = false;
        if (pauseController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out buttonPressed))
        {
            if (buttonPressed && !previousButtonState)
            {
                RequestTogglePause();
            }
            previousButtonState = buttonPressed;
        }

        // Keyboard ESC fallback (New Input System)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
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
        pauseController = InputDevices.GetDeviceAtXRNode(pauseInputHand);
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
        previousButtonState = pauseController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool pausePressed) &&
                              pausePressed;
        previousSelectButtonState = menuController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool selectPressed) &&
                                    selectPressed;
        previousTriggerButtonState = menuController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerPressed) &&
                                     triggerPressed;
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
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }

        Time.timeScale = freezeGameWhenPaused && timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
        GameIsPaused = false;
    }

    public void Pause()
    {
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
