using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// Central manager for one restaurant stall. Holds references to all
    /// components so they can be wired from a single place.
    /// Similar to FoodStore's role of coordinating ItemContainers → PickupPoint.
    /// </summary>
    public class RTStall : MonoBehaviour
    {
        [Header("Stall Info")]
        [SerializeField] private string stallName = "Restaurant Stall";

        [Header("Kitchen")]
        [SerializeField] private RTIngredientContainer ingredientContainer;
        [SerializeField] private RTCookInputContainer cookInputContainer;
        [SerializeField] private RTCookingSpot cookingSpot;
        [SerializeField] private RTItemOutputContainer itemOutputContainer;

        [Header("Service")]
        [SerializeField] private RTCustomerCounter customerCounter;
        [SerializeField] private RTCashier cashier;

        [Header("Dining")]
        [SerializeField] private RTDiningArea diningArea;

        [Header("Optional NPC Cook (can be enabled as upgrade)")]
        [SerializeField] private RTCook npcCook;

        // Public accessors for other scripts that need stall references
        public string StallName => stallName;
        public RTIngredientContainer IngredientContainer => ingredientContainer;
        public RTCookInputContainer CookInputContainer => cookInputContainer;
        public RTCookingSpot CookingSpot => cookingSpot;
        public RTItemOutputContainer ItemOutputContainer => itemOutputContainer;
        public RTCustomerCounter CustomerCounter => customerCounter;
        public RTCashier Cashier => cashier;
        public RTDiningArea DiningArea => diningArea;
        public RTCook NpcCook => npcCook;
    }
}
