using UnityEngine;

[CreateAssetMenu(fileName = "StoreUnlockData", menuName = "Mall Mania/Store Unlock Data")]
public class StoreUnlockData : ScriptableObject
{
    [Header("Unlock Requirements")]
    [Tooltip("Player level required to show the unlock spot")]
    [SerializeField] private int requiredPlayerLevel = 1;
    
    [Header("Unlock Cost")]
    [Tooltip("Total cost to unlock this store")]
    [SerializeField] private int unlockCost = 100;
    
    [Header("Display")]
    [Tooltip("Name of the store")]
    [SerializeField] private string storeName = "New Store";
    [Tooltip("Description shown in UI")]
    [TextArea(2, 4)]
    [SerializeField] private string description = "";
    
    public int RequiredPlayerLevel => requiredPlayerLevel;
    public int UnlockCost => unlockCost;
    public string StoreName => storeName;
    public string Description => description;
}
