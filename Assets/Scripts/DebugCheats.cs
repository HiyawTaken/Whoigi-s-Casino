using UnityEngine;
using UnityEngine.InputSystem; // Added this for the new system

public class DebugCheats : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        // Check if the 'M' key was pressed this frame
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            PlayerData wallet = PlayerData.EnsureExists();
            if (wallet != null)
            {
                wallet.AddMoney(100);
                Debug.Log("Cheated: Added $100");
            }
        }

        // Check if the 'T' key was pressed this frame
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            PlayerData wallet = PlayerData.EnsureExists();
            if (wallet != null)
            {
                wallet.AddTokens(10);
                Debug.Log("Cheated: Added 10 Tokens");
            }
        }
    }
}
