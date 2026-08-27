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
            RefreshTables();
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
            RefreshTables();

            foreach (var table in tables)
            {
                if (table == null || !table.gameObject.activeInHierarchy) continue;
                RTDiningSeat seat = table.GetAvailableSeat();
                if (seat != null) return seat;
            }
            return null;
        }

        public bool HasAvailableSeat()
        {
            RefreshTables();

            foreach (var table in tables)
                if (table != null && table.gameObject.activeInHierarchy && table.HasAvailableSeat()) return true;
            return false;
        }

        public int GetAvailableSeatCount()
        {
            RefreshTables();

            int count = 0;
            foreach (var table in tables)
                if (table != null && table.gameObject.activeInHierarchy)
                    count += table.GetAvailableSeatCount();
            return count;
        }

        private void RefreshTables()
        {
            for (int i = tables.Count - 1; i >= 0; i--)
            {
                if (tables[i] == null)
                    tables.RemoveAt(i);
            }

            RTDiningTable[] childTables = GetComponentsInChildren<RTDiningTable>(true);
            foreach (var table in childTables)
            {
                if (table == null || tables.Contains(table)) continue;

                tables.Add(table);
            }

            foreach (var table in tables)
            {
                table.OnDishesCleared -= OnTableDishesCleared;
                table.OnDishesCleared += OnTableDishesCleared;
            }
        }
    }
}
