using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Add this for VR Input

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Header("UI Reference")]
    public GameObject pauseMenuUI;

    [Header("VR Input Action")]
    // This allows you to map a specific button (like the Menu button on Oculus/Index)
    public InputActionProperty menuButton;

    // Use this line instead of the old Input.GetKeyDown
    void Update()
    {
        // Check if the Escape key was pressed using the New Input System
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (GameIsPaused)
            {
                Resume();
            }
            else 
            {
                Pause();
            }    
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        GameIsPaused = false;
        
        // Optional: Unlock cursor if testing on PC
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Pause()
    {
        // Position the menu in front of the player before showing it
        PositionMenuInFront();

        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f; // This pauses physics and AI
        GameIsPaused = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Helper to make sure the menu doesn't appear behind the player
    void PositionMenuInFront()
    {
        Transform cameraTransform = Camera.main.transform;
        pauseMenuUI.transform.position = cameraTransform.position + cameraTransform.forward * 2.0f; 
        pauseMenuUI.transform.rotation = Quaternion.LookRotation(pauseMenuUI.transform.position - cameraTransform.position);
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu"); // Ensure your scene name matches exactly
    }

    public void QuitGame()
    {
        Debug.Log("Quitting...");
        Application.Quit();
    }
}