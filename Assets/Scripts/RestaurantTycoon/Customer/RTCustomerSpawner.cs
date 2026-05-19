using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RestaurantTycoon
{
    /// <summary>
    /// Spawns a fixed number of customers one by one with a delay.
    /// The next batch only spawns after ALL customers from the current batch
    /// have fully exited the restaurant.
    /// </summary>
    public class RTCustomerSpawner : MonoBehaviour
    {
        [Header("Spawning")]
        [SerializeField] private GameObject customerPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform exitPoint;

        [Header("Batch Settings")]
        [Tooltip("Number of customers to spawn per batch")]
        [SerializeField] private int customerCount = 3;
        [SerializeField] private float spawnDelay = 1.5f;
        [Tooltip("Delay before starting the next batch after all customers exit")]
        [SerializeField] private float batchDelay = 2f;

        [Header("Target Counters")]
        [Tooltip("All counters in the scene. Each spawned customer is randomly assigned one.")]
        [SerializeField] private List<RTCustomerCounter> targetCounters = new List<RTCustomerCounter>();

        [Header("Dining")]
        [SerializeField] private RTDiningArea diningArea;

        [Header("Cashier")]
        [SerializeField] private RTCashier cashier;

        [Header("Appearance")]
        [Tooltip("Random skins assigned to customers")]
        [SerializeField] private List<Material> customerSkins = new List<Material>();

        private List<RTCustomer> activeCustomers = new List<RTCustomer>();
        private bool isSpawning = false;
        private int currentBatchTotal = 0;

        public int CustomerCount => customerCount;
        public int ActiveCount => activeCustomers.Count;
        public bool IsSpawning => isSpawning;

        private bool AnyCounterAvailable => targetCounters.Exists(c => c != null && c.gameObject.activeInHierarchy && c.CanAcceptCustomer);

        private RTCustomerCounter GetRandomAvailableCounter()
        {
            List<RTCustomerCounter> available = targetCounters.FindAll(c => c != null && c.gameObject.activeInHierarchy && c.CanAcceptCustomer);
            if (available.Count == 0) return null;
            return available[Random.Range(0, available.Count)];
        }

        private void Start()
        {
            if (spawnPoint == null) spawnPoint = transform;

            // Auto-find counters if none assigned
            if (targetCounters.Count == 0)
            {
                targetCounters.AddRange(FindObjectsOfType<RTCustomerCounter>());
            }

            // Subscribe to item placed events on all counters
            foreach (var counter in targetCounters)
                if (counter != null)
                    counter.OnItemPlaced += OnItemPlacedOnCounter;

            StartCoroutine(BatchLoop());
        }

        private void OnDestroy()
        {
            foreach (var counter in targetCounters)
                if (counter != null)
                    counter.OnItemPlaced -= OnItemPlacedOnCounter;
        }

        private IEnumerator BatchLoop()
        {
            while (true)
            {
                // Wait until previous batch is fully done
                while (activeCustomers.Count > 0)
                    yield return new WaitForSeconds(0.5f);

                yield return new WaitForSeconds(batchDelay);

                // Spawn batch
                yield return StartCoroutine(SpawnBatch());
            }
        }

        private IEnumerator SpawnBatch()
        {
            isSpawning = true;
            currentBatchTotal = customerCount;

            Debug.Log($"[RTCustomerSpawner] Starting batch of {customerCount} customers.");

            for (int i = 0; i < customerCount; i++)
            {
                // Wait until at least one counter has queue space
                while (!AnyCounterAvailable)
                    yield return new WaitForSeconds(0.3f);

                SpawnOneCustomer();

                if (i < customerCount - 1)
                    yield return new WaitForSeconds(spawnDelay);
            }

            isSpawning = false;
            Debug.Log($"[RTCustomerSpawner] Batch spawning complete. Active: {activeCustomers.Count}");
        }

        private void SpawnOneCustomer()
        {
            if (customerPrefab == null)
            {
                Debug.LogError("[RTCustomerSpawner] customerPrefab is null!");
                return;
            }

            RTCustomerCounter counter = GetRandomAvailableCounter();
            if (counter == null)
            {
                Debug.LogError("[RTCustomerSpawner] No available counter to assign!");
                return;
            }

            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            GameObject obj = Instantiate(customerPrefab, pos, rot);
            RTCustomer customer = obj.GetComponent<RTCustomer>();

            if (customer == null)
            {
                Debug.LogError("[RTCustomerSpawner] customerPrefab missing RTCustomer component!");
                Destroy(obj);
                return;
            }

            activeCustomers.Add(customer);
            customer.Initialize(counter, exitPoint, this, customerSkins, diningArea, cashier);

            Debug.Log($"[RTCustomerSpawner] Spawned customer -> counter '{counter.name}'. Active: {activeCustomers.Count}/{currentBatchTotal}");
        }

        /// <summary>
        /// Called by RTCustomer when it fully exits the restaurant.
        /// </summary>
        public void OnCustomerExited(RTCustomer customer)
        {
            activeCustomers.Remove(customer);
            Debug.Log($"[RTCustomerSpawner] Customer exited. Active: {activeCustomers.Count}/{currentBatchTotal}");
        }

        /// <summary>
        /// When an item is placed on any counter, notify that counter's front customer.
        /// </summary>
        private void OnItemPlacedOnCounter()
        {
            foreach (var counter in targetCounters)
            {
                if (counter == null) continue;
                RTCustomer front = counter.GetFrontCustomer();
                if (front != null && front.IsWaitingAtCounter)
                    front.TryPickUpItem();
            }
        }

        /// <summary>
        /// Set the batch size at runtime (e.g., from upgrades).
        /// </summary>
        public void SetCustomerCount(int count)
        {
            customerCount = Mathf.Max(1, count);
        }

        private void OnDrawGizmos()
        {
            if (spawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);
            }

            if (exitPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(exitPoint.position, 0.5f);
            }

            Gizmos.color = Color.yellow;
            foreach (var counter in targetCounters)
            {
                if (counter != null)
                    Gizmos.DrawLine(spawnPoint != null ? spawnPoint.position : transform.position,
                        counter.transform.position);
            }
        }
    }
}
