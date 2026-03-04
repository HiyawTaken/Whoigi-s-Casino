using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Header("UI Reference")]
    public GameObject pauseMenuUI;

    private InputDevice rightController;
    private bool previousButtonState = false;

    void Start()
    {
        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }

    void Update()
    {
        // Re-acquire device if lost
        if (!rightController.isValid)
        {
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        // A button on right controller
        bool buttonPressed = false;
        if (rightController.TryGetFeatureValue(CommonUsages.primaryButton, out buttonPressed))
        {
            if (buttonPressed && !previousButtonState)
            {
                if (GameIsPaused)
                    Resume();
                else
                    Pause();
            }
            previousButtonState = buttonPressed;
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        GameIsPaused = false;
    }

    void Pause()
    {
        PositionMenuInFront();
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        GameIsPaused = true;
    }

    void PositionMenuInFront()
    {
        Transform cam = Camera.main.transform;
        pauseMenuUI.transform.position = cam.position + cam.forward * 2.0f;
        // Face the player: menu looks back toward camera
        pauseMenuUI.transform.rotation = Quaternion.LookRotation(cam.position - pauseMenuUI.transform.position);
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting...");
        Application.Quit();
    }
}