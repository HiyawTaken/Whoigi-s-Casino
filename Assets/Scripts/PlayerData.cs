using UnityEngine;
using System;

public class PlayerData : MonoBehaviour
{
    public static PlayerData Instance;

    // Renamed from Points to Money
    public static event Action<int> OnMoneyChanged;
    public static event Action<int> OnTokensChanged;

    private int _money; 
    private int _tokens;

    void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddMoney(int amount) {
        _money += amount;
        OnMoneyChanged?.Invoke(_money); 
    }

    public void AddTokens(int amount) {
        _tokens += amount;
        OnTokensChanged?.Invoke(_tokens);
    }
}