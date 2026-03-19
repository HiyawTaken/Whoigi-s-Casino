using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionDoor : MonoBehaviour
{
    [SerializeField] private string sceneToLoad = "Forest";

    private void OnTriggerEnter(Collider other)
    {
        // 1. This will print a message to the Console no matter WHAT touches the door
        Debug.Log($"<color=yellow>DOOR TOUCHED BY:</color> {other.gameObject.name} | <color=orange>TAG:</color> {other.tag}");

        // 2. This checks for the Player tag
        if (other.CompareTag("Player"))
        {
            Debug.Log("<color=green>Player detected! Loading scene...</color>");
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.Log("<color=red>Touch detected, but it wasn't the Player tag.</color>");
        }
    }
}