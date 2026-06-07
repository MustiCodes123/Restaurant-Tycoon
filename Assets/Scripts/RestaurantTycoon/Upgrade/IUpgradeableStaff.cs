namespace RestaurantTycoon
{
    /// <summary>
    /// Implemented by any staff MonoBehaviour that supports upgrade-driven
    /// duration reduction (RTCook, RTPorterController, RTCashierCharacter).
    /// </summary>
    public interface IUpgradeableStaff
    {
        /// <summary>
        /// Apply a new work duration coming from an upgrade purchase.
        /// Each staff type maps this to its own relevant field(s).
        /// </summary>
        void SetUpgradedDuration(float newDuration);
    }
}
