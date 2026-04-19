using UnityEngine;

[CreateAssetMenu(fileName = "JanitorUnlockData", menuName = "Mall Mania/Janitor Unlock Data")]
public class JanitorUnlockData : ScriptableObject
{
    [Header("Unlock Requirements")]
    [Tooltip("Player level required to show the unlock spot")]
    [SerializeField] private int requiredPlayerLevel = 1;
    
    [Header("Unlock Cost")]
    [Tooltip("Total cost to unlock this janitor")]
    [SerializeField] private int unlockCost = 500;
    
    [Header("Display")]
    [Tooltip("Name of the janitor")]
    [SerializeField] private string janitorName = "Janitor";
    [Tooltip("Description shown in UI")]
    [TextArea(2, 4)]
    [SerializeField] private string description = "";
    
    [Header("Janitor Settings")]
    [Tooltip("Prefab to spawn when unlocked")]
    [SerializeField] private GameObject janitorPrefab;
    [Tooltip("Movement speed of the janitor")]
    [SerializeField] private float moveSpeed = 3.5f;
    
    public int RequiredPlayerLevel => requiredPlayerLevel;
    public int UnlockCost => unlockCost;
    public string JanitorName => janitorName;
    public string Description => description;
    public GameObject JanitorPrefab => janitorPrefab;
    public float MoveSpeed => moveSpeed;
}
