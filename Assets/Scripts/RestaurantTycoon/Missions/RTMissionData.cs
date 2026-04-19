using UnityEngine;

namespace RestaurantTycoon
{
    [CreateAssetMenu(fileName = "New RT Mission", menuName = "Mall Mania/Restaurant Tycoon/Mission Data")]
    public class RTMissionData : ScriptableObject
    {
        [Header("Mission")]
        [Tooltip("Target money amount to collect")]
        public int targetAmount;

        [TextArea]
        public string description;

        public string GetProgressText(int currentValue)
        {
            return $"Earn ${currentValue} / ${targetAmount}";
        }
    }
}
