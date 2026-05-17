using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// Identifies a specific type of ingredient.
    /// Create one asset per ingredient type (e.g. Tomato, Lettuce, Patty).
    /// Assign the same asset to the RTIngredient prefab AND to the RTCookInputContainer
    /// that should accept it.
    /// </summary>
    [CreateAssetMenu(fileName = "NewIngredientType", menuName = "Restaurant Tycoon/Ingredient Type")]
    public class RTIngredientType : ScriptableObject
    {
        [Tooltip("Display name shown in debug logs and UI.")]
        public string displayName;
    }
}
