using UnityEngine;
using TMPro;

public class InventoryHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText; // Updated reference
    [SerializeField] private TextMeshProUGUI tokensText;

    void OnEnable() {
        PlayerData.OnMoneyChanged += UpdateMoneyUI;
        PlayerData.OnTokensChanged += UpdateTokensUI;
    }

    void OnDisable() {
        PlayerData.OnMoneyChanged -= UpdateMoneyUI;
        PlayerData.OnTokensChanged -= UpdateTokensUI;
    }

    void UpdateMoneyUI(int val) => moneyText.text = $"Money: ${val}"; // Added a '$' for flair
    void UpdateTokensUI(int val) => tokensText.text = $"Tokens: {val}";
}