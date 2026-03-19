using UnityEngine;
using UnityEngine.SceneManagement; // Required for changing scenes

public class SceneTransitionDoor : MonoBehaviour
{
    [Tooltip("Type the exact name of the scene you want to load")]
    [SerializeField] private string sceneToLoad = "Forest";

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the trigger has the "Player" tag
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entered the door. Loading scene: " + sceneToLoad);
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}