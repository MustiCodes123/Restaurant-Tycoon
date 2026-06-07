using UnityEngine;
using System;
using System.Collections.Generic;

namespace RestaurantTycoon
{
    /// <summary>
    /// ScriptableObject that defines the upgrade levels for a single staff member
    /// (cook, porter, or cashier). Each level reduces the staff's work duration.
    /// </summary>
    [CreateAssetMenu(fileName = "RTStaffUpgradeData", menuName = "Restaurant Tycoon/Staff Upgrade Data")]
    public class RTStaffUpgradeData : ScriptableObject
    {
        [Serializable]
        public class UpgradeLevel
        {
            [Tooltip("Label shown in the upgrade spot UI.")]
            public string upgradeName = "Speed Up";

            [Tooltip("Minimum game level the player must reach before this upgrade becomes visible.")]
            public int requiredPlayerLevel = 1;

            [Tooltip("Cost the player must pay to buy this upgrade.")]
            public int cost = 100;

            [Tooltip("The new work duration (seconds) after this upgrade is applied.\n" +
                     "For cooks this is cookDuration.\n" +
                     "For porters this is collectDelay & deliverDelay.\n" +
                     "For cashiers this is serviceDuration.")]
            public float newDuration = 1.5f;
        }

        [Header("Identity")]
        [Tooltip("Unique key used for PlayerPrefs save — must be unique per upgrade instance.")]
        [SerializeField] private string upgradeId;

        [Header("Upgrade Levels")]
        [SerializeField] private List<UpgradeLevel> upgradeLevels = new List<UpgradeLevel>();

        public string UpgradeId => upgradeId;
        public int MaxLevel => upgradeLevels.Count;

        /// <summary>Returns the upgrade level at the given 0-based index, or null if out of range.</summary>
        public UpgradeLevel GetLevel(int index)
        {
            if (index >= 0 && index < upgradeLevels.Count)
                return upgradeLevels[index];
            return null;
        }
    }
}
