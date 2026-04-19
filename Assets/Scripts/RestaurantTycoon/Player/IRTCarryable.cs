using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// Interface for any item that can be carried by the player.
    /// Ingredients, finished items (coffee etc.), and garbage all implement this.
    /// </summary>
    public interface IRTCarryable
    {
        CarryableType CarryType { get; }
        GameObject GameObject { get; }
        void OnPickedUp(Transform carryPoint);
        void OnDropped();
        void OnDisposed();
    }

    public enum CarryableType
    {
        Ingredient,
        FinishedItem,
        Garbage
    }
}
