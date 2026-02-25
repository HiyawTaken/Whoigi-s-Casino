using UnityEngine;
using UnityEngine.InputSystem; // Added this for the new system

public class DebugCheats : MonoBehaviour
{
    void Update()
    {
        // Check if the 'M' key was pressed this frame
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            if (PlayerData.Instance != null)
            {
                PlayerData.Instance.AddMoney(100);
                Debug.Log("Cheated: Added $100");
            }
        }

        // Check if the 'T' key was pressed this frame
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            if (PlayerData.Instance != null)
            {
                PlayerData.Instance.AddTokens(10);
                Debug.Log("Cheated: Added 10 Tokens");
            }
        }
    }
}