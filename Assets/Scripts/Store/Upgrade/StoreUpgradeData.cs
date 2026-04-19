using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "StoreUpgradeData", menuName = "Mall Mania/Store Upgrade Data")]
public class StoreUpgradeData : ScriptableObject
{
    [Serializable]
    public class UpgradeLevel
    {
        [Header("Unlock Requirements")]
        [Tooltip("Player level required to unlock this upgrade")]
        public int requiredPlayerLevel = 1;
        
        [Header("Upgrade Cost")]
        [Tooltip("Total cost to complete this upgrade")]
        public int upgradeCost = 100;
        
        [Header("Optional Benefits")]
        [Tooltip("Additional money per customer after upgrade")]
        public int bonusMoneyPerCustomer = 0;
        
        [Header("Display")]
        [Tooltip("Name shown in UI")]
        public string upgradeName = "Upgrade";
        [Tooltip("Description shown in UI")]
        [TextArea(2, 4)]
        public string description = "";
    }
    
    [Header("Store Identification")]
    [SerializeField] private string storeId;
    
    [Header("Upgrade Levels")]
    [SerializeField] private List<UpgradeLevel> upgradeLevels = new List<UpgradeLevel>();
    
    public string StoreId => storeId;
    public List<UpgradeLevel> UpgradeLevels => upgradeLevels;
    public int MaxUpgradeLevel => upgradeLevels.Count;
    
    /// <summary>
    /// Gets upgrade data for a specific level (0-indexed)
    /// </summary>
    public UpgradeLevel GetUpgradeLevel(int levelIndex)
    {
        if (levelIndex >= 0 && levelIndex < upgradeLevels.Count)
        {
            return upgradeLevels[levelIndex];
        }
        return null;
    }
    
    /// <summary>
    /// Checks if a specific upgrade level is unlockable at the given player level
    /// </summary>
    public bool IsUpgradeUnlocked(int upgradeLevelIndex, int currentPlayerLevel)
    {
        var upgrade = GetUpgradeLevel(upgradeLevelIndex);
        if (upgrade == null) return false;
        
        return currentPlayerLevel >= upgrade.requiredPlayerLevel;
    }
}
