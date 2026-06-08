using UnityEngine;
using System;
using System.Collections.Generic;

namespace RestaurantTycoon
{
    /// <summary>
    /// Static registry that tracks every active RT interaction spot.
    /// Spot scripts call RegisterSpot / UnregisterSpot from their Show / Hide methods.
    /// RTPlayerArrow listens to OnTargetChanged and reads CurrentTarget each frame.
    ///
    /// No MonoBehaviour needed — pure C# static class, no scene setup required.
    /// </summary>
    public static class RTSpotRegistry
    {
        // Ordered list — first registered = first targeted (FIFO).
        private static readonly List<Transform> activeSpots = new List<Transform>();

        /// <summary>Fired whenever the front-of-queue target changes (new spot added, or current spot removed).</summary>
        public static event Action OnTargetChanged;

        /// <summary>The spot the arrow should point toward, or null if none are active.</summary>
        public static Transform CurrentTarget => activeSpots.Count > 0 ? activeSpots[0] : null;

        /// <summary>How many spots are currently registered.</summary>
        public static int Count => activeSpots.Count;

        /// <summary>
        /// Called by a spot's Show() method.
        /// Adds the spot to the queue; fires OnTargetChanged if it becomes the new front target.
        /// </summary>
        public static void RegisterSpot(Transform spot)
        {
            if (spot == null || activeSpots.Contains(spot)) return;

            Transform previousTarget = CurrentTarget;
            activeSpots.Add(spot);

            if (CurrentTarget != previousTarget)
                OnTargetChanged?.Invoke();
        }

        /// <summary>
        /// Called by a spot's Hide() method.
        /// Removes the spot from the queue; fires OnTargetChanged if the front target changed.
        /// </summary>
        public static void UnregisterSpot(Transform spot)
        {
            if (spot == null) return;

            Transform previousTarget = CurrentTarget;
            activeSpots.Remove(spot);

            if (CurrentTarget != previousTarget)
                OnTargetChanged?.Invoke();
        }

        /// <summary>Clears all registered spots (e.g. on scene unload).</summary>
        public static void Clear()
        {
            activeSpots.Clear();
            OnTargetChanged?.Invoke();
        }
    }
}
