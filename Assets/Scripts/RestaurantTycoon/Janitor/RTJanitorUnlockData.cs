using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// ScriptableObject that configures a single RT janitor unlock.
    /// Create via: Assets > Create > Restaurant Tycoon > Janitor Unlock Data
    /// </summary>
    [CreateAssetMenu(fileName = "NewJanitorUnlockData", menuName = "Restaurant Tycoon/Janitor Unlock Data")]
    public class RTJanitorUnlockData : ScriptableObject
    {
        [Header("Unlock Requirements")]
        [Tooltip("Player level required before the unlock spot becomes visible.")]
        [SerializeField] private int requiredPlayerLevel = 1;

        [Header("Cost")]
        [SerializeField] private int unlockCost = 500;

        [Header("Display")]
        [SerializeField] private string janitorName = "Janitor";
        [TextArea(2, 4)]
        [SerializeField] private string description = "";

        [Header("Janitor Prefab")]
        [Tooltip("The RTJanitorController prefab to spawn on unlock.")]
        [SerializeField] private GameObject janitorPrefab;

        [Header("Janitor Settings")]
        [SerializeField] private float moveSpeed = 3.5f;

        public int RequiredPlayerLevel => requiredPlayerLevel;
        public int UnlockCost => unlockCost;
        public string JanitorName => janitorName;
        public string Description => description;
        public GameObject JanitorPrefab => janitorPrefab;
        public float MoveSpeed => moveSpeed;
    }
}
