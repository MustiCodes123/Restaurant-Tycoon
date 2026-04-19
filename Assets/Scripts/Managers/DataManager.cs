using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }
    
    private const string KEY_CURRENT_LEVEL = "CurrentLevel";
    private const string KEY_TOTAL_EARNINGS = "TotalEarnings";
    private const string KEY_TOTAL_CUSTOMERS_SERVED = "TotalCustomersServed";
    private const string KEY_CUSTOMERS_SERVED_CASHIER = "CustomersServedCashier";
    private const string KEY_CUSTOMERS_SERVED_STORE_PREFIX = "CustomersServedStore_";
    private const string KEY_CURRENT_MONEY = "CurrentMoney";
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    // Current Level
    public int CurrentLevel
    {
        get => PlayerPrefs.GetInt(KEY_CURRENT_LEVEL, 1);
        set
        {
            PlayerPrefs.SetInt(KEY_CURRENT_LEVEL, value);
            PlayerPrefs.Save();
        }
    }
    
    // Total Earnings (lifetime, never decreases)
    public int TotalEarnings
    {
        get => PlayerPrefs.GetInt(KEY_TOTAL_EARNINGS, 0);
        set
        {
            PlayerPrefs.SetInt(KEY_TOTAL_EARNINGS, value);
            PlayerPrefs.Save();
        }
    }
    
    // Total Customers Served (lifetime, all locations)
    public int TotalCustomersServed
    {
        get => PlayerPrefs.GetInt(KEY_TOTAL_CUSTOMERS_SERVED, 0);
        set
        {
            PlayerPrefs.SetInt(KEY_TOTAL_CUSTOMERS_SERVED, value);
            PlayerPrefs.Save();
        }
    }
    
    // Customers Served at Cashier
    public int CustomersServedAtCashier
    {
        get => PlayerPrefs.GetInt(KEY_CUSTOMERS_SERVED_CASHIER, 0);
        set
        {
            PlayerPrefs.SetInt(KEY_CUSTOMERS_SERVED_CASHIER, value);
            PlayerPrefs.Save();
        }
    }
    
    // Customers Served at specific Store
    public int GetCustomersServedAtStore(string storeName)
    {
        return PlayerPrefs.GetInt(KEY_CUSTOMERS_SERVED_STORE_PREFIX + storeName, 0);
    }
    
    public void SetCustomersServedAtStore(string storeName, int value)
    {
        PlayerPrefs.SetInt(KEY_CUSTOMERS_SERVED_STORE_PREFIX + storeName, value);
        PlayerPrefs.Save();
    }
    
    public void IncrementCustomersServedAtStore(string storeName)
    {
        int current = GetCustomersServedAtStore(storeName);
        SetCustomersServedAtStore(storeName, current + 1);
    }
    
    // Current Money (spendable)
    public int CurrentMoney
    {
        get => PlayerPrefs.GetInt(KEY_CURRENT_MONEY, 0);
        set
        {
            PlayerPrefs.SetInt(KEY_CURRENT_MONEY, value);
            PlayerPrefs.Save();
        }
    }
    
    // Helper method to add earnings (updates both current and lifetime)
    public void AddMoney(int amount)
    {
        CurrentMoney += amount;
        TotalEarnings += amount;
    }
    
    // Helper method to spend money
    public bool SpendMoney(int amount)
    {
        if (CurrentMoney >= amount)
        {
            CurrentMoney -= amount;
            return true;
        }
        return false;
    }
    
    // Reset all data (for testing or new game)
    public void ResetAllData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
