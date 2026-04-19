using UnityEngine;

[CreateAssetMenu(fileName = "TableCleanerUnlockData", menuName = "Mall Mania/Table Cleaner Unlock Data")]
public class TableCleanerUnlockData : ScriptableObject
{
    [Header("Unlock Requirements")]
    [Tooltip("Player level required to show the unlock spot")]
    [SerializeField] private int requiredPlayerLevel = 1;
    
    [Header("Unlock Cost")]
    [Tooltip("Total cost to unlock this table cleaner")]
    [SerializeField] private int unlockCost = 500;
    
    [Header("Display")]
    [Tooltip("Name of the table cleaner")]
    [SerializeField] private string cleanerName = "Table Cleaner";
    [Tooltip("Description shown in UI")]
    [TextArea(2, 4)]
    [SerializeField] private string description = "";
    
    [Header("Table Cleaner Settings")]
    [Tooltip("Prefab to spawn when unlocked")]
    [SerializeField] private GameObject cleanerPrefab;
    [Tooltip("Movement speed of the table cleaner")]
    [SerializeField] private float moveSpeed = 3.5f;
    [Tooltip("How many trays the cleaner can carry at once")]
    [SerializeField] private int maxTrayCapacity = 1;
    
    public int RequiredPlayerLevel => requiredPlayerLevel;
    public int UnlockCost => unlockCost;
    public string CleanerName => cleanerName;
    public string Description => description;
    public GameObject CleanerPrefab => cleanerPrefab;
    public float MoveSpeed => moveSpeed;
    public int MaxTrayCapacity => maxTrayCapacity;
}
