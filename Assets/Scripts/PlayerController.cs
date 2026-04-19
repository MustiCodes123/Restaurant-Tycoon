using UnityEngine;

public class PlayerController : MonoBehaviour
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
    [SerializeField] private PlayerCarryController carryController;
    
    private Vector3 velocity;
    private bool isMoving;
    private ServiceSpot currentServiceSpot;
    private CleaningSpot currentCleaningSpot;
    private UpgradeSpot currentUpgradeSpot;
    private ServiceGuyUnlockSpot currentServiceGuyUnlockSpot;
    private JanitorUnlockSpot currentJanitorUnlockSpot;
    private TableCleanerUnlockSpot currentTableCleanerUnlockSpot;
    private bool isServicing = false;
    private bool isCleaning = false;
    private bool isUpgrading = false;
    private bool isUnlockingServiceGuy = false;
    private bool isUnlockingJanitor = false;
    private bool isUnlockingTableCleaner = false;
    
    private void Start()
    {
        // Get components if not assigned
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
            
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
            
        if (radialProgressUI == null)
            radialProgressUI = GetComponentInChildren<RadialProgressUI>();
            
        if (carryController == null)
            carryController = GetComponent<PlayerCarryController>();
    }
    
    private void Update()
    {
        HandleMovement();
        HandleAnimation();
        ApplyGravity();
        HandleServiceInteraction();
        HandleCleaningInteraction();
        HandleUpgradeInteraction();
        HandleServiceGuyUnlockInteraction();
        HandleJanitorUnlockInteraction();
        HandleTableCleanerUnlockInteraction();
    }
    
    private void HandleMovement()
    {
        // Get input from joystick
        float horizontal = joystick.Horizontal;
        float vertical = joystick.Vertical;
        
        // Calculate input magnitude for smooth movement
        float inputMagnitude = Mathf.Clamp01(new Vector2(horizontal, vertical).magnitude);
        
        // Check if player is actually moving
        isMoving = inputMagnitude > movementDeadzone;
        
        if (isMoving)
        {
            // Create movement direction
            Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
            
            // Apply movement
            Vector3 movement = moveDirection * moveSpeed * inputMagnitude * Time.deltaTime;
            characterController.Move(movement);
            
            // Smooth rotation towards movement direction
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
            // Only set normal walking if not carrying anything (carry controller handles its own animations)
            bool isCarrying = carryController != null && carryController.IsCarrying;
            
            if (!isCarrying)
            {
                animator.SetBool("IsWalking", isMoving);
            }
        }
        
        // Update carry controller movement state
        if (carryController != null)
        {
            carryController.SetMoving(isMoving);
        }
    }
    
    private void ApplyGravity()
    {
        // Apply gravity if character controller is grounded
        if (characterController.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small value to keep grounded
        }
        
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }
    
    private void HandleServiceInteraction()
    {
        // If player is moving, cancel any ongoing service
        if (isMoving && isServicing)
        {
            CancelService();
        }
        
        // If standing at service spot with a customer AND can serve, start serving
        if (!isMoving && currentServiceSpot != null && currentServiceSpot.CanServe && !isServicing && !isCleaning)
        {
            StartService();
        }
    }
    
    private void HandleCleaningInteraction()
    {
        // If player is moving, cancel any ongoing cleaning
        if (isMoving && isCleaning)
        {
            CancelCleaning();
        }
        
        // If standing at dirty cleaning spot, start cleaning
        if (!isMoving && currentCleaningSpot != null && currentCleaningSpot.IsDirty && !isCleaning && !isServicing && !isUpgrading)
        {
            StartCleaning();
        }
        
        // Update cleaning progress if actively cleaning
        if (isCleaning && currentCleaningSpot != null)
        {
            currentCleaningSpot.UpdateCleaning(Time.deltaTime);
            
            // Check if cleaning completed (spot will set IsDirty to false)
            if (!currentCleaningSpot.IsDirty)
            {
                isCleaning = false;
            }
        }
    }
    
    private void HandleUpgradeInteraction()
    {
        // If player is moving, cancel any ongoing upgrade
        if (isMoving && isUpgrading)
        {
            CancelUpgrading();
        }
        
        // Don't start upgrading if player is on an unlock spot (unlock spots take priority)
        bool onUnlockSpot = (currentServiceGuyUnlockSpot != null && currentServiceGuyUnlockSpot.PlayerInRange) ||
                           (currentJanitorUnlockSpot != null && currentJanitorUnlockSpot.PlayerInRange);
        
        // If standing at upgrade spot and not doing other activities, start upgrading
        if (!isMoving && currentUpgradeSpot != null && currentUpgradeSpot.PlayerInRange && !isUpgrading && !isServicing && !isCleaning && !isUnlockingServiceGuy && !isUnlockingJanitor && !onUnlockSpot)
        {
            StartUpgrading();
        }
    }
    
    private void HandleServiceGuyUnlockInteraction()
    {
        // If player is moving, cancel any ongoing unlock
        if (isMoving && isUnlockingServiceGuy)
        {
            CancelServiceGuyUnlock();
        }
        
        // If standing at service guy unlock spot and not doing other activities, start unlocking
        if (!isMoving && currentServiceGuyUnlockSpot != null && currentServiceGuyUnlockSpot.PlayerInRange && !isUnlockingServiceGuy && !isServicing && !isCleaning && !isUpgrading && !isUnlockingJanitor)
        {
            StartServiceGuyUnlock();
        }
    }
    
    private void HandleJanitorUnlockInteraction()
    {
        // If player is moving, cancel any ongoing unlock
        if (isMoving && isUnlockingJanitor)
        {
            CancelJanitorUnlock();
        }
        
        // If standing at janitor unlock spot and not doing other activities, start unlocking
        if (!isMoving && currentJanitorUnlockSpot != null && currentJanitorUnlockSpot.PlayerInRange && !isUnlockingJanitor && !isServicing && !isCleaning && !isUpgrading && !isUnlockingServiceGuy)
        {
            StartJanitorUnlock();
        }
    }
    
    private void StartCleaning()
    {
        if (currentCleaningSpot == null || !currentCleaningSpot.IsDirty)
            return;
        
        isCleaning = true;
        currentCleaningSpot.StartCleaning();
        
        Debug.Log("Started cleaning spot");
    }
    
    private void CancelCleaning()
    {
        if (currentCleaningSpot != null && currentCleaningSpot.IsBeingCleaned)
        {
            currentCleaningSpot.CancelCleaning();
        }
        
        isCleaning = false;
        Debug.Log("Cleaning cancelled - player moved away");
    }
    
    private void StartUpgrading()
    {
        if (currentUpgradeSpot == null || !currentUpgradeSpot.PlayerInRange)
            return;
        
        isUpgrading = true;
        currentUpgradeSpot.StartUpgrade(transform);
        
        Debug.Log("Started upgrading store");
    }
    
    private void CancelUpgrading()
    {
        if (currentUpgradeSpot != null && currentUpgradeSpot.IsUpgrading)
        {
            currentUpgradeSpot.CancelUpgrade();
        }
        
        isUpgrading = false;
        Debug.Log("Upgrading cancelled - player moved away");
    }
    
    private void StartServiceGuyUnlock()
    {
        if (currentServiceGuyUnlockSpot == null || !currentServiceGuyUnlockSpot.PlayerInRange)
            return;
        
        isUnlockingServiceGuy = true;
        currentServiceGuyUnlockSpot.StartPayment(transform);
        
        Debug.Log("Started unlocking service guy");
    }
    
    private void CancelServiceGuyUnlock()
    {
        if (currentServiceGuyUnlockSpot != null && currentServiceGuyUnlockSpot.IsPaymentActive)
        {
            currentServiceGuyUnlockSpot.CancelPayment();
        }
        
        isUnlockingServiceGuy = false;
        Debug.Log("Service guy unlock cancelled - player moved away");
    }
    
    private void StartJanitorUnlock()
    {
        if (currentJanitorUnlockSpot == null || !currentJanitorUnlockSpot.PlayerInRange)
            return;
        
        isUnlockingJanitor = true;
        currentJanitorUnlockSpot.StartPayment(transform);
        
        Debug.Log("Started unlocking janitor");
    }
    
    private void CancelJanitorUnlock()
    {
        if (currentJanitorUnlockSpot != null && currentJanitorUnlockSpot.IsPaymentActive)
        {
            currentJanitorUnlockSpot.CancelPayment();
        }
        
        isUnlockingJanitor = false;
        Debug.Log("Janitor unlock cancelled - player moved away");
    }
    
    private void HandleTableCleanerUnlockInteraction()
    {
        // If player is moving, cancel any ongoing unlock
        if (isMoving && isUnlockingTableCleaner)
        {
            CancelTableCleanerUnlock();
        }
        
        // If standing at table cleaner unlock spot and not doing other activities, start unlocking
        if (!isMoving && currentTableCleanerUnlockSpot != null && currentTableCleanerUnlockSpot.PlayerInRange && !isUnlockingTableCleaner && !isServicing && !isCleaning && !isUpgrading && !isUnlockingServiceGuy && !isUnlockingJanitor)
        {
            StartTableCleanerUnlock();
        }
    }
    
    private void StartTableCleanerUnlock()
    {
        if (currentTableCleanerUnlockSpot == null || !currentTableCleanerUnlockSpot.PlayerInRange)
            return;
        
        isUnlockingTableCleaner = true;
        currentTableCleanerUnlockSpot.StartPayment(transform);
        
        Debug.Log("Started unlocking table cleaner");
    }
    
    private void CancelTableCleanerUnlock()
    {
        if (currentTableCleanerUnlockSpot != null && currentTableCleanerUnlockSpot.IsPaymentActive)
        {
            currentTableCleanerUnlockSpot.CancelPayment();
        }
        
        isUnlockingTableCleaner = false;
        Debug.Log("Table cleaner unlock cancelled - player moved away");
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
    
    private System.Collections.IEnumerator WaitForServiceComplete()
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
        
        Debug.Log("Service completed!");
    }
    
    private void CancelService()
    {
        isServicing = false;
        
        if (radialProgressUI != null)
        {
            radialProgressUI.StopProgress();
        }
        
        Debug.Log("Service cancelled - player moved away");
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Check for ServiceSpot
        ServiceSpot serviceSpot = other.GetComponent<ServiceSpot>();
        if (serviceSpot != null)
        {
            currentServiceSpot = serviceSpot;
            Debug.Log("Player entered service spot");
        }
        
        // Check for CleaningSpot
        CleaningSpot cleaningSpot = other.GetComponent<CleaningSpot>();
        if (cleaningSpot != null)
        {
            currentCleaningSpot = cleaningSpot;
            Debug.Log("Player entered cleaning spot");
        }
        
        // Check for UpgradeSpot
        UpgradeSpot upgradeSpot = other.GetComponent<UpgradeSpot>();
        if (upgradeSpot != null)
        {
            currentUpgradeSpot = upgradeSpot;
            Debug.Log("Player entered upgrade spot");
        }
        
        // Check for ServiceGuyUnlockSpot
        ServiceGuyUnlockSpot serviceGuyUnlockSpot = other.GetComponent<ServiceGuyUnlockSpot>();
        if (serviceGuyUnlockSpot != null)
        {
            currentServiceGuyUnlockSpot = serviceGuyUnlockSpot;
            Debug.Log("Player entered service guy unlock spot");
        }
        
        // Check for JanitorUnlockSpot
        JanitorUnlockSpot janitorUnlockSpot = other.GetComponent<JanitorUnlockSpot>();
        if (janitorUnlockSpot != null)
        {
            currentJanitorUnlockSpot = janitorUnlockSpot;
            Debug.Log("Player entered janitor unlock spot");
        }
        
        // Check for TableCleanerUnlockSpot
        TableCleanerUnlockSpot tableCleanerUnlockSpot = other.GetComponent<TableCleanerUnlockSpot>();
        if (tableCleanerUnlockSpot != null)
        {
            currentTableCleanerUnlockSpot = tableCleanerUnlockSpot;
            Debug.Log("Player entered table cleaner unlock spot");
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        // Check for ServiceSpot
        ServiceSpot serviceSpot = other.GetComponent<ServiceSpot>();
        if (serviceSpot != null && serviceSpot == currentServiceSpot)
        {
            if (isServicing)
            {
                CancelService();
            }
            currentServiceSpot = null;
            Debug.Log("Player left service spot");
        }
        
        // Check for CleaningSpot
        CleaningSpot cleaningSpot = other.GetComponent<CleaningSpot>();
        if (cleaningSpot != null && cleaningSpot == currentCleaningSpot)
        {
            if (isCleaning)
            {
                CancelCleaning();
            }
            currentCleaningSpot = null;
            Debug.Log("Player left cleaning spot");
        }
        
        // Check for UpgradeSpot
        UpgradeSpot upgradeSpot = other.GetComponent<UpgradeSpot>();
        if (upgradeSpot != null && upgradeSpot == currentUpgradeSpot)
        {
            if (isUpgrading)
            {
                CancelUpgrading();
            }
            currentUpgradeSpot = null;
            Debug.Log("Player left upgrade spot");
        }
        
        // Check for ServiceGuyUnlockSpot
        ServiceGuyUnlockSpot serviceGuyUnlockSpot = other.GetComponent<ServiceGuyUnlockSpot>();
        if (serviceGuyUnlockSpot != null && serviceGuyUnlockSpot == currentServiceGuyUnlockSpot)
        {
            if (isUnlockingServiceGuy)
            {
                CancelServiceGuyUnlock();
            }
            currentServiceGuyUnlockSpot = null;
            Debug.Log("Player left service guy unlock spot");
        }
        
        // Check for JanitorUnlockSpot
        JanitorUnlockSpot janitorUnlockSpot = other.GetComponent<JanitorUnlockSpot>();
        if (janitorUnlockSpot != null && janitorUnlockSpot == currentJanitorUnlockSpot)
        {
            if (isUnlockingJanitor)
            {
                CancelJanitorUnlock();
            }
            currentJanitorUnlockSpot = null;
            Debug.Log("Player left janitor unlock spot");
        }
        
        // Check for TableCleanerUnlockSpot
        TableCleanerUnlockSpot tableCleanerUnlockSpot = other.GetComponent<TableCleanerUnlockSpot>();
        if (tableCleanerUnlockSpot != null && tableCleanerUnlockSpot == currentTableCleanerUnlockSpot)
        {
            if (isUnlockingTableCleaner)
            {
                CancelTableCleanerUnlock();
            }
            currentTableCleanerUnlockSpot = null;
            Debug.Log("Player left table cleaner unlock spot");
        }
    }
}
