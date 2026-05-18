using UnityEngine;
using System;
using System.Collections.Generic;

namespace RestaurantTycoon
{
    /// <summary>
    /// Manages all RT dining tables. Finds available seats for customers.
    /// Fires event when a seat becomes available (after dirty dishes cleared).
    /// </summary>
    public class RTDiningArea : MonoBehaviour
    {
        [Header("Tables")]
        [SerializeField] private List<RTDiningTable> tables = new List<RTDiningTable>();

        public event Action OnSeatBecameAvailable;
        public List<RTDiningTable> Tables => tables;

        private void Awake()
        {
            if (tables.Count == 0)
                tables.AddRange(GetComponentsInChildren<RTDiningTable>());

            foreach (var table in tables)
                table.OnDishesCleared += OnTableDishesCleared;
        }

        private void OnDestroy()
        {
            foreach (var table in tables)
                if (table != null)
                    table.OnDishesCleared -= OnTableDishesCleared;
        }

        private void OnTableDishesCleared()
        {
            Debug.Log("[RTDiningArea] Table cleared - seat now available");
            OnSeatBecameAvailable?.Invoke();
        }

        public RTDiningSeat FindAvailableSeat()
        {
            foreach (var table in tables)
            {
                RTDiningSeat seat = table.GetAvailableSeat();
                if (seat != null) return seat;
            }
            return null;
        }

        public bool HasAvailableSeat()
        {
            foreach (var table in tables)
                if (table.HasAvailableSeat()) return true;
            return false;
        }

        public int GetAvailableSeatCount()
        {
            int count = 0;
            foreach (var table in tables)
                count += table.GetAvailableSeatCount();
            return count;
        }
    }
}
