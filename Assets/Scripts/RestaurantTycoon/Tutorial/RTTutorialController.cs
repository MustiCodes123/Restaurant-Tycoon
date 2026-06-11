using UnityEngine;
using System.Collections;

namespace RestaurantTycoon
{
    /// <summary>
    /// Orchestrates the first-loop gameplay tutorial.
    /// Shows one DynamicMission step at a time in the RT Level Panel and
    /// points the arrow at the relevant station for each step.
    ///
    /// Attach to any persistent GameObject in the RT scene.
    /// Wire all references in the Inspector.
    ///
    /// Steps:
    ///  1  Collect ingredients from the ingredient station
    ///  2  Drop ingredients at the cooking station
    ///  3  Stand at the cooking spot to cook
    ///  4  Pick up the finished dish
    ///  5  Place the dish at the serving counter
    ///  6  Wait for the customer to finish eating (arrow → cashier)
    ///  7  Serve the customer at the cashier
    ///  8  Pick up the dirty dish from the table
    ///  9  Dispose the dish in the garbage bin
    ///
    /// Completion is persisted via PlayerPrefs and the tutorial never repeats.
    /// </summary>
    public class RTTutorialController : MonoBehaviour
    {
        private const string DONE_KEY = "RTTutorial_Done";

        // ── Inspector References ─────────────────────────────────────────────

        [Header("Gameplay References")]
        [SerializeField] private RTIngredientContainer ingredientContainer;
        [SerializeField] private RTCookInputContainer  cookInputContainer;
        [SerializeField] private RTCookingSpot         cookingSpot;
        [SerializeField] private RTItemOutputContainer itemOutputContainer;
        [SerializeField] private RTCustomerCounter     customerCounter;
        [SerializeField] private RTCashier             cashier;
        [SerializeField] private RTDiningTable         diningTable;
        [SerializeField] private RTGarbageBin          garbageBin;

        [Header("Arrow Target Transforms")]
        [Tooltip("Transform the arrow points at during step 6 (wait for customer — usually the cashier or dining area).")]
        [SerializeField] private Transform waitStepArrowTarget;

        [Header("Player")]
        [SerializeField] private RTPlayerCarryController playerCarryController;

        [Header("Arrow")]
        [SerializeField] private RTPlayerArrow playerArrow;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        // ── Runtime ──────────────────────────────────────────────────────────

        private int currentStep = 0;   // 0 = not started; 1-9 = active step
        private bool isActive   = false;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Start()
        {
            if (PlayerPrefs.GetInt(DONE_KEY, 0) == 1)
            {
                Log("Tutorial already completed — skipping.");
                return;
            }

            // Defer by one frame so all other Start() calls have finished wiring up.
            StartCoroutine(BeginNextFrame());
        }

        private IEnumerator BeginNextFrame()
        {
            yield return null;
            BeginTutorial();
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
        }

        // ── Tutorial Flow ────────────────────────────────────────────────────

        private void BeginTutorial()
        {
            isActive = true;
            SubscribeAll();
            AdvanceToStep(1);
        }

        private void AdvanceToStep(int step)
        {
            // Complete the previous step's mission card
            if (currentStep > 0)
            {
                DynamicMissionManager.Instance?.CompleteTutorialStep(StepId(currentStep));
                Log($"Step {currentStep} completed.");
            }

            currentStep = step;

            if (step > 9)
            {
                CompleteTutorial();
                return;
            }

            // Show the new mission card
            DynamicMissionManager.Instance?.RegisterTutorialStep(StepId(step), StepText(step));
            Log($"Step {step} started: {StepText(step)}");

            // Point the arrow
            if (playerArrow != null)
                playerArrow.SetTutorialOverrideTarget(StepArrowTarget(step));
        }

        private void CompleteTutorial()
        {
            isActive = false;
            UnsubscribeAll();

            PlayerPrefs.SetInt(DONE_KEY, 1);
            PlayerPrefs.Save();

            if (playerArrow != null)
                playerArrow.ClearTutorialOverride();

            Log("Tutorial complete!");
        }

        // ── Step Definitions ─────────────────────────────────────────────────

        private static string StepId(int step) => $"Step{step}";

        private static string StepText(int step)
        {
            switch (step)
            {
                case 1: return "Collect ingredients from the backyard";
                case 2: return "Drop ingredients at the cooking area";
                case 3: return "Stand at the cooking spot to cook";
                case 4: return "Pick up the finished dish";
                case 5: return "Place the dish at the serving counter";
                case 6: return "Wait for the customer to finish eating";
                case 7: return "Serve the customer at the cashier";
                case 8: return "Pick up the dirty dish from the table";
                case 9: return "Dispose the dirty dish in the garbage bin";
                default: return string.Empty;
            }
        }

        private Transform StepArrowTarget(int step)
        {
            switch (step)
            {
                case 1: return ingredientContainer  != null ? ingredientContainer.transform  : null;
                case 2: return cookInputContainer   != null ? cookInputContainer.transform   : null;
                case 3: return cookingSpot          != null ? cookingSpot.transform          : null;
                case 4: return itemOutputContainer  != null ? itemOutputContainer.transform  : null;
                case 5: return customerCounter      != null ? customerCounter.transform      : null;
                case 6: return waitStepArrowTarget  != null ? waitStepArrowTarget            :
                               cashier              != null ? cashier.transform              : null;
                case 7: return cashier              != null ? cashier.transform              : null;
                case 8: return diningTable          != null ? diningTable.transform          : null;
                case 9: return garbageBin           != null ? garbageBin.transform           : null;
                default: return null;
            }
        }

        // ── Event Subscriptions ──────────────────────────────────────────────

        private void SubscribeAll()
        {
            if (playerCarryController != null)
                playerCarryController.OnCarryChanged += OnCarryChanged;

            if (cookInputContainer != null)
                cookInputContainer.OnIngredientAdded += OnIngredientDropped;

            if (cookingSpot != null)
                cookingSpot.OnCookingCompleted += OnCookingCompleted;

            if (customerCounter != null)
                customerCounter.OnItemPlaced += OnItemPlacedAtCounter;

            if (cashier != null)
            {
                cashier.OnCustomerReadyAtCashier += OnCustomerReadyAtCashier;
                cashier.OnCustomerServed        += OnCustomerServed;
            }

            if (garbageBin != null)
                garbageBin.OnItemsDisposed += OnItemsDisposed;
        }

        private void UnsubscribeAll()
        {
            if (playerCarryController != null)
                playerCarryController.OnCarryChanged -= OnCarryChanged;

            if (cookInputContainer != null)
                cookInputContainer.OnIngredientAdded -= OnIngredientDropped;

            if (cookingSpot != null)
                cookingSpot.OnCookingCompleted -= OnCookingCompleted;

            if (customerCounter != null)
                customerCounter.OnItemPlaced -= OnItemPlacedAtCounter;

            if (cashier != null)
            {
                cashier.OnCustomerReadyAtCashier -= OnCustomerReadyAtCashier;
                cashier.OnCustomerServed        -= OnCustomerServed;
            }

            if (garbageBin != null)
                garbageBin.OnItemsDisposed -= OnItemsDisposed;
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void OnCarryChanged()
        {
            if (!isActive) return;

            if (currentStep == 1)
            {
                // Advance when the player picks up at least one Ingredient
                if (playerCarryController.CountOfType(CarryableType.Ingredient) > 0)
                    AdvanceToStep(2);
            }
            else if (currentStep == 4)
            {
                // Advance when the player picks up at least one FinishedItem
                if (playerCarryController.CountOfType(CarryableType.FinishedItem) > 0)
                    AdvanceToStep(5);
            }
            else if (currentStep == 8)
            {
                // Advance when the player picks up at least one dirty dish (Garbage type)
                if (playerCarryController.CountOfType(CarryableType.Garbage) > 0)
                    AdvanceToStep(9);
            }
        }

        private void OnIngredientDropped()
        {
            if (!isActive || currentStep != 2) return;
            AdvanceToStep(3);
        }

        private void OnCookingCompleted()
        {
            if (!isActive || currentStep != 3) return;
            AdvanceToStep(4);
        }

        private void OnItemPlacedAtCounter()
        {
            if (!isActive || currentStep != 5) return;
            AdvanceToStep(6);
        }

        private void OnCustomerReadyAtCashier()
        {
            if (!isActive || currentStep != 6) return;
            AdvanceToStep(7);
        }

        private void OnCustomerServed()
        {
            if (!isActive || currentStep != 7) return;
            AdvanceToStep(8);
        }

        private void OnItemsDisposed()
        {
            if (!isActive || currentStep != 9) return;
            AdvanceToStep(10); // > 9 triggers CompleteTutorial()
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void Log(string msg)
        {
            if (showDebugLogs)
                Debug.Log($"[RTTutorialController] {msg}");
        }
    }
}
