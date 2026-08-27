using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// A single seat at a dining table. Tracks occupation by RTCustomer.
    /// Reuses the same physical seat objects as the FoodStore scene.
    /// </summary>
    public class RTDiningSeat : MonoBehaviour
    {
        [Header("Positions")]
        [SerializeField] private Transform sitPoint;
        [SerializeField] private Transform approachPoint;

        [Header("Settings")]
        [SerializeField] private float eatingDuration = 5f;

        private RTDiningTable parentTable;
        private RTCustomer occupyingCustomer;
        private bool isOccupied;

        public Transform SitPoint => sitPoint;
        public Transform ApproachPoint => approachPoint;
        public RTDiningTable ParentTable => parentTable;
        public float EatingDuration => eatingDuration;
        public bool IsOccupied
        {
            get
            {
                ClearMissingOccupant();
                return isOccupied;
            }
        }
        public bool IsAvailable
        {
            get
            {
                ClearMissingOccupant();
                return !isOccupied && (parentTable == null || !parentTable.HasDirtyDishes);
            }
        }

        public void Initialize(RTDiningTable table)
        {
            parentTable = table;
        }

        public Vector3 GetNavigationTargetPosition()
        {
            if (approachPoint != null)
                return approachPoint.position;

            if (sitPoint != null)
                return sitPoint.position;

            return transform.position;
        }

        public Quaternion GetSeatedRotation()
        {
            if (parentTable != null && sitPoint != null)
            {
                Vector3 dir = parentTable.transform.position - sitPoint.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.001f)
                    return Quaternion.LookRotation(dir);
            }
            return sitPoint != null ? sitPoint.rotation : Quaternion.identity;
        }

        public bool Reserve(RTCustomer customer)
        {
            ClearMissingOccupant();
            if (!IsAvailable)
            {
                Debug.LogWarning("[RTDiningSeat] Already occupied!");
                return false;
            }
            isOccupied = true;
            occupyingCustomer = customer;
            Debug.Log("[RTDiningSeat] Seat reserved");
            return true;
        }

        public void Release()
        {
            isOccupied = false;
            occupyingCustomer = null;
            Debug.Log("[RTDiningSeat] Seat released");
        }

        private void ClearMissingOccupant()
        {
            if (isOccupied && occupyingCustomer == null)
            {
                isOccupied = false;
                occupyingCustomer = null;
            }
        }

        private void OnDrawGizmos()
        {
            if (sitPoint != null)
            {
                Gizmos.color = isOccupied ? Color.red : Color.green;
                Gizmos.DrawWireSphere(sitPoint.position, 0.2f);
            }

            if (approachPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(approachPoint.position, 0.15f);
                if (sitPoint != null)
                    Gizmos.DrawLine(approachPoint.position, sitPoint.position);
            }
        }
    }
}
