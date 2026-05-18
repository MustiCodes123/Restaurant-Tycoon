using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// ScriptableObject that configures a single RT cook unlock.
    /// Create via: Assets > Create > Restaurant Tycoon > Cook Unlock Data
    /// </summary>
    [CreateAssetMenu(fileName = "NewCookUnlockData", menuName = "Restaurant Tycoon/Cook Unlock Data")]
    public class RTCookUnlockData : ScriptableObject
    {
        [Header("Unlock Requirements")]
        [Tooltip("Player level required before the unlock spot becomes visible.")]
        [SerializeField] private int requiredPlayerLevel = 1;

        [Header("Cost")]
        [SerializeField] private int unlockCost = 500;

        [Header("Display")]
        [SerializeField] private string cookName = "Cook";
        [TextArea(2, 4)]
        [SerializeField] private string description = "";

        public int RequiredPlayerLevel => requiredPlayerLevel;
        public int UnlockCost => unlockCost;
        public string CookName => cookName;
        public string Description => description;
    }
}
