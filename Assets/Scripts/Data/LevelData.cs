using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Level", menuName = "Mall Mania/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Level Info")]
    public int levelNumber;
    public string levelName;
    
    [Header("Missions")]
    public List<MissionData> missions = new List<MissionData>();
}
