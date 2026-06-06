using UnityEngine;
using System.Collections;

namespace RestaurantTycoon
{
    /// <summary>
    /// NPC cashier character that stands at the register and automatically serves
    /// customers waiting in the RTCashier queue. Mirrors how RTCook works:
    /// watches for a ready customer, plays a serving animation with RadialProgressUI,
    /// then completes the transaction and loops.
    /// </summary>
    public class RTCashierCharacter : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The RTCashier counter that manages the customer queue.")]
        [SerializeField] private RTCashier cashier;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string servingParam = "IsServing";

        [Header("Radial Progress UI")]
        [Tooltip("RadialProgressUI as a child of this character (same setup as RTCook).")]
        [SerializeField] private RadialProgressUI radialProgressUI;

        [Header("Service")]
        [Tooltip("How long it takes to serve one customer (seconds).")]
        [SerializeField] private float serviceDuration = 2f;
        [Tooltip("How often to poll the queue when idle (seconds).")]
        [SerializeField] private float checkInterval = 0.3f;

        private bool isServicing = false;
        private Coroutine serviceLoopCoroutine;

        public bool IsServicing => isServicing;

        private void Start()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (radialProgressUI == null)
                radialProgressUI = GetComponentInChildren<RadialProgressUI>();

            if (radialProgressUI != null)
                radialProgressUI.SetFillDuration(serviceDuration);

            if (cashier == null)
                cashier = GetComponentInParent<RTCashier>();

            if (cashier == null)
            {
                Debug.LogError("[RTCashierCharacter] No RTCashier assigned! Please assign one in the Inspector.");
                return;
            }

            // Register this character's transform so money flows to us
            cashier.SetServiceTransform(transform);

            // Subscribe to know when a customer joins so we can wake up immediately
            cashier.OnCustomerServed += OnCustomerServed;

            serviceLoopCoroutine = StartCoroutine(ServiceLoop());

            Debug.Log($"[RTCashierCharacter] Started. Cashier: {cashier.name}");
        }

        private void OnDestroy()
        {
            if (cashier != null)
                cashier.OnCustomerServed -= OnCustomerServed;
        }

        private void OnCustomerServed()
        {
            // Wake up the loop if it stopped waiting
            if (!isServicing && serviceLoopCoroutine == null)
                serviceLoopCoroutine = StartCoroutine(ServiceLoop());
        }

        private IEnumerator ServiceLoop()
        {
            Debug.Log("[RTCashierCharacter] ServiceLoop started.");
            while (true)
            {
                // Wait until we finish any current service
                while (isServicing)
                    yield return null;

                if (cashier.CanServe)
                {
                    Debug.Log("[RTCashierCharacter] Customer ready — starting service.");
                    yield return StartCoroutine(ServeOneCustomer());
                }
                else
                {
                    yield return new WaitForSeconds(checkInterval);
                }
            }
        }

        private IEnumerator ServeOneCustomer()
        {
            isServicing = true;

            // Notify the cashier counter that servicing has started
            cashier.StartService();

            // Play serving animation
            SetServing(true);

            // Start radial progress UI
            if (radialProgressUI != null)
            {
                radialProgressUI.SetFillDuration(serviceDuration);
                radialProgressUI.StartProgress();
            }

            // Wait for the full service duration
            yield return new WaitForSeconds(serviceDuration);

            // Stop radial progress UI
            if (radialProgressUI != null)
                radialProgressUI.StopProgress();

            // Stop serving animation
            SetServing(false);

            // Complete the transaction: handle money, notify customer, fire events
            cashier.CompleteService();

            isServicing = false;

            Debug.Log("[RTCashierCharacter] Customer served.");
        }

        private void SetServing(bool serving)
        {
            if (animator != null)
                animator.SetBool(servingParam, serving);
        }
    }
}
