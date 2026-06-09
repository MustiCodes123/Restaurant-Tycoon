using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

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

        [Header("Frenzy Mode")]
        [Tooltip("Seconds after game start before the first frenzy begins.")]
        [SerializeField] private float frenzyStartDelay = 60f;
        [Tooltip("How long the frenzy lasts in seconds.")]
        [SerializeField] private float frenzyDuration = 30f;
        [Tooltip("Max customers queued per counter during frenzy (matches the 3-slot queue layout).")]
        [SerializeField] private int frenzyCustomersPerCounter = 3;
        [Tooltip("UI panel that is shown while frenzy is active.")]
        [SerializeField] private GameObject frenzyPanel;
        [Tooltip("Text that displays the remaining frenzy time countdown.")]
        [SerializeField] private TMP_Text frenzyTimerText;

        private List<RTCustomer> activeCustomers = new List<RTCustomer>();
        private bool isSpawning = false;
        private int currentBatchTotal = 0;

        private bool isFrenzy = false;

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

            if (frenzyPanel != null) frenzyPanel.SetActive(false);

            StartCoroutine(FrenzySchedulerLoop());
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
                // Suspend while frenzy is running — resume once it ends
                while (isFrenzy)
                    yield return new WaitForSeconds(0.5f);

                // Wait until previous batch is fully done
                while (activeCustomers.Count > 0)
                    yield return new WaitForSeconds(0.5f);

                yield return new WaitForSeconds(batchDelay);

                // Frenzy may have started during batchDelay
                if (isFrenzy) continue;

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

        // assignedCounter: pass a specific counter (frenzy refill), or null to pick randomly.
        private void SpawnOneCustomer(RTCustomerCounter assignedCounter = null)
        {
            if (customerPrefab == null)
            {
                Debug.LogError("[RTCustomerSpawner] customerPrefab is null!");
                return;
            }

            RTCustomerCounter counter = assignedCounter ?? GetRandomAvailableCounter();
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

            // During frenzy: immediately spawn a replacement at the most underfilled counter
            if (isFrenzy)
                TrySpawnFrenzyReplacement();
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

        #region Frenzy Mode

        /// <summary>
        /// Runs forever. Each cycle: waits until active customers drop below the normal
        /// batch size, then waits frenzyStartDelay, then runs a full frenzy session.
        /// </summary>
        private IEnumerator FrenzySchedulerLoop()
        {
            while (true)
            {
                // Wait until the restaurant is not at full normal capacity
                while (activeCustomers.Count >= customerCount)
                    yield return new WaitForSeconds(0.5f);

                // Customer count dropped below threshold — begin countdown
                Debug.Log("[RTCustomerSpawner] Frenzy countdown started.");
                yield return new WaitForSeconds(frenzyStartDelay);

                // Run the frenzy session and wait for it to finish
                yield return StartCoroutine(RunFrenzy());

                // Small gap before re-arming so the batch loop can stabilise
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator RunFrenzy()
        {
            isFrenzy = true;
            Debug.Log("[RTCustomerSpawner] Frenzy started!");

            if (frenzyPanel != null) frenzyPanel.SetActive(true);

            // Immediately fill every active counter to frenzyCustomersPerCounter
            FillFrenzyCapacity();

            float remaining = frenzyDuration;
            while (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                if (frenzyTimerText != null)
                    frenzyTimerText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
                yield return null;
            }

            EndFrenzy();
        }

        private void FillFrenzyCapacity()
        {
            foreach (var counter in targetCounters)
            {
                if (counter == null || !counter.gameObject.activeInHierarchy) continue;
                int toSpawn = Mathf.Max(0, frenzyCustomersPerCounter - counter.QueueCount);
                for (int i = 0; i < toSpawn; i++)
                    SpawnOneCustomer(counter);
            }
        }

        private void TrySpawnFrenzyReplacement()
        {
            RTCustomerCounter counter = GetFrenzyAvailableCounter();
            if (counter != null)
                SpawnOneCustomer(counter);
        }

        /// <summary>Returns the active counter with the fewest customers that is still below the frenzy cap.</summary>
        private RTCustomerCounter GetFrenzyAvailableCounter()
        {
            var available = targetCounters.FindAll(c =>
                c != null &&
                c.gameObject.activeInHierarchy &&
                c.CanAcceptCustomer &&
                c.QueueCount < frenzyCustomersPerCounter);

            if (available.Count == 0) return null;
            available.Sort((a, b) => a.QueueCount.CompareTo(b.QueueCount));
            return available[0];
        }

        private void EndFrenzy()
        {
            isFrenzy = false;
            if (frenzyPanel != null) frenzyPanel.SetActive(false);
            if (frenzyTimerText != null) frenzyTimerText.text = string.Empty;
            Debug.Log("[RTCustomerSpawner] Frenzy ended. Returning to normal batch mode.");
        }

        #endregion

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
