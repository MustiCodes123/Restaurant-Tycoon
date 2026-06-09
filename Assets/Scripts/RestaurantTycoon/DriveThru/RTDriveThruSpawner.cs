using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RestaurantTycoon
{
    /// <summary>
    /// Spawns drive-through cars at a configurable interval.
    ///
    /// ── Inspector Setup ──────────────────────────────────────────────────────
    ///
    /// 1. Assign carPrefab (has RTDriveThruCar component).
    ///
    /// 2. Build the APPROACH path (spawn → stop window):
    ///    - spawnPoint          : where the car appears (off-screen).
    ///    - approachWaypoints[] : intermediate road points.
    ///    - stopPoint           : where the car parks and waits for the player.
    ///
    /// 3. Build the DEPARTURE path (stop window → off-screen):
    ///    - departWaypoints[]   : intermediate road points.
    ///    - destroyPoint        : final off-screen position; car is destroyed here.
    ///
    ///    Full approach path passed to car: spawnPoint → approachWaypoints → stopPoint
    ///    Full depart path passed to car  : stopPoint  → departWaypoints   → destroyPoint
    ///
    /// 4. Drag every RTCustomerCounter in the scene into allCounters.
    ///    The spawner filters to counters whose GameObject is activeInHierarchy
    ///    (= stall is unlocked). If none are active, the spawn is skipped.
    ///
    /// 5. Tune firstSpawnDelay, spawnInterval, minPayment, maxPayment.
    ///    Use maxCarsAtOnce = 1 to allow only one car at a time (recommended).
    /// </summary>
    public class RTDriveThruSpawner : MonoBehaviour
    {
        // ── Prefab & Path ─────────────────────────────────────────────────────

        [Header("Car Prefabs")]
        [Tooltip("All car prefabs. One is chosen at random each spawn.")]
        [SerializeField] private List<GameObject> carPrefabs = new List<GameObject>();

        [Header("Approach Path (Spawn → Stop)")]
        [Tooltip("Car is instantiated here (should be off-screen).")]
        [SerializeField] private Transform spawnPoint;
        [Tooltip("Optional intermediate waypoints the car follows on the way to the stop window.")]
        [SerializeField] private Transform[] approachWaypoints;
        [Tooltip("Where the car stops and waits for the player.")]
        [SerializeField] private Transform stopPoint;

        [Header("Departure Path (Stop → Destroy)")]
        [Tooltip("Optional intermediate waypoints after the car is served / timed out.")]
        [SerializeField] private Transform[] departWaypoints;
        [Tooltip("Car is destroyed when it reaches this point (should be off-screen).")]
        [SerializeField] private Transform destroyPoint;

        // ── Spawn Timing ──────────────────────────────────────────────────────

        [Header("Spawn Timing")]
        [Tooltip("Seconds before the first car spawns after the scene loads.")]
        [SerializeField] private float firstSpawnDelay = 5f;
        [Tooltip("Seconds between each subsequent car spawn attempt.")]
        [SerializeField] private float spawnInterval = 30f;
        [Tooltip("Start spawning automatically on Start().")]
        [SerializeField] private bool autoStart = true;
        [Tooltip("Maximum cars allowed in the drive-through at once. 0 = unlimited.")]
        [SerializeField] private int maxCarsAtOnce = 1;

        // ── Order Settings ────────────────────────────────────────────────────

        [Header("Order Settings")]
        [Tooltip("All RTCustomerCounters in the scene. Spawner only orders food from active (unlocked) counters.")]
        [SerializeField] private List<RTCustomerCounter> allCounters = new List<RTCustomerCounter>();
        [SerializeField] private int minPayment = 40;
        [SerializeField] private int maxPayment = 80;

        // ── Runtime ───────────────────────────────────────────────────────────

        private Coroutine spawnCoroutine;
        private List<RTDriveThruCar> activeCars = new List<RTDriveThruCar>();

        // ─────────────────────────────────────────────────────────────────────
        // Unity
        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            if (autoStart)
                StartSpawning();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public void StartSpawning()
        {
            if (spawnCoroutine != null) return;
            spawnCoroutine = StartCoroutine(SpawnCoroutine());
        }

        public void StopSpawning()
        {
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Spawn loop
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator SpawnCoroutine()
        {
            yield return new WaitForSeconds(firstSpawnDelay);

            while (true)
            {
                TrySpawnCar();
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        private void TrySpawnCar()
        {
            // Cap check.
            CleanDeadCars();
            if (maxCarsAtOnce > 0 && activeCars.Count >= maxCarsAtOnce)
            {
                Debug.Log("[RTDriveThruSpawner] Max cars already in lane, skipping spawn.");
                return;
            }

            // Find available counters (= food stalls that are unlocked / active).
            var available = new List<RTCustomerCounter>();
            foreach (var counter in allCounters)
            {
                if (counter != null && counter.gameObject.activeInHierarchy)
                    available.Add(counter);
            }

            if (available.Count == 0)
            {
                Debug.Log("[RTDriveThruSpawner] No active counters found, skipping spawn.");
                return;
            }

            // Pick a random active counter.
            RTCustomerCounter chosen = available[Random.Range(0, available.Count)];
            RTIngredientType itemType = chosen.AcceptedItemType;

            if (itemType == null)
            {
                Debug.LogWarning("[RTDriveThruSpawner] Chosen counter has no AcceptedItemType, skipping.");
                return;
            }

            SpawnCar(itemType, Random.Range(minPayment, maxPayment + 1));
        }

        private void SpawnCar(RTIngredientType itemType, int payment)
        {
            if (carPrefabs == null || carPrefabs.Count == 0 || spawnPoint == null || stopPoint == null || destroyPoint == null)
            {
                Debug.LogError("[RTDriveThruSpawner] Missing required references (carPrefabs / spawnPoint / stopPoint / destroyPoint).");
                return;
            }

            // Pick a random car prefab from the list (filter out nulls first).
            var validPrefabs = carPrefabs.FindAll(p => p != null);
            if (validPrefabs.Count == 0)
            {
                Debug.LogError("[RTDriveThruSpawner] carPrefabs list has no valid (non-null) entries.");
                return;
            }
            GameObject carPrefab = validPrefabs[Random.Range(0, validPrefabs.Count)];

            // Build approach path: spawnPoint → approachWaypoints → stopPoint
            Vector3[] approachPath = BuildPath(spawnPoint, approachWaypoints, stopPoint);

            // Build depart path: stopPoint → departWaypoints → destroyPoint
            Vector3[] departPath = BuildPath(stopPoint, departWaypoints, destroyPoint);

            GameObject carGO = Instantiate(carPrefab, spawnPoint.position, spawnPoint.rotation);
            RTDriveThruCar car = carGO.GetComponent<RTDriveThruCar>();

            if (car == null)
            {
                Debug.LogError("[RTDriveThruSpawner] carPrefab is missing an RTDriveThruCar component.");
                Destroy(carGO);
                return;
            }

            activeCars.Add(car);
            car.Initialize(itemType, payment, approachPath, departPath);

            Debug.Log($"[RTDriveThruSpawner] Spawned car ordering '{itemType.displayName}' for ${payment}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a flat Vector3 array from: start (excluded) → midPoints → end.
        /// The car is already at "start", so we only pass points it needs to MOVE TO.
        /// </summary>
        private static Vector3[] BuildPath(Transform start, Transform[] midPoints, Transform end)
        {
            int mid = midPoints != null ? midPoints.Length : 0;
            Vector3[] path = new Vector3[mid + 1];

            for (int i = 0; i < mid; i++)
                path[i] = midPoints[i].position;

            path[mid] = end.position;
            return path;
        }

        private void CleanDeadCars()
        {
            activeCars.RemoveAll(c => c == null);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw approach path.
            Gizmos.color = Color.green;
            DrawPathGizmo(spawnPoint, approachWaypoints, stopPoint);

            // Draw depart path.
            Gizmos.color = Color.red;
            DrawPathGizmo(stopPoint, departWaypoints, destroyPoint);

            // Mark stop point.
            if (stopPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(stopPoint.position, 0.5f);
                UnityEditor.Handles.Label(stopPoint.position + Vector3.up * 0.7f, "STOP");
            }
        }

        private static void DrawPathGizmo(Transform from, Transform[] mids, Transform to)
        {
            if (from == null || to == null) return;

            Vector3 prev = from.position;

            if (mids != null)
            {
                foreach (var wp in mids)
                {
                    if (wp == null) continue;
                    Gizmos.DrawLine(prev, wp.position);
                    Gizmos.DrawWireSphere(wp.position, 0.2f);
                    prev = wp.position;
                }
            }

            Gizmos.DrawLine(prev, to.position);
            Gizmos.DrawWireSphere(to.position, 0.3f);
        }
#endif
    }
}
