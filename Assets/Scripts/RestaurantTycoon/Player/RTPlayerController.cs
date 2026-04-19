using UnityEngine;
using System.Collections;

namespace RestaurantTycoon
{
    public class RTPlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private FloatingJoystick joystick;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 15f;
        [SerializeField] private float movementDeadzone = 0.1f;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        [Header("Physics")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private float gravity = -9.81f;

        [Header("Service Interaction")]
        [SerializeField] private RadialProgressUI radialProgressUI;

        [Header("Carry System")]
        [SerializeField] private RTPlayerCarryController carryController;

        private Vector3 velocity;
        private bool isMoving;

        // Interaction spots
        private ServiceSpot currentServiceSpot;
        private CleaningSpot currentCleaningSpot;
        private UpgradeSpot currentUpgradeSpot;
        private RTCashier currentCashier;
        private RTCookingSpot currentCookingSpot;

        // Interaction states
        private bool isServicing = false;
        private bool isCleaning = false;
        private bool isUpgrading = false;
        private bool isServicingCashier = false;
        private bool isCooking = false;

        public bool IsMoving => isMoving;
        public RTPlayerCarryController CarryController => carryController;

        private void Start()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (radialProgressUI == null)
                radialProgressUI = GetComponentInChildren<RadialProgressUI>();

            if (carryController == null)
                carryController = GetComponent<RTPlayerCarryController>();
        }

        private void Update()
        {
            HandleMovement();
            HandleAnimation();
            ApplyGravity();
            HandleServiceInteraction();
            HandleCashierInteraction();
            HandleCookingInteraction();
            HandleCleaningInteraction();
            HandleUpgradeInteraction();
        }

        private void HandleMovement()
        {
            float horizontal = joystick.Horizontal;
            float vertical = joystick.Vertical;

            float inputMagnitude = Mathf.Clamp01(new Vector2(horizontal, vertical).magnitude);
            isMoving = inputMagnitude > movementDeadzone;

            if (isMoving)
            {
                // Camera-relative movement so joystick matches what the player sees on screen
                Transform cam = Camera.main.transform;
                Vector3 camForward = cam.forward;
                Vector3 camRight = cam.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                Vector3 moveDirection = (camForward * vertical + camRight * horizontal).normalized;
                Vector3 movement = moveDirection * moveSpeed * inputMagnitude * Time.deltaTime;
                characterController.Move(movement);

                if (moveDirection != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
        }

        private void HandleAnimation()
        {
            if (animator != null)
            {
                bool isCarrying = carryController != null && carryController.IsCarrying;

                if (!isCarrying)
                {
                    animator.SetBool("IsWalking", isMoving);
                }
            }

            if (carryController != null)
            {
                carryController.SetMoving(isMoving);
            }
        }

        private void ApplyGravity()
        {
            if (characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        #region Service Interaction (Cashier)

        private void HandleServiceInteraction()
        {
            if (isMoving && isServicing)
            {
                CancelService();
            }

            if (!isMoving && currentServiceSpot != null && currentServiceSpot.CanServe && !isServicing && !isCleaning && !isCooking)
            {
                StartService();
            }
        }

        private void StartService()
        {
            if (currentServiceSpot == null || !currentServiceSpot.CanServe)
                return;

            isServicing = true;
            currentServiceSpot.StartService();

            if (radialProgressUI != null)
            {
                radialProgressUI.StartProgress();
                StartCoroutine(WaitForServiceComplete());
            }
        }

        private IEnumerator WaitForServiceComplete()
        {
            if (radialProgressUI == null)
                yield break;

            yield return new WaitForSeconds(radialProgressUI.FillDuration);

            if (isServicing && currentServiceSpot != null)
            {
                CompleteService();
            }
        }

        private void CompleteService()
        {
            if (currentServiceSpot != null)
            {
                currentServiceSpot.CompleteService();
            }

            isServicing = false;

            if (radialProgressUI != null)
            {
                radialProgressUI.StopProgress();
            }
        }

        private void CancelService()
        {
            isServicing = false;

            if (radialProgressUI != null)
            {
                radialProgressUI.StopProgress();
            }
        }

        #endregion

        #region Cleaning Interaction

        private void HandleCleaningInteraction()
        {
            if (isMoving && isCleaning)
            {
                CancelCleaning();
            }

            if (!isMoving && currentCleaningSpot != null && currentCleaningSpot.IsDirty && !isCleaning && !isServicing && !isUpgrading && !isCooking)
            {
                StartCleaning();
            }

            if (isCleaning && currentCleaningSpot != null)
            {
                currentCleaningSpot.UpdateCleaning(Time.deltaTime);

                if (!currentCleaningSpot.IsDirty)
                {
                    isCleaning = false;
                }
            }
        }

        private void StartCleaning()
        {
            if (currentCleaningSpot == null || !currentCleaningSpot.IsDirty)
                return;

            isCleaning = true;
            currentCleaningSpot.StartCleaning();
        }

        private void CancelCleaning()
        {
            if (currentCleaningSpot != null && currentCleaningSpot.IsBeingCleaned)
            {
                currentCleaningSpot.CancelCleaning();
            }

            isCleaning = false;
        }

        #endregion

        #region Upgrade Interaction

        private void HandleUpgradeInteraction()
        {
            if (isMoving && isUpgrading)
            {
                CancelUpgrading();
            }

            if (!isMoving && currentUpgradeSpot != null && currentUpgradeSpot.PlayerInRange && !isUpgrading && !isServicing && !isCleaning && !isCooking)
            {
                StartUpgrading();
            }
        }

        private void StartUpgrading()
        {
            if (currentUpgradeSpot == null || !currentUpgradeSpot.PlayerInRange)
                return;

            isUpgrading = true;
            currentUpgradeSpot.StartUpgrade(transform);
        }

        private void CancelUpgrading()
        {
            if (currentUpgradeSpot != null && currentUpgradeSpot.IsUpgrading)
            {
                currentUpgradeSpot.CancelUpgrade();
            }

            isUpgrading = false;
        }

        #endregion

        #region Cooking Interaction

        private void HandleCookingInteraction()
        {
            if (isMoving && isCooking)
            {
                CancelCooking();
            }

            if (!isMoving && currentCookingSpot != null && currentCookingSpot.CanCook
                && !isCooking && !isServicing && !isServicingCashier && !isCleaning && !isUpgrading)
            {
                StartCooking();
            }
        }

        private void StartCooking()
        {
            if (currentCookingSpot == null || !currentCookingSpot.CanCook)
                return;

            isCooking = true;
            currentCookingSpot.StartCooking();

            if (radialProgressUI != null)
            {
                radialProgressUI.SetFillDuration(currentCookingSpot.CookDuration);
                radialProgressUI.StartProgress();
                StartCoroutine(WaitForCookingComplete());
            }
        }

        private IEnumerator WaitForCookingComplete()
        {
            if (radialProgressUI == null)
                yield break;

            yield return new WaitForSeconds(currentCookingSpot.CookDuration);

            if (isCooking && currentCookingSpot != null)
            {
                CompleteCooking();
            }
        }

        private void CompleteCooking()
        {
            if (currentCookingSpot != null)
            {
                currentCookingSpot.CompleteCooking();
            }

            isCooking = false;

            if (radialProgressUI != null)
            {
                radialProgressUI.StopProgress();
            }
        }

        private void CancelCooking()
        {
            isCooking = false;

            if (currentCookingSpot != null)
            {
                currentCookingSpot.CancelCooking();
            }

            if (radialProgressUI != null)
            {
                radialProgressUI.StopProgress();
            }
        }

        #endregion

        #region Trigger Handling

        private void OnTriggerEnter(Collider other)
        {
            ServiceSpot serviceSpot = other.GetComponent<ServiceSpot>();
            if (serviceSpot != null)
            {
                currentServiceSpot = serviceSpot;
            }

            CleaningSpot cleaningSpot = other.GetComponent<CleaningSpot>();
            if (cleaningSpot != null)
            {
                currentCleaningSpot = cleaningSpot;
            }

            UpgradeSpot upgradeSpot = other.GetComponent<UpgradeSpot>();
            if (upgradeSpot != null)
            {
                currentUpgradeSpot = upgradeSpot;
            }

            RTCashier cashier = other.GetComponent<RTCashier>();
            if (cashier != null)
            {
                currentCashier = cashier;
            }

            RTCookingSpot cookingSpot = other.GetComponent<RTCookingSpot>();
            if (cookingSpot != null)
            {
                currentCookingSpot = cookingSpot;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            ServiceSpot serviceSpot = other.GetComponent<ServiceSpot>();
            if (serviceSpot != null && serviceSpot == currentServiceSpot)
            {
                if (isServicing) CancelService();
                currentServiceSpot = null;
            }

            CleaningSpot cleaningSpot = other.GetComponent<CleaningSpot>();
            if (cleaningSpot != null && cleaningSpot == currentCleaningSpot)
            {
                if (isCleaning) CancelCleaning();
                currentCleaningSpot = null;
            }

            UpgradeSpot upgradeSpot = other.GetComponent<UpgradeSpot>();
            if (upgradeSpot != null && upgradeSpot == currentUpgradeSpot)
            {
                if (isUpgrading) CancelUpgrading();
                currentUpgradeSpot = null;
            }

            RTCashier cashier = other.GetComponent<RTCashier>();
            if (cashier != null && cashier == currentCashier)
            {
                if (isServicingCashier) CancelCashierService();
                currentCashier = null;
            }

            RTCookingSpot cookingSpot = other.GetComponent<RTCookingSpot>();
            if (cookingSpot != null && cookingSpot == currentCookingSpot)
            {
                if (isCooking) CancelCooking();
                currentCookingSpot = null;
            }
        }

        #endregion

        #region Cashier Interaction

        private void HandleCashierInteraction()
        {
            if (isMoving && isServicingCashier)
            {
                CancelCashierService();
            }

            if (!isMoving && currentCashier != null && currentCashier.CanServe && !isServicingCashier && !isServicing && !isCleaning && !isUpgrading && !isCooking)
            {
                StartCashierService();
            }
        }

        private void StartCashierService()
        {
            if (currentCashier == null || !currentCashier.CanServe)
                return;

            isServicingCashier = true;
            currentCashier.StartService();

            if (radialProgressUI != null)
            {
                radialProgressUI.StartProgress();
                StartCoroutine(WaitForCashierServiceComplete());
            }
        }

        private IEnumerator WaitForCashierServiceComplete()
        {
            if (radialProgressUI == null)
                yield break;

            yield return new WaitForSeconds(radialProgressUI.FillDuration);

            if (isServicingCashier && currentCashier != null)
            {
                CompleteCashierService();
            }
        }

        private void CompleteCashierService()
        {
            if (currentCashier != null)
            {
                currentCashier.CompleteService();
            }

            isServicingCashier = false;

            if (radialProgressUI != null)
            {
                radialProgressUI.StopProgress();
            }
        }

        private void CancelCashierService()
        {
            isServicingCashier = false;

            if (radialProgressUI != null)
            {
                radialProgressUI.StopProgress();
            }
        }

        #endregion
    }
}
