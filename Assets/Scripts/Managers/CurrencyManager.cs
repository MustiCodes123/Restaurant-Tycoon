using UnityEngine;
using UnityEngine.UI;
using System;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }
    
    [Header("Currency")]
    [SerializeField] private int currentMoney = 0;
    
    [Header("Testing")]
    [SerializeField] private Button addTestMoneyButton;
    
    public event Action<int> OnMoneyChanged;
    
    public int CurrentMoney => currentMoney;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Load saved money directly from PlayerPrefs
            currentMoney = PlayerPrefs.GetInt("CurrentMoney", 0);
            Debug.Log($"Loaded money from PlayerPrefs: {currentMoney}");
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // Notify UI of initial money value
        OnMoneyChanged?.Invoke(currentMoney);
        
        // Set up test button if assigned
        if (addTestMoneyButton != null)
        {
            addTestMoneyButton.onClick.AddListener(AddTestMoney);
        }
    }
    
    public void AddMoney(int amount)
    {
        currentMoney += amount;
        OnMoneyChanged?.Invoke(currentMoney);
        
        // Save directly to PlayerPrefs
        PlayerPrefs.SetInt("CurrentMoney", currentMoney);
        PlayerPrefs.Save();
        
        // Also update DataManager if available
        if (DataManager.Instance != null)
        {
            DataManager.Instance.CurrentMoney = currentMoney;
        }
        
        Debug.Log($"Money added: {amount}. Total: {currentMoney}");
    }
    
    public bool SpendMoney(int amount)
    {
        if (currentMoney >= amount)
        {
            currentMoney -= amount;
            OnMoneyChanged?.Invoke(currentMoney);
            
            // Save directly to PlayerPrefs
            PlayerPrefs.SetInt("CurrentMoney", currentMoney);
            PlayerPrefs.Save();
            
            // Also update DataManager if available
            if (DataManager.Instance != null)
            {
                DataManager.Instance.CurrentMoney = currentMoney;
            }
            
            Debug.Log($"Money spent: {amount}. Remaining: {currentMoney}");
            return true;
        }
        
        Debug.LogWarning("Not enough money!");
        return false;
    }
    
    public void SetMoney(int amount)
    {
        currentMoney = amount;
        OnMoneyChanged?.Invoke(currentMoney);
        
        // Save directly to PlayerPrefs
        PlayerPrefs.SetInt("CurrentMoney", currentMoney);
        PlayerPrefs.Save();
        
        // Also update DataManager if available
        if (DataManager.Instance != null)
        {
            DataManager.Instance.CurrentMoney = currentMoney;
        }
    }
    
    /// <summary>
    /// Adds $100 for testing purposes
    /// </summary>
    public void AddTestMoney()
    {
        AddMoney(100);
        Debug.Log("[TEST] Added $100 to player money");
    }
}
