using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class LanternExitPortal : MonoBehaviour
{
    public string returnSceneName = "Forest";

    private bool loading;

    private void OnTriggerEnter(Collider other)
    {
        if (loading || !other.CompareTag("Player"))
        {
            return;
        }

        loading = true;
        PauseMenu.ResetPauseState();
        SceneManager.LoadScene(returnSceneName);
    }
}
