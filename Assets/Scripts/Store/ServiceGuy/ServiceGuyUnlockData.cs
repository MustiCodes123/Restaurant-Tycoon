using UnityEngine;

[CreateAssetMenu(fileName = "ServiceGuyUnlockData", menuName = "Mall Mania/Service Guy Unlock Data")]
public class ServiceGuyUnlockData : ScriptableObject
{
    [Header("Unlock Requirements")]
    [Tooltip("Player level required to show the unlock spot")]
    [SerializeField] private int requiredPlayerLevel = 1;
    
    [Header("Unlock Cost")]
    [Tooltip("Total cost to unlock this service guy")]
    [SerializeField] private int unlockCost = 300;
    
    [Header("Display")]
    [Tooltip("Name of the service guy")]
    [SerializeField] private string serviceGuyName = "Staff";
    [Tooltip("Description shown in UI")]
    [TextArea(2, 4)]
    [SerializeField] private string description = "";
    
    [Header("Service Guy Settings")]
    [Tooltip("Prefab to spawn when unlocked")]
    [SerializeField] private GameObject serviceGuyPrefab;
    [Tooltip("Movement speed of the service guy")]
    [SerializeField] private float moveSpeed = 3.5f;
    [Tooltip("Time to serve a customer")]
    [SerializeField] private float serviceDuration = 2f;
    
    public int RequiredPlayerLevel => requiredPlayerLevel;
    public int UnlockCost => unlockCost;
    public string ServiceGuyName => serviceGuyName;
    public string Description => description;
    public GameObject ServiceGuyPrefab => serviceGuyPrefab;
    public float MoveSpeed => moveSpeed;
    public float ServiceDuration => serviceDuration;
}
