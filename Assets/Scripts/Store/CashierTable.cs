using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// DEPRECATED: This class is no longer used. Customers now go directly to store service spots.
/// Kept for backwards compatibility but does nothing.
/// Can be safely deleted from the project.
/// </summary>
public class CashierTable : MonoBehaviour
{
    [Header("Deprecated - No longer used")]
    [SerializeField] private Transform queueStartPoint;
    [SerializeField] private Transform queueDirection;
    [SerializeField] private float queueSpacing = 1.5f;
    [SerializeField] private int maxQueueSize = 5;
    [SerializeField] private ServiceSpot serviceSpot;
    
    private void Awake()
    {
        Debug.LogWarning("[CashierTable] This component is deprecated and no longer used. Please remove it from your scene.");
    }
}
