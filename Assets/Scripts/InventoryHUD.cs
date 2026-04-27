using UnityEngine;
using TMPro;

public class InventoryHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI tokensText;

    void OnEnable()
    {
        PlayerData.EnsureExists();
        PlayerData.OnMoneyChanged += UpdateMoneyUI;
        PlayerData.OnTokensChanged += UpdateTokensUI;
        UpdateMoneyUI(PlayerData.money);
        UpdateTokensUI(PlayerData.tokens);
    }

    void OnDisable()
    {
        PlayerData.OnMoneyChanged -= UpdateMoneyUI;
        PlayerData.OnTokensChanged -= UpdateTokensUI;
    }

    // --- ADD THIS START METHOD ---
    void Start()
    {
        PlayerData.EnsureExists();
        // Fetch the current static values immediately when the scene loads
        // so the UI isn't blank before the first event fires!
        UpdateMoneyUI(PlayerData.money);
        UpdateTokensUI(PlayerData.tokens);
    }

    void UpdateMoneyUI(int val)
    {
        if (moneyText != null)
            moneyText.text = $"Money: ${val}";
    }

    void UpdateTokensUI(int val)
    {
        if (tokensText != null)
            tokensText.text = $"Tokens: {val}";
    }
}
