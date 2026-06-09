namespace RestaurantTycoon
{
    /// <summary>
    /// Implemented by any staff MonoBehaviour that supports runtime upgrades.
    /// Speed and carry capacity methods have default no-op implementations so
    /// existing staff (RTCook, RTCashierCharacter) don't need to change.
    /// </summary>
    public interface IUpgradeableStaff
    {
        /// <summary>Apply a new work duration (cook time, collect/deliver delay, etc.).</summary>
        void SetUpgradedDuration(float newDuration);

        /// <summary>Apply a new movement speed. Default: no-op.</summary>
        void SetUpgradedSpeed(float newSpeed) { }

        /// <summary>
        /// Apply a new carry capacity.
        /// Porter: max ingredients per trip. Janitor: max tables per trip.
        /// Default: no-op.
        /// </summary>
        void SetCarryCapacity(int capacity) { }
    }
}
