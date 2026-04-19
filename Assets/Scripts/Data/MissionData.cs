using UnityEngine;

public enum MissionType
{
    ServeCustomers,
    TotalEarnings
}

public enum ServiceLocation
{
    Any,
    Store
}

[CreateAssetMenu(fileName = "New Mission", menuName = "Mall Mania/Mission Data")]
public class MissionData : ScriptableObject
{
    [Header("Mission Settings")]
    public MissionType missionType;
    public int targetValue;
    
    [Header("Serve Customers Settings")]
    [Tooltip("Only used for ServeCustomers mission type")]
    public ServiceLocation serviceLocation = ServiceLocation.Any;
    [Tooltip("If location is Store, specify which store (leave empty for any store)")]
    public string specificStoreName;
    
    [Header("Display")]
    [TextArea] public string description;
    
    public string GetProgressText(int currentValue)
    {
        switch (missionType)
        {
            case MissionType.ServeCustomers:
                string locationText = GetLocationText();
                return $"Serve {currentValue}/{targetValue} customers{locationText}";
            
            case MissionType.TotalEarnings:
                // Now shows level-based earnings, not lifetime
                return $"Earn ${currentValue} / ${targetValue}";
            
            default:
                return $"{currentValue}/{targetValue}";
        }
    }
    
    private string GetLocationText()
    {
        switch (serviceLocation)
        {
            case ServiceLocation.Store:
                if (!string.IsNullOrEmpty(specificStoreName))
                    return $" at {specificStoreName}";
                return " at Store";
            default:
                return "";
        }
    }
}
