using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RestaurantTycoon
{
    public class RTStaffUpgradePanelUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform rowsParent;
        [SerializeField] private RTStaffUpgradeRowUI rowPrefab;
        [SerializeField] private bool hidePanelOnStart = true;

        [Header("Upgrade Sources")]
        [SerializeField] private bool autoFindStaffUpgrades = true;
        [SerializeField] private List<RTStaffUpgrade> staffUpgrades = new List<RTStaffUpgrade>();

        private readonly List<RTStaffUpgradeRowUI> rows = new List<RTStaffUpgradeRowUI>();
        private readonly List<RTStaffUpgrade> subscribedUpgrades = new List<RTStaffUpgrade>();

        private void Awake()
        {
            if (panelRoot == null)
                panelRoot = gameObject;

            if (openButton != null)
                openButton.onClick.AddListener(Open);

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (hidePanelOnStart && panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void Start()
        {
            BuildRows();
            RefreshRows();
        }

        private void OnEnable()
        {
            SubscribeToLevelEvents();

            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnMoneyChanged += OnMoneyChanged;
        }

        private void OnDisable()
        {
            UnsubscribeFromLevelEvents();

            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnMoneyChanged -= OnMoneyChanged;

            UnsubscribeFromUpgradeEvents();
        }

        public void Open()
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);

            BuildRows();
            RefreshRows();
        }

        public void Close()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        public void RefreshRows()
        {
            foreach (var row in rows)
                row.Refresh();
        }

        private void BuildRows()
        {
            if (rowsParent == null || rowPrefab == null)
                return;

            CacheStaffUpgrades();
            ClearRows();
            SubscribeToUpgradeEvents();

            rowPrefab.gameObject.SetActive(false);

            foreach (var upgrade in OrderedUpgrades())
            {
                var row = Instantiate(rowPrefab, rowsParent);
                row.gameObject.SetActive(true);
                row.Setup(upgrade, this);
                rows.Add(row);
            }
        }

        private void CacheStaffUpgrades()
        {
            staffUpgrades.RemoveAll(upgrade => upgrade == null);

            if (!autoFindStaffUpgrades)
                return;

            var found = Object.FindObjectsByType<RTStaffUpgrade>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var upgrade in found)
            {
                if (upgrade != null && !staffUpgrades.Contains(upgrade))
                    staffUpgrades.Add(upgrade);
            }
        }

        private IEnumerable<RTStaffUpgrade> OrderedUpgrades()
        {
            return staffUpgrades
                .Where(upgrade => upgrade != null)
                .Select(upgrade =>
                {
                    upgrade.EnsureStateLoaded();
                    return upgrade;
                })
                .OrderBy(upgrade => upgrade.RequiredPlayerLevel)
                .ThenBy(upgrade => upgrade.UpgradeId);
        }

        private void ClearRows()
        {
            foreach (var row in rows)
            {
                if (row != null)
                    Destroy(row.gameObject);
            }

            rows.Clear();
        }

        private void SubscribeToUpgradeEvents()
        {
            UnsubscribeFromUpgradeEvents();

            foreach (var upgrade in staffUpgrades)
            {
                if (upgrade == null)
                    continue;

                upgrade.OnUpgradeChanged += RefreshRows;
                subscribedUpgrades.Add(upgrade);
            }
        }

        private void UnsubscribeFromUpgradeEvents()
        {
            foreach (var upgrade in subscribedUpgrades)
            {
                if (upgrade != null)
                    upgrade.OnUpgradeChanged -= RefreshRows;
            }

            subscribedUpgrades.Clear();
        }

        private void SubscribeToLevelEvents()
        {
            if (RTLevelManager.Instance == null)
                return;

            RTLevelManager.Instance.OnLevelUp -= OnLevelChanged;
            RTLevelManager.Instance.OnLevelLoaded -= OnLevelChanged;
            RTLevelManager.Instance.OnLevelUp += OnLevelChanged;
            RTLevelManager.Instance.OnLevelLoaded += OnLevelChanged;
        }

        private void UnsubscribeFromLevelEvents()
        {
            if (RTLevelManager.Instance == null)
                return;

            RTLevelManager.Instance.OnLevelUp -= OnLevelChanged;
            RTLevelManager.Instance.OnLevelLoaded -= OnLevelChanged;
        }

        private void OnLevelChanged(int _)
        {
            BuildRows();
            RefreshRows();
        }

        private void OnMoneyChanged(int _) => RefreshRows();
    }
}
