using UnityEngine;
using System.Collections.Generic;

namespace RestaurantTycoon
{
    [CreateAssetMenu(fileName = "New RT Level", menuName = "Mall Mania/Restaurant Tycoon/Level Data")]
    public class RTLevelData : ScriptableObject
    {
        [Header("Level Info")]
        public int levelNumber;
        public string levelName;

        [Header("Missions")]
        public List<RTMissionData> missions = new List<RTMissionData>();
    }
}
