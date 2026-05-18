using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// ScriptableObject that configures a generic scene-object unlock.
    /// Create via: Assets > Create > Restaurant Tycoon > Scene Object Unlock Data
    /// </summary>
    [CreateAssetMenu(fileName = "NewSceneObjectUnlockData", menuName = "Restaurant Tycoon/Scene Object Unlock Data")]
    public class RTSceneObjectUnlockData : ScriptableObject
    {
        [Header("Unlock Requirements")]
        [Tooltip("Player level required before the unlock spot becomes visible.")]
        [SerializeField] private int requiredPlayerLevel = 1;

        [Header("Cost")]
        [SerializeField] private int unlockCost = 500;

        [Header("Display")]
        [SerializeField] private string unlockName = "Unlock";
        [TextArea(2, 4)]
        [SerializeField] private string description = "";

        public int RequiredPlayerLevel => requiredPlayerLevel;
        public int UnlockCost => unlockCost;
        public string UnlockName => unlockName;
        public string Description => description;
    }
}
