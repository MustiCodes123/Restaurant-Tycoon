using UnityEngine;

[CreateAssetMenu(fileName = "WashroomUnlockData", menuName = "Mall Mania/Washroom Unlock Data")]
public class WashroomUnlockData : ScriptableObject
{
    [Header("Unlock Requirements")]
    [Tooltip("Player level required to show the unlock spot")]
    [SerializeField] private int requiredPlayerLevel = 1;
    
    [Header("Unlock Cost")]
    [Tooltip("Total cost to unlock this washroom")]
    [SerializeField] private int unlockCost = 500;
    
    [Header("Display")]
    [Tooltip("Name of the washroom")]
    [SerializeField] private string washroomName = "Washroom";
    [Tooltip("Description shown in UI")]
    [TextArea(2, 4)]
    [SerializeField] private string description = "";
    
    [Header("Washroom Settings")]
    [Tooltip("How many customers can use a stall per toilet paper refill")]
    [SerializeField] private int usesPerToiletPaper = 3;
    
    public int RequiredPlayerLevel => requiredPlayerLevel;
    public int UnlockCost => unlockCost;
    public string WashroomName => washroomName;
    public string Description => description;
    public int UsesPerToiletPaper => usesPerToiletPaper;
}
