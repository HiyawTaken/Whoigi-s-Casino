using UnityEngine;
using System;

public class PlayerData : MonoBehaviour
{
    public static PlayerData Instance;

    public static event Action<int> OnMoneyChanged;
    public static event Action<int> OnTokensChanged;

    // FIX: Changed to public static so your HUD can find exactly "PlayerData.money" and "PlayerData.tokens"
    // { get; private set; } means other scripts can read it, but only this script can modify it!
    public static int money { get; private set; }
    public static int tokens { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load the saved data the moment the game starts
            LoadData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddMoney(int amount)
    {
        money += amount;
        OnMoneyChanged?.Invoke(money);

        // Save the new amount under the name "SavedMoney"
        PlayerPrefs.SetInt("SavedMoney", money);
        PlayerPrefs.Save(); // Forces Unity to write it to the device immediately
    }

    public void AddTokens(int amount)
    {
        tokens += amount;
        OnTokensChanged?.Invoke(tokens);

        // Save the new amount under the name "SavedTokens"
        PlayerPrefs.SetInt("SavedTokens", tokens);
        PlayerPrefs.Save();
    }

    // A helper method to grab the data when the game boots up
    private void LoadData()
    {
        money = PlayerPrefs.GetInt("SavedMoney", 0);
        tokens = PlayerPrefs.GetInt("SavedTokens", 0);
    }
}