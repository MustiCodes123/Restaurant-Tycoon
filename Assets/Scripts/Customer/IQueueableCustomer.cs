using UnityEngine;

/// <summary>
/// Interface for any customer that can be queued at a ServiceSpot.
/// Implemented by both Customer and FoodCustomer.
/// </summary>
public interface IQueueableCustomer
{
    /// <summary>
    /// The current state of the customer for queue purposes
    /// </summary>
    bool IsWaitingAtStore { get; }
    
    /// <summary>
    /// The GameObject this customer is attached to
    /// </summary>
    GameObject GameObject { get; }
    
    /// <summary>
    /// Called when the customer's queue position changes
    /// </summary>
    void OnQueuePositionChanged(int newPosition, ServiceSpot spot);
    
    /// <summary>
    /// Called when the customer is served at the store
    /// </summary>
    void OnServed();
}
