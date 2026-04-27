using UnityEngine;
using System;

[DisallowMultipleComponent]
public class PlayerData : MonoBehaviour
{
    private const string SavedMoneyKey = "SavedMoney";
    private const string SavedTokensKey = "SavedTokens";
    private const string StarterTokensGrantedKey = "StarterTokensGranted";

    public static PlayerData Instance;

    public static event Action<int> OnMoneyChanged;
    public static event Action<int> OnTokensChanged;

    // FIX: Changed to public static so your HUD can find exactly "PlayerData.money" and "PlayerData.tokens"
    // { get; private set; } means other scripts can read it, but only this script can modify it!
    public static int money { get; private set; }
    public static int tokens { get; private set; }

    private static bool dataLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BootstrapWallet()
    {
        EnsureExists();
    }

    public static PlayerData EnsureExists()
    {
        if (Instance != null)
        {
            return Instance;
        }

        PlayerData existing = FindFirstObjectByType<PlayerData>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.BecomeInstance();
            return existing;
        }

        GameObject dataObject = new GameObject("PlayerData");
        return dataObject.AddComponent<PlayerData>();
    }

    void Awake()
    {
        if (Instance == this)
        {
            return;
        }

        if (Instance == null)
        {
            BecomeInstance();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void BecomeInstance()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!dataLoaded)
        {
            LoadData();
            dataLoaded = true;
        }

        PublishCurrentValues();
        PersistentWalletHUD.EnsureExists();
    }

    public void AddMoney(int amount)
    {
        money = Mathf.Max(0, money + amount);
        PlayerPrefs.SetInt(SavedMoneyKey, money);
        PlayerPrefs.Save();
        OnMoneyChanged?.Invoke(money);
    }

    public void AddTokens(int amount)
    {
        SetTokens(tokens + amount);
    }

    public bool CanAffordTokens(int amount)
    {
        return amount <= 0 || tokens >= amount;
    }

    public bool TrySpendTokens(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (!CanAffordTokens(amount))
        {
            return false;
        }

        SetTokens(tokens - amount);
        return true;
    }

    public void EnsureStarterTokens(int minimumTokens)
    {
        minimumTokens = Mathf.Max(0, minimumTokens);
        if (minimumTokens == 0 || PlayerPrefs.GetInt(StarterTokensGrantedKey, 0) == 1)
        {
            return;
        }

        bool changedTokens = tokens < minimumTokens;
        if (changedTokens)
        {
            SetTokens(minimumTokens);
        }

        PlayerPrefs.SetInt(StarterTokensGrantedKey, 1);
        PlayerPrefs.Save();
        if (!changedTokens)
        {
            OnTokensChanged?.Invoke(tokens);
        }
    }

    public void SetTokens(int value)
    {
        tokens = Mathf.Max(0, value);
        PlayerPrefs.SetInt(SavedTokensKey, tokens);
        PlayerPrefs.Save();
        OnTokensChanged?.Invoke(tokens);
    }

    public void PublishCurrentValues()
    {
        OnMoneyChanged?.Invoke(money);
        OnTokensChanged?.Invoke(tokens);
    }

    // A helper method to grab the data when the game boots up
    private void LoadData()
    {
        money = Mathf.Max(0, PlayerPrefs.GetInt(SavedMoneyKey, 0));
        tokens = Mathf.Max(0, PlayerPrefs.GetInt(SavedTokensKey, 0));
    }
}
